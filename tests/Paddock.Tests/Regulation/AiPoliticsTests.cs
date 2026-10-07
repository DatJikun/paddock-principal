using Paddock.Domain.Career;
using Paddock.Domain.Racing;
using Paddock.Simulation.Regulation;

namespace Paddock.Tests.Regulation;

/// <summary>How an AI team votes and spends its one proposal (#275, owner decisions 3, 6 to 9): by its own interest and its leaning, using only what it may know.</summary>
public class AiPoliticsTests
{
    private static readonly TeamView Middle = new("t05", 0.5, 0.0, null);

    private static RuleOption Knockout() => OptionOf("qualifying_format", "two_session_best_time", "knockout_q1_q2_q3");

    private static RuleOption OptionOf(string dimension, string from, string to, double home = 0) => new(
        dimension,
        from,
        to,
        LiveRules.TraitsOf(dimension, to) - LiveRules.TraitsOf(dimension, from),
        home);

    private static AiVote VoteOf(PoliticalLeaning leaning, RuleOption option, VoteMode mode = VoteMode.OneVoteEach, int bank = 0, TeamView? team = null, double noise = 0) =>
        AiPolitics.Vote(new AiVoteInputs(team ?? Middle, leaning, [("v1", option, noise)], mode, bank));

    [Fact]
    public void SameTeamDataAndDifferentLeaningsGiveDifferentVotesOnTheSameProposal()
    {
        var option = Knockout();

        var traditionalist = VoteOf(PoliticalLeaning.Traditionalist, option);
        var progressive = VoteOf(PoliticalLeaning.Progressive, option);
        var gimmicky = VoteOf(PoliticalLeaning.Gimmicky, option);

        Assert.Equal(BallotOptions.StatusQuo, traditionalist.Option);
        Assert.Equal("v1", progressive.Option);
        Assert.Equal("v1", gimmicky.Option);
        Assert.Equal(VoteInterest.ReasonLeaning(PoliticalLeaning.Traditionalist), traditionalist.ReasonKey);
        Assert.Equal(VoteInterest.ReasonLeaning(PoliticalLeaning.Gimmicky), gimmicky.ReasonKey);
        Assert.Equal(4, Enum.GetValues<PoliticalLeaning>().Select(leaning => VoteInterest.ReasonLeaning(leaning)).Distinct().Count());
    }

    [Fact]
    public void AnEgalitarianBacksWhatNarrowsTheGapAndAWeakTeamLikesItMoreThanAStrongOne()
    {
        var option = OptionOf("points_scale", "top5_8_6_4_3_2", "top10_25_18_15_12_10_8_6_4_2_1");
        var weak = new TeamView("t09", 0.0, 0.0, null);
        var strong = new TeamView("t01", 1.0, 0.0, null);

        var weakUtility = VoteInterest.Total(VoteInterest.Evaluate(weak, PoliticalLeaning.Egalitarian, option.DimensionId, option.Value, option.Delta, 0));
        var strongUtility = VoteInterest.Total(VoteInterest.Evaluate(strong, PoliticalLeaning.Egalitarian, option.DimensionId, option.Value, option.Delta, 0));

        Assert.True(weakUtility > strongUtility);
        Assert.Contains(VoteInterest.Evaluate(weak, PoliticalLeaning.Egalitarian, option.DimensionId, option.Value, option.Delta, 0), f => f.Reason == VoteInterest.ReasonPosition && f.Value > 0);
        Assert.Contains(VoteInterest.Evaluate(strong, PoliticalLeaning.Egalitarian, option.DimensionId, option.Value, option.Delta, 0), f => f.Reason == VoteInterest.ReasonPosition && f.Value < 0);
    }

