using Paddock.Application.Commands;
using Paddock.Application.Managers;

namespace Paddock.Application.Inbox;

/// <summary>An item was posted to a manager's inbox.</summary>
public sealed record InboxItemCreated(ManagerId ManagerId, DateOnly OccurredOn, string ItemId, string Kind, bool NeedsDecision) : IDomainEvent
{
    public string TypeId => "inbox.item.created";
}

/// <summary>A manager chose an option of a decision item and its owner executed it.</summary>
public sealed record InboxItemResolved(ManagerId ManagerId, DateOnly OccurredOn, string ItemId, string Kind, string OptionId) : IDomainEvent
{
    public string TypeId => "inbox.item.resolved";
}

/// <summary>
/// An item passed its validity date. <paramref name="OptionId"/> is the default option that was executed, or null when
/// the item had none (information) or its owner could no longer accept it.
/// </summary>
public sealed record InboxItemExpired(ManagerId ManagerId, DateOnly OccurredOn, string ItemId, string Kind, string? OptionId) : IDomainEvent
{
    public string TypeId => "inbox.item.expired";
}
