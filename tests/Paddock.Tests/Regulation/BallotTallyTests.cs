using Paddock.Domain.Racing;
using Paddock.Simulation.Regulation;

namespace Paddock.Tests.Regulation;

/// <summary>The count of one ballot item (#275): who leads, when the FIA president decides and when he does not.</summary>
public class BallotTallyTests
{
    private static VoterBallot Vote(string team, string option, int weight = 1, int spent = 0, bool banked = false) =>
        new(team, option, weight, spent, banked, "test.reason");

    private static IReadOnlyList<VoterBallot> Votes(params string[] options) =>
        [.. options.Select((option, index) => Vote("t" + (index + 1).ToString("00"), option))];

    private static string NeverCalled(IReadOnlyList<string> tied) =>
        throw new InvalidOperationException("The president was asked to decide a vote that was not tied: " + string.Join(",", tied));

    [Fact]
    public void ATiedTallyIsDecidedByThePresidentAndOnlyATiedOne()
    {
        string[] tiedOptions = [];
        var tied = BallotTally.Resolve(
            ["v1"],
            Votes("v1", "v1", BallotOptions.StatusQuo, BallotOptions.StatusQuo),
            VoteThreshold.SimpleMajority,
            options =>
            {
                tiedOptions = [.. options];
                return "v1";
            });

        Assert.True(tied.PresidentDecided);
        Assert.Equal([BallotOptions.StatusQuo, "v1"], tiedOptions);
        Assert.Equal(BallotOutcome.Adopted, tied.Outcome);
        Assert.Equal("v1", tied.WinningOption);
        Assert.Equal(BallotTally.ReasonAdoptedByPresident, tied.ReasonKey);
        Assert.Equal(3, tied.Tally.Single(entry => entry.Option == "v1").Weight);

        var kept = BallotTally.Resolve(["v1"], Votes("v1", BallotOptions.StatusQuo), VoteThreshold.SimpleMajority, _ => BallotOptions.StatusQuo);
        Assert.True(kept.PresidentDecided);
        Assert.Equal(BallotOutcome.Rejected, kept.Outcome);
        Assert.Equal(BallotTally.ReasonPresidentKeptStatusQuo, kept.ReasonKey);

        // Not tied: the president is never asked, whatever he would have wanted.
        var clear = BallotTally.Resolve(["v1"], Votes("v1", "v1", "v1", BallotOptions.StatusQuo), VoteThreshold.SimpleMajority, NeverCalled);
        Assert.False(clear.PresidentDecided);
        Assert.Equal(BallotOutcome.Adopted, clear.Outcome);
        Assert.Equal(BallotTally.ReasonAdopted, clear.ReasonKey);
    }

    [Fact]
    public void ThePresidentAddsOneVoteToTheOptionHePicksAndNothingElse()
    {
        var result = BallotTally.Resolve(
            ["v1", "v2"],
            Votes("v1", "v2", BallotOptions.StatusQuo),
            VoteThreshold.SimpleMajority,
            _ => "v2");

        Assert.Equal(
            new Dictionary<string, int> { [BallotOptions.StatusQuo] = 1, ["v1"] = 1, ["v2"] = 2 },
            result.Tally.ToDictionary(entry => entry.Option, entry => entry.Weight));
        Assert.Equal("v2", result.WinningOption);
    }

    [Fact]
    public void ALeadingStatusQuoChangesNothingAndAClearLeaderMustReachTheThreshold()
    {
        var statusQuo = BallotTally.Resolve(["v1"], Votes("v1", BallotOptions.StatusQuo, BallotOptions.StatusQuo), VoteThreshold.SimpleMajority, NeverCalled);
        Assert.Equal(BallotOutcome.Rejected, statusQuo.Outcome);
        Assert.Equal(BallotTally.ReasonStatusQuoLeads, statusQuo.ReasonKey);

        // Two thirds of the cast weight: 5 of 8 is a clear lead but not enough.
        var short5of8 = BallotTally.Resolve(
            ["v1"],
            Votes("v1", "v1", "v1", "v1", "v1", BallotOptions.StatusQuo, BallotOptions.StatusQuo, BallotOptions.StatusQuo),
            VoteThreshold.TwoThirds,
            NeverCalled);
        Assert.Equal(BallotOutcome.Rejected, short5of8.Outcome);
        Assert.Equal(BallotTally.ReasonBelowThreshold, short5of8.ReasonKey);
        Assert.Equal("v1", short5of8.WinningOption);

        var enough = BallotTally.Resolve(
            ["v1"],
            Votes("v1", "v1", "v1", "v1", "v1", "v1", BallotOptions.StatusQuo, BallotOptions.StatusQuo),
            VoteThreshold.TwoThirds,
            NeverCalled);
        Assert.Equal(BallotOutcome.Adopted, enough.Outcome);
    }

