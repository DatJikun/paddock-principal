using System.Globalization;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Inbox;

/// <summary>
/// Every manager's inbox, as the world section named <see cref="SectionName"/>. Items are stored in order of their
/// number; the number comes from this section's own counter, which only moves forward, so an id is never reused (INV-009).
/// The world-level allocator issues person, organization, and contract ids only; adding a counter there would change the
/// T15 hash, so each section keeps the counter for the ids it issues.
/// Immutable: every change returns a new section. This type holds truth; who may read which item is decided one layer up.
/// Closed items stay as records. Nothing here prunes them (not decided yet).
/// <para>
/// Canonical text (<see cref="SchemaVersion"/> 1), after the section header written by the state hash:
/// <code>
/// next &lt;nextNumber&gt;
/// items &lt;count&gt;
/// item &lt;len&gt;:&lt;id&gt;
/// manager &lt;len&gt;:&lt;managerId&gt;
/// created &lt;len&gt;:&lt;date&gt;
/// kind &lt;len&gt;:&lt;kind&gt;
/// subject &lt;len&gt;:&lt;key&gt;
/// arguments &lt;count&gt;
/// argument &lt;len&gt;:&lt;name&gt; &lt;len&gt;:&lt;value&gt;
/// options &lt;count&gt;
/// option &lt;len&gt;:&lt;id&gt; &lt;len&gt;:&lt;labelKey&gt; &lt;len&gt;:&lt;consequenceKey&gt;
/// until &lt;len&gt;:&lt;date or -&gt;
/// default &lt;len&gt;:&lt;optionId or -&gt;
/// status &lt;Open|Resolved|Dismissed|Expired&gt;
/// closed &lt;len&gt;:&lt;date or -&gt;
/// chosen &lt;len&gt;:&lt;optionId or -&gt;
/// </code>
/// </para>
/// </summary>
public sealed class InboxSection : IWorldSection
{
    public const string SectionName = "inbox";

    public const string IdPrefix = "inb:";

    private readonly SortedDictionary<long, InboxItem> _items;

    private InboxSection(long nextNumber, SortedDictionary<long, InboxItem> items)
    {
        NextNumber = nextNumber;
        _items = items;
    }

    public static InboxSection Empty { get; } = new(1, []);

    public string Name => SectionName;

    public int SchemaVersion => 1;

    /// <summary>The number the next item gets. Starts at 1.</summary>
    public long NextNumber { get; }

    /// <summary>All items, open and closed, in order of their number.</summary>
    public IReadOnlyList<InboxItem> Items => _items.Values.ToArray();

    public static string IdOf(long number) =>
        IdPrefix + number.ToString(CultureInfo.InvariantCulture);

