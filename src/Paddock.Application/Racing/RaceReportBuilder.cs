using System.Collections.Immutable;
using Paddock.Domain.Racing;
using Paddock.Simulation.Racing.Incidents;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Points;
using Paddock.Simulation.Racing.Weekend;

namespace Paddock.Application.Racing;

/// <summary>
/// Turns one race (its tape and the weekend result) into a short narrative: conditions, qualifying, the race in
/// phases, the pit stops, shared drives, the result and the points. A pure function (INV-005): it reads values, draws no
/// random number, changes nothing and gives the same report for the same input. It returns keys and arguments, not
/// sentences (TECH 6.3); <see cref="RaceReportRenderer"/> makes the text.
/// <para>
/// The era is not written anywhere in here. A 1955 race reads differently from a 1988 one because its tape does: few
/// stops and many failures, shared cars, no safety car, or the other way round. Sections with nothing to say are left
/// out (shared drives, a safety car), not filled with filler.
/// </para>
/// </summary>
public static class RaceReportBuilder
{
    /// <summary>ESTIMATE (presentation only): the share of the laps that counts as the opening and as the closing phase.</summary>
    public const double PhaseFraction = 0.1;

    /// <summary>ESTIMATE (presentation only): places gained or lost on the opening lap that count as a big move.</summary>
    public const int BigMoveMinPlaces = 3;

    /// <summary>The most big gains and the most big losses of the opening lap that are named.</summary>
    public const int BigMovesShown = 3;

    /// <summary>Stops without a fault or a repair needed before the fastest and the slowest of them are named.</summary>
    public const int CleanStopsForExtremes = 3;

    public static RaceReport Build(RaceReportInput input, RaceReportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        var context = new Context(input);
        var sections = ImmutableArray.CreateBuilder<RaceReportSection>();
        sections.Add(Conditions(context));
        sections.Add(Qualifying(context));
        sections.AddRange(Phases(context));
        if (Pits(context) is { } pits)
        {
            sections.Add(pits);
        }

        if (Drives(context) is { } drives)
        {
            sections.Add(drives);
        }

        sections.Add(Result(context));
        sections.Add(Points(context));
        if (options?.Verbose == true)
        {
            sections.Add(Log(context));
        }

        return new RaceReport(
            RaceReportLine.Of(
                RaceReportKeys.Title,
                ("year", input.Season),
                ("round", input.Round),
                ("layout", input.TrackId),
                ("laps", input.ScheduledLaps)),
            sections.ToImmutable());
    }

    // ---- Conditions -----------------------------------------------------------------------------------------

    private static RaceReportSection Conditions(Context c) =>
        new(
            RaceReportLine.Of(RaceReportKeys.SectionConditions),
            [RaceReportLine.Of(RaceReportKeys.ConditionStartKey(c.Input.Conditions.Track), ("temp", Math.Round(c.Input.Conditions.AirTempC)))]);

    // ---- Qualifying -----------------------------------------------------------------------------------------