    [Fact]
    public void VariantsAreTalliedByWeightAndTheMostVotesWins()
    {
        var result = BallotTally.Resolve(
            ["v1", "v2", "v3"],
            Votes("v1", "v2", "v2", "v2", "v3", BallotOptions.StatusQuo),
            VoteThreshold.SimpleMajority,
            NeverCalled);

        Assert.Equal("v2", result.WinningOption);
        Assert.Equal(3, result.Tally.Single(entry => entry.Option == "v2").Weight);
        Assert.Equal(BallotOutcome.Rejected, result.Outcome); // 3 of 6 is not more than half
        Assert.Equal(6, result.Stances.Count);
    }

    [Fact]
    public void AbstainingCountsForNothingAndSpentVotesAddWeight()
    {
        var result = BallotTally.Resolve(
            ["v1"],
            [
                Vote("t01", "v1", spent: 2),
                Vote("t02", BallotOptions.StatusQuo),
                Vote("t03", BallotOptions.Abstain, banked: true),
                Vote("t04", BallotOptions.Abstain),
            ],
            VoteThreshold.SimpleMajority,
            NeverCalled);

        Assert.Equal(3, result.Tally.Single(entry => entry.Option == "v1").Weight);
        Assert.Equal(1, result.Tally.Single(entry => entry.Option == BallotOptions.StatusQuo).Weight);
        Assert.Equal(BallotOutcome.Adopted, result.Outcome);
        Assert.True(result.Stances.Single(stance => stance.TeamId == "t03").Banked);
        Assert.Equal(0, result.Stances.Single(stance => stance.TeamId == "t03").Weight);
        Assert.Equal(3, result.Stances.Single(stance => stance.TeamId == "t01").Weight);
    }

    [Fact]
    public void ABallotNobodyVotedOnChangesNothingAndSaysSo()
    {
        var result = BallotTally.Resolve(
            ["v1"],
            [Vote("t01", BallotOptions.Abstain), Vote("t02", BallotOptions.Abstain)],
            VoteThreshold.SimpleMajority,
            NeverCalled);

        Assert.Equal(BallotOutcome.NoVotes, result.Outcome);
        Assert.Equal(BallotTally.ReasonNoVotes, result.ReasonKey);
        Assert.False(result.PresidentDecided);
    }

    [Fact]
    public void AnOptionThatIsNotOnTheBallotIsRefused()
    {
        Assert.Throws<ArgumentException>(() =>
            BallotTally.Resolve(["v1"], [Vote("t01", "v9")], VoteThreshold.SimpleMajority, NeverCalled));
    }

    [Fact]
    public void ThePresidentBacksTheFiasOwnAgendaAndKeepsARuleAgainstAProposalOfTheTeams()
    {
        var modern = new ValueTraits(Modern: 1);
        Assert.Equal("v1", FiaPresident.Choose(BallotOrigin.Fia, [BallotOptions.StatusQuo, "v1"], _ => modern));
        Assert.Equal(BallotOptions.StatusQuo, FiaPresident.Choose(BallotOrigin.Teams, [BallotOptions.StatusQuo, "v1"], _ => default));
        Assert.Equal("v1", FiaPresident.Choose(BallotOrigin.Teams, [BallotOptions.StatusQuo, "v1"], _ => new ValueTraits(Modern: 1.5)));
    }
}
