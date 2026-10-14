using System.Globalization;
using Paddock.Application.Career;
using Paddock.Simulation.Career;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Tyres;

namespace Paddock.Application.Racing;

/// <summary>
/// One car of a watched race: who drives it, for whom, from where on the grid. <see cref="CarId"/> is the id the tape and the frames use.
/// <see cref="SeatOrder"/> is the place of the car among its team's cars, counted from 0: the first driver is 0, the second 1 (#325).
/// A screen that lists a team's cars side by side or one under the other keeps this order.
/// </summary>
public sealed record LiveCarView(
    string CarId,
    string DriverName,
    string ShortName,
    string Nationality,
    string TeamId,
    string TeamName,
    int? Grid,
    bool Own,
    int SeatOrder = 0);

/// <summary>
/// One fact of the race tape as the screen gets it. The structured fields feed the timing tower and the map; <see cref="Key"/> and
/// <see cref="Args"/> are the transcript line (null when the fact is not narrated, like most lap crossings). Kinds: <c>start</c>,
/// <c>lap</c>, <c>pitIn</c>, <c>pitOut</c>, <c>gain</c>, <c>loss</c>, <c>incident</c>, <c>retire</c>, <c>weather</c>, <c>sc</c>,
/// <c>scEnd</c>, <c>red</c>, <c>fastest</c>, <c>finish</c>, <c>end</c>, <c>call</c> (own strategist on the radio), and, for own
/// cars only (#286), <c>driver</c> (the driver on the radio about tyres and fuel) and <c>order</c> (the driver answering an order).
/// </summary>
public sealed record LiveEventView(
    int Seq,
    int Lap,
    long TimeMs,
    string Kind,
    string? CarId,
    int? Position,
    long? LapTimeMs,
    long? GapMs,
    long? DurationMs,
    string? Tyres,
    IReadOnlyList<string> Others,
    bool Own,
    string? Key,
    IReadOnlyList<ReportArgView> Args);

/// <summary>The player's pit wall: who calls the strategy. Own staff only (INV-003).</summary>
public sealed record LiveStrategyView(string? StrategistId, string? StrategistName);

/// <summary>
/// What the pit wall knows about one of its own cars at the start of a lap (#286): tyres and their age, fuel and how many laps
/// it lasts at the pace the car runs (<see cref="FuelLaps"/>), laps still to run counting this one, the pace and whether it is the
/// pit wall's order (<see cref="Manual"/>), how the driver says the tyres feel (<c>good</c>, <c>worn</c>, <c>gone</c>), the engine mode
/// (<c>lean</c>, <c>standard</c>, <c>full</c>) and whether the team order to let the team-mate by is on.
/// </summary>
public sealed record LivePitWallLapView(
    string CarId,
    int Lap,
    long StartMs,
    string Tyres,
    int TyreLaps,
    double FuelKg,
    double FuelLaps,
    int LapsLeft,
    string Pace,
    bool Manual,
    string Feel,
    string Engine,
    bool LetBy);

/// <summary>One pit wall order the host took (#286): the car, the lap it acts on, when it was given, and what it asks.</summary>
public sealed record LiveOrderView(string CarId, int Lap, long AtMs, string Kind, string? Pace, string? Tyres, string? Engine, bool On);

/// <summary>
/// The player's side of the pit wall (#286). <see cref="CanOrder"/> says whether orders are taken now; when not,
/// <see cref="Locked"/> is the reason key. <see cref="Compounds"/> are the tyres a stop may fit (softest first, then the wet
/// tyre), <see cref="StartTyres"/> the set every car starts on. <see cref="Laps"/> and <see cref="Orders"/> hold own cars only (INV-003).
/// </summary>
public sealed record LivePitWallView(
    bool CanOrder,
    string? Locked,
    int Revision,
    IReadOnlyList<string> Compounds,
    string? StartTyres,
    bool TyreChange,
    bool Refuelling,
    IReadOnlyList<LivePitWallLapView> Laps,
    IReadOnlyList<LiveOrderView> Orders);

