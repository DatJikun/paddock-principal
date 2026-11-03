using System.Collections.Immutable;
using System.Globalization;
using Paddock.Application.Career;
using Paddock.Domain.Finance;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Career;
using Paddock.Simulation.Racing.Points;

namespace Paddock.Application.Racing;

/// <summary>A row of the table before and after the round: place (null when not in the table before) and counted points.</summary>
public sealed record SummaryStandingView(string Id, string Name, int? Before, int After, string PointsBefore, string PointsAfter);

/// <summary>One ledger line of the race day for the team: category, reason key and amount (negative is a cost).</summary>
public sealed record SummaryMoneyView(string Category, string ReasonKey, long AmountCents);

/// <summary>
/// The post-race summary of one round for one manager (#316): the round's result and report (the same as the round result), what
/// the round changed in the tables for the team and its drivers, the team's money of the race day, its balance and the board's
/// mood after the day's review. Derived from the world only (INV-005).
/// </summary>
public sealed record RaceSummaryView(
    bool Found,
    int Season,
    int Round,
    RaceResultView Result,
    string? TeamId,
    SummaryStandingView? Team,
    IReadOnlyList<SummaryStandingView> Drivers,
    IReadOnlyList<SummaryMoneyView> Money,
    long? BalanceCents,
    Paddock.Application.Board.BoardStateView? Board);

public static class RaceSummaryRead
{
    public static RaceSummaryView Read(
        CareerSession session,
        CareerInputs inputs,
        OrganizationId? observer,
        int? season,
        int? round,
        Paddock.Application.Board.BoardStateView? board = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(inputs);
        var result = ChampionshipRead.Result(session, season, round);
        if (!result.Found)
        {
            return new RaceSummaryView(false, result.Season, result.Round, result, null, null, [], [], null, null);
        }

        var team = observer is { IsAssigned: true } own ? own.Value : null;
        var world = session.World;
        var (teamRow, drivers) = Tables(session, inputs, result, team);
        var money = new List<SummaryMoneyView>();
        long? balance = null;
        var finance = world.Section<FinanceSection>(FinanceSection.SectionName);
        if (team is not null && observer is OrganizationId organization && finance is not null && finance.HasBook(organization)
            && RaceDay(world, result.Season, result.Round) is { } day)
        {
            money.AddRange(finance.EntriesOf(organization)
                .Where(entry => entry.Date == day)
                .Select(entry => new SummaryMoneyView(entry.Category, entry.ReasonKey, entry.AmountCents)));
            balance = finance.BalanceOf(organization);
        }

        return new RaceSummaryView(true, result.Season, result.Round, result, team, teamRow, drivers, money, balance, team is null ? null : board);
    }

    private static GameDate? RaceDay(WorldState world, int season, int round) =>
        world.Section<RaceCalendarSection>(RaceCalendarSection.SectionName)?.SeasonSessions(season)
            .FirstOrDefault(session => session.Round == round && session.TypeId == Paddock.Simulation.Time.ScheduledEventType.Race)?.Date;

    /// <summary>
    /// The team's and its drivers' places before and after the round. "Before" is the table restored without the round's points and
    /// finishing places, with the same rules, so ties break as they did. Only for the latest round of the season being raced.
    /// </summary>
    private static (SummaryStandingView? Team, IReadOnlyList<SummaryStandingView> Drivers) Tables(
        CareerSession session,
        CareerInputs inputs,
        RaceResultView result,
        string? team)
    {
        var section = session.World.Section<ChampionshipSection>(ChampionshipSection.SectionName);
        if (team is null || section is null || section.Season != result.Season || section.RoundsCompleted < 1
            || ChampionshipRead.RulesFor(inputs, session.World, section.Season) is not { } rules)
        {
            return (null, []);
        }

        var latest = session.World.Section<RaceResultsSection>(RaceResultsSection.SectionName)?.Latest();
        if (latest is null || latest.Season != result.Season || latest.Round != result.Round)
        {
            return (null, []);
        }

        var points = PointsRules.For(rules);
        var after = Standings.Restore(points, section.TotalRounds, section.RoundsCompleted, Snapshots(section.Drivers), Snapshots(section.Constructors));
        var placed = latest.Rows.Where(row => row.Classified).ToArray();
        var before = Standings.Restore(
            points,
            section.TotalRounds,
            section.RoundsCompleted - 1,
            Strip(section.Drivers, id => placed.Where(row => row.DriverId == id).Select(row => row.Position)),
            Strip(section.Constructors, id => placed.Where(row => row.TeamId == id).Select(row => row.Position)));

        var ownDrivers = latest.Rows.Where(row => row.TeamId == team).Select(row => row.DriverId).ToHashSet(StringComparer.Ordinal);
        var drivers = after.Drivers()
            .Where(row => ownDrivers.Contains(row.Id))
            .Select(row => Row(row, before.Drivers(), ChampionshipRead.PersonName(session.World, row.Id)))
            .ToArray();
        var teamRow = after.Constructors().FirstOrDefault(row => row.Id == team) is { } constructor
            ? Row(constructor, before.Constructors(), LiveRaceRead.TeamName(session.World, team))
            : null;
        return (teamRow, drivers);
    }

    private static SummaryStandingView Row(StandingsRow after, ImmutableArray<StandingsRow> before, string name)
    {
        var earlier = before.FirstOrDefault(row => row.Id == after.Id);
        var hadPoints = earlier is not null && (earlier.TotalPoints != 0 || earlier.FinishCounts.Any(count => count > 0));
        return new SummaryStandingView(
            after.Id,
            name,
            hadPoints ? earlier!.Position : null,
            after.Position,
            Points(earlier?.CountedPoints ?? 0m),
            Points(after.CountedPoints));
    }

    private static Standings.LedgerSnapshot[] Snapshots(IReadOnlyList<ChampionshipLedger> rows) =>
        [.. rows.Select(row => new Standings.LedgerSnapshot(row.Id, [.. row.RoundPoints], [.. row.Positions]))];

    /// <summary>The ledgers without their last round: the last round's points and one copy of each place it added.</summary>
    private static Standings.LedgerSnapshot[] Strip(IReadOnlyList<ChampionshipLedger> rows, Func<string, IEnumerable<int>> lastPlaces)
    {
        var snapshots = new List<Standings.LedgerSnapshot>(rows.Count);
        foreach (var row in rows)
        {
            var positions = row.Positions.ToList();
            foreach (var place in lastPlaces(row.Id))
            {
                var at = positions.LastIndexOf(place);
                if (at >= 0)
                {
                    positions.RemoveAt(at);
                }
            }

            snapshots.Add(new Standings.LedgerSnapshot(row.Id, [.. row.RoundPoints.Take(row.RoundPoints.Count - 1)], [.. positions]));
        }

        return [.. snapshots];
    }

    private static string Points(decimal points) => points.ToString(CultureInfo.InvariantCulture);
}
