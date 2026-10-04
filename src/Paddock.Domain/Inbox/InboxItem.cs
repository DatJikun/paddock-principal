using System.Collections.ObjectModel;
using Paddock.Domain.Time;

namespace Paddock.Domain.Inbox;

public enum InboxStatus
{
    Open,
    Resolved,
    Dismissed,
    Expired,
}

/// <summary>
/// One choice on a decision item. <see cref="LabelKey"/> and <see cref="ConsequenceKey"/> are translation keys.
/// The consequence preview is mandatory: the player sees what a choice does before making it (DESIGN §3.2).
/// </summary>
public sealed record InboxOption
{
    public InboxOption(string id, string labelKey, string consequenceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(labelKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(consequenceKey);
        Id = id;
        LabelKey = labelKey;
        ConsequenceKey = consequenceKey;
    }

    public string Id { get; }

    public string LabelKey { get; }

    public string ConsequenceKey { get; }
}

/// <summary>
/// What an owning system posts: everything about an item except its id, owner, and date, which the section assigns.
/// Texts are translation keys plus invariant argument values (PP-021). The poster builds it from what that manager
/// knows, never from truth the manager has no access to (INV-003).
/// <para>
/// A decision item has at least two options. When it carries <see cref="ValidUntil"/> (an offer that lapses) it must
/// name the <see cref="DefaultOptionId"/> that applies if nobody answers; the item is valid through that date.
/// A decision item with no validity date never expires on its own.
/// A non-decision item has no options and no default; it is information the manager reads and dismisses.
/// </para>
/// </summary>
public sealed class InboxItemDraft
{
    public InboxItemDraft(
        string kind,
        string subjectKey,
        IEnumerable<KeyValuePair<string, string>>? arguments,
        IReadOnlyList<InboxOption>? options,
        GameDate? validUntil,
        string? defaultOptionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectKey);
        Kind = kind;
        SubjectKey = subjectKey;
        Arguments = CopyArguments(arguments);
        Options = options is null ? [] : Array.AsReadOnly(options.ToArray());
        ValidUntil = validUntil;
        DefaultOptionId = defaultOptionId;

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var option in Options)
        {
            ArgumentNullException.ThrowIfNull(option);
            if (!ids.Add(option.Id))
            {
                throw new ArgumentException($"Option id '{option.Id}' appears twice.", nameof(options));
            }
        }

        if (Options.Count == 1)
        {
            throw new ArgumentException("A decision needs at least two options.", nameof(options));
        }

        if (!NeedsDecision)
        {
            if (defaultOptionId is not null)
            {
                throw new ArgumentException("An item with no options cannot have a default option.", nameof(defaultOptionId));
            }

            return;
        }

        if (defaultOptionId is not null && !ids.Contains(defaultOptionId))
        {
            throw new ArgumentException($"Default option '{defaultOptionId}' is not one of the options.", nameof(defaultOptionId));
        }

        if (validUntil is not null && defaultOptionId is null)
        {
            throw new ArgumentException("A decision that can lapse must name a default option.", nameof(defaultOptionId));
        }

        if (validUntil is null && defaultOptionId is not null)
        {
            throw new ArgumentException("A default option only applies when the decision has a validity date.", nameof(defaultOptionId));
        }
    }

    public string Kind { get; }

    public string SubjectKey { get; }

    /// <summary>Argument values for every text of this item, in ordinal order of the name.</summary>
    public IReadOnlyDictionary<string, string> Arguments { get; }

    public IReadOnlyList<InboxOption> Options { get; }

    public GameDate? ValidUntil { get; }

    public string? DefaultOptionId { get; }

    /// <summary>True when the item has options. Only such an item holds the clock.</summary>
    public bool NeedsDecision => Options.Count > 0;

    internal static IReadOnlyDictionary<string, string> CopyArguments(IEnumerable<KeyValuePair<string, string>>? arguments)
    {
        var copy = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in arguments ?? [])
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            ArgumentNullException.ThrowIfNull(pair.Value);
            if (!copy.TryAdd(pair.Key, pair.Value))
            {
                throw new ArgumentException($"Argument '{pair.Key}' appears twice.", nameof(arguments));
            }
        }

        return new ReadOnlyDictionary<string, string>(copy);
    }
}

