using Paddock.Application.Managers;

namespace Paddock.Application.Queries;

/// <summary>
/// Empty manager-scoped view (INV-003). Later fields are filled only for <see cref="ManagerId"/>.
/// There is no parameterless constructor: a view without a manager id cannot be built.
/// </summary>
public sealed class ManagerView
{
    public ManagerView(ManagerId managerId)
    {
        if (!managerId.IsAssigned)
        {
            throw new ArgumentException("A manager view requires a manager id.", nameof(managerId));
        }

        ManagerId = managerId;
    }

    public ManagerId ManagerId { get; }
}