    private static RaceReportSection Qualifying(Context c)
    {
        var q = c.Input.Qualifying;
        var lines = ImmutableArray.CreateBuilder<RaceReportLine>();
        var pole = q.Grid.FirstOrDefault(s => s.GridPosition == 1);
        var second = q.Grid.FirstOrDefault(s => s.GridPosition == 2);
        var third = q.Grid.FirstOrDefault(s => s.GridPosition == 3);
        if (pole is not null)
        {
            lines.Add(second is null
                ? RaceReportLine.Of(
                    RaceReportKeys.QualifyingPoleAlone,
                    ("driver", new DriverRef(pole.DriverId)),
                    ("team", new TeamRef(pole.ConstructorId)),
                    ("time", LapTime.FromSeconds(pole.ScoreSeconds)))
                : RaceReportLine.Of(
                    RaceReportKeys.QualifyingPole,
                    ("driver", new DriverRef(pole.DriverId)),
                    ("team", new TeamRef(pole.ConstructorId)),
                    ("time", LapTime.FromSeconds(pole.ScoreSeconds)),
                    ("margin", new Seconds(second.ScoreSeconds - pole.ScoreSeconds)),
                    ("second", new DriverRef(second.DriverId))));
        }

        if (second is not null && pole is not null)
        {
            lines.Add(RaceReportLine.Of(
                RaceReportKeys.QualifyingSecond,
                ("driver", new DriverRef(second.DriverId)),
                ("team", new TeamRef(second.ConstructorId)),
                ("time", LapTime.FromSeconds(second.ScoreSeconds)),
                ("gap", new Seconds(second.ScoreSeconds - pole.ScoreSeconds))));
        }

        if (third is not null && pole is not null)
        {
            lines.Add(RaceReportLine.Of(
                RaceReportKeys.QualifyingThird,
                ("driver", new DriverRef(third.DriverId)),
                ("team", new TeamRef(third.ConstructorId)),
                ("time", LapTime.FromSeconds(third.ScoreSeconds)),
                ("gap", new Seconds(third.ScoreSeconds - pole.ScoreSeconds))));
        }

        var pitLane = q.Grid.Where(s => s.StartsFromPitLane).Select(s => s.DriverId).ToImmutableArray();
        if (pitLane.Length > 0)
        {
            lines.Add(RaceReportLine.Plural(RaceReportKeys.QualifyingPitLane, pitLane.Length, ("drivers", new DriverList(pitLane))));
        }

        var missed = q.NonQualifiers.Select(n => n.DriverId).ToImmutableArray();
        if (missed.Length > 0)
        {
            lines.Add(RaceReportLine.Plural(RaceReportKeys.QualifyingNotQualified, missed.Length, ("drivers", new DriverList(missed))));
        }

        return new RaceReportSection(RaceReportLine.Of(RaceReportKeys.SectionQualifying), lines.ToImmutable());
    }

    // ---- The race, in phases ---------------------------------------------------------------------------------

    private readonly record struct Item(int Lap, long Order, RaceReportLine Line);

    private static IEnumerable<RaceReportSection> Phases(Context c)
    {
        var total = Math.Max(1, c.Input.LapsRun);
        var span = Math.Max(1, (int)Math.Ceiling(total * PhaseFraction));
        var openingEnd = Math.Min(total, span);
        var closingStart = Math.Max(openingEnd + 1, total - span + 1);

        var items = new List<Item>();
        StartItems(c, items);
        var leader = default(string);
        foreach (var e in c.Input.Tape.Events)
        {
            var lap = Math.Clamp(e.Lap, 1, total);
            var order = e.Seq * 4L;
            switch (e)
            {
                case WeatherChange or Incident or SafetyCar:
                    items.Add(new Item(lap, order, c.LineFor(e)));
                    break;
                case RedFlag flag:
                    // A red flag that restarts the race says so; one that ends the race is the plain "stopped" line.
                    items.Add(new Item(lap, order, c.Input.Neutralisations.IsDefault || !c.Input.Neutralisations.Any(n => n.Kind == NeutralisationKind.RedFlag && n.IncidentLap == flag.Lap && n.RaceResumed)
                        ? c.LineFor(flag)
                        : RaceReportLine.Of(RaceReportKeys.RedFlagRestart, ("lap", flag.Lap))));
                    break;
                case Retirement retirement:
                    items.Add(new Item(lap, order, c.LineFor(retirement)));
                    if (c.WhoRetired(retirement) is { } person && person.Injury != InjuryGrade.None)
                    {
                        items.Add(new Item(lap, order + 1, RaceReportLine.Of(RaceReportKeys.InjuryKey(person.Injury), ("driver", new DriverRef(person.DriverId)))));
                    }

                    if (c.WhoRetired(retirement) is { Fatal: true } dead)
                    {
                        items.Add(new Item(lap, order + 2, RaceReportLine.Of(RaceReportKeys.InjuryFatal, ("driver", new DriverRef(dead.DriverId)))));
                    }

                    break;
                case LapCompleted { Position: 1 } front:
                    if (leader is not null && leader != front.DriverId)
                    {
                        items.Add(new Item(lap, order, RaceReportLine.Of(
                            RaceReportKeys.LeadChange,
                            ("lap", front.Lap),
                            ("driver", new DriverRef(front.DriverId)),
                            ("from", new DriverRef(leader)))));
                    }

                    leader = front.DriverId;
                    break;
            }
        }

        var phases = new List<(string Key, int From, int To)> { (RaceReportKeys.PhaseOpening, 1, openingEnd) };
        if (closingStart - 1 >= openingEnd + 1)
        {
            phases.Add((RaceReportKeys.PhaseMiddle, openingEnd + 1, closingStart - 1));
        }

        if (closingStart <= total)
        {
            phases.Add((RaceReportKeys.PhaseClosing, closingStart, total));
        }

        foreach (var (key, from, to) in phases)
        {
            var lines = items
                .Where(i => i.Lap >= from && i.Lap <= to)
                .OrderBy(i => i.Order)
                .Select(i => i.Line)
                .ToImmutableArray();
            yield return new RaceReportSection(
                RaceReportLine.Of(key, ("from", from), ("to", to)),
                lines.IsEmpty ? [RaceReportLine.Of(RaceReportKeys.PhaseQuiet)] : lines);
        }
    }

