using Paddock.Domain.Racing;

namespace Paddock.Simulation.Racing;

/// <summary>
/// The only way career code and SimRunner start a race. Pass <paramref name="engine"/> (default
/// <see cref="RaceEngineKind.Lap"/>) or pass a simulator, including a test stub.
/// </summary>
public static class RaceSession
{
    public static RaceTape Run(RaceSimulationRequest request, RaceEngineKind engine = RaceEngineKind.Lap)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Run(RaceSimulators.Resolve(engine), request);
    }

    public static RaceTape Run(IRaceSimulator simulator, RaceSimulationRequest request)
    {
        ArgumentNullException.ThrowIfNull(simulator);
        ArgumentNullException.ThrowIfNull(request);
        return simulator.Simulate(request);
    }
}
