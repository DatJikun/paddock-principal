using Paddock.Domain.Racing;

namespace Paddock.Simulation.Regulation;

/// <summary>
/// One team's vote at the deadline: the option it backs (or <see cref="BallotOptions.Abstain"/>), the weight it carries in this body,
/// the banked votes it spends on it, whether an abstention went into its bank, and the reason that weighed most.
/// </summary>
public readonly record struct VoterBallot(string TeamId, string Option, int Weight, int Spent, bool Banked, string ReasonKey);

/// <summary>
/// The count of one ballot item (#275). Every voter backs one option (the status quo or a variant) or abstains. The option with the
/// most weight leads. When two or more options share the top, the FIA president decides among them and only then (owner
/// decision 4); his vote counts <see cref="RegulationEstimates.PresidentWeight"/> for the option he picks, and his pick is the
/// result. A clear leader is adopted when its weight reaches the threshold of the body of that era against everything else that was
/// cast; a leading status quo changes nothing. A variant tied for the lead with the status quo is decided by the president, not by
/// a rule that favours change or favours the old. Pure: no random number is drawn here.
/// </summary>
public static class BallotTally
{
    public const string ReasonAdopted = "regulation.result.adopted";

    public const string ReasonAdoptedByPresident = "regulation.result.adoptedByPresident";

    public const string ReasonStatusQuoLeads = "regulation.result.statusQuoLeads";

    public const string ReasonPresidentKeptStatusQuo = "regulation.result.presidentKeptStatusQuo";

    public const string ReasonBelowThreshold = "regulation.result.belowThreshold";

    public const string ReasonNoVotes = "regulation.result.noVotes";

    /// <param name="variantIds">Ids of the variants on the ballot.</param>
    /// <param name="votes">One entry per team of the series, abstentions included.</param>
    /// <param name="threshold">The share of the cast weight a variant needs.</param>
    /// <param name="president">Called with the tied top options (status quo first, then variants in ordinal order) and returns the one he picks.</param>
    public static BallotResult Resolve(
        IReadOnlyList<string> variantIds,
        IReadOnlyList<VoterBallot> votes,
        VoteThreshold threshold,
        Func<IReadOnlyList<string>, string> president)
    {
        ArgumentNullException.ThrowIfNull(variantIds);
        ArgumentNullException.ThrowIfNull(votes);
        ArgumentNullException.ThrowIfNull(president);
        var options = new List<string>(variantIds.Count + 1) { BallotOptions.StatusQuo };
        options.AddRange(variantIds.OrderBy(id => id, StringComparer.Ordinal));
        var weight = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var option in options)
        {
            weight[option] = 0;
        }

        var stances = new List<TeamStance>(votes.Count);
        foreach (var vote in votes)
        {
            if (vote.Option != BallotOptions.Abstain && !weight.ContainsKey(vote.Option))
            {
                throw new ArgumentException("Option '" + vote.Option + "' is not on the ballot.", nameof(votes));
            }

            var carried = vote.Option == BallotOptions.Abstain ? 0 : checked(vote.Weight * (1 + vote.Spent));
            if (vote.Option != BallotOptions.Abstain)
            {
                weight[vote.Option] += carried;
            }

            stances.Add(new TeamStance(vote.TeamId, vote.Option, carried, vote.Spent, vote.Banked, vote.ReasonKey));
        }

        var total = weight.Values.Sum();
        if (total == 0)
        {
            return new BallotResult(BallotOutcome.NoVotes, BallotOptions.StatusQuo, ReasonNoVotes, false, Tally(weight), stances);
        }

        var top = weight.Values.Max();
        var leaders = options.Where(option => weight[option] == top).ToArray();
        var decidedByPresident = leaders.Length > 1;
        var leader = leaders[0];
        if (decidedByPresident)
        {
            leader = president(leaders);
            if (!leaders.Contains(leader, StringComparer.Ordinal))
            {
                throw new InvalidOperationException("The president picked '" + leader + "', which was not tied for the lead.");
            }

            weight[leader] += RegulationEstimates.PresidentWeight;
            total += RegulationEstimates.PresidentWeight;
        }

        if (leader == BallotOptions.StatusQuo)
        {
            return new BallotResult(
                BallotOutcome.Rejected,
                leader,
                decidedByPresident ? ReasonPresidentKeptStatusQuo : ReasonStatusQuoLeads,
                decidedByPresident,
                Tally(weight),
                stances);
        }

        // A tie the president decided is decided: his pick is the result. Only a clear leader has to reach the threshold.
        var passes = decidedByPresident || SeasonVote.Passes(threshold, weight[leader], total - weight[leader], total);
        if (!passes)
        {
            return new BallotResult(BallotOutcome.Rejected, leader, ReasonBelowThreshold, decidedByPresident, Tally(weight), stances);
        }

        return new BallotResult(
            BallotOutcome.Adopted,
            leader,
            decidedByPresident ? ReasonAdoptedByPresident : ReasonAdopted,
            decidedByPresident,
            Tally(weight),
            stances);
    }

    private static List<OptionTally> Tally(Dictionary<string, int> weight)
    {
        var list = new List<OptionTally>(weight.Count);
        foreach (var (option, carried) in weight)
        {
            list.Add(new OptionTally(option, carried));
        }

        return list;
    }
}
