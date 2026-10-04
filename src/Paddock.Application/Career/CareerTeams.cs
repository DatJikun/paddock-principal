using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Career;

/// <summary>The teams a career run looks after: one rule for finance, cars and the board, so they agree on who exists.</summary>
public static class CareerTeams
{
    /// <summary>The organizations that run a team on <paramref name="today"/>, in ordinal order of their id.</summary>
    public static IReadOnlyList<Organization> Active(WorldState world, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.Organizations
            .Where(organization => organization.Kind == OrganizationKind.Team
                && organization.Founded <= today
                && (organization.Dissolved is null || organization.Dissolved >= today))
            .OrderBy(organization => organization.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }
}
