using Paddock.Domain.Racing;

namespace Paddock.Simulation.Racing;

/// <summary>
/// One race, whatever engine runs it (PP-052). The output is the tape. Callers do not read lap records or
/// <c>WeekendRun</c> state. The engine is chosen with <see cref="RaceSimulators.Resolve"/>; the default is
/// <see cref="RaceEngineKind.Lap"/>.
/// </summary>
public interface IRaceSimulator
{
    RaceTape Simulate(RaceSimulationRequest request);
}
