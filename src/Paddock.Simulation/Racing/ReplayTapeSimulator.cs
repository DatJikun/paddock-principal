using Paddock.Domain.Racing;

namespace Paddock.Simulation.Racing;

/// <summary>
/// The replay tool's simulator: the canned tape <see cref="FakeRaceTapeBuilder"/> already built from a seed.
/// It is not a race engine. It exists so replay goes through <see cref="IRaceSimulator"/> and never reads a weekend.
/// </summary>
public sealed class ReplayTapeSimulator : IRaceSimulator
{
    public static ReplayTapeSimulator Instance { get; } = new();

    private ReplayTapeSimulator()
    {
    }

    public RaceTape Simulate(RaceSimulationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return FakeRaceTapeBuilder.Build(request.Seed).Tape;
    }
}