    private static void StartItems(Context c, List<Item> items)
    {
        var order = -1000L;
        var firstLap = c.Input.Tape.Events.OfType<LapCompleted>().Where(l => l.Lap == 1).ToList();
        var pole = c.Input.Qualifying.Grid.FirstOrDefault(s => s.GridPosition == 1)?.DriverId;
        if (firstLap.FirstOrDefault(l => l.Position == 1) is { } leader)
        {
            if (pole == leader.DriverId)
            {
                items.Add(new Item(1, order++, RaceReportLine.Of(RaceReportKeys.StartLeaderPole, ("driver", new DriverRef(leader.DriverId)))));
            }
            else if (pole is not null)
            {
                var poleLap = firstLap.FirstOrDefault(l => l.DriverId == pole);
                items.Add(new Item(1, order++, poleLap is null
                    ? RaceReportLine.Of(RaceReportKeys.StartLeaderOtherOut, ("driver", new DriverRef(leader.DriverId)), ("pole", new DriverRef(pole)))
                    : RaceReportLine.Of(
                        RaceReportKeys.StartLeaderOther,
                        ("driver", new DriverRef(leader.DriverId)),
                        ("pole", new DriverRef(pole)),
                        ("position", poleLap.Position))));
            }
        }

        var moves = c.Input.Tape.Events.OfType<PositionChange>().Where(p => p.Lap == 1).ToList();
        var gains = moves.Where(p => p.FromPosition - p.ToPosition >= BigMoveMinPlaces)
            .OrderByDescending(p => p.FromPosition - p.ToPosition).ThenBy(p => p.Seq).Take(BigMovesShown).ToList();
        var losses = moves.Where(p => p.ToPosition - p.FromPosition >= BigMoveMinPlaces)
            .OrderByDescending(p => p.ToPosition - p.FromPosition).ThenBy(p => p.Seq).Take(BigMovesShown).ToList();
        foreach (var move in gains)
        {
            items.Add(new Item(1, order++, RaceReportLine.Plural(
                RaceReportKeys.StartGain,
                move.FromPosition - move.ToPosition,
                ("driver", new DriverRef(move.DriverId)),
                ("from", move.FromPosition),
                ("to", move.ToPosition))));
        }

        foreach (var move in losses)
        {
            items.Add(new Item(1, order++, RaceReportLine.Plural(
                RaceReportKeys.StartLoss,
                move.ToPosition - move.FromPosition,
                ("driver", new DriverRef(move.DriverId)),
                ("from", move.FromPosition),
                ("to", move.ToPosition))));
        }

        if (gains.Count == 0 && losses.Count == 0)
        {
            items.Add(new Item(1, order, RaceReportLine.Of(RaceReportKeys.StartQuiet, ("places", BigMoveMinPlaces))));
        }
    }

    // ---- Pit stops --------------------------------------------------------------------------------------------

