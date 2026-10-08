using Paddock.Application.Regulation;
using Paddock.Domain.Career;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Simulation.Regulation;

namespace Paddock.Tests.Regulation;

/// <summary>The vote-bank mode (#275, PP-048): abstaining stores exactly one vote, spending is capped by the balance, and AI teams obey the same rules.</summary>
public class VoteBankTests
{
    private const string Series = PoliticsHarness.Series;

    /// <summary>Opens the first FIA item with every team a human, so that every vote is the test's.</summary>
    private static (PoliticsHarness Harness, BallotItem Item) FirstFiaItem(VoteMode mode)
    {
        var harness = PoliticsHarness.Create(new HarnessOptions { AllHuman = true, Mode = mode });
        harness.LiveTo(new GameDate(1955, 1, 2));
        harness.LiveToFia();
        return (harness, harness.Items().First(item => item.Origin == BallotOrigin.Fia));
    }

    [Fact]
    public void AbstainingBanksExactlyOneVoteAndOnlyInTheBankMode()
    {
        var (bank, bankItem) = FirstFiaItem(VoteMode.VoteBank);
        bank.Politics.Cast(Series, "t01", bankItem.Id, BallotOptions.Abstain, 0, bank.Today);
        bank.Politics.Cast(Series, "t02", bankItem.Id, BallotOptions.StatusQuo, 0, bank.Today);
        bank.LiveTo(bankItem.Deadline);

        Assert.Equal(1, bank.Of().TeamOf("t01")!.Bank);
        Assert.Equal(0, bank.Of().TeamOf("t02")!.Bank);
        Assert.True(bank.Of().ItemOf(bankItem.Id)!.Result!.Stances.Single(stance => stance.TeamId == "t01").Banked);

        var (plain, plainItem) = FirstFiaItem(VoteMode.OneVoteEach);
        plain.Politics.Cast(Series, "t01", plainItem.Id, BallotOptions.Abstain, 0, plain.Today);
        plain.LiveTo(plainItem.Deadline);
        Assert.Equal(0, plain.Of().TeamOf("t01")!.Bank);
        Assert.False(plain.Of().ItemOf(plainItem.Id)!.Result!.Stances.Single(stance => stance.TeamId == "t01").Banked);
    }

    [Fact]
    public void ATeamThatDoesNotVoteAtAllHoldsItsVoteBackLikeAnAbstention()
    {
        var (harness, item) = FirstFiaItem(VoteMode.VoteBank);

        harness.LiveTo(item.Deadline);

        Assert.All(harness.Teams, team => Assert.Equal(1, harness.Of().TeamOf(team)!.Bank));
    }

    [Fact]
    public void SpendingIsRefusedWithoutTheModeAndCappedOnlyByTheBalanceNotByTheItem()
    {
        var (plain, plainItem) = FirstFiaItem(VoteMode.OneVoteEach);
        Assert.Equal(RegulationKeys.SpendNeedsBank, plain.Politics.CheckCast(Series, "t01", plainItem.Id, "v1", 1, plain.Today)!.Key);

        var (harness, item) = FirstFiaItem(VoteMode.VoteBank);
        Assert.Equal(RegulationKeys.SpendOverBank, harness.Politics.CheckCast(Series, "t01", item.Id, "v1", 1, harness.Today)!.Key);
        Assert.Equal(RegulationKeys.SpendOnAbstain, harness.Politics.CheckCast(Series, "t01", item.Id, BallotOptions.Abstain, 1, harness.Today)!.Key);
        Assert.Equal(RegulationKeys.SpendNeedsBank, harness.Politics.CheckCast(Series, "t01", item.Id, "v1", -1, harness.Today)!.Key);

        // A bank of twelve (far above the old cap of five): all twelve can go on one item, a thirteenth cannot.
        SetBank(harness, "t01", 12);
        Assert.Null(harness.Politics.CheckCast(Series, "t01", item.Id, "v1", 12, harness.Today));
        Assert.Equal(RegulationKeys.SpendOverBank, harness.Politics.CheckCast(Series, "t01", item.Id, "v1", 13, harness.Today)!.Key);

        harness.Politics.Cast(Series, "t01", item.Id, "v1", 12, harness.Today);
        foreach (var other in harness.Teams.Skip(1))
        {
            harness.Politics.Cast(Series, other, item.Id, BallotOptions.StatusQuo, 0, harness.Today);
        }

        harness.LiveTo(item.Deadline);

        var result = harness.Of().ItemOf(item.Id)!.Result!;
        Assert.Equal(0, harness.Of().TeamOf("t01")!.Bank);
        Assert.Equal(13, result.Stances.Single(stance => stance.TeamId == "t01").Weight); // one vote and twelve spent
        Assert.Equal(13, result.Tally.Single(entry => entry.Option == "v1").Weight);
        Assert.Equal(9, result.Tally.Single(entry => entry.Option == BallotOptions.StatusQuo).Weight);
    }

