using Paddock.Application.Managers;

namespace Paddock.Application.Queries;

/// <summary>
/// Returns an empty <see cref="ManagerView"/> for the requested manager.
/// </summary>
public sealed class StubWorldQuery : IWorldQuery
{
    public ManagerView ViewFor(ManagerId managerId) => new(managerId);
}