/// <summary>
/// One message in one manager's inbox. Immutable; the public constructor is for loading a save, new items come from <see cref="InboxSection.Add"/>; <see cref="InboxSection"/> returns a new item when the status moves.
/// <see cref="Id"/> is stable and never reused (INV-009). An item that is no longer open stays in the section as a record.
/// </summary>
public sealed class InboxItem
{
    public InboxItem(
        long number,
        string managerId,
        GameDate created,
        InboxItemDraft draft,
        InboxStatus status,
        GameDate? closedOn,
        string? chosenOptionId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(managerId);
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.ValidUntil is GameDate until && until < created)
        {
            throw new ArgumentException("The validity date is before the day the item was created.", nameof(draft));
        }

        if (status == InboxStatus.Open != (closedOn is null))
        {
            throw new ArgumentException("An open item has no closing date, and a closed item has one.", nameof(closedOn));
        }

        if (closedOn is GameDate closed && closed < created)
        {
            throw new ArgumentException("The closing date is before the day the item was created.", nameof(closedOn));
        }

        ValidateChoice(draft, status, chosenOptionId);
        Number = number;
        ManagerId = managerId;
        Created = created;
        Draft = draft;
        Status = status;
        ClosedOn = closedOn;
        ChosenOptionId = chosenOptionId;
    }

    public InboxItemDraft Draft { get; }

    public long Number { get; }

    /// <summary>Stable id, <c>inb:{number}</c>.</summary>
    public string Id => InboxSection.IdOf(Number);

    /// <summary>The manager whose inbox this is. An item is only ever read by its owner (INV-003).</summary>
    public string ManagerId { get; }

    public GameDate Created { get; }

    public string Kind => Draft.Kind;

    public string SubjectKey => Draft.SubjectKey;

    public IReadOnlyDictionary<string, string> Arguments => Draft.Arguments;

    public bool NeedsDecision => Draft.NeedsDecision;

    public IReadOnlyList<InboxOption> Options => Draft.Options;

    public GameDate? ValidUntil => Draft.ValidUntil;

    public string? DefaultOptionId => Draft.DefaultOptionId;

    public InboxStatus Status { get; }

    public GameDate? ClosedOn { get; }

    /// <summary>The option that was executed. Null while open, for a dismissed item, and for an item that lapsed without a default.</summary>
    public string? ChosenOptionId { get; }

    public bool IsOpen => Status == InboxStatus.Open;

    /// <summary>True for an open decision item. Such an item holds the shared clock for its manager.</summary>
    public bool IsOpenDecision => IsOpen && NeedsDecision;

    internal InboxItem Closed(InboxStatus status, GameDate on, string? chosenOptionId) =>
        new(Number, ManagerId, Created, Draft, status, on, chosenOptionId);

    private static void ValidateChoice(InboxItemDraft draft, InboxStatus status, string? chosenOptionId)
    {
        switch (status)
        {
            case InboxStatus.Open:
            case InboxStatus.Dismissed:
                if (chosenOptionId is not null)
                {
                    throw new ArgumentException("Only a resolved or lapsed item has a chosen option.", nameof(chosenOptionId));
                }

                if (status == InboxStatus.Dismissed && draft.NeedsDecision)
                {
                    throw new ArgumentException("A decision item cannot be dismissed.", nameof(status));
                }

                break;
            case InboxStatus.Resolved:
                if (!draft.NeedsDecision || chosenOptionId is null || !draft.Options.Any(option => option.Id == chosenOptionId))
                {
                    throw new ArgumentException("A resolved item is a decision with one of its options chosen.", nameof(chosenOptionId));
                }

                break;
            case InboxStatus.Expired:
                // An item with no validity date can only be withdrawn by its poster (T45: a dismissed manager can no longer
                // answer their open decisions), which closes it with no option taken.
                if (draft.ValidUntil is null && chosenOptionId is not null)
                {
                    throw new ArgumentException("Only an item with a validity date can take its default option.", nameof(status));
                }

                if (chosenOptionId is not null && chosenOptionId != draft.DefaultOptionId)
                {
                    throw new ArgumentException("A lapsed item can only have taken its default option.", nameof(chosenOptionId));
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown status.");
        }
    }
}
