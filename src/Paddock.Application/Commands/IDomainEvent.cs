using Paddock.Application.Managers;

namespace Paddock.Application.Commands;

public interface IDomainEvent
{
    string TypeId { get; }

    ManagerId ManagerId { get; }

    DateOnly OccurredOn { get; }
}