    private static RaceReportSection? Pits(Context c)
    {
        var title = RaceReportLine.Of(RaceReportKeys.SectionPits);
        var stops = c.Input.PitStops;
        if (stops.IsDefaultOrEmpty)
        {
            return new RaceReportSection(title, [RaceReportLine.Of(RaceReportKeys.PitsNone)]);
        }

        var lines = ImmutableArray.CreateBuilder<RaceReportLine>();
        lines.Add(RaceReportLine.Plural(RaceReportKeys.PitsTotal, stops.Length));

        var noStops = c.Input.CarResults.Where(r => r.Stops == 0 && r.Status == FinishStatus.Classified).Select(r => r.DriverId).ToImmutableArray();
        if (noStops.Length > 0)
        {
            lines.Add(RaceReportLine.Of(RaceReportKeys.PitsNoStops, ("drivers", new DriverList(noStops))));
        }

        foreach (var group in c.Input.CarResults.Where(r => r.Stops > 0).GroupBy(r => r.Stops).OrderBy(g => g.Key))
        {
            lines.Add(RaceReportLine.Plural(
                RaceReportKeys.PitsByCount,
                group.Key,
                ("drivers", new DriverList([.. group.Select(r => r.DriverId)]))));
        }

        // Repairs and errors are named one by one, in the order they happened; slow stops (a few seconds lost) are one line.
        foreach (var stop in stops)
        {
            if (stop.IsRepair)
            {
                lines.Add(RaceReportLine.Of(
                    RaceReportKeys.PitsRepair,
                    ("lap", stop.Lap),
                    ("driver", c.DriverOfCar(stop.CarId)),
                    ("time", new Seconds(stop.TotalSeconds))));
            }
            else if (stop.Fault == PitStopFault.Error)
            {
                lines.Add(RaceReportLine.Of(
                    RaceReportKeys.PitsFaultError,
                    ("lap", stop.Lap),
                    ("driver", c.DriverOfCar(stop.CarId)),
                    ("time", new Seconds(stop.TotalSeconds))));
            }
        }

        var slow = stops.Where(s => !s.IsRepair && s.Fault == PitStopFault.SlowStop).ToList();
        if (slow.Count > 0)
        {
            var worst = slow.MaxBy(s => s.TotalSeconds)!;
            lines.Add(RaceReportLine.Plural(
                RaceReportKeys.PitsSlowStops,
                slow.Count,
                ("lap", worst.Lap),
                ("driver", c.DriverOfCar(worst.CarId)),
                ("time", new Seconds(worst.TotalSeconds))));
        }

        var clean = stops.Where(s => !s.IsRepair && s.Fault == PitStopFault.None).ToList();
        if (clean.Count >= CleanStopsForExtremes)
        {
            var fastest = clean.MinBy(s => s.TotalSeconds)!;
            var slowest = clean.MaxBy(s => s.TotalSeconds)!;
            lines.Add(RaceReportLine.Of(
                RaceReportKeys.PitsFastest,
                ("lap", fastest.Lap),
                ("driver", c.DriverOfCar(fastest.CarId)),
                ("time", new Seconds(fastest.TotalSeconds))));
            lines.Add(RaceReportLine.Of(
                RaceReportKeys.PitsSlowest,
                ("lap", slowest.Lap),
                ("driver", c.DriverOfCar(slowest.CarId)),
                ("time", new Seconds(slowest.TotalSeconds))));
        }

        return new RaceReportSection(title, lines.ToImmutable());
    }

    // ---- Shared drives -----------------------------------------------------------------------------------------

    private static RaceReportSection? Drives(Context c)
    {
        var lines = ImmutableArray.CreateBuilder<RaceReportLine>();
        foreach (var car in c.Input.CarResults.Where(r => r.DriversWhoDrove.Length > 1))
        {
            var swap = c.Input.PitStops.FirstOrDefault(s => s.CarId == car.CarId && s.DriverSwap);
            lines.Add(swap is null
                ? RaceReportLine.Of(
                    RaceReportKeys.DriveShared,
                    ("first", new DriverRef(car.DriversWhoDrove[0])),
                    ("second", new DriverRef(car.DriversWhoDrove[1])),
                    ("team", new TeamRef(car.ConstructorId)))
                : RaceReportLine.Of(
                    RaceReportKeys.DriveSwap,
                    ("lap", swap.Lap),
                    ("first", new DriverRef(car.DriversWhoDrove[0])),
                    ("second", new DriverRef(car.DriversWhoDrove[1])),
                    ("team", new TeamRef(car.ConstructorId))));
        }

        return lines.Count == 0 ? null : new RaceReportSection(RaceReportLine.Of(RaceReportKeys.SectionDrives), lines.ToImmutable());
    }

