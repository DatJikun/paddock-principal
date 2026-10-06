using Paddock.Application.Managers;
using Paddock.Domain.Inbox;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Inbox;

/// <summary>
/// The live inbox: the <see cref="InboxSection"/> the host holds between days, the resolvers of the game, and the rule that
/// an open decision item holds the shared clock for its manager (DESIGN §11, only decisions block time).
/// <para>
/// The block uses the manager's single <see cref="BlockingItem"/> slot with the kind <see cref="BlockingKind"/>:
/// it is posted with the first open decision and cleared when the last one is answered or lapses.
/// If another system holds the slot, the inbox leaves it alone and <see cref="SyncBlocking"/> takes the slot when it frees up.
/// Posting is how owning systems write to an inbox; they build the draft from what that manager knows (INV-003).
/// A manager's items are only ever shown through <see cref="InboxQuery"/>.
/// </para>
/// </summary>
public sealed class InboxBook
{
    /// <summary>Kind of the blocking item the inbox posts. A stable code, not text.</summary>
    public const string BlockingKind = "inbox.decision";

    public InboxBook(InboxResolvers? resolvers = null, InboxSection? initial = null)
    {
        Resolvers = resolvers ?? new InboxResolvers();
        Section = initial ?? InboxSection.Empty;
    }

    public InboxResolvers Resolvers { get; }

    /// <summary>The current inbox contents (immutable value).</summary>
    public InboxSection Section { get; private set; }

    /// <summary>The book for a world: its inbox section, or an empty one when it has none.</summary>
    public static InboxBook From(WorldState world, InboxResolvers? resolvers = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        return new InboxBook(resolvers, world.Section<InboxSection>(InboxSection.SectionName));
    }

    /// <summary>
    /// The world with this inbox in it. An inbox that never held an item is left out, so a world that does not use
    /// the inbox keeps its hash.
    /// </summary>
    public WorldState Into(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return Section.NextNumber == 1 ? world.WithoutSection(InboxSection.SectionName) : world.WithSection(Section);
    }

    /// <summary>
    /// Posts an item to a manager's inbox and, for a decision, holds the clock for that manager.
    /// A decision needs a registered resolver for its kind, otherwise nobody could carry the answer out.
    /// </summary>
    public InboxItemCreated Post(ManagerRegistry managers, ManagerId manager, InboxItemDraft draft, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(managers);
        ArgumentNullException.ThrowIfNull(draft);
        if (!managers.Contains(manager))
        {
            throw new ArgumentException($"Unknown manager '{manager}'.", nameof(manager));
        }

        if (draft.NeedsDecision && Resolvers.Find(draft.Kind) is null)
        {
            throw new InvalidOperationException($"No resolver is registered for the decision kind '{draft.Kind}'.");
        }

        var (section, item) = Section.Add(manager.Value, draft, today);
        Section = section;
        SyncBlocking(managers, manager);
        return new InboxItemCreated(manager, ToDateOnly(today), item.Id, item.Kind, item.NeedsDecision);
    }

    /// <summary>Records the answer to a decision. Carrying it out is the resolver's part, which runs first.</summary>
    public void Resolve(ManagerRegistry managers, string itemId, string optionId, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(managers);
        var item = Section.Find(itemId) ?? throw new InvalidOperationException($"Unknown inbox item '{itemId}'.");
        Section = Section.Resolve(itemId, optionId, today);
        SyncBlocking(managers, new ManagerId(item.ManagerId));
    }

    public void Dismiss(ManagerRegistry managers, string itemId, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(managers);
        var item = Section.Find(itemId) ?? throw new InvalidOperationException($"Unknown inbox item '{itemId}'.");
        Section = Section.Dismiss(itemId, today);
        SyncBlocking(managers, new ManagerId(item.ManagerId));
    }

    /// <summary>Closes an open item with no option taken (see <see cref="InboxSection.Withdraw"/>) and frees the clock if it was the last decision.</summary>
    public void Withdraw(ManagerRegistry managers, string itemId, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(managers);
        var item = Section.Find(itemId) ?? throw new InvalidOperationException($"Unknown inbox item '{itemId}'.");
        Section = Section.Withdraw(itemId, today);
        SyncBlocking(managers, new ManagerId(item.ManagerId));
    }

    public void Expire(ManagerRegistry managers, string itemId, GameDate today, bool applyDefault)
    {
        ArgumentNullException.ThrowIfNull(managers);
        var item = Section.Find(itemId) ?? throw new InvalidOperationException($"Unknown inbox item '{itemId}'.");
        Section = Section.Expire(itemId, today, applyDefault);
        SyncBlocking(managers, new ManagerId(item.ManagerId));
    }

    /// <summary>
    /// Brings every manager's blocking slot in line with the inbox: held while a decision is open, released when none is.
    /// Safe to call at any time. A slot held by another system is never touched.
    /// </summary>
    public void SyncBlocking(ManagerRegistry managers)
    {
        ArgumentNullException.ThrowIfNull(managers);
        foreach (var manager in managers.All)
        {
            SyncBlocking(managers, manager.Id);
        }
    }

    private void SyncBlocking(ManagerRegistry managers, ManagerId manager)
    {
        var current = managers.Get(manager).BlockingItem;
        var wanted = Section.HoldingClockCount(manager.Value) > 0;
        if (wanted && current is null)
        {
            managers.PostBlockingItem(manager, new BlockingItem(BlockingKind));
        }
        else if (!wanted && current?.Kind == BlockingKind)
        {
            managers.ClearBlockingItem(manager);
        }
    }

    internal static DateOnly ToDateOnly(GameDate date) => new(date.Year, date.Month, date.Day);

    internal static GameDate ToGameDate(DateOnly date) => new(date.Year, date.Month, date.Day);
}