    [Fact]
    public void AHomeRaceAtStakeMovesTheVoteOfTheTeamWhoseCountryItIs()
    {
        var drop = new RuleOption("calendar.circuit.monza", CalendarPolicy.Kept, CalendarPolicy.Dropped, CalendarPolicy.TraitsOf(CalendarPolicy.Dropped), -1);

        var home = VoteInterest.Total(VoteInterest.Evaluate(Middle, PoliticalLeaning.Progressive, drop.DimensionId, drop.Value, drop.Delta, drop.HomeEffect));
        var away = VoteInterest.Total(VoteInterest.Evaluate(Middle, PoliticalLeaning.Progressive, drop.DimensionId, drop.Value, drop.Delta, 0));

        Assert.True(home < away);
        var option = new CalendarPolicy.Option("calendar.circuit.monza", "monza", "ITA", true, CalendarPolicy.Kept, [CalendarPolicy.Dropped]);
        Assert.Equal(-1, CalendarPolicy.HomeEffect(option, CalendarPolicy.Dropped, "ITA"));
        Assert.Equal(0, CalendarPolicy.HomeEffect(option, CalendarPolicy.Dropped, "GBR"));
        Assert.Equal(0, CalendarPolicy.HomeEffect(option, CalendarPolicy.Dropped, null));
    }

