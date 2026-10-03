using Paddock.Domain.Random;
using Paddock.Domain.Spy;
using Paddock.Simulation.Racing.Pits;

namespace Paddock.Simulation.Racing.Weekend;

/// <summary>What <see cref="StrategistFactory"/> gets to build the strategist of one car.</summary>
/// <param name="Entry">The car's entry.</param>
/// <param name="Calculators">The tyre and fuel calculators of the car (its supplier's compounds).</param>
/// <param name="AiDecisionsStream">The race's <c>AiDecisions</c> stream (not advanced).</param>
/// <param name="Options">Search limits.</param>
/// <param name="Tracing">Where to record decision traces; null when the sink is disabled.</param>
public sealed record StrategistRequest(
    RaceEntry Entry,
    StrategyCalculators Calculators,
    RngStream AiDecisionsStream,
    StrategistOptions Options,
    StrategistTracing? Tracing);

/// <summary>Builds the strategist of a car. The default builds a <see cref="RuleBasedStrategist"/> from the entry's skill.</summary>
public delegate IRaceStrategist StrategistFactory(StrategistRequest request);

/// <summary>
/// One race weekend as a pure function (T35): weather, qualifying, grid, then the race lap loop with tyres and fuel, lap
/// times, failures, incidents and neutralisation, strategist decisions, pit stops and a pass rule; out come the tape and the
/// classification. It reads and writes no state: the same input gives the same result, byte for byte (INV-002).
/// </summary>
/// <remarks>
/// Randomness (INV-004): only the existing streams, each derived by (master seed, name, season, round), and every car gets its
/// own sub-stream by its id, so adding a car or changing one car's decision shifts nobody else's draws.
/// <list type="bullet">
/// <item><c>Weather</c>: the true weather (<see cref="Racing.Weather.RaceWeather"/>) and, hashed from it, the forecasts.</item>
/// <item><c>LapNoise</c>: qualifying (<see cref="Racing.Qualifying.QualifyingSimulator"/>), the lap-time noise of every car and lap
/// (a child per car and lap), and the hidden track abrasiveness (a child).</item>
/// <item><c>Failures</c>: <see cref="Racing.Reliability.FailureSampler"/>, once per car at the start of the race.</item>
/// <item><c>Incidents</c>: <see cref="Racing.Incidents.IncidentSampler"/>, per car and lap.</item>
/// <item><c>PitStops</c>: <see cref="PitStopModel.Sample"/>, per car and stop.</item>
/// <item><c>AiDecisions</c>: the strategist's own derived stream.</item>
/// </list>
/// Truth versus knowledge (INV-003): the strategist gets only a <see cref="KnowledgeSnapshot"/> (own car, timing-screen gaps,
/// rules, a noisy forecast). Passive Spy (INV-006): the sink only receives decision traces; whether it is enabled changes no
/// draw and no decision. The sink is not part of the result.
/// </remarks>
public static class RaceWeekend
{
    /// <summary>Runs the weekend with the default strategist.</summary>
    public static RaceWeekendResult Run(RaceWeekendInput input, ITraceSink sink) => Run(input, sink, null);

    /// <summary>Runs the weekend; <paramref name="strategistFactory"/> replaces the default strategist (for another AI or for tests).</summary>
    public static RaceWeekendResult Run(RaceWeekendInput input, ITraceSink sink, StrategistFactory? strategistFactory)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(sink);
        return new WeekendRun(input, sink, strategistFactory).Execute();
    }
}
