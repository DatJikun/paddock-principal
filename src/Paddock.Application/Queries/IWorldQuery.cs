using Paddock.Application.Managers;

namespace Paddock.Application.Queries;

/// <summary>
/// Read side of the core. Every query is scoped to one manager (INV-003).
/// Implementations must not mutate the world and must not draw RNG (INV-005).
/// </summary>
public interface IWorldQuery
{
    ManagerView ViewFor(ManagerId managerId);
}