/// <summary>
/// A watched race (PP-052): everything the race mode draws except the frames, which come in windows
/// (<see cref="LiveFramesView"/>). Built from the tape the career just ran and the round's archive; it reads no truth the
/// player may not see (no lap noise, no true weather, rival strategists stay silent) and draws no random numbers (INV-005).
/// </summary>
public sealed record LiveRaceView(
    bool Found,
    int Season,
    int Round,
    string? LayoutId,
    string? CircuitName,
    string? Country,
    int TotalLaps,
    double LapLengthM,
    long DurationMs,
    string? StartCondition,
    string? StartAirC,
    bool FramesApproximate,
    IReadOnlyList<LiveCarView> Cars,
    IReadOnlyList<LiveEventView> Events,
    LiveStrategyView Strategy,
    LivePitWallView PitWall);

/// <summary>Display samples of one car in a window, as parallel arrays (time, racing-line metres, speed, pit lane, pit-lane metres).</summary>
public sealed record LiveCarFramesView(
    string CarId,
    IReadOnlyList<long> TimeMs,
    IReadOnlyList<double> DistanceM,
    IReadOnlyList<double> SpeedMps,
    IReadOnlyList<bool> InPit,
    IReadOnlyList<double> PitM);

/// <summary>The frames of a watched race between two race times (inclusive). Frames are display only, never results.</summary>
public sealed record LiveFramesView(bool Found, long FromMs, long ToMs, IReadOnlyList<LiveCarFramesView> Cars);

/// <summary>Reads the race the career ran last for one team's manager. No state change, no RNG (INV-005).</summary>
public static class LiveRaceRead
{
    /// <summary>The longest window one read returns, so a guest on a slow link never asks for a whole race at once.</summary>
    public const long MaxWindowMs = 5 * 60 * 1000;

    public static LiveRaceView Read(
        CareerSession session,
        RaceWatch watch,
        OrganizationId? observer,
        IReadOnlyDictionary<string, CircuitLabel> circuits,
        IReadOnlyDictionary<string, TrackFacts> tracks)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(watch);
        ArgumentNullException.ThrowIfNull(circuits);
        ArgumentNullException.ThrowIfNull(tracks);
        if (watch.Tape is not { } tape || tape.Events.IsDefaultOrEmpty || tape.Events[0] is not RaceStarted started)
        {
            return Empty(session.Date.Year);
        }

        var world = session.World;
        var stored = world.Section<RaceResultsSection>(RaceResultsSection.SectionName)?.Find(watch.Season, watch.Round);
        var rows = new Dictionary<string, RaceResultRow>(StringComparer.Ordinal);
        foreach (var row in stored?.Rows ?? [])
        {
            rows[row.DriverId] = row;
        }

        var people = new Dictionary<string, Person>(StringComparer.Ordinal);
        foreach (var person in world.Persons)
        {
            people[person.Id.Value] = person;
        }

