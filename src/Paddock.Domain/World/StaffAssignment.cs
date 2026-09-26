namespace Paddock.Domain.World;

/// <summary>
/// One staff stint: a person holding a role at an organization for an inclusive span of seasons.
/// </summary>
public readonly record struct StaffAssignment(
    string PersonId,
    string Org,
    string Role,
    int FromYear,
    int ToYear);