    // ---- Result ---------------------------------------------------------------------------------------------------

    private static RaceReportSection Result(Context c)
    {
        var lines = ImmutableArray.CreateBuilder<RaceReportLine>();
        var cars = c.Input.CarResults;
        var winner = cars.IsDefaultOrEmpty ? null : cars[0];
        if (winner is null || winner.Status != FinishStatus.Classified)
        {
            lines.Add(RaceReportLine.Of(RaceReportKeys.ResultNoWinner));
        }
        else
        {
            lines.Add(RaceReportLine.Of(
                RaceReportKeys.ResultWinner,
                ("driver", c.Crew(winner)),
                ("team", new TeamRef(winner.ConstructorId)),
                ("time", RaceClock.FromSeconds(winner.TotalSeconds))));
            var second = cars.Length > 1 && cars[1].Status == FinishStatus.Classified ? cars[1] : null;
            if (second is null)
            {
                lines.Add(RaceReportLine.Of(RaceReportKeys.ResultAlone));
            }
            else if (second.LapsCompleted == winner.LapsCompleted)
            {
                lines.Add(RaceReportLine.Of(
                    RaceReportKeys.ResultMarginTime,
                    ("second", c.Crew(second)),
                    ("gap", new Seconds(second.TotalSeconds - winner.TotalSeconds))));
            }
            else
            {
                lines.Add(RaceReportLine.Plural(
                    RaceReportKeys.ResultMarginLaps,
                    winner.LapsCompleted - second.LapsCompleted,
                    ("second", c.Crew(second))));
            }
        }

        if (c.Input.Tape.Events.OfType<FastestLap>().LastOrDefault() is { } fastest)
        {
            lines.Add(RaceReportLine.Of(
                RaceReportKeys.ResultFastest,
                ("driver", new DriverRef(fastest.DriverId)),
                ("team", c.TeamOf(fastest.DriverId)),
                ("time", new LapTime(fastest.LapTimeMs)),
                ("lap", fastest.Lap)));
        }

        if (c.Input.LapsRun < c.Input.ScheduledLaps)
        {
            lines.Add(RaceReportLine.Of(RaceReportKeys.ResultShortened, ("lap", c.Input.LapsRun), ("total", c.Input.ScheduledLaps)));
        }

        var running = cars.Where(r => r.Status == FinishStatus.Classified).ToList();
        lines.Add(RaceReportLine.Plural(RaceReportKeys.ResultFinishers, cars.Length, ("finished", running.Count)));
        var retired = cars.Where(r => r.Status != FinishStatus.Classified).ToList();
        lines.Add(retired.Count == 0
            ? RaceReportLine.Of(RaceReportKeys.ResultNoRetirements)
            : RaceReportLine.Of(
                RaceReportKeys.ResultAttrition,
                ("count", retired.Count),
                ("mechanical", retired.Count(r => r.Status == FinishStatus.Mechanical)),
                ("accident", retired.Count(r => r.Status == FinishStatus.Accident)),
                ("other", retired.Count(r => r.Status == FinishStatus.Other))));

        if (winner is not null && winner.Status == FinishStatus.Classified)
        {
            var onLead = running.Count(r => r.LapsCompleted == winner.LapsCompleted);
            lines.Add(RaceReportLine.Plural(RaceReportKeys.ResultLeadLap, onLead));
            foreach (var group in running.Where(r => r.LapsCompleted < winner.LapsCompleted)
                         .GroupBy(r => winner.LapsCompleted - r.LapsCompleted)
                         .OrderBy(g => g.Key))
            {
                lines.Add(RaceReportLine.Plural(
                    RaceReportKeys.ResultLapped,
                    group.Key,
                    ("drivers", new DriverList([.. group.Select(r => r.DriverId)]))));
            }
        }

        var led = new List<(string DriverId, int Laps)>();
        var current = default(string);
        var changes = 0;
        foreach (var lap in c.Input.Tape.Events.OfType<LapCompleted>().Where(l => l.Position == 1))
        {
            if (current is not null && current != lap.DriverId)
            {
                changes++;
            }

            current = lap.DriverId;
            var index = led.FindIndex(x => x.DriverId == lap.DriverId);
            if (index < 0)
            {
                led.Add((lap.DriverId, 1));
            }
            else
            {
                led[index] = (led[index].DriverId, led[index].Laps + 1);
            }
        }

        if (led.Count == 1)
        {
            lines.Add(RaceReportLine.Of(RaceReportKeys.ResultLedAll, ("driver", new DriverRef(led[0].DriverId))));
        }
        else if (led.Count > 1)
        {
            lines.Add(RaceReportLine.Plural(RaceReportKeys.ResultLeadChanges, changes));
            var most = led.MaxBy(x => x.Laps);
            lines.Add(RaceReportLine.Plural(
                RaceReportKeys.ResultMostLaps,
                most.Laps,
                ("driver", new DriverRef(most.DriverId)),
                ("total", Math.Max(1, c.Input.LapsRun))));
        }

        return new RaceReportSection(RaceReportLine.Of(RaceReportKeys.SectionResult), lines.ToImmutable());
    }

