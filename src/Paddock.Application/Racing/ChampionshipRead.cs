using System.Globalization;
using Paddock.Application.Career;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Career;
using Paddock.Simulation.Racing.Points;
using Paddock.Simulation.Time;

namespace Paddock.Application.Racing;

/// <summary>The public name of a circuit, for a calendar tile. Not a hidden rating.</summary>
public sealed record CircuitLabel(string CircuitId, string Name, string Country);

/// <summary>One championship round: the days still on the clock, the layout, and whether the race has a result.</summary>
public sealed record CalendarRoundView(
    int Season,
    int Round,
    string LayoutId,
    string CircuitId,
    string CircuitName,
    string Country,
    string? Practice,
    string? Qualifying,
    string? Race,
    bool Finished);

/// <summary>The season's rounds, in order. A pure read (INV-005).</summary>
public sealed record CalendarView(int Season, IReadOnlyList<CalendarRoundView> Rounds);

/// <summary>The era's points table, as the catalog wrote it. Names are the catalog values, which are identifiers.</summary>
public sealed record PointsScaleView(
    IReadOnlyList<int> PositionPoints,
    string FastestLap,
    string ResultsCounting,
    bool DoublePointsFinale,
    string Constructors);

/// <summary>One line of a table. <see cref="Points"/> is the counted total, in invariant text.</summary>
public sealed record StandingRowView(int Position, string Id, string Name, string Points, int Wins);

/// <summary>Drivers and constructors after the era's points rules. Empty until the first race.</summary>
public sealed record StandingsView(
    int Season,
    int RoundsCompleted,
    int TotalRounds,
    PointsScaleView? Rules,
    IReadOnlyList<StandingRowView> Drivers,
    IReadOnlyList<StandingRowView> Constructors);

/// <summary>One classification row. <see cref="RetirementKey"/> is empty when the car was classified.</summary>
public sealed record RaceRowView(int Position, bool Classified, string DriverId, string DriverName, string TeamId, string TeamName, string Points, string RetirementKey);

/// <summary>One report argument. A person or team value is the name the player may see.</summary>
public sealed record ReportArgView(string Name, string Value);

/// <summary>One report line: a key, its arguments, and a plural count when the line has one.</summary>
public sealed record ReportLineView(string Key, IReadOnlyList<ReportArgView> Args, double? Count);

/// <summary>One section of the report. The title is a line.</summary>
public sealed record ReportSectionView(ReportLineView Title, IReadOnlyList<ReportLineView> Lines);

/// <summary>A finished round, or an empty view when that round has not been run.</summary>
public sealed record RaceResultView(
    bool Found,
    int Season,
    int Round,
    string? LayoutId,
    IReadOnlyList<RaceRowView> Rows,
    IReadOnlyList<ReportSectionView> Sections);

/// <summary>The next race on the shared calendar, for the top bar.</summary>
public sealed record NextRaceView(int? Season, int? Round, string? Date, string? LayoutId, string? CircuitId, string? CircuitName, string? Country);

/// <summary>Calendar, standings and race results as the career stored them. No state change and no RNG (INV-005).</summary>
public static class ChampionshipRead
{
    public static CalendarView Calendar(CareerSession session, CareerInputs inputs, IReadOnlyDictionary<string, CircuitLabel> circuits)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(circuits);
        var season = session.Date.Year;
        var finished = FinishedRounds(session, season);
        var rounds = new Dictionary<int, RoundBuilder>();
        foreach (var scheduled in session.Clock.Queue.Events)
        {
            if (scheduled.Payload is not RaceSessionPayload payload || payload.Season != season)
            {
                continue;
            }

            if (!rounds.TryGetValue(payload.Round, out var builder))
            {
                builder = new RoundBuilder(payload.Round, payload.LayoutId);
                rounds.Add(payload.Round, builder);
            }

            var date = scheduled.Date.ToString();
            if (scheduled.TypeId == ScheduledEventType.Practice)
            {
                builder.Practice = date;
            }
            else if (scheduled.TypeId == ScheduledEventType.Qualifying)
            {
                builder.Qualifying = date;
            }
            else if (scheduled.TypeId == ScheduledEventType.Race)
            {
                builder.Race = date;
                builder.LayoutId = payload.LayoutId;
            }
        }

