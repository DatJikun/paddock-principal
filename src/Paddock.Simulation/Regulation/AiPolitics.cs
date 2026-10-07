using Paddock.Domain.Career;
using Paddock.Domain.Racing;

namespace Paddock.Simulation.Regulation;

/// <summary>One thing a team could vote on or propose: a rule and the value it would move to, with the trait difference and the home effect.</summary>
public sealed record RuleOption(string DimensionId, string Current, string Value, ValueTraits Delta, double HomeEffect);

/// <summary>What an AI team knows when it weighs spending its one proposal (INV-003): itself, its money and the options on the table.</summary>
/// <param name="CashCents">Its own balance now. May be negative.</param>
/// <param name="RevenueBasisCents">The revenue its fee is a share of.</param>
public sealed record AiProposalInputs(
    TeamView Team,
    PoliticalLeaning Leaning,
    long FeeCents,
    long CashCents,
    long RevenueBasisCents,
    IReadOnlyList<RuleOption> Options);

/// <summary>The reasoning of an AI team about a proposal, kept so a decision can be explained.</summary>
public sealed record AiProposalDecision(
    bool Propose,
    RuleOption? Choice,
    double Gain,
    double FeeBurden,
    double DebtPenalty,
    double CooldownCost,
    double Net,
    string ReasonKey);

/// <summary>What an AI team knows when it votes: itself, the variants on the ballot (with the noise drawn for each), its bank and the mode.</summary>
/// <param name="Variants">The variants in ballot order, each with the option it stands for and the noise drawn for it.</param>
public sealed record AiVoteInputs(
    TeamView Team,
    PoliticalLeaning Leaning,
    IReadOnlyList<(string VariantId, RuleOption Option, double Noise)> Variants,
    VoteMode Mode,
    int Bank);

/// <summary>The vote of an AI team and the reasoning behind it. <see cref="Stake"/> is how much the choice matters to it.</summary>
public sealed record AiVote(string Option, int Spent, bool Banked, string ReasonKey, double Stake, IReadOnlyList<UtilityFactor> Factors);

/// <summary>
/// How an AI team behaves in the political life of a series (#275, owner decisions 6 to 9): it proposes only when it expects to
/// benefit, weighing the fee against its own finances and counting the wait before it may propose again as a cost; it votes by its own
/// interest plus its leaning; and in vote-bank mode it banks the vote on a ballot that matters little and spends banked votes on one
/// that matters. It uses the same rules as the player. Pure: noise is drawn by the caller from the Regulations stream.
/// </summary>
public static class AiPolitics
{
    public const string ReasonNothingWorthIt = "regulation.ai.reason.nothingWorthIt";

    public const string ReasonTooExpensive = "regulation.ai.reason.tooExpensive";

    public const string ReasonInDebt = "regulation.ai.reason.inDebt";

    public const string ReasonBenefits = "regulation.ai.reason.benefits";

    public static AiProposalDecision Consider(AiProposalInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        RuleOption? best = null;
        var bestGain = 0.0;
        var factors = new List<UtilityFactor>();
        foreach (var option in inputs.Options)
        {
            var current = VoteInterest.Evaluate(inputs.Team, inputs.Leaning, option.DimensionId, option.Value, option.Delta, option.HomeEffect);
            var gain = VoteInterest.Total(current);
            if (gain > bestGain || (gain == bestGain && best is not null && IsBefore(option, best)))
            {
                best = option;
                bestGain = gain;
                factors.Clear();
                factors.AddRange(current);
            }
        }

        var basis = Math.Max(1, Math.Max(inputs.RevenueBasisCents, inputs.FeeCents));
        var burden = RegulationEstimates.FeeBurdenScale * inputs.FeeCents / basis;
        var left = inputs.CashCents - inputs.FeeCents;
        var debt = left < 0 ? RegulationEstimates.DebtPenaltyScale * Math.Min(1.0, -left / (double)basis) : 0.0;
        if (best is null)
        {
            return new AiProposalDecision(false, null, 0, burden, debt, RegulationEstimates.CooldownCost, -burden - debt - RegulationEstimates.CooldownCost, ReasonNothingWorthIt);
        }

        var net = bestGain - burden - debt - RegulationEstimates.CooldownCost;
        var propose = net >= RegulationEstimates.MinimumNetGain;
        var reason = propose ? ReasonBenefits : debt > burden ? ReasonInDebt : ReasonTooExpensive;
        return new AiProposalDecision(propose, best, bestGain, burden, debt, RegulationEstimates.CooldownCost, net, reason);
    }

    public static AiVote Vote(AiVoteInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var bestOption = BallotOptions.StatusQuo;
        var bestUtility = 0.0;
        IReadOnlyList<UtilityFactor> bestFactors = [];
        var strongestFactors = bestFactors;
        var strongestUtility = double.NegativeInfinity;
        foreach (var (variantId, option, noise) in inputs.Variants)
        {
            var factors = VoteInterest.Evaluate(inputs.Team, inputs.Leaning, option.DimensionId, option.Value, option.Delta, option.HomeEffect);
            var utility = VoteInterest.Total(factors) + noise;
            if (utility > bestUtility)
            {
                bestOption = variantId;
                bestUtility = utility;
                bestFactors = factors;
            }

            if (utility > strongestUtility)
            {
                strongestUtility = utility;
                strongestFactors = factors;
            }
        }

        // Voting for the status quo is as strong a stand as the best variant is weak: the stake is how far the best variant is from zero.
        var stake = Math.Abs(bestOption == BallotOptions.StatusQuo ? strongestUtility : bestUtility);
        var reasonFactors = bestOption == BallotOptions.StatusQuo ? strongestFactors : bestFactors;
        var reason = VoteInterest.DominantReason(reasonFactors);
        if (inputs.Mode == VoteMode.VoteBank)
        {
            if (stake < RegulationEstimates.LowStake && inputs.Bank < RegulationEstimates.BankCap)
            {
                return new AiVote(BallotOptions.Abstain, 0, true, VoteInterest.ReasonBanked, stake, reasonFactors);
            }

            var spend = stake >= RegulationEstimates.HighStake ? Math.Min(inputs.Bank, RegulationEstimates.MaxSpendPerItem) : 0;
            return new AiVote(bestOption, spend, false, reason, stake, reasonFactors);
        }

        return new AiVote(bestOption, 0, false, reason, stake, reasonFactors);
    }

    private static bool IsBefore(RuleOption left, RuleOption right)
    {
        var byDimension = string.CompareOrdinal(left.DimensionId, right.DimensionId);
        return byDimension != 0 ? byDimension < 0 : string.CompareOrdinal(left.Value, right.Value) < 0;
    }
}