    /// <summary>Rebuilds a section from stored items. Every item number must be below <paramref name="nextNumber"/>.</summary>
    public static InboxSection Restore(long nextNumber, IEnumerable<InboxItem> items)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nextNumber, 1);
        ArgumentNullException.ThrowIfNull(items);
        var map = new SortedDictionary<long, InboxItem>();
        foreach (var item in items)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (item.Number >= nextNumber)
            {
                throw new InvalidOperationException($"Item '{item.Id}' is not below the counter {nextNumber}.");
            }

            if (!map.TryAdd(item.Number, item))
            {
                throw new InvalidOperationException($"Item '{item.Id}' appears twice.");
            }
        }

        return new InboxSection(nextNumber, map);
    }

    public InboxItem? Find(string itemId)
    {
        ArgumentNullException.ThrowIfNull(itemId);
        if (!itemId.StartsWith(IdPrefix, StringComparison.Ordinal)
            || !long.TryParse(itemId.AsSpan(IdPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            || IdOf(number) != itemId)
        {
            return null;
        }

        return _items.GetValueOrDefault(number);
    }

    /// <summary>The items of one manager, open and closed, in order of their number.</summary>
    public IReadOnlyList<InboxItem> ItemsOf(string managerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managerId);
        return _items.Values.Where(item => item.ManagerId == managerId).ToArray();
    }

    /// <summary>How many decision items of this manager are still waiting for an answer.</summary>
    public int OpenDecisionCount(string managerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managerId);
        return _items.Values.Count(item => item.ManagerId == managerId && item.IsOpenDecision);
    }

    /// <summary>The managers that have at least one item, in ordinal order.</summary>
    public IReadOnlyList<string> Managers() =>
        _items.Values.Select(item => item.ManagerId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Open items whose validity date is before <paramref name="today"/>, in order of their number.</summary>
    public IReadOnlyList<InboxItem> Lapsed(GameDate today) =>
        _items.Values.Where(item => item.IsOpen && item.ValidUntil is GameDate until && until < today).ToArray();

    public (InboxSection Section, InboxItem Item) Add(string managerId, InboxItemDraft draft, GameDate today)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managerId);
        ArgumentNullException.ThrowIfNull(draft);
        if (NextNumber == long.MaxValue)
        {
            throw new InvalidOperationException("The inbox id counter is exhausted.");
        }

        var item = new InboxItem(NextNumber, managerId, today, draft, InboxStatus.Open, null, null);
        var items = new SortedDictionary<long, InboxItem>(_items) { [item.Number] = item };
        return (new InboxSection(NextNumber + 1, items), item);
    }

    /// <summary>Marks an open decision item resolved with one of its options.</summary>
    public InboxSection Resolve(string itemId, string optionId, GameDate today)
    {
        var item = RequireOpen(itemId);
        if (!item.NeedsDecision)
        {
            throw new InvalidOperationException($"Item '{itemId}' is not a decision.");
        }

        if (!item.Options.Any(option => option.Id == optionId))
        {
            throw new InvalidOperationException($"Item '{itemId}' has no option '{optionId}'.");
        }

        return Replace(item.Closed(InboxStatus.Resolved, today, optionId));
    }

    /// <summary>Marks an open information item dismissed. A decision cannot be dismissed, it has to be answered.</summary>
    public InboxSection Dismiss(string itemId, GameDate today)
    {
        var item = RequireOpen(itemId);
        if (item.NeedsDecision)
        {
            throw new InvalidOperationException($"Item '{itemId}' is a decision and cannot be dismissed.");
        }

        return Replace(item.Closed(InboxStatus.Dismissed, today, null));
    }

    /// <summary>
    /// Closes an open item with no option taken, whatever its validity date. For the poster only: a decision whose manager
    /// can no longer answer it (a dismissed principal's renewal prompt) must not hold the shared clock for ever.
    /// </summary>
    public InboxSection Withdraw(string itemId, GameDate today)
    {
        var item = RequireOpen(itemId);
        return Replace(item.Closed(InboxStatus.Expired, today, null));
    }

    /// <summary>
    /// Marks an open item whose validity date has passed as lapsed. <paramref name="applyDefault"/> records that the
    /// declared default option was executed; it is only possible for an item that declares one.
    /// </summary>
    public InboxSection Expire(string itemId, GameDate today, bool applyDefault)
    {
        var item = RequireOpen(itemId);
        if (item.ValidUntil is not GameDate until || until >= today)
        {
            throw new InvalidOperationException($"Item '{itemId}' is still valid on {today}.");
        }

        if (applyDefault && item.DefaultOptionId is null)
        {
            throw new InvalidOperationException($"Item '{itemId}' declares no default option.");
        }

        return Replace(item.Closed(InboxStatus.Expired, today, applyDefault ? item.DefaultOptionId : null));
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Number("next", NextNumber);
        writer.Count("items", _items.Count);
        foreach (var item in _items.Values)
        {
            writer.TextLine("item", item.Id);
            writer.TextLine("manager", item.ManagerId);
            writer.TextLine("created", item.Created.ToString());
            writer.TextLine("kind", item.Kind);
            writer.TextLine("subject", item.SubjectKey);
            writer.Count("arguments", item.Arguments.Count);
            foreach (var argument in item.Arguments)
            {
                writer.Begin("argument");
                writer.Field(argument.Key);
                writer.Space();
                writer.Field(argument.Value);
                writer.End();
            }

            writer.Count("options", item.Options.Count);
            foreach (var option in item.Options)
            {
                writer.Begin("option");
                writer.Field(option.Id);
                writer.Space();
                writer.Field(option.LabelKey);
                writer.Space();
                writer.Field(option.ConsequenceKey);
                writer.End();
                if (option.Arguments.Count > 0)
                {
                    writer.Count("option_arguments", option.Arguments.Count);
                    foreach (var argument in option.Arguments)
                    {
                        writer.Begin("option_argument");
                        writer.Field(argument.Key);
                        writer.Space();
                        writer.Field(argument.Value);
                        writer.End();
                    }
                }
            }

            writer.TextLine("until", item.ValidUntil?.ToString() ?? "-");
            writer.TextLine("default", item.DefaultOptionId ?? "-");
            writer.Line("status " + item.Status);
            writer.TextLine("closed", item.ClosedOn?.ToString() ?? "-");
            writer.TextLine("chosen", item.ChosenOptionId ?? "-");
        }
    }

    private InboxItem RequireOpen(string itemId)
    {
        var item = Find(itemId) ?? throw new InvalidOperationException($"Unknown inbox item '{itemId}'.");
        if (!item.IsOpen)
        {
            throw new InvalidOperationException($"Item '{itemId}' is already {item.Status}.");
        }

        return item;
    }

    private InboxSection Replace(InboxItem item)
    {
        var items = new SortedDictionary<long, InboxItem>(_items) { [item.Number] = item };
        return new InboxSection(NextNumber, items);
    }
}