        var own = observer is { IsAssigned: true } team ? team.Value : null;
        var cars = new List<LiveCarView>(started.DriverIds.Length);
        var teamOf = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in started.DriverIds)
        {
            rows.TryGetValue(id, out var row);
            var teamId = row?.TeamId ?? "";
            teamOf[id] = teamId;
            people.TryGetValue(id, out var person);
            cars.Add(new LiveCarView(
                id,
                person?.Name ?? id,
                person is null ? id : Short(person),
                person?.Nationality ?? "",
                teamId,
                TeamName(world, teamId),
                row?.Detail?.GridPosition,
                own is not null && teamId == own));
        }

        cars = SeatOrdered(cars, world, session.Date);

        string Name(string id) => people.TryGetValue(id, out var person) ? person.Name : id;
        string Team(string id) => teamOf.TryGetValue(id, out var teamId) ? TeamName(world, teamId) : "";
        bool Mine(string id) => own is not null && teamOf.TryGetValue(id, out var teamId) && teamId == own;

        var events = new List<LiveEventView>(tape.Count);
        foreach (var raceEvent in tape.Events)
        {
            if (Map(raceEvent, started, rows, Name, Team, Mine) is { } view)
            {
                events.Add(view);
            }
        }

        var pitWall = OwnPitWall(watch, started.TotalLaps, Mine);
        var manual = pitWall.Where(l => l.Manual).Select(l => (l.CarId, l.Lap)).ToHashSet();
        events.AddRange(Calls(watch.Calls, tape, Mine, events.Count, manual));
        events.AddRange(Radio(pitWall, watch.Tyres, Name, events.Count + tape.Count));
        events.AddRange(Acks(watch.Steering, Mine, Name, events.Count + (2 * tape.Count)));
        events.Sort((a, b) => a.TimeMs != b.TimeMs ? a.TimeMs.CompareTo(b.TimeMs) : a.Seq.CompareTo(b.Seq));

        tracks.TryGetValue(watch.LayoutId, out var track);
        circuits.TryGetValue(watch.LayoutId, out var circuit);
        var (condition, air) = StartConditions(stored);
        return new LiveRaceView(
            true,
            watch.Season,
            watch.Round,
            watch.LayoutId,
            circuit?.Name,
            circuit?.Country,
            started.TotalLaps,
            track is null ? (stored?.Facts?.LapLengthMeters ?? 0) : track.LengthKm * 1000d,
            tape.Events[^1].RaceTime,
            condition,
            air,
            tape.FrameAccuracy is not FrameAccuracy.Exact,
            cars,
            events,
            Strategy(world, observer, session),
            PitWallOf(watch, pitWall, Mine));
    }

    /// <summary>The frames between <paramref name="fromMs"/> and <paramref name="toMs"/>, cut to <see cref="MaxWindowMs"/>.</summary>
    public static LiveFramesView Frames(RaceWatch watch, long fromMs, long toMs)
    {
        ArgumentNullException.ThrowIfNull(watch);
        if (watch.Tape is not { } tape || tape.Frames.IsDefaultOrEmpty)
        {
            return new LiveFramesView(false, fromMs, toMs, []);
        }

        var from = Math.Max(0, fromMs);
        var to = Math.Min(Math.Max(from, toMs), from + MaxWindowMs);
        var frames = tape.Frames;
        var start = First(frames, from);
        var order = new List<string>();
        var byCar = new Dictionary<string, (List<long> T, List<double> D, List<double> S, List<bool> P, List<double> M)>(StringComparer.Ordinal);
        for (var i = start; i < frames.Length && frames[i].RaceTimeMs <= to; i++)
        {
            var frame = frames[i];
            if (!byCar.TryGetValue(frame.CarId, out var lists))
            {
                lists = ([], [], [], [], []);
                byCar[frame.CarId] = lists;
                order.Add(frame.CarId);
            }

            lists.T.Add(frame.RaceTimeMs);
            lists.D.Add(Math.Round(frame.DistanceM, 1));
            lists.S.Add(Math.Round(frame.SpeedMps, 1));
            lists.P.Add(frame.InPitLane);
            lists.M.Add(Math.Round(frame.PitDistanceM, 1));
        }

        var cars = order
            .Select(id => byCar[id])
            .Zip(order, (lists, id) => new LiveCarFramesView(id, lists.T, lists.D, lists.S, lists.P, lists.M))
            .ToArray();
        return new LiveFramesView(true, from, to, cars);
    }

    /// <summary>
    /// The cars with their <see cref="LiveCarView.SeatOrder"/> set: within a team the first driver comes first, then the second, then the
    /// rest, by the seat on their contract on the day of the read. Equal seats keep the order of the grid, then of the id, so a team
    /// whose drivers have no hierarchy is listed as it always was (#325).
    /// </summary>
    private static List<LiveCarView> SeatOrdered(List<LiveCarView> cars, WorldState world, GameDate today)
    {
        var seats = new Dictionary<(string Team, string Driver), int>();
        foreach (var contract in world.Contracts)
        {
            if (contract.Role.IsDriver && contract.IsActiveOn(today))
            {
                seats[(contract.OrganizationId.Value, contract.PersonId.Value)] = (int)contract.Role.Seat;
            }
        }

        int Rank(LiveCarView car) => seats.TryGetValue((car.TeamId, car.CarId), out var seat) ? seat : int.MaxValue;
        var ordered = new List<LiveCarView>(cars.Count);
        foreach (var team in cars.GroupBy(car => car.TeamId, StringComparer.Ordinal))
        {
            var order = 0;
            foreach (var car in team.OrderBy(Rank).ThenBy(car => car.Grid ?? int.MaxValue).ThenBy(car => car.CarId, StringComparer.Ordinal))
            {
                ordered.Add(car with { SeatOrder = order++ });
            }
        }

        var byId = ordered.ToDictionary(car => car.CarId, StringComparer.Ordinal);
        return cars.Select(car => byId[car.CarId]).ToList();
    }

    private static LiveRaceView Empty(int season) =>
        new(false, season, 0, null, null, null, 0, 0, 0, null, null, true, [], [], new LiveStrategyView(null, null), NoPitWall);

    private static LivePitWallView NoPitWall { get; } = new(false, LiveOrderKeys.Locked, 0, [], null, false, false, [], []);

    private static LivePitWallView PitWallOf(RaceWatch watch, IReadOnlyList<LivePitWallLapView> laps, Func<string, bool> mine)
    {
        var steering = watch.Steering;
        var orders = steering is null
            ? []
            : steering.Issued
                .Where(o => mine(o.Order.CarId))
                .Select(o => new LiveOrderView(
                    o.Order.CarId,
                    o.Order.Lap,
                    o.AtMs,
                    Camel(o.Order.Kind.ToString()),
                    o.Order.Kind == PitWallOrderKind.Pace ? PaceText(o.Order.Pace) : null,
                    o.Order.CompoundId,
                    o.Order.Kind == PitWallOrderKind.Engine ? EngineText(o.Order.Engine) : null,
                    o.Order.On))
                .ToArray();
        var tyres = watch.Tyres;
        return new LivePitWallView(
            steering is not null,
            steering is null ? LiveOrderKeys.Locked : null,
            steering?.Revision ?? 0,
            tyres?.Compounds ?? [],
            tyres?.StartCompound,
            tyres?.TyreChange ?? false,
            tyres?.Refuelling ?? false,
            laps,
            orders);
    }

    /// <summary>Own cars' pit wall laps, with fuel turned into laps at the pace run (the same burn the lap engine uses).</summary>
    private static List<LivePitWallLapView> OwnPitWall(RaceWatch watch, int totalLaps, Func<string, bool> mine)
    {
        var laps = new List<LivePitWallLapView>();
        foreach (var lap in watch.PitWall)
        {
            if (!mine(lap.CarId))
            {
                continue;
            }

            var burn = lap.BurnKg * PaceEffects.Of(lap.Pace).BurnFactor * PaceEffects.Burn(lap.Engine);
            laps.Add(new LivePitWallLapView(
                lap.CarId,
                lap.Lap,
                lap.StartMs,
                lap.CompoundId,
                lap.TyreAgeLaps,
                Math.Round(lap.FuelKg, 1),
                burn > 0 ? Math.Round(lap.FuelKg / burn, 1) : 0,
                Math.Max(0, totalLaps - lap.Lap + 1),
                PaceText(lap.Pace),
                lap.Manual,
                Camel(lap.Feel.ToString()),
                EngineText(lap.Engine),
                lap.LetBy));
        }

        return laps;
    }

    /// <summary>
    /// The driver on the radio (#286): the tyres going off or past their cliff, and, where nobody may refuel, fuel that will not
    /// last at this pace. Heard at the start of the lap the driver feels it; own cars only (INV-003).
    /// </summary>
    private static IEnumerable<LiveEventView> Radio(IReadOnlyList<LivePitWallLapView> laps, TyreOffer? tyres, Func<string, string> name, int firstSeq)
    {
        var seq = firstSeq;
        var last = new Dictionary<string, LivePitWallLapView>(StringComparer.Ordinal);
        var shortOfFuel = new HashSet<string>(StringComparer.Ordinal);
        foreach (var lap in laps)
        {
            last.TryGetValue(lap.CarId, out var before);
            last[lap.CarId] = lap;
            if (before is not null && before.Feel != lap.Feel && lap.Feel != "good")
            {
                yield return Said(lap, lap.Feel == "gone" ? LiveRaceKeys.TyresGone : LiveRaceKeys.TyresWorn, [("driver", name(lap.CarId))]);
            }

            var willRunOut = tyres is { Refuelling: false } && lap.FuelLaps < lap.LapsLeft;
            if (willRunOut && shortOfFuel.Add(lap.CarId))
            {
                yield return Said(lap, LiveRaceKeys.FuelShort, [("driver", name(lap.CarId)), ("laps", Math.Floor(lap.FuelLaps).ToString("0", CultureInfo.InvariantCulture))]);
            }
            else if (!willRunOut)
            {
                shortOfFuel.Remove(lap.CarId);
            }
        }

        LiveEventView Said(LivePitWallLapView lap, string key, (string Name, string Value)[] args) =>
            new(seq++, lap.Lap, lap.StartMs, "driver", lap.CarId, null, null, null, null, lap.Tyres, [], true, key, args.Select(a => new ReportArgView(a.Name, a.Value)).ToArray());
    }

    /// <summary>The driver answering an order of our pit wall, when it was given (#286). Rivals' orders are never sent (INV-003).</summary>
    private static IEnumerable<LiveEventView> Acks(RaceSteering? steering, Func<string, bool> mine, Func<string, string> name, int firstSeq)
    {
        if (steering is null)
        {
            yield break;
        }

        var seq = firstSeq;
        foreach (var issued in steering.Issued)
        {
            var order = issued.Order;
            if (!mine(order.CarId))
            {
                continue;
            }

            var key = LiveRaceKeys.Ack(order);
            IReadOnlyList<ReportArgView> args =
            [
                new("driver", name(order.CarId)),
                new("lap", Number(order.Lap)),
                .. order.CompoundId is { } tyre ? [new ReportArgView("tyres", tyre)] : Array.Empty<ReportArgView>(),
            ];
            yield return new LiveEventView(seq++, order.Lap, issued.AtMs, "order", order.CarId, null, null, null, null, order.CompoundId, [], true, key, args);
        }
    }

    internal static string PaceText(PaceMode pace) => pace switch
    {
        PaceMode.Push => "push",
        PaceMode.Save => "save",
        PaceMode.Conserve => "conserve",
        PaceMode.Qualifying => "qualifying",
        _ => "standard",
    };

    internal static string EngineText(EngineMode engine) => engine switch
    {
        EngineMode.Lean => "lean",
        EngineMode.Full => "full",
        _ => "standard",
    };

    private static string Camel(string text) => text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text[1..];

    /// <summary>Index of the first frame at or after <paramref name="time"/> (frames are in time order).</summary>
    private static int First(System.Collections.Immutable.ImmutableArray<CarFrame> frames, long time)
    {
        var lo = 0;
        var hi = frames.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (frames[mid].RaceTimeMs < time)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    private static LiveEventView? Map(
        RaceEvent raceEvent,
        RaceStarted started,
        IReadOnlyDictionary<string, RaceResultRow> rows,
        Func<string, string> name,
        Func<string, string> team,
        Func<string, bool> mine)
    {
        LiveEventView Make(
            string kind,
            string? car = null,
            int? position = null,
            long? lapTime = null,
            long? gap = null,
            long? duration = null,
            string? tyres = null,
            IReadOnlyList<string>? others = null,
            string? key = null,
            params (string Name, string Value)[] args) =>
            new(
                raceEvent.Seq,
                raceEvent.Lap,
                raceEvent.RaceTime,
                kind,
                car,
                position,
                lapTime,
                gap,
                duration,
                tyres,
                others ?? [],
                (car is not null && mine(car)) || (others?.Any(mine) ?? false),
                key,
                args.Select(arg => new ReportArgView(arg.Name, arg.Value)).ToArray());

        switch (raceEvent)
        {
            case RaceStarted:
                return Make("start", key: LiveRaceKeys.Start, args: [("cars", Number(started.DriverIds.Length)), ("laps", Number(started.TotalLaps))]);
            case LapCompleted lap:
                return Make("lap", lap.DriverId, lap.Position, lap.LapTimeMs, lap.GapToLeaderMs);
            case PitStop { Phase: PitLanePhase.In } pit:
                return Make("pitIn", pit.DriverId, key: LiveRaceKeys.PitIn, args: [("driver", name(pit.DriverId))]);
            case PitStop pit:
                return Make(
                    "pitOut",
                    pit.DriverId,
                    duration: pit.DurationMs,
                    tyres: pit.Tyres,
                    key: LiveRaceKeys.PitOut,
                    args: [("driver", name(pit.DriverId)), ("time", Seconds(pit.DurationMs)), ("tyres", pit.Tyres)]);
            case PositionChange change when change.ToPosition < change.FromPosition:
                return Make("gain", change.DriverId, change.ToPosition, key: LiveRaceKeys.Gain, args: [("driver", name(change.DriverId)), ("position", Number(change.ToPosition))]);
            case PositionChange change:
                return Make(
                    "loss",
                    change.DriverId,
                    change.ToPosition,
                    key: mine(change.DriverId) ? LiveRaceKeys.Loss : null,
                    args: [("driver", name(change.DriverId)), ("position", Number(change.ToPosition))]);
            case Incident incident:
                return Make(
                    "incident",
                    incident.InvolvedIds.IsDefaultOrEmpty ? null : incident.InvolvedIds[0],
                    others: incident.InvolvedIds.IsDefaultOrEmpty ? [] : incident.InvolvedIds.Skip(1).ToArray(),
                    key: LiveRaceKeys.Incident(incident.Severity),
                    args: [("drivers", string.Join(", ", incident.InvolvedIds.Select(name)))]);
            case Retirement retirement:
                return Make(
                    "retire",
                    retirement.DriverId,
                    key: LiveRaceKeys.Retire(rows.TryGetValue(retirement.DriverId, out var row) ? row.RetirementKey : "", retirement.Reason),
                    args: [("driver", name(retirement.DriverId)), ("team", team(retirement.DriverId))]);
            case WeatherChange weather:
                return Make("weather", key: LiveRaceKeys.Weather(weather.Condition));
            case SafetyCar { Phase: SafetyCarPhase.Deployed } safety:
                return Make("sc", key: safety.IsVirtual ? LiveRaceKeys.VirtualSafetyCar : LiveRaceKeys.SafetyCar);
            case SafetyCar safety:
                return Make("scEnd", key: safety.IsVirtual ? LiveRaceKeys.VirtualSafetyCarEnds : LiveRaceKeys.SafetyCarEnds);
            case RedFlag:
                return Make("red", key: LiveRaceKeys.RedFlag);
            case FastestLap fastest:
                return Make(
                    "fastest",
                    fastest.DriverId,
                    lapTime: fastest.LapTimeMs,
                    key: LiveRaceKeys.Fastest,
                    args: [("driver", name(fastest.DriverId)), ("time", LapTime(fastest.LapTimeMs))]);
            case Finished finished:
                return Make(
                    "finish",
                    finished.DriverId,
                    finished.Position,
                    gap: finished.TotalTimeMs,
                    key: finished.Position == 1 ? LiveRaceKeys.Winner : mine(finished.DriverId) ? LiveRaceKeys.OwnFinish : null,
                    args: [("driver", name(finished.DriverId)), ("team", team(finished.DriverId)), ("position", Number(finished.Position))]);
            case RaceEnded:
                return Make("end");
            default:
                return null;
        }
    }

    /// <summary>
    /// The own strategist's calls as radio lines. A call is made before the lap it names, so it is heard when the car
    /// crosses the line before that lap (the start for lap 1). Rivals' calls are never sent (INV-003).
    /// </summary>
    private static IEnumerable<LiveEventView> Calls(
        IReadOnlyList<StrategyCall> calls,
        RaceTape tape,
        Func<string, bool> mine,
        int firstSeq,
        IReadOnlySet<(string Car, int Lap)> manual)
    {
        var crossings = new Dictionary<(string Car, int Lap), (long Time, int Seq)>();
        foreach (var raceEvent in tape.Events)
        {
            if (raceEvent is LapCompleted lap)
            {
                crossings[(lap.DriverId, lap.Lap)] = (lap.RaceTime, lap.Seq);
            }
        }

        var seq = Math.Max(firstSeq, tape.Count);
        foreach (var call in calls)
        {
            // A pace call the pit wall overruled is not heard: the car runs the ordered pace (#286).
            if (!mine(call.CarId) || (call.Call != StrategyCalls.Pit && manual.Contains((call.CarId, call.Lap))))
            {
                continue;
            }

            var heard = call.Lap <= 1 ? (0L, 0) : crossings.TryGetValue((call.CarId, call.Lap - 1), out var at) ? at : (-1L, 0);
            if (heard.Item1 < 0)
            {
                continue;
            }

            var key = LiveRaceKeys.Call(call.Call, call.Compound is not null);
            IReadOnlyList<ReportArgView> args = call.Compound is null ? [] : [new ReportArgView("tyres", call.Compound)];
            yield return new LiveEventView(seq++, Math.Max(0, call.Lap - 1), heard.Item1, "call", call.CarId, null, null, null, null, call.Compound, [], true, key, args);
        }
    }

    private static LiveStrategyView Strategy(WorldState world, OrganizationId? observer, CareerSession session)
    {
        if (observer is not { IsAssigned: true } team)
        {
            return new LiveStrategyView(null, null);
        }

        var staff = RaceStaff.Of(world, team, session.Date);
        if (staff.StrategistId is null)
        {
            return new LiveStrategyView(null, null);
        }

        var person = world.Persons.FirstOrDefault(candidate => candidate.Id.Value == staff.StrategistId);
        return new LiveStrategyView(staff.StrategistId, person?.Name);
    }

    /// <summary>The start of the race as the report told it (track state and air temperature), never the true weather (INV-003).</summary>
    private static (string? Condition, string? Air) StartConditions(StoredRace? stored)
    {
        foreach (var section in stored?.Sections ?? [])
        {
            foreach (var line in section.Lines)
            {
                if (line.Key.StartsWith(LiveRaceKeys.StartConditionPrefix, StringComparison.Ordinal))
                {
                    var temp = line.Args.FirstOrDefault(arg => arg.Name == "temp")?.Value;
                    return (line.Key[LiveRaceKeys.StartConditionPrefix.Length..], temp);
                }
            }
        }

        return (null, null);
    }

    private static string Short(Person person) =>
        string.IsNullOrWhiteSpace(person.FamilyName) ? person.Name : person.FamilyName;

    private static string TeamName(WorldState world, string id)
    {
        foreach (var organization in world.Organizations)
        {
            if (organization.Id.Value == id)
            {
                return organization.NameOn(world.CurrentDate);
            }
        }

        return id;
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Seconds(long ms) => (ms / 1000d).ToString("0.0", CultureInfo.InvariantCulture);

    /// <summary>A lap time as a timing board shows it ("1:23.456"); the same fixed format as the race page.</summary>
    private static string LapTime(long ms) =>
        string.Create(CultureInfo.InvariantCulture, $"{ms / 60000}:{ms % 60000 / 1000:00}.{ms % 1000:000}");
}

/// <summary>Translation keys of the race transcript and the pit-wall radio. Every key is in <c>strings/pl.json</c> and <c>strings/en.json</c>.</summary>
public static class LiveRaceKeys
{
    public const string Start = "live.event.start";
    public const string PitIn = "live.event.pitIn";
    public const string PitOut = "live.event.pitOut";
    public const string Gain = "live.event.gain";
    public const string Loss = "live.event.loss";
    public const string SafetyCar = "live.event.sc";
    public const string SafetyCarEnds = "live.event.scEnd";
    public const string VirtualSafetyCar = "live.event.vsc";
    public const string VirtualSafetyCarEnds = "live.event.vscEnd";
    public const string RedFlag = "live.event.red";
    public const string Fastest = "live.event.fastest";
    public const string Winner = "live.event.winner";
    public const string OwnFinish = "live.event.ownFinish";

    /// <summary>The report's start-conditions line, whose suffix is the track state (dry, damp, wet, extreme).</summary>
    public const string StartConditionPrefix = "report.conditions.start.";

    /// <summary>Retirement causes the report names (<c>report.retire.*</c>); anything else falls back to the tape's bucket.</summary>
    public static IReadOnlyList<string> RetireCauses { get; } =
        ["engine", "gearbox", "suspension", "brakes", "electrics", "cooling", "mechanical", "sudden", "accident", "other"];

    public static IReadOnlyList<string> Conditions { get; } = ["dry", "damp", "wet", "extreme"];

    public static IReadOnlyList<string> CallKinds { get; } =
        [StrategyCalls.Pit, StrategyCalls.Push, StrategyCalls.Save, StrategyCalls.Standard];

    public static string Incident(IncidentSeverity severity) => "live.event.incident." + severity.ToString().ToLowerInvariant();

    public static string Weather(string condition) =>
        "live.event.weather." + (Conditions.Contains(condition) ? condition : "dry");

    public static string Retire(string reportKey, RetirementReason reason)
    {
        const string prefix = "report.retire.";
        var cause = reportKey.StartsWith(prefix, StringComparison.Ordinal) ? reportKey[prefix.Length..] : "";
        if (!RetireCauses.Contains(cause))
        {
            cause = reason switch
            {
                RetirementReason.Mechanical => "mechanical",
                RetirementReason.Accident => "accident",
                _ => "other",
            };
        }

        return "live.event.retire." + cause;
    }

    public static string Call(string call, bool withTyres) =>
        "live.call." + call + (call == StrategyCalls.Pit && withTyres ? "Tyres" : "");

    public const string TyresWorn = "live.driver.tyresWorn";
    public const string TyresGone = "live.driver.tyresGone";
    public const string FuelShort = "live.driver.fuelShort";

    /// <summary>Every answer a driver gives to an order (#286).</summary>
    public static IReadOnlyList<string> Acks { get; } =
        [
            "live.ack.push", "live.ack.save", "live.ack.standard", "live.ack.conserve", "live.ack.qualifying", "live.ack.auto",
            "live.ack.pit", "live.ack.pitTyres", "live.ack.engine.lean", "live.ack.engine.standard", "live.ack.engine.full",
            "live.ack.letByOn", "live.ack.letByOff",
        ];

    /// <summary>The driver's answer to <paramref name="order"/>.</summary>
    public static string Ack(PitWallOrder order) => order.Kind switch
    {
        PitWallOrderKind.Pace => "live.ack." + LiveRaceRead.PaceText(order.Pace),
        PitWallOrderKind.Auto => "live.ack.auto",
        PitWallOrderKind.Engine => "live.ack.engine." + LiveRaceRead.EngineText(order.Engine),
        PitWallOrderKind.LetBy => order.On ? "live.ack.letByOn" : "live.ack.letByOff",
        _ => order.CompoundId is null ? "live.ack.pit" : "live.ack.pitTyres",
    };
}