        foreach (var race in finished)
        {
            if (!rounds.ContainsKey(race.Round))
            {
                rounds.Add(race.Round, new RoundBuilder(race.Round, race.LayoutId));
            }
        }

        var views = new List<CalendarRoundView>(rounds.Count);
        foreach (var builder in rounds.Values.OrderBy(item => item.Round))
        {
            circuits.TryGetValue(builder.LayoutId, out var circuit);
            views.Add(new CalendarRoundView(
                season,
                builder.Round,
                builder.LayoutId,
                circuit?.CircuitId ?? builder.LayoutId,
                circuit?.Name ?? builder.LayoutId,
                circuit?.Country ?? "",
                builder.Practice,
                builder.Qualifying,
                builder.Race,
                finished.Any(race => race.Round == builder.Round)));
        }

        return new CalendarView(season, views);
    }

    public static StandingsView Standings(CareerSession session, CareerInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(inputs);
        var section = session.World.Section<ChampionshipSection>(ChampionshipSection.SectionName);
        if (section is null || section.Season != session.Date.Year)
        {
            return new StandingsView(session.Date.Year, 0, 0, null, [], []);
        }

        var rules = Rules(inputs, session.World, section.Season);
        if (rules is null)
        {
            return new StandingsView(section.Season, section.RoundsCompleted, section.TotalRounds, null, [], []);
        }

        var points = PointsRules.For(rules);
        var table = Paddock.Simulation.Racing.Points.Standings.Restore(
            points,
            section.TotalRounds,
            section.RoundsCompleted,
            Snapshots(section.Drivers),
            Snapshots(section.Constructors));
        return new StandingsView(
            section.Season,
            section.RoundsCompleted,
            section.TotalRounds,
            new PointsScaleView(
                points.PositionPoints.ToArray(),
                points.FastestLap.ToString()!,
                points.ResultsCounting.ToString()!,
                points.DoublePointsFinale,
                points.ConstructorCounting.ToString()!),
            table.Drivers().Select(row => DriverRow(session.World, row)).ToArray(),
            table.Constructors().Select(row => TeamRow(session.World, session.Date, row)).ToArray());
    }

    public static RaceResultView Result(CareerSession session, int? season, int? round)
    {
        ArgumentNullException.ThrowIfNull(session);
        var archive = session.World.Section<RaceResultsSection>(RaceResultsSection.SectionName);
        var race = archive is null
            ? null
            : season is int wantedSeason && round is int wantedRound
                ? archive.Find(wantedSeason, wantedRound)
                : archive.Latest();
        if (race is null)
        {
            return new RaceResultView(false, season ?? session.Date.Year, round ?? 0, null, [], []);
        }

        var rows = new RaceRowView[race.Rows.Count];
        for (var i = 0; i < rows.Length; i++)
        {
            var row = race.Rows[i];
            rows[i] = new RaceRowView(
                row.Position,
                row.Classified,
                row.DriverId,
                PersonName(session.World, row.DriverId),
                row.TeamId,
                TeamName(session.World, session.Date, row.TeamId),
                row.Points,
                row.RetirementKey);
        }

        var sections = new ReportSectionView[race.Sections.Count];
        for (var i = 0; i < sections.Length; i++)
        {
            var section = race.Sections[i];
            sections[i] = new ReportSectionView(
                Line(session.World, section.Title),
                section.Lines.Select(line => Line(session.World, line)).ToArray());
        }

        return new RaceResultView(true, race.Season, race.Round, race.LayoutId, rows, sections);
    }

    public static NextRaceView Next(CareerSession session, IReadOnlyDictionary<string, CircuitLabel> circuits)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(circuits);
        foreach (var scheduled in session.Clock.Queue.Events)
        {
            if (scheduled.TypeId != ScheduledEventType.Race || scheduled.Date < session.Date)
            {
                continue;
            }

            if (scheduled.Payload is not RaceSessionPayload payload)
            {
                continue;
            }

            circuits.TryGetValue(payload.LayoutId, out var circuit);
            return new NextRaceView(
                payload.Season,
                payload.Round,
                scheduled.Date.ToString(),
                payload.LayoutId,
                circuit?.CircuitId ?? payload.LayoutId,
                circuit?.Name ?? payload.LayoutId,
                circuit?.Country ?? "");
        }

        return new NextRaceView(null, null, null, null, null, null, null);
    }

    private static List<StoredRace> FinishedRounds(CareerSession session, int season)
    {
        var archive = session.World.Section<RaceResultsSection>(RaceResultsSection.SectionName);
        var list = new List<StoredRace>();
        if (archive is null)
        {
            return list;
        }

        foreach (var race in archive.Races)
        {
            if (race.Season == season)
            {
                list.Add(race);
            }
        }

        return list;
    }

    private static RuleSet? Rules(CareerInputs inputs, WorldState world, int season)
    {
        var stored = world.Section<RegulationsSection>(RegulationsSection.SectionName);
        if (stored is not null && stored.Season == season)
        {
            return stored.ToRuleSet();
        }

        if (inputs.RegulationDimensionIds is not { } dimensions || inputs.RulePeriods is not { } periods)
        {
            return null;
        }

        return RuleSet.For(season, dimensions, periods);
    }

    private static Standings.LedgerSnapshot[] Snapshots(IReadOnlyList<ChampionshipLedger> rows)
    {
        var snapshots = new Standings.LedgerSnapshot[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            snapshots[i] = new Standings.LedgerSnapshot(rows[i].Id, [.. rows[i].RoundPoints], [.. rows[i].Positions]);
        }

        return snapshots;
    }

    private static StandingRowView DriverRow(WorldState world, StandingsRow row) =>
        new(row.Position, row.Id, PersonName(world, row.Id), Points(row.CountedPoints), row.Wins);

    private static StandingRowView TeamRow(WorldState world, GameDate on, StandingsRow row) =>
        new(row.Position, row.Id, TeamName(world, on, row.Id), Points(row.CountedPoints), row.Wins);

    private static string Points(decimal points) => points.ToString(CultureInfo.InvariantCulture);

    private static string PersonName(WorldState world, string id)
    {
        foreach (var person in world.Persons)
        {
            if (person.Id.Value == id)
            {
                return person.Name;
            }
        }

        return id;
    }

    private static string TeamName(WorldState world, GameDate on, string id)
    {
        foreach (var organization in world.Organizations)
        {
            if (organization.Id.Value == id)
            {
                return organization.NameOn(on);
            }
        }

        return id;
    }

    private static ReportLineView Line(WorldState world, StoredReportLine line)
    {
        var args = new ReportArgView[line.Args.Count];
        for (var i = 0; i < args.Length; i++)
        {
            args[i] = new ReportArgView(line.Args[i].Name, Show(world, line.Args[i].Value));
        }

        double? count = null;
        if (line.Count is not null && double.TryParse(line.Count, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            count = parsed;
        }

        return new ReportLineView(line.Key, args, count);
    }

    private static string Show(WorldState world, string value)
    {
        if (value.StartsWith(RaceArchive.PersonPrefix, StringComparison.Ordinal))
        {
            return PersonName(world, value[RaceArchive.PersonPrefix.Length..]);
        }

        if (value.StartsWith(RaceArchive.TeamPrefix, StringComparison.Ordinal))
        {
            return TeamName(world, world.CurrentDate, value[RaceArchive.TeamPrefix.Length..]);
        }

        if (value.StartsWith(RaceArchive.PeoplePrefix, StringComparison.Ordinal))
        {
            var ids = value[RaceArchive.PeoplePrefix.Length..].Split(',', StringSplitOptions.RemoveEmptyEntries);
            return string.Join(", ", ids.Select(id => PersonName(world, id)));
        }

        return value;
    }

    private sealed class RoundBuilder
    {
        public RoundBuilder(int round, string layoutId)
        {
            Round = round;
            LayoutId = layoutId;
        }

        public int Round { get; }

        public string LayoutId { get; set; }

        public string? Practice { get; set; }

        public string? Qualifying { get; set; }

        public string? Race { get; set; }
    }
}
