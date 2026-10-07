using System.Collections.Immutable;
using Paddock.Domain.Racing;
using Paddock.Domain.World;
using Paddock.Simulation.Career;
using Paddock.Simulation.Racing.Pits;

namespace Paddock.Application.Racing;

/// <summary>What a pit wall order asks for, as the screen sends it.</summary>
public enum LiveOrderAction
{
    /// <summary>Run at a pace from the next lap on.</summary>
    Pace,

    /// <summary>Come in at the end of this lap (or the next, when the car is past the pit entry).</summary>
    Pit,

    /// <summary>Take back a stop the pit wall called that has not happened yet.</summary>
    CancelPit,

    /// <summary>Give the car's pace back to the strategist.</summary>
    Auto,

    /// <summary>Run the engine in a mode from the next lap on.</summary>
    Engine,

    /// <summary>Switch the team order to let the team-mate by on or off, from the next lap on.</summary>
    LetBy,
}

/// <summary>One order from the screen: the car (by its tape id), the action, and the pace, tyres, engine mode or switch it needs.</summary>
public sealed record LiveOrderRequest(
    string CarId,
    LiveOrderAction Action,
    PaceMode Pace = PaceMode.Standard,
    string? CompoundId = null,
    EngineMode Engine = EngineMode.Standard,
    bool On = false);

/// <summary>
/// Takes a pit wall order during a watched race (#286). The host decides the lap from the race time (never the screen), checks
/// that the car is the manager's and still running, re-runs the race with every order so far, and shows the new race only when
/// nothing before now changed (INV-002). A refusal is a translation key and changes nothing.
/// </summary>
public static class LiveRaceOrders
{
    /// <summary>
    /// ESTIMATE: share of a lap after which a stop can no longer be made at its end: the car is past the pit entry, so the stop
    /// waits a lap. The map draws the pit lane from about the same point (ui race-map, PIT_FROM).
    /// </summary>
    public const double PitCallShare = 0.9;

    public static string? Issue(
        CareerSession session,
        RaceWatch watch,
        string managerId,
        OrganizationId? observer,
        LiveOrderRequest request,
        long nowMs)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(watch);
        ArgumentNullException.ThrowIfNull(request);
        if (watch.Steering is not { } steering || watch.Tape is not { } tape || watch.Tyres is not { } tyres
            || tape.Events.IsDefaultOrEmpty || tape.Events[0] is not RaceStarted started)
        {
            return LiveOrderKeys.Locked;
        }

        if (observer is not { IsAssigned: true } team || TeamOf(session, watch, request.CarId) != team.Value)
        {
            return LiveOrderKeys.NotOwn;
        }

        var car = Laps(tape, request.CarId, nowMs);
        if (car.Over)
        {
            return LiveOrderKeys.NotRunning;
        }

        var issued = steering.Issued;
        PitWallOrder order;
        switch (request.Action)
        {
            case LiveOrderAction.Pace or LiveOrderAction.Auto or LiveOrderAction.Engine or LiveOrderAction.LetBy:
            {
                var lap = nowMs <= car.LapStartMs ? car.Current : car.Current + 1;
                if (lap > started.TotalLaps)
                {
                    return LiveOrderKeys.TooLate;
                }

                if (request.Action == LiveOrderAction.LetBy && request.On && !MateRunning(session, watch, tape, request.CarId, nowMs))
                {
                    return LiveOrderKeys.NoMate;
                }

                order = request.Action switch
                {
                    LiveOrderAction.Auto => new PitWallOrder(request.CarId, lap, PitWallOrderKind.Auto),
                    LiveOrderAction.Engine => new PitWallOrder(request.CarId, lap, PitWallOrderKind.Engine, Engine: request.Engine),
                    LiveOrderAction.LetBy => new PitWallOrder(request.CarId, lap, PitWallOrderKind.LetBy, On: request.On),
                    _ => new PitWallOrder(request.CarId, lap, PitWallOrderKind.Pace, request.Pace),
                };
                break;
            }

            case LiveOrderAction.Pit:
            {
                var tyre = request.CompoundId;
                if (tyre is not null && (!tyres.TyreChange || !tyres.Compounds.Contains(tyre, StringComparer.Ordinal)))
                {
                    return LiveOrderKeys.BadTyre;
                }

                if (tyre is null && !tyres.Refuelling)
                {
                    return LiveOrderKeys.NoStop;
                }

                var lap = car.StopLap(nowMs);
                if (lap >= started.TotalLaps || (car.FinishedLaps is int laps && lap >= laps))
                {
                    return LiveOrderKeys.TooLate;
                }

                order = new PitWallOrder(request.CarId, lap, PitWallOrderKind.Pit, CompoundId: tyre);
                issued = [.. issued.Where(o => !(o.Order.CarId == request.CarId && o.Order.Kind == PitWallOrderKind.Pit && o.Order.Lap == lap))];
                break;
            }

            default:
            {
                var from = car.StopLap(nowMs);
                var kept = issued.Where(o => !(o.Order.CarId == request.CarId && o.Order.Kind == PitWallOrderKind.Pit && o.Order.Lap >= from)).ToImmutableArray();
                if (kept.Length == issued.Length)
                {
                    return LiveOrderKeys.NoPit;
                }

                return steering.TryApply(kept, tape, nowMs) ? null : LiveOrderKeys.Unsafe;
            }
        }

