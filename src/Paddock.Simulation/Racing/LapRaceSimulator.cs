using Paddock.Domain.Racing;
using Paddock.Simulation.Racing.Weekend;

namespace Paddock.Simulation.Racing;

/// <summary>
/// The lap engine behind <see cref="IRaceSimulator"/>. It calls <see cref="RaceWeekend"/> and returns only the tape.
/// Same input and seed, same tape, byte for byte (INV-002). It opens no RNG stream of its own: the weekend still
/// derives the streams it already had.
/// </summary>
public sealed class LapRaceSimulator : IRaceSimulator
{
    public static LapRaceSimulator Instance { get; } = new();

    private LapRaceSimulator()
    {
    }

    public RaceTape Simulate(RaceSimulationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Weekend is null)
        {
            throw new ArgumentException("The lap engine needs a weekend input.", nameof(request));
        }

        var result = RaceWeekend.Run(request.Weekend, request.Sink, request.Strategist);
        var tape = LapFrameInterpolator.Attach(
            result.Tape,
            request.Weekend.Track.LengthKm * 1000d,
            request.FrameSampleSeconds);
        request.Publish(RacePublishedFacts.From(result) with { Tape = tape });
        return tape;
    }
}
