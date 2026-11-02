using System.Collections.Immutable;

namespace Paddock.Simulation.Racing.Pits;

/// <summary>One stop of a pre-race plan (#316): come in at the end of <paramref name="Lap"/> for <paramref name="CompoundId"/>.</summary>
public sealed record PlannedStop(int Lap, string CompoundId);

/// <summary>
/// A team's plan for one car, set before the start (#316). It is data, like a pit wall order, so the same plan gives the same race
/// (INV-002). <see cref="CarId"/> is the id the tape uses for the car (its first driver).
/// </summary>
/// <param name="CarId">The car, by its tape id.</param>
/// <param name="StartCompoundId">The tyres the car starts on; null keeps the weekend's default (the middle race compound).</param>
/// <param name="Pace">The driver's pace from the start; null leaves it to the strategist.</param>
/// <param name="Engine">The engine mode from the start.</param>
/// <param name="Stops">The planned stops, by lap; null leaves the stops to the strategist.</param>
public sealed record RacePlan(
    string CarId,
    string? StartCompoundId = null,
    PaceMode? Pace = null,
    EngineMode Engine = EngineMode.Standard,
    ImmutableArray<PlannedStop>? Stops = null)
{
    /// <summary>
    /// The plan as the orders a pit wall would give before lap 1: the pace and engine mode from lap 1 and one stop order per planned
    /// stop. An engine reads a plan through these orders, so a plan and a pit wall order are one mechanism (PP-058).
    /// </summary>
    public IEnumerable<PitWallOrder> AsOrders()
    {
        if (Pace is { } pace)
        {
            yield return new PitWallOrder(CarId, 1, PitWallOrderKind.Pace, pace);
        }

        if (Engine != EngineMode.Standard)
        {
            yield return new PitWallOrder(CarId, 1, PitWallOrderKind.Engine, Engine: Engine);
        }

        if (Stops is { } stops)
        {
            foreach (var stop in stops)
            {
                yield return new PitWallOrder(CarId, stop.Lap, PitWallOrderKind.Pit, CompoundId: stop.CompoundId);
            }
        }
    }

    /// <summary>True when the plan names the stops, so the strategist calls only the stops the weather forces.</summary>
    public bool FixesStops => Stops is not null;
}
