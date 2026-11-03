using Paddock.Application.Career;
using Paddock.Domain.Racing;
using Paddock.Domain.World;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Qualifying;

namespace Paddock.Application.Racing;

/// <summary>What the team learned about one dry compound in practice: the laps a set lasts, as a range (own cars only, INV-003).</summary>
public sealed record PracticeTyreView(string Tyres, int LowLaps, int HighLaps);

/// <summary>One car in the practice report: public times for every car, tyre reads for own cars only.</summary>
public sealed record PracticeLineView(
    int Position,
    string CarId,
    string DriverName,
    string TeamId,
    string TeamName,
    bool Own,
    long BestLapMs,
    long GapMs,
    long LongRunMs,
    IReadOnlyList<PracticeTyreView> Tyres);

/// <summary>One car on the grid after qualifying (public timing). <see cref="Position"/> 0 starts from the pit lane.</summary>
public sealed record QualifyingLineView(
    int Position,
    string CarId,
    string DriverName,
    string TeamId,
    string TeamName,
    bool Own,
    long? BestLapMs,
    long? GapMs);

/// <summary>A car that did not make the grid, with the reason (<c>carNotRunning</c>, <c>failedPreQualifying</c>, <c>outsideCutoff</c>, <c>gridLimit</c>).</summary>
public sealed record QualifyingOutView(string CarId, string DriverName, string TeamName, bool Own, string Reason, long? BestLapMs);

/// <summary>A planned stop: in at the end of <see cref="Lap"/> for <see cref="Tyres"/>.</summary>
public sealed record PlannedStopView(int Lap, string Tyres);

/// <summary>
/// The plan of one own car (#316). Null <see cref="Pace"/> leaves the pace to the strategist; null <see cref="Stops"/> leaves the
/// stops to it. <see cref="Set"/> is false for a car with no plan yet (the defaults are shown).
/// </summary>
public sealed record RacePlanView(
    string CarId,
    string DriverName,
    int Grid,
    bool Set,
    string StartTyres,
    string? Pace,
    string Engine,
    IReadOnlyList<PlannedStopView>? Stops);

/// <summary>
/// Today's race weekend for one manager (#316): the practice report, the qualifying result, the tyres and rules a plan works with,
/// and the plans of the manager's cars. <see cref="CanPlan"/> is false once the race is being watched (<see cref="PlanLocked"/>
/// then names why); a plan acts from lap 1. Everything here is derived from the world and changes nothing (INV-005).
/// </summary>
public sealed record RaceWeekendView(
    bool Found,
    int Season,
    int Round,
    string LayoutId,
    string? CircuitName,
    int TotalLaps,
    bool Watched,
    bool CanPlan,
    string? PlanLocked,
    IReadOnlyList<string> DryTyres,
    string WetTyres,
    string DefaultStartTyres,
    bool TyreChange,
    bool Refuelling,
    int RequiredDryCompounds,
    int MaxStops,
    IReadOnlyList<PracticeLineView> Practice,
    IReadOnlyList<QualifyingLineView> Grid,
    IReadOnlyList<QualifyingOutView> NotQualified,
    IReadOnlyList<RacePlanView> Plans)
{
    public static RaceWeekendView None { get; } = new(false, 0, 0, "", null, 0, false, false, null, [], "", "", false, false, 1, RacePlanRules.MaxStops, [], [], [], []);
}

/// <summary>Builds <see cref="RaceWeekendView"/> from today's weekend sessions (#316).</summary>
public static class RaceWeekendRead
{
    public static RaceWeekendView Read(
        CareerModuleContext modules,
        OrganizationId? observer,
        IReadOnlyDictionary<string, CircuitLabel> circuits)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(circuits);
        var session = modules.Session;
        if (RaceWeekendSupport.Today(session) is not { } payload
            || new RaceWeekendDay(modules, modules.Require<RaceWatch>()).Sessions(session.Date, payload) is not { } sessions)
        {
            return RaceWeekendView.None;
        }

        var world = session.World;
        var own = observer is { IsAssigned: true } team ? team.Value : null;
        var people = world.Persons.ToDictionary(person => person.Id.Value, StringComparer.Ordinal);
        string Name(string id) => people.TryGetValue(id, out var person) ? person.Name : id;

