using Paddock.Domain.Racing;
using Paddock.Domain.Time;

namespace Paddock.Tests.Regulation;

/// <summary>Each racing series votes separately (#275, owner decision 5): a result in one does not change the other.</summary>
public class TwoSeriesTests
{
    private static PoliticsHarness Create() => PoliticsHarness.Create(new HarnessOptions { AllHuman = true, Directory = new TwoSeries() });

    [Fact]
    public void BothSeriesAreKeptSeparatelyWithTheirOwnTeamsRulesAndCooldowns()
    {
        var harness = Create();
        var first = harness.Of(TwoSeries.First);
        var second = harness.Of(TwoSeries.Second);

        Assert.Equal(["t01", "t02", "t03", "t04", "t05"], first.Teams.Select(team => team.TeamId));
        Assert.Equal(["t06", "t07", "t08", "t09", "t10"], second.Teams.Select(team => team.TeamId));
        Assert.Equal(first.Values, second.Values);
        Assert.Equal(["f1", "f2"], harness.Section.Series.Select(series => series.SeriesId));
    }

    [Fact]
    public void AResultInOneSeriesDoesNotChangeTheOther()
    {
        var harness = Create();
        harness.LiveFrom(new GameDate(1955, 1, 2), new GameDate(1955, 2, 10));
        var choice = harness.Politics.ChoicesFor(harness.Of(TwoSeries.First), 1955, new HashSet<string>(StringComparer.Ordinal))
            .First(c => c.DimensionId == "points_scale");
        harness.Politics.Propose(TwoSeries.First, "t01", choice.DimensionId, choice.Value, harness.Today);
        var other = harness.Politics.ChoicesFor(harness.Of(TwoSeries.Second), 1955, new HashSet<string>(StringComparer.Ordinal))
            .First(c => c.DimensionId == "safety_car");
        harness.Politics.Propose(TwoSeries.Second, "t06", other.DimensionId, other.Value, harness.Today);

        harness.LiveTo(new GameDate(1955, 5, 1));
        var firstItem = Assert.Single(harness.Items(TwoSeries.First), item => item.Origin == BallotOrigin.Teams);
        var secondItem = Assert.Single(harness.Items(TwoSeries.Second), item => item.Origin == BallotOrigin.Teams);
        Assert.Equal("points_scale", firstItem.DimensionId);
        Assert.Equal("safety_car", secondItem.DimensionId);
        Assert.StartsWith("f1:", firstItem.Id, StringComparison.Ordinal);
        Assert.StartsWith("f2:", secondItem.Id, StringComparison.Ordinal);

        // Only the first series votes the change in; the second keeps its rule.
        foreach (var team in harness.Of(TwoSeries.First).Teams.Select(t => t.TeamId))
        {
            harness.Politics.Cast(TwoSeries.First, team, firstItem.Id, "v1", 0, harness.Today);
        }

        // A team of one series cannot vote in the other.
        Assert.Equal(
            Paddock.Application.Regulation.RegulationKeys.UnknownTeam,
            harness.Politics.CheckCast(TwoSeries.First, "t06", firstItem.Id, "v1", 0, harness.Today)!.Key);
        Assert.Equal(
            Paddock.Application.Regulation.RegulationKeys.UnknownItem,
            harness.Politics.CheckCast(TwoSeries.Second, "t06", firstItem.Id, "v1", 0, harness.Today)!.Key);

        harness.LiveTo(firstItem.Deadline);

        Assert.Equal(BallotOutcome.Adopted, harness.Of(TwoSeries.First).ItemOf(firstItem.Id)!.Result!.Outcome);
        Assert.Equal(choice.Value, harness.Section.RuleSetFor(TwoSeries.First, 1956)!.Value("points_scale"));
        Assert.Equal(BallotOutcome.NoVotes, harness.Of(TwoSeries.Second).ItemOf(secondItem.Id)!.Result!.Outcome);
        Assert.Equal(
            PoliticsHarness.Data.RuleSetFor(1955).Value("points_scale"),
            harness.Section.RuleSetFor(TwoSeries.Second, 1956)!.Value("points_scale"));
        Assert.Equal(harness.Of(TwoSeries.Second).Values, harness.Of(TwoSeries.Second).NextValues ?? harness.Of(TwoSeries.Second).Values);

        // The cooldown of the first series' team is not the second series' team's.
        Assert.Equal(1958, harness.Of(TwoSeries.First).TeamOf("t01")!.ProposeFromSeason);
        Assert.Equal(1958, harness.Of(TwoSeries.Second).TeamOf("t06")!.ProposeFromSeason);
        Assert.Equal(1955, harness.Of(TwoSeries.Second).TeamOf("t07")!.ProposeFromSeason);
        Assert.Equal(1955, harness.Of(TwoSeries.First).TeamOf("t02")!.ProposeFromSeason);
    }

    [Fact]
    public void EachSeriesHasItsOwnFourToSixFiaVotes()
    {
        var harness = Create();
        harness.LiveTo(new GameDate(1955, 12, 31));

        foreach (var series in new[] { TwoSeries.First, TwoSeries.Second })
        {
            var fia = harness.Items(series).Count(item => item.Origin == BallotOrigin.Fia);
            Assert.InRange(fia, 4, 6);
        }

        // The two series draw their counts and proposals independently.
        Assert.NotEqual(
            string.Join(",", harness.Items(TwoSeries.First).Select(item => item.DimensionId + item.Variants[0].Value)),
            string.Join(",", harness.Items(TwoSeries.Second).Select(item => item.DimensionId + item.Variants[0].Value)));
    }
}
