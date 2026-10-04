using Paddock.Domain.Spy;
using Paddock.Simulation.Racing.Weekend;

namespace Paddock.Simulation.Racing;

/// <summary>
/// Input of <see cref="IRaceSimulator"/>: the entry list, the track version, the weather source, the era regulations
/// and the master seed live on <see cref="Weekend"/>. The engine derives its existing RNG streams from that seed
/// (season, round, stream name). No new stream is added here. <see cref="ForSeed"/> is the replay tool's input,
/// which has no weekend.
/// </summary>
public sealed class RaceSimulationRequest
{
    /// <summary>The weekend the lap engine runs. Null for a seed-only replay.</summary>
    public RaceWeekendInput? Weekend { get; init; }

    /// <summary>Master seed. For a weekend this is <see cref="RaceWeekendInput.MasterSeed"/>.</summary>
    public ulong Seed { get; init; }

    /// <summary>Decision traces. A disabled sink changes nothing the engine draws (INV-006).</summary>
    public ITraceSink Sink { get; init; } = NullSink.Instance;

    /// <summary>Replaces the default strategist. Null keeps the lap engine's own default.</summary>
    public StrategistFactory? Strategist { get; init; }

    /// <summary>
    /// Spectator facts the lap engine publishes. A stub simulator leaves this null. Never holds lap records.
    /// </summary>
    public RacePublishedFacts? Published { get; internal set; }

    public static RaceSimulationRequest ForWeekend(RaceWeekendInput weekend, ITraceSink sink, StrategistFactory? strategist = null)
    {
        ArgumentNullException.ThrowIfNull(weekend);
        ArgumentNullException.ThrowIfNull(sink);
        return new RaceSimulationRequest
        {
            Weekend = weekend,
            Seed = weekend.MasterSeed,
            Sink = sink,
            Strategist = strategist,
        };
    }

    public static RaceSimulationRequest ForSeed(ulong seed) => new() { Seed = seed };

    internal void Publish(RacePublishedFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        Published = facts;
    }
}