        var practice = new List<PracticeLineView>(sessions.Practice.Cars.Length);
        var fastest = sessions.Practice.Cars.IsDefaultOrEmpty ? 0d : sessions.Practice.Cars[0].BestLapSeconds;
        var place = 0;
        foreach (var car in sessions.Practice.Cars)
        {
            var mine = own is not null && car.ConstructorId == own;
            practice.Add(new PracticeLineView(
                ++place,
                car.CarId,
                Name(car.CarId),
                car.ConstructorId,
                LiveRaceRead.TeamName(world, car.ConstructorId),
                mine,
                Ms(car.BestLapSeconds),
                Ms(car.BestLapSeconds) - Ms(fastest),
                Ms(car.LongRunSeconds),
                mine ? [.. car.Tyres.Select(read => new PracticeTyreView(read.CompoundId, read.LowLaps, read.HighLaps))] : []));
        }

        var qualifying = sessions.Qualifying;
        var pole = qualifying.Pole?.BestLapSeconds;
        var grid = qualifying.Grid
            .OrderBy(slot => slot.StartsFromPitLane ? int.MaxValue : slot.GridPosition)
            .Select(slot => new QualifyingLineView(
                slot.GridPosition,
                slot.DriverId,
                Name(slot.DriverId),
                slot.ConstructorId,
                LiveRaceRead.TeamName(world, slot.ConstructorId),
                own is not null && slot.ConstructorId == own,
                Lap(slot.BestLapSeconds),
                Lap(slot.BestLapSeconds) is long best && pole is double first ? best - Ms(first) : null))
            .ToArray();
        var outs = qualifying.NonQualifiers
            .Select(slot => new QualifyingOutView(
                slot.DriverId,
                Name(slot.DriverId),
                LiveRaceRead.TeamName(world, slot.ConstructorId),
                own is not null && slot.ConstructorId == own,
                Reason(slot.Reason),
                slot.BestLapSeconds is double best ? Lap(best) : null))
            .ToArray();

        var weekend = RaceWeekendState.Of(session, payload.Season, payload.Round);
        var offer = sessions.Tyres;
        var plans = new List<RacePlanView>();
        foreach (var entry in sessions.Field.Entries.Where(entry => own is not null && entry.ConstructorId == own))
        {
            var car = entry.Primary.DriverId;
            var slot = qualifying.Grid.FirstOrDefault(item => item.DriverId == car);
            var stored = weekend?.Plans.FirstOrDefault(plan => plan.CarId == car);
            var plan = stored is null ? new RacePlan(car) : RaceWeekendState.ToPlan(stored);
            plans.Add(new RacePlanView(
                car,
                Name(car),
                slot?.GridPosition ?? -1,
                stored is not null,
                plan.StartCompoundId ?? offer.StartCompound,
                plan.Pace is { } pace ? LiveRaceRead.PaceText(pace) : null,
                LiveRaceRead.EngineText(plan.Engine),
                plan.Stops is { } stops ? [.. stops.Select(stop => new PlannedStopView(stop.Lap, stop.CompoundId))] : null));
        }

        circuits.TryGetValue(payload.LayoutId, out var circuit);
        var watched = weekend?.WatchedTapeHash is not null;
        return new RaceWeekendView(
            true,
            payload.Season,
            payload.Round,
            payload.LayoutId,
            circuit?.Name,
            sessions.Input.TotalLaps,
            watched,
            own is not null && !watched,
            own is null ? RaceWeekendKeys.NotOwn : watched ? RaceWeekendKeys.Started : null,
            [.. offer.Compounds.Where(id => id != offer.WetCompound)],
            offer.WetCompound,
            offer.StartCompound,
            offer.TyreChange,
            offer.Refuelling,
            sessions.Pits.RequiredDistinctDryCompounds,
            RacePlanRules.MaxStops,
            practice,
            grid,
            outs,
            plans);
    }

    private static long Ms(double seconds) => (long)Math.Round(seconds * 1000d, MidpointRounding.AwayFromZero);

    private static long? Lap(double seconds) => double.IsFinite(seconds) && seconds > 0 ? Ms(seconds) : null;

    private static string Reason(NonQualifierReason reason) => reason switch
    {
        NonQualifierReason.CarNotRunning => "carNotRunning",
        NonQualifierReason.FailedPreQualifying => "failedPreQualifying",
        NonQualifierReason.OutsideCutoff => "outsideCutoff",
        _ => "gridLimit",
    };

    /// <summary>The pace and engine names of the screen as the engine's modes; null when unknown.</summary>
    public static PaceMode? PaceOf(string? text) => text switch
    {
        "push" => PaceMode.Push,
        "save" => PaceMode.Save,
        "conserve" => PaceMode.Conserve,
        "qualifying" => PaceMode.Qualifying,
        "standard" => PaceMode.Standard,
        _ => null,
    };

    public static EngineMode? EngineOf(string? text) => text switch
    {
        "lean" => EngineMode.Lean,
        "full" => EngineMode.Full,
        "standard" or null => EngineMode.Standard,
        _ => null,
    };
}
