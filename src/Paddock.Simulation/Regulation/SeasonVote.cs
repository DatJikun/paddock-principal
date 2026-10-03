using Paddock.Domain.Random;
using Paddock.Domain.World;

namespace Paddock.Simulation.Regulation;

public static class SeasonVote
{
    private const double Epsilon = 1e-9;

    /// <summary>
    /// Every voter votes on every proposal, in input order. Exactly one noise draw is taken per
    /// voter per proposal (voter-major within proposal), so the draw count does not depend on the outcome.
    /// A utility above zero is a vote for; zero or below is against.
    /// </summary>
    public static IReadOnlyList<ProposalOutcome> Run(
        RuleSet ruleSet,
        IReadOnlyList<RuleProposal> proposals,
        IReadOnlyList<Voter> voters,
        VotingBodyOptions body,
        Xoshiro256StarStar rng,
        LobbyingInfluence? lobbying = null)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        ArgumentNullException.ThrowIfNull(proposals);
        ArgumentNullException.ThrowIfNull(voters);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(rng);
        if (voters.Count == 0)
        {
            throw new ArgumentException("A vote needs at least one voter.", nameof(voters));
        }

        lobbying ??= LobbyingInfluence.None;
        var weights = voters.Select(v => WeightOf(v, voters.Count, body.Weighting)).ToArray();
        var outcomes = new List<ProposalOutcome>(proposals.Count);
        foreach (var proposal in proposals)
        {
            var traces = new List<VoteTrace>(voters.Count);
            double weightFor = 0;
            double weightTotal = 0;
            double weightAgainst = 0;
            for (var i = 0; i < voters.Count; i++)
            {
                var voter = voters[i];
                var factors = voter.Interest.Evaluate(ruleSet, proposal);
                var shift = lobbying.ShiftFor(voter.Id);
                var noise = (rng.NextDouble() * 2 - 1) * body.NoiseScale;
                var total = factors.Sum(f => f.Value) + shift + noise;
                var cast = total > 0 ? VoteCast.For : VoteCast.Against;
                traces.Add(new VoteTrace(proposal.Id, voter.Id, weights[i], factors, shift, noise, total, cast));
                weightTotal += weights[i];
                if (cast == VoteCast.For)
                {
                    weightFor += weights[i];
                }
                else
                {
                    weightAgainst += weights[i];
                }
            }

            outcomes.Add(new ProposalOutcome(
                proposal,
                Passes(body.Threshold, weightFor, weightAgainst, weightTotal),
                weightFor,
                weightTotal,
                traces));
        }

        return outcomes;
    }

    /// <summary>
    /// The next season's rule set. Passed proposals are applied in order; a later passed proposal on a
    /// dimension already changed this season is dropped.
    /// </summary>
    public static RuleSet Apply(
        RuleSet ruleSet,
        IReadOnlyList<ProposalOutcome> outcomes,
        IReadOnlyDictionary<string, RuleDimensionSpec> catalog,
        int nextSeason)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var changes = new List<RuleChange>();
        foreach (var outcome in outcomes)
        {
            if (outcome.Passed && seen.Add(outcome.Proposal.DimensionId))
            {
                changes.Add(outcome.Proposal.ToChange());
            }
        }

        return ruleSet.With(catalog, nextSeason, changes);
    }

    private static double WeightOf(Voter voter, int count, VoteWeighting weighting) => weighting switch
    {
        VoteWeighting.Equal => 1,
        VoteWeighting.ByPoints => Math.Max(0, voter.ConstructorPoints),
        VoteWeighting.ByChampionshipPosition => Math.Max(0, count - voter.ChampionshipPosition + 1),
        VoteWeighting.FixedSeats => Math.Max(0, voter.FixedSeats),
        _ => throw new ArgumentOutOfRangeException(nameof(weighting)),
    };

    private static bool Passes(VoteThreshold threshold, double weightFor, double weightAgainst, double total)
    {
        if (total <= 0)
        {
            return false;
        }

        return threshold switch
        {
            VoteThreshold.SimpleMajority => weightFor > total / 2 + Epsilon,
            VoteThreshold.TwoThirds => weightFor * 3 >= total * 2 - Epsilon,
            VoteThreshold.Unanimity => weightAgainst <= Epsilon && weightFor > 0,
            _ => throw new ArgumentOutOfRangeException(nameof(threshold)),
        };
    }
}