        return steering.TryApply(issued.Add(new IssuedOrder(managerId, nowMs, order)), tape, nowMs) ? null : LiveOrderKeys.Unsafe;
    }

    /// <summary>The team of a car in the watched race, from the round's archive (the same rows the race read uses).</summary>
    internal static string? TeamOf(CareerSession session, RaceWatch watch, string carId) =>
        session.World.Section<RaceResultsSection>(RaceResultsSection.SectionName)?.Find(watch.Season, watch.Round)?.Rows
            .FirstOrDefault(row => row.DriverId == carId)?.TeamId;

    // Another car of the same team still in the race: the only one a team order can let by.
    private static bool MateRunning(CareerSession session, RaceWatch watch, RaceTape tape, string carId, long nowMs)
    {
        var team = TeamOf(session, watch, carId);
        var started = (RaceStarted)tape.Events[0];
        return started.DriverIds.Any(other =>
            other != carId && TeamOf(session, watch, other) == team && !Laps(tape, other, nowMs).Over);
    }

    private static CarLaps Laps(RaceTape tape, string carId, long nowMs)
    {
        var completed = 0;
        var lapStart = 0L;
        long? lapEnd = null;
        long? pitIn = null;
        int? finished = null;
        var over = true;
        foreach (var raceEvent in tape.Events)
        {
            switch (raceEvent)
            {
                case RaceStarted started:
                    over = !started.DriverIds.Contains(carId);
                    break;
                case LapCompleted lap when lap.DriverId == carId:
                    if (lap.RaceTime <= nowMs)
                    {
                        completed = lap.Lap;
                        lapStart = lap.RaceTime;
                    }
                    else
                    {
                        lapEnd ??= lap.RaceTime;
                    }

                    break;
                case PitStop { Phase: PitLanePhase.In } pit when pit.DriverId == carId && pit.RaceTime > nowMs:
                    pitIn ??= pit.RaceTime;
                    break;
                case Finished done when done.DriverId == carId:
                    finished = done.LapsCompleted;
                    over |= done.RaceTime <= nowMs;
                    break;
                case Retirement gone when gone.DriverId == carId:
                    over |= gone.RaceTime <= nowMs;
                    break;
            }
        }

        // A stop already on the tape for this lap closes the window at its pit entry.
        var close = lapEnd is long end ? Math.Min(pitIn ?? long.MaxValue, lapStart + (long)((end - lapStart) * PitCallShare)) : (long?)null;
        return new CarLaps(completed + 1, lapStart, close, finished, over);
    }

    private sealed record CarLaps(int Current, long LapStartMs, long? StopClosesMs, int? FinishedLaps, bool Over)
    {
        /// <summary>The lap a stop called now is made at the end of.</summary>
        public int StopLap(long nowMs) => StopClosesMs is long close && nowMs < close ? Current : Current + 1;
    }
}

/// <summary>Refusals of a pit wall order.</summary>
public static class LiveOrderKeys
{
    public const string Locked = "live.order.locked";
    public const string NotOwn = "live.order.notOwn";
    public const string NotRunning = "live.order.notRunning";
    public const string TooLate = "live.order.tooLate";
    public const string BadTyre = "live.order.badTyre";
    public const string NoStop = "live.order.noStop";
    public const string NoPit = "live.order.noPit";
    public const string Unsafe = "live.order.unsafe";
    public const string NoMate = "live.order.noMate";

    public static IReadOnlyList<string> All { get; } = [Locked, NotOwn, NotRunning, TooLate, BadTyre, NoStop, NoPit, Unsafe, NoMate];
}
