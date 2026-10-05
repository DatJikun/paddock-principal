using Paddock.Domain.Finance;
using Paddock.Domain.World;
using Paddock.Simulation.Racing.Points;

namespace Paddock.Application.Finance;

/// <summary>
/// Builds the finance records from a T27 <see cref="Standings"/> table. T47 will publish the same records;
/// this adapter is how a test, or a host that already has the table, does it without finance referencing race code
/// the other way around.
/// </summary>
public static class FinanceStandings
{
    public static SeasonEnded Season(
        int season,
        int races,
        IReadOnlyList<StandingsRow> constructors,
        IReadOnlyList<string> distinctWinningOrganizationIds,
        IReadOnlyList<StandingsRow>? drivers = null)
    {
        ArgumentNullException.ThrowIfNull(constructors);
        ArgumentNullException.ThrowIfNull(distinctWinningOrganizationIds);
        var rows = constructors
            .Select(row => new ConstructorTitleRow(FinanceIds.Parse(row.Id), row.Position, row.CountedPoints))
            .ToArray();
        var winners = distinctWinningOrganizationIds.Select(FinanceIds.Parse).ToArray();
        var driverRows = (drivers ?? [])
            .Select(row => new DriverTitleRow(row.Id, row.Position, row.CountedPoints))
            .ToArray();
        return new SeasonEnded(season, races, rows, winners, driverRows);
    }

    public static RaceResultsPublished Race(
        int season,
        int round,
        int racesInSeason,
        string revenueModel,
        IReadOnlyList<(string OrganizationId, string DriverId, int Position, bool Classified)> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var rows = entries
            .Select(entry => new RaceEntryResult(FinanceIds.Parse(entry.OrganizationId), entry.DriverId, entry.Position, entry.Classified))
            .ToArray();
        return new RaceResultsPublished(season, round, racesInSeason, revenueModel, rows);
    }
}
