using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Board;

/// <summary>
/// Public facts a team can be ranked by. The board and the sponsors both read this, so a sponsor's target uses the same
/// budget rank the board blends into an expected position. Last season's championship place is supplied by the caller
/// when the standings exist; it is not stored here.
/// </summary>
public static class PublicStrength
{
    public static IReadOnlyList<Organization> ActiveTeams(WorldState world, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.Organizations
            .Where(organization => organization.Kind == OrganizationKind.Team
                && organization.Founded <= today
                && (organization.Dissolved is null || organization.Dissolved >= today))
            .OrderBy(organization => organization.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>Rank of the organization's budget among the active teams (1 is the richest). A missing team ranks last.</summary>
    public static int BudgetRank(WorldState world, OrganizationId organization, GameDate today)
    {
        var teams = ActiveTeams(world, today);
        var own = teams.FirstOrDefault(team => team.Id == organization);
        if (own is null)
        {
            return Math.Max(1, teams.Count);
        }

        return 1 + teams.Count(team => team.Budget > own.Budget
            || (team.Budget == own.Budget && string.CompareOrdinal(team.Id.Value, own.Id.Value) < 0));
    }
}