    [Fact]
    public void TheVoteOfATeamIsAPureFunctionOfItsKnowledge()
    {
        var option = Knockout();

        var one = VoteOf(PoliticalLeaning.Gimmicky, option, noise: 0.05);
        var two = VoteOf(PoliticalLeaning.Gimmicky, option, noise: 0.05);
        Assert.Equal((one.Option, one.Spent, one.Banked, one.ReasonKey, one.Stake), (two.Option, two.Spent, two.Banked, two.ReasonKey, two.Stake));
        Assert.Equal(one.Factors, two.Factors);

        // What an AI team may know about itself is exactly this: nothing hidden, nothing about the future.
        Assert.Equal(
            ["CashStress", "Country", "Strength", "TeamId"],
            typeof(TeamView).GetProperties().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>A gimmicky team and a rule that moves only the show axis: the utility is linear in the show it adds, so it can be set exactly.</summary>
    private static RuleOption OptionWorth(double wantedUtility)
    {
        var zero = new RuleOption("qualifying_format", "a", "b", default, 0);
        var baseline = VoteInterest.Total(VoteInterest.Evaluate(Middle, PoliticalLeaning.Gimmicky, zero.DimensionId, zero.Value, zero.Delta, 0));
        return new RuleOption("qualifying_format", "a", "b", new ValueTraits(Show: (wantedUtility - baseline) / RegulationEstimates.GimmickyWeight), 0);
    }

    private static AiProposalDecision Consider(double wantedGain, long cash, long fee = 6_000_000, long revenue = 100_000_000)
    {
        var option = OptionWorth(wantedGain);
        var decision = AiPolitics.Consider(new AiProposalInputs(Middle, PoliticalLeaning.Gimmicky, fee, cash, revenue, [option]));
        Assert.Equal(wantedGain, decision.Gain, 6);
        return decision;
    }

    [Fact]
    public void AnAiTeamThatGainsMuchAndCanAffordTheFeeSpendsItsProposal()
    {
        var decision = Consider(1.4, cash: 50_000_000);

        Assert.True(decision.Propose);
        Assert.Equal(AiPolitics.ReasonBenefits, decision.ReasonKey);
        Assert.Equal(1.4 - decision.FeeBurden - decision.DebtPenalty - RegulationEstimates.CooldownCost, decision.Net, 9);
    }

    [Fact]
    public void AnAiTeamThatGainsLittleDoesNotSpendItsProposalOnIt()
    {
        var decision = Consider(0.5, cash: 50_000_000);

        Assert.False(decision.Propose);
        Assert.Equal(AiPolitics.ReasonTooExpensive, decision.ReasonKey);
    }

    [Fact]
    public void TheThreeSeasonWaitCountsAsACostEvenWhenTheFeeAloneWouldBeWorthIt()
    {
        var decision = Consider(0.6, cash: 50_000_000);

        Assert.True(decision.Gain - decision.FeeBurden > RegulationEstimates.MinimumNetGain, "worth the fee alone");
        Assert.False(decision.Propose, "but not worth the fee plus the wait");
        Assert.Equal(RegulationEstimates.CooldownCost, decision.CooldownCost);
    }

    [Fact]
    public void AnAiTeamInDeepDebtDoesNotSpendItsProposalWhateverItWouldGain()
    {
        var decision = Consider(1.4, cash: -100_000_000);

        Assert.False(decision.Propose);
        Assert.Equal(AiPolitics.ReasonInDebt, decision.ReasonKey);
        Assert.True(decision.DebtPenalty > decision.FeeBurden);

        var shallow = Consider(1.4, cash: 2_000_000);
        Assert.True(shallow.DebtPenalty > 0);
        Assert.True(shallow.DebtPenalty < decision.DebtPenalty);
    }

    [Fact]
    public void ARuleThatSuitsNoOneIsNeverAProposal()
    {
        var decision = AiPolitics.Consider(new AiProposalInputs(Middle, PoliticalLeaning.Traditionalist, 6_000_000, 50_000_000, 100_000_000, [Knockout()]));

        Assert.False(decision.Propose);
        Assert.Null(decision.Choice);
        Assert.Equal(AiPolitics.ReasonNothingWorthIt, decision.ReasonKey);
    }

    [Fact]
    public void WithOneVoteEachAnAiTeamAlwaysVotesAndNeverSpendsOrBanks()
    {
        foreach (var leaning in Enum.GetValues<PoliticalLeaning>())
        {
            foreach (var option in new[] { Knockout(), OptionOf("refuelling", "allowed", "banned") })
            {
                var vote = VoteOf(leaning, option, VoteMode.OneVoteEach, bank: 5);

                Assert.NotEqual(BallotOptions.Abstain, vote.Option);
                Assert.Equal(0, vote.Spent);
                Assert.False(vote.Banked);
            }
        }
    }

    [Fact]
    public void WithAVoteBankAnAiTeamBanksALowStakeVoteAndSpendsOnAHighStakeOneUpToTheBalance()
    {
        var trivial = OptionWorth(0.05);
        var banked = VoteOf(PoliticalLeaning.Gimmicky, trivial, VoteMode.VoteBank, bank: 0, team: Middle);
        Assert.Equal(BallotOptions.Abstain, banked.Option);
        Assert.True(banked.Banked);
        Assert.Equal(VoteInterest.ReasonBanked, banked.ReasonKey);

        var full = VoteOf(PoliticalLeaning.Gimmicky, trivial, VoteMode.VoteBank, bank: RegulationEstimates.BankCap);
        Assert.NotEqual(BallotOptions.Abstain, full.Option);
        Assert.False(full.Banked);

        var big = OptionWorth(1.2);
        var spends = VoteOf(PoliticalLeaning.Gimmicky, big, VoteMode.VoteBank, bank: 2);
        Assert.Equal("v1", spends.Option);
        Assert.Equal(2, spends.Spent); // capped by the balance
        var plenty = VoteOf(PoliticalLeaning.Gimmicky, big, VoteMode.VoteBank, bank: 5);
        Assert.Equal(RegulationEstimates.MaxSpendPerItem, plenty.Spent); // and by the most one item takes
        var none = VoteOf(PoliticalLeaning.Gimmicky, big, VoteMode.VoteBank, bank: 0);
        Assert.Equal(0, none.Spent);
        Assert.False(none.Banked);
    }

    [Fact]
    public void ABankCanNeverGoNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TeamPolitics("t01", PoliticalLeaning.Progressive, 1955, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TeamPolitics("t01", PoliticalLeaning.Progressive, 1955, 0).WithBank(-1));
    }
}
