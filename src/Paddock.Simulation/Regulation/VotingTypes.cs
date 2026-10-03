using Paddock.Domain.World;

namespace Paddock.Simulation.Regulation;

/// <summary>A rule change put to the vote. <see cref="Id"/> is stable within one season's batch.</summary>
public sealed record RuleProposal(
    string Id,
    string DimensionId,
    string ProposedValue,
    string ProposerId,
    int StartSeason)
{
    public RuleChange ToChange() => new(DimensionId, ProposedValue);
}

/// <summary>One named reason behind a voter's utility, e.g. a translation key. Positive favours the proposal.</summary>
public readonly record struct UtilityFactor(string Reason, double Value);

/// <summary>
/// How a voter values a proposal (estimated cost or benefit). Supplied by the caller; the
/// engine never looks at team state itself.
/// </summary>
public interface IVoterInterest
{
    IReadOnlyList<UtilityFactor> Evaluate(RuleSet current, RuleProposal proposal);
}

/// <summary>A team (or seat) with a vote. Points, position and seats feed the weighting of the body.</summary>
public sealed record Voter(
    string Id,
    IVoterInterest Interest,
    double ConstructorPoints = 0,
    int ChampionshipPosition = 1,
    int FixedSeats = 1);

public enum VoteThreshold
{
    SimpleMajority,
    TwoThirds,
    Unanimity,
}

public enum VoteWeighting
{
    /// <summary>One voter, one vote.</summary>
    Equal,

    /// <summary>Weight = constructor points (at least zero).</summary>
    ByPoints,

    /// <summary>Weight = voterCount - position + 1, so the champion weighs the most.</summary>
    ByChampionshipPosition,

    /// <summary>Weight = <see cref="Voter.FixedSeats"/>.</summary>
    FixedSeats,
}

/// <summary>Era-independent configuration of the body; defaults per era are in <see cref="VotingBodyDefaults"/>.</summary>
/// <param name="NoiseScale">Half-width of the uniform random term added to each voter's utility (an estimate).</param>
public sealed record VotingBodyOptions(
    VoteThreshold Threshold,
    VoteWeighting Weighting,
    double NoiseScale = 0.1);

/// <summary>
/// Default body per era band. Every value here is an ESTIMATE, not a calibrated or historically
/// sourced fact (AGENTS.md: numbers are estimates until calibrated).
/// </summary>
public static class VotingBodyDefaults
{
    public static VotingBodyOptions ForSeason(int season) => season switch
    {
        < 1981 => new VotingBodyOptions(VoteThreshold.SimpleMajority, VoteWeighting.Equal),
        < 2009 => new VotingBodyOptions(VoteThreshold.TwoThirds, VoteWeighting.Equal),
        _ => new VotingBodyOptions(VoteThreshold.TwoThirds, VoteWeighting.ByChampionshipPosition),
    };
}

/// <summary>Per-voter additive shift to vote utility that a manager's lobbying can feed later.</summary>
public sealed class LobbyingInfluence
{
    public static readonly LobbyingInfluence None = new(new Dictionary<string, double>());

    private readonly IReadOnlyDictionary<string, double> _shiftByVoter;

    public LobbyingInfluence(IReadOnlyDictionary<string, double> shiftByVoter)
    {
        ArgumentNullException.ThrowIfNull(shiftByVoter);
        _shiftByVoter = shiftByVoter;
    }

    public double ShiftFor(string voterId) => _shiftByVoter.TryGetValue(voterId, out var shift) ? shift : 0;
}

public enum VoteCast
{
    For,
    Against,
}

/// <summary>Why one voter voted as they did on one proposal.</summary>
public sealed record VoteTrace(
    string ProposalId,
    string VoterId,
    double Weight,
    IReadOnlyList<UtilityFactor> Factors,
    double Lobbying,
    double Noise,
    double TotalUtility,
    VoteCast Cast);

public sealed record ProposalOutcome(
    RuleProposal Proposal,
    bool Passed,
    double WeightFor,
    double WeightTotal,
    IReadOnlyList<VoteTrace> Votes);
