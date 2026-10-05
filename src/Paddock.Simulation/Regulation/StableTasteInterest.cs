using Paddock.Domain.Random;
using Paddock.Domain.World;

namespace Paddock.Simulation.Regulation;

/// <summary>
/// ESTIMATE voter until a principal has regulation preferences: a stable taste per team, dimension and value, plus a
/// status-quo bias. The draw is <c>Derive(0, team|dimension|value, 0)</c>, so it does not move any career stream (INV-004).
/// The same inputs always give the same utility (INV-002).
/// </summary>
public sealed class StableTasteInterest : IVoterInterest
{
    /// <summary>ESTIMATE: how much a voter prefers the rule it already has. The vote-sim stand-in uses the same figure.</summary>
    public const double StatusQuoBias = -0.2;

    private readonly string _teamId;

    public StableTasteInterest(string teamId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamId);
        _teamId = teamId;
    }

    public IReadOnlyList<UtilityFactor> Evaluate(RuleSet current, RuleProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(proposal);
        var taste = RngStreams
            .Derive(0, _teamId + "|" + proposal.DimensionId + "|" + proposal.ProposedValue, 0)
            .NextDouble() * 2 - 1;
        return
        [
            new UtilityFactor("vote.reason.taste", taste),
            new UtilityFactor("vote.reason.statusQuo", StatusQuoBias),
        ];
    }
}