    // ---- Points -----------------------------------------------------------------------------------------------------

    private static RaceReportSection Points(Context c)
    {
        var lines = ImmutableArray.CreateBuilder<RaceReportLine>();
        foreach (var score in c.Input.Classification.DriverScores
                     .Where(s => s.Points > 0)
                     .OrderByDescending(s => s.Points)
                     .ThenBy(s => s.DriverId, StringComparer.Ordinal))
        {
            lines.Add(RaceReportLine.Plural(RaceReportKeys.PointsDriver, (double)score.Points, ("driver", new DriverRef(score.DriverId))));
        }

        foreach (var score in c.Input.Classification.ConstructorScores
                     .Where(s => s.Points > 0)
                     .OrderByDescending(s => s.Points)
                     .ThenBy(s => s.ConstructorId, StringComparer.Ordinal))
        {
            lines.Add(RaceReportLine.Plural(RaceReportKeys.PointsTeam, (double)score.Points, ("team", new TeamRef(score.ConstructorId))));
        }

        if (lines.Count == 0)
        {
            lines.Add(RaceReportLine.Of(RaceReportKeys.PointsNone));
        }

        return new RaceReportSection(RaceReportLine.Of(RaceReportKeys.SectionPoints), lines.ToImmutable());
    }

    // ---- Lap by lap (verbose) -------------------------------------------------------------------------------------

    private static RaceReportSection Log(Context c)
    {
        var lastFastest = c.Input.Tape.Events.OfType<FastestLap>().LastOrDefault();
        var lines = ImmutableArray.CreateBuilder<RaceReportLine>();
        foreach (var e in c.Input.Tape.Events)
        {
            // The lap chart is the leader's laps; the lap record improves nearly every lap, so only its last holder is shown.
            if (e is LapCompleted { Position: not 1 } || (e is FastestLap f && f != lastFastest))
            {
                continue;
            }

            lines.Add(c.LineFor(e));
        }

        return new RaceReportSection(RaceReportLine.Of(RaceReportKeys.SectionLog), lines.ToImmutable());
    }

    // ---- Lookups ----------------------------------------------------------------------------------------------------

    private sealed class Context
    {
        private readonly Dictionary<string, CarRaceResult> _byDriver;
        private readonly Dictionary<string, CarRaceResult> _byCar;

        public Context(RaceReportInput input)
        {
            Input = input;
            var cars = input.CarResults.IsDefault ? [] : input.CarResults;
            _byDriver = cars.ToDictionary(r => r.DriverId, StringComparer.Ordinal);
            _byCar = cars.ToDictionary(r => r.CarId, StringComparer.Ordinal);
        }

