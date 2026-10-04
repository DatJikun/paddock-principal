using System.Globalization;
using Paddock.Domain.Random;
using Paddock.Domain.Spy;

namespace Paddock.Simulation.Ai;

/// <summary>
/// The hook through which the history of the world may colour an AI's choice (PP-046, T46). The default is neutral. A proposal that
/// carries history into AI choice has to arrive here, as a signed adjustment of one option's utility; it is recorded in the trace
/// as a factor the player cannot see. This type gives no foresight: it is handed the decision and the option, never the future (D-010).
/// </summary>
public interface IHistoricalBias
{
    /// <summary>Utility added to the option. A pure function of its arguments: no state, no RNG (INV-005).</summary>
    double Adjustment(string organization, string decisionKind, string optionId, DateOnly today);
}

/// <summary>No historical pull on any choice.</summary>
public sealed class NeutralHistoricalBias : IHistoricalBias
{
    public static NeutralHistoricalBias Instance { get; } = new();

    public double Adjustment(string organization, string decisionKind, string optionId, DateOnly today) => 0.0;
}

/// <summary>
/// The named stream of AI choices (<see cref="RngStreamName.AiDecisions"/>), derived from the master seed and the season (INV-004). It
/// is never advanced: every draw comes from a child keyed by the organization, the decision and the option, so the same question
/// always gets the same noise and an extra option or an extra team never shifts another one's.
/// </summary>
public static class AiRandom
{
    public static RngStream ForSeason(ulong masterSeed, int season) => RngStream.Derive(masterSeed, RngStreamName.AiDecisions, season);
}

/// <summary>Everything a decision of one principal needs besides its inputs.</summary>
public sealed class DecisionContext
{
    public DecisionContext(
        string organization,
        string manager,
        DateOnly today,
        PrincipalArchetype archetype,
        PrincipalSkillset skills,
        RngStream stream,
        IHistoricalBias? bias = null,
        ITraceSink? sink = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organization);
        ArgumentException.ThrowIfNullOrWhiteSpace(manager);
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(stream);
        Organization = organization;
        Manager = manager;
        Today = today;
        Archetype = archetype;
        Profile = ArchetypeProfile.Of(archetype);
        Skills = skills;
        Stream = stream;
        Bias = bias ?? NeutralHistoricalBias.Instance;
        Sink = sink ?? NullSink.Instance;
    }

    public string Organization { get; }

    /// <summary>The id of the AI manager the commands are filed under.</summary>
    public string Manager { get; }

    public DateOnly Today { get; }

    public int Season => Today.Year;

    public PrincipalArchetype Archetype { get; }

    public ArchetypeProfile Profile { get; }

    public PrincipalSkillset Skills { get; }

    /// <summary>The overall decision level, 1 to <see cref="AiEstimates.LevelCount"/>.</summary>
    public int Level => Skills.Level;

    public RngStream Stream { get; }

    public IHistoricalBias Bias { get; }

    public ITraceSink Sink { get; }

    /// <summary>The noise of one option in utility units: zero-mean, spread by the tier of the principal's facet. Pure: the stream is not advanced.</summary>
    public double Noise(DecisionFacet facet, string decisionKey, string optionId)
    {
        var sd = AiEstimates.NoiseSdByLevel[Skills.For(facet) - 1];
        var rng = Stream.DeriveChild(string.Create(CultureInfo.InvariantCulture, $"ai:{Organization}:{decisionKey}:{optionId}"));
        var first = rng.NextDouble();
        var second = rng.NextDouble();

        // A triangular draw on [-a, a] has variance a*a/6, so a = sd*sqrt(6) gives the tier's standard deviation. Only +, -, *, / and sqrt: exact on every platform.
        return (first + second - 1.0) * sd * Math.Sqrt(6.0);
    }

    /// <summary>A unit draw for the tie of two options that no number separates, from the same stream, keyed by the decision.</summary>
    public double Tie(string decisionKey, string optionId)
    {
        var rng = Stream.DeriveChild(string.Create(CultureInfo.InvariantCulture, $"ai-tie:{Organization}:{decisionKey}:{optionId}"));
        return rng.NextDouble();
    }
}
