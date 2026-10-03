using Paddock.Application.Commands;
using Paddock.Application.Managers;
using Paddock.Domain.Inbox;
using Paddock.Domain.Time;

namespace Paddock.Application.Inbox;

/// <summary>
/// The manager answers a decision item by choosing one of its options. Only the item's own manager can do it;
/// anyone else gets the same reply as for an item that does not exist, so ids reveal nothing (INV-003).
/// </summary>
public sealed record ResolveInboxItemCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string ItemId { get; init; }

    public required string OptionId { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>The manager closes an information item. A decision cannot be dismissed, it has to be answered.</summary>
public sealed record DismissInboxItemCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string ItemId { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>
/// Issued by the host for the item's own manager when an item is past its validity date (see <see cref="InboxExpiry"/>).
/// The declared default option is executed, or, when the item has none or its owner can no longer accept it,
/// the item only lapses. It is a command so that expiry is logged and replayed like any other change (INV-001, INV-002).
/// </summary>
public sealed record ExpireInboxItemCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string ItemId { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

public sealed class ResolveInboxItemHandler : CommandHandler<ResolveInboxItemCommand>
{
    protected override TranslationMessage? ValidateTyped(ResolveInboxItemCommand command, CommandContext context)
    {
        var item = InboxHandlers.OwnItem(command.ManagerId, command.ItemId, context);
        if (item is null)
        {
            return TranslationMessage.Of(InboxKeys.ItemUnknown);
        }

        if (!item.IsOpen)
        {
            return TranslationMessage.Of(InboxKeys.ItemClosed);
        }

        if (!item.NeedsDecision)
        {
            return TranslationMessage.Of(InboxKeys.NotADecision);
        }

        if (!item.Options.Any(option => option.Id == command.OptionId))
        {
            return TranslationMessage.Of(InboxKeys.OptionUnknown);
        }

        // Posting a decision requires a resolver, so a missing one means the host changed its registrations since.
        var resolver = context.Inbox.Resolvers.Find(item.Kind);
        if (resolver is null)
        {
            return TranslationMessage.Of(InboxKeys.ItemUnknown);
        }

        return resolver.Validate(item, command.OptionId, context);
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(ResolveInboxItemCommand command, CommandContext context)
    {
        var item = InboxHandlers.OwnItem(command.ManagerId, command.ItemId, context)
            ?? throw new InvalidOperationException("Execute ran for a command that should have been rejected.");
        var resolver = context.Inbox.Resolvers.Find(item.Kind)
            ?? throw new InvalidOperationException("Execute ran for a command that should have been rejected.");

        // The owner's change first: it may post new items, and the answer is recorded on the section as it stands afterwards.
        var effects = resolver.Execute(item, command.OptionId, context);
        var today = InboxBook.ToGameDate(command.IssuedOn);
        context.Inbox.Resolve(context.Managers, item.Id, command.OptionId, today);
        var events = new List<IDomainEvent> { new InboxItemResolved(command.ManagerId, command.IssuedOn, item.Id, item.Kind, command.OptionId) };
        events.AddRange(effects);
        return events;
    }
}

public sealed class DismissInboxItemHandler : CommandHandler<DismissInboxItemCommand>
{
    protected override TranslationMessage? ValidateTyped(DismissInboxItemCommand command, CommandContext context)
    {
        var item = InboxHandlers.OwnItem(command.ManagerId, command.ItemId, context);
        if (item is null)
        {
            return TranslationMessage.Of(InboxKeys.ItemUnknown);
        }

        if (!item.IsOpen)
        {
            return TranslationMessage.Of(InboxKeys.ItemClosed);
        }

        return item.NeedsDecision ? TranslationMessage.Of(InboxKeys.DecisionCannotBeDismissed) : null;
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(DismissInboxItemCommand command, CommandContext context)
    {
        context.Inbox.Dismiss(context.Managers, command.ItemId, InboxBook.ToGameDate(command.IssuedOn));
        return [];
    }
}

public sealed class ExpireInboxItemHandler : CommandHandler<ExpireInboxItemCommand>
{
    protected override TranslationMessage? ValidateTyped(ExpireInboxItemCommand command, CommandContext context)
    {
        var item = InboxHandlers.OwnItem(command.ManagerId, command.ItemId, context);
        if (item is null)
        {
            return TranslationMessage.Of(InboxKeys.ItemUnknown);
        }

        if (!item.IsOpen)
        {
            return TranslationMessage.Of(InboxKeys.ItemClosed);
        }

        var today = InboxBook.ToGameDate(command.IssuedOn);
        return item.ValidUntil is GameDate until && until < today ? null : TranslationMessage.Of(InboxKeys.NotYetLapsed);
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(ExpireInboxItemCommand command, CommandContext context)
    {
        var item = InboxHandlers.OwnItem(command.ManagerId, command.ItemId, context)
            ?? throw new InvalidOperationException("Execute ran for a command that should have been rejected.");
        var today = InboxBook.ToGameDate(command.IssuedOn);
        var events = new List<IDomainEvent>();
        string? applied = null;
        IReadOnlyList<IDomainEvent> effects = [];

        // The default option is declared when the item is posted, so what happens at expiry is known up front.
        // If its owner can no longer carry it out, the item lapses without effect instead of staying open forever.
        if (item.NeedsDecision && item.DefaultOptionId is string defaultOption
            && context.Inbox.Resolvers.Find(item.Kind) is IInboxResolver resolver
            && resolver.Validate(item, defaultOption, context) is null)
        {
            effects = resolver.Execute(item, defaultOption, context);
            applied = defaultOption;
        }

        context.Inbox.Expire(context.Managers, item.Id, today, applied is not null);
        events.Add(new InboxItemExpired(command.ManagerId, command.IssuedOn, item.Id, item.Kind, applied));
        events.AddRange(effects);
        return events;
    }
}

internal static class InboxHandlers
{
    /// <summary>The item, only when it belongs to <paramref name="manager"/>. Another manager's item reads as missing.</summary>
    public static InboxItem? OwnItem(ManagerId manager, string itemId, CommandContext context)
    {
        var item = context.Inbox.Section.Find(itemId);
        return item is not null && item.ManagerId == manager.Value ? item : null;
    }
}

/// <summary>Finds the items that have passed their validity date and asks for their expiry as commands.</summary>
public static class InboxExpiry
{
    /// <summary>
    /// Enqueues one <see cref="ExpireInboxItemCommand"/>, dated <paramref name="today"/> and issued for the item's own manager,
    /// for every open item whose validity date is before today, in order of item number. Returns how many were queued.
    /// A pure read of the inbox; the state changes when the dispatcher runs the commands.
    /// </summary>
    public static int EnqueueDue(CommandQueue queue, InboxBook book, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(book);
        var due = book.Section.Lapsed(today);
        foreach (var item in due)
        {
            queue.Enqueue(new ExpireInboxItemCommand
            {
                ManagerId = new ManagerId(item.ManagerId),
                IssuedOn = InboxBook.ToDateOnly(today),
                ItemId = item.Id,
            });
        }

        return due.Count;
    }
}