        public RaceReportInput Input { get; }

        public object DriverOfCar(string carId) =>
            new DriverRef(_byCar.TryGetValue(carId, out var car) ? car.DriverId : carId);

        public object TeamOf(string driverId) =>
            new TeamRef(_byDriver.TryGetValue(driverId, out var car) ? car.ConstructorId : string.Empty);

        /// <summary>The drivers of a car as the result names them: one, or both for a shared car that was swapped.</summary>
        public object Crew(CarRaceResult car) =>
            car.DriversWhoDrove.Length > 1 ? new DriverList(car.DriversWhoDrove, Crew: true) : new DriverRef(car.DriverId);

        /// <summary>The person whose race ended with this retirement: the driver at the wheel, which for a swapped shared car is not always the primary one.</summary>
        public PersonRaceOutcome? WhoRetired(Retirement retirement)
        {
            if (!_byDriver.TryGetValue(retirement.DriverId, out var car))
            {
                return null;
            }

            return Input.PersonOutcomes.IsDefault
                ? null
                : Input.PersonOutcomes.FirstOrDefault(p => p.CarId == car.CarId && p.Retired);
        }

        /// <summary>One line for one tape event; the key comes from <see cref="RaceReportKeys"/>, never from the event's type name.</summary>
        public RaceReportLine LineFor(RaceEvent e)
        {
            switch (e)
            {
                case RaceStarted x:
                    return RaceReportLine.Of(RaceReportKeys.For(e), ("cars", x.DriverIds.Length), ("laps", x.TotalLaps));
                case LapCompleted x:
                    return RaceReportLine.Of(
                        RaceReportKeys.For(e),
                        ("lap", x.Lap),
                        ("driver", new DriverRef(x.DriverId)),
                        ("position", x.Position),
                        ("time", new LapTime(x.LapTimeMs)));
                case PitStop x:
                    return RaceReportLine.Of(
                        RaceReportKeys.For(e),
                        ("lap", x.Lap),
                        ("driver", new DriverRef(x.DriverId)),
                        ("time", new Seconds(x.DurationMs / 1000d)));
                case PositionChange x:
                    return RaceReportLine.Of(
                        RaceReportKeys.For(e),
                        ("lap", x.Lap),
                        ("driver", new DriverRef(x.DriverId)),
                        ("from", x.FromPosition),
                        ("to", x.ToPosition));
                case Incident x:
                    return RaceReportLine.Of(
                        RaceReportKeys.For(e),
                        ("lap", x.Lap),
                        ("drivers", new DriverList(x.InvolvedIds.IsDefault ? [] : x.InvolvedIds)));
                case Retirement x:
                {
                    var car = _byDriver.GetValueOrDefault(x.DriverId);
                    var person = WhoRetired(x);
                    var key = RaceReportKeys.RetirementKey(x.Reason, x.Reason == RetirementReason.Mechanical ? car?.FailedComponent : null);
                    return RaceReportLine.Of(
                        key,
                        ("lap", x.Lap),
                        ("driver", new DriverRef(person?.DriverId ?? x.DriverId)),
                        ("team", new TeamRef(car?.ConstructorId ?? string.Empty)));
                }

                case FastestLap x:
                    return RaceReportLine.Of(
                        RaceReportKeys.For(e),
                        ("lap", x.Lap),
                        ("driver", new DriverRef(x.DriverId)),
                        ("time", new LapTime(x.LapTimeMs)));
                case Finished x:
                    return RaceReportLine.Of(
                        RaceReportKeys.For(e),
                        ("driver", new DriverRef(x.DriverId)),
                        ("position", x.Position),
                        ("laps", x.LapsCompleted),
                        ("time", new RaceClock(x.TotalTimeMs)));
                default:
                    // WeatherChange, SafetyCar, RedFlag, RaceEnded: the lap is all they say.
                    return RaceReportLine.Of(RaceReportKeys.For(e), ("lap", e.Lap));
            }
        }
    }
}