    private static void SetBank(PoliticsHarness harness, string team, int bank)
    {
        var series = harness.Of();
        harness.SetSection(harness.Section.WithSeries(series.WithTeam(series.TeamOf(team)!.WithBank(bank))));
    }

    [Fact]
    public void VotesBankedForOneItemCannotBeSpentTwiceOnItemsOpenAtTheSameTime()
    {
        var harness = PoliticsHarness.Create(new HarnessOptions { AllHuman = true, Mode = VoteMode.VoteBank });
        harness.LiveFrom(new GameDate(1955, 1, 2), new GameDate(1955, 2, 10));
        var choices = harness.Politics.ChoicesFor(harness.Of(), 1955, new HashSet<string>(StringComparer.Ordinal));
        foreach (var (team, dimension) in new[] { ("t02", "safety_car"), ("t03", "refuelling") })
        {
            var choice = choices.First(c => c.DimensionId == dimension);
            harness.Politics.Propose(Series, team, choice.DimensionId, choice.Value, harness.Today);
        }

        harness.LiveToTeamBallot();
        var items = harness.Items().Where(item => item.Origin == BallotOrigin.Teams).OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
        Assert.Equal(2, items.Length);

        // Give t01 two banked votes, then spend both on the first item: nothing is left for the second.
        var section = harness.Section;
        var series = harness.Of();
        harness.SetSection(section.WithSeries(series.WithTeam(series.TeamOf("t01")!.WithBank(2))));
        harness.Politics.Cast(Series, "t01", items[0].Id, "v1", 2, harness.Today);

        Assert.Equal(RegulationKeys.SpendOverBank, harness.Politics.CheckCast(Series, "t01", items[1].Id, "v1", 1, harness.Today)!.Key);
        Assert.Null(harness.Politics.CheckCast(Series, "t01", items[1].Id, "v1", 0, harness.Today));
        // Changing the first vote frees the votes again.
        harness.Politics.Cast(Series, "t01", items[0].Id, "v1", 0, harness.Today);
        Assert.Null(harness.Politics.CheckCast(Series, "t01", items[1].Id, "v1", 2, harness.Today));
    }

    [Fact]
    public void TheBankHasNoLimitAndAnAbstentionIsNeverRefusedOrLost()
    {
        var (harness, item) = FirstFiaItem(VoteMode.VoteBank);
        SetBank(harness, "t01", 500);

        Assert.Null(harness.Politics.CheckCast(Series, "t01", item.Id, BallotOptions.Abstain, 0, harness.Today));
        harness.Politics.Cast(Series, "t01", item.Id, BallotOptions.Abstain, 0, harness.Today);
        harness.LiveTo(item.Deadline);

        Assert.Equal(501, harness.Of().TeamOf("t01")!.Bank);
    }

    [Fact]
    public void AHumanWhoNeverVotesKeepsEveryVoteOverSeasons()
    {
        var harness = PoliticsHarness.Create(new HarnessOptions { Mode = VoteMode.VoteBank, Humans = ["t01"], Seed = 5 });
        var held = 0;
        for (var year = 1955; year <= 1957; year++)
        {
            harness.LiveTo(new GameDate(year, 12, 31));
            held += harness.Items().Count(item => item.Season == year && item.IsResolved);
        }

        Assert.True(held > 10, "three seasons bring more than ten ballots");
        Assert.Equal(held, harness.Of().TeamOf("t01")!.Bank);
        Assert.True(harness.Of().TeamOf("t01")!.Bank > 5, "the old cap of five is gone");
    }

    [Fact]
    public void ABankIsNeverNegativeAndTheAiTeamsFollowTheSameRulesAndDoNotHoardForEverOverManySeasons()
    {
        foreach (var seed in new ulong[] { 1, 2, 3 })
        {
            var harness = PoliticsHarness.Create(new HarnessOptions { Seed = seed, Mode = VoteMode.VoteBank });
            var banked = false;
            var spent = false;
            var largest = 0;
            for (var day = new GameDate(1955, 1, 2); day <= new GameDate(1964, 12, 31); day = day.AddDays(1))
            {
                harness.LiveTo(day);
                foreach (var team in harness.Of().Teams)
                {
                    Assert.True(team.Bank >= 0);
                    banked |= team.Bank > 0;
                    if (!harness.Environment.IsHuman(PoliticsHarness.Org(team.TeamId)))
                    {
                        largest = Math.Max(largest, team.Bank);
                    }
                }
            }

            foreach (var item in harness.Items().Where(i => i.IsResolved))
            {
                spent |= item.Result!.Stances.Any(stance => stance.Spent > 0);
                Assert.All(item.Result.Stances, stance => Assert.True(stance.Weight >= 0));
            }

            Assert.True(banked, "AI teams bank votes on ballots that matter little");
            Assert.True(spent, "AI teams spend banked votes: they do not hoard for ever (seed " + seed + ")");
            Assert.True(largest <= RegulationEstimates.AiBankReserve + 8, "an AI bank stays modest (largest " + largest + ", seed " + seed + ")");
        }
    }
}
