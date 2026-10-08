using Paddock.Application.Access;
using Paddock.Application.Commands;
using Paddock.Domain.Inbox;
using Paddock.Domain.Time;

namespace Paddock.Application.Inbox;

/// <summary>One choice of a decision item, with what it does shown up front.</summary>
public sealed record InboxOptionView(string Id, TranslationMessage Label, TranslationMessage Consequence, bool IsDefault);

/// <summary>
/// One item as a manager reads it. Texts are translation keys with the item's argument values (PP-021).
/// <see cref="ValidUntil"/> and <see cref="DefaultOptionId"/> are set together for an offer that lapses:
/// the player can see what happens if nobody answers. <see cref="Important"/> says whether the item should stop automatic play (see <see cref="InboxImportance"/>).
/// </summary>
public sealed record InboxItemView(
    string Id,
    string ManagerId,
    string Kind,
    DateOnly Created,
    TranslationMessage Subject,
    bool NeedsDecision,
    IReadOnlyList<InboxOptionView> Options,
    DateOnly? ValidUntil,
    string? DefaultOptionId,
    InboxStatus Status,
    DateOnly? ClosedOn,
    string? ChosenOptionId,
    bool Important);

/// <summary>The items one viewer may see, in order of item number.</summary>
public sealed record InboxView(AccessContext Viewer, IReadOnlyList<InboxItemView> Items)
{
    public int OpenCount => Items.Count(item => item.Status == InboxStatus.Open);

    public int OpenDecisionCount => Items.Count(item => item.Status == InboxStatus.Open && item.NeedsDecision);
}

/// <summary>
/// The read side of the inbox (INV-003, INV-005). A manager or an AI manager sees only the items of their own inbox;
/// the developer sees every inbox. It changes nothing and draws no RNG.
/// </summary>
public sealed class InboxQuery
{
    private readonly InboxBook _book;

    public InboxQuery(InboxBook book)
    {
        ArgumentNullException.ThrowIfNull(book);
        _book = book;
    }

    public InboxView View(AccessContext access)
    {
        ArgumentNullException.ThrowIfNull(access);
        var section = _book.Section;
        IEnumerable<InboxItem> visible = access.Kind == AccessKind.Developer
            ? section.Items
            : section.ItemsOf((access.Manager ?? throw new InvalidOperationException("Non-developer context without a manager.")).Value);
        return new InboxView(access, visible.Select(Describe).ToArray());
    }

    private static InboxItemView Describe(InboxItem item)
    {
        var itemArguments = item.Arguments.Select(pair => (pair.Key, pair.Value)).ToArray();
        var options = item.Options
            .Select(option =>
            {
                (string Key, string Value)[] args;
                if (option.Arguments.Count == 0)
                {
                    args = itemArguments;
                }
                else
                {
                    var merged = new Dictionary<string, string>(item.Arguments, StringComparer.Ordinal);
                    foreach (var (k, v) in option.Arguments)
                    {
                        merged[k] = v;
                    }

                    args = merged.Select(pair => (pair.Key, pair.Value)).ToArray();
                }

                return new InboxOptionView(
                    option.Id,
                    TranslationMessage.Of(option.LabelKey, args),
                    TranslationMessage.Of(option.ConsequenceKey, args),
                    option.Id == item.DefaultOptionId);
            })
            .ToArray();
        return new InboxItemView(
            item.Id,
            item.ManagerId,
            item.Kind,
            InboxBook.ToDateOnly(item.Created),
            TranslationMessage.Of(item.SubjectKey, itemArguments),
            item.NeedsDecision,
            options,
            item.ValidUntil is GameDate until ? InboxBook.ToDateOnly(until) : null,
            item.DefaultOptionId,
            item.Status,
            item.ClosedOn is GameDate closed ? InboxBook.ToDateOnly(closed) : null,
            item.ChosenOptionId,
            InboxImportance.IsImportant(item.Kind, item.NeedsDecision));
    }
}
