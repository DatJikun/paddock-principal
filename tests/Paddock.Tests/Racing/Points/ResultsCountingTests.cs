using Paddock.Simulation.Racing.Points;
using static Paddock.Tests.Racing.Points.PointsTestKit;

namespace Paddock.Tests.Racing.Points;

public class ResultsCountingTests
{
    [Theory]
    [InlineData("all", ResultsCountingKind.All, 0, 0, 0, null)]
    [InlineData("best_4", ResultsCountingKind.BestOverall, 4, 0, 0, null)]
    [InlineData("best_11", ResultsCountingKind.BestOverall, 11, 0, 0, null)]
    [InlineData("best_9_split_5_and_4", ResultsCountingKind.Split, 9, 5, 4, null)]
    [InlineData("best_15_split_8_and_7", ResultsCountingKind.Split, 15, 8, 7, null)]
    [InlineData("best_8_split_4_and_4", ResultsCountingKind.Split, 8, 4, 4, null)]
    [InlineData("best_10_split_5_and_5_of_6", ResultsCountingKind.Split, 10, 5, 5, 6)]
    [InlineData("best_10_split_5_and_5_of_7", ResultsCountingKind.Split, 10, 5, 5, 7)]
    public void ParsesTheCatalogValues(string value, ResultsCountingKind kind, int total, int first, int second, int? block)
    {
        var rule = ResultsCountingRule.Parse(value);

        Assert.Equal(kind, rule.Kind);
        Assert.Equal(total, rule.TotalCounted);
        Assert.Equal(first, rule.FirstQuota);
        Assert.Equal(second, rule.SecondQuota);
        Assert.Equal(block, rule.FirstBlockRounds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("best")]
    [InlineData("best_x")]
    [InlineData("best_9_split_5_and_5")] // 5 + 5 is not 9
    [InlineData("top_3")]
    public void RejectsUnknownOrInconsistentValues(string value)
    {
        Assert.ThrowsAny<Exception>(() => ResultsCountingRule.Parse(value));
    }

    [Fact]
    public void AllCountsEverything()
    {
        Assert.Equal(25m, ResultsCountingRule.All.Counted([9m, 0m, 6m, 4m, 6m], 5));
    }

    [Fact]
    public void BestN_DropsTheWorstResults()
    {
        // 1950 style: best four of six.
        Assert.Equal(9m + 9m + 6m + 4m, ResultsCountingRule.Parse("best_4").Counted([9m, 1m, 6m, 4m, 2m, 9m], 6));
    }

    [Fact]
    public void BestN_CountsEverythingWhileFewerThanNResultsExist()
    {
        Assert.Equal(7m, ResultsCountingRule.Best(5).Counted([4m, 3m], 8));
    }

    [Fact]
    public void Split_TakesTheBestOfEachBlock_NotTheBestOverall()
    {
        // 1967 style, 11 races: best 5 of the first 6 and best 4 of the last 5.
        var rule = ResultsCountingRule.Parse("best_9_split_5_and_4");
        decimal[] first = [9, 9, 9, 9, 9, 9]; // 5 count: 45
        decimal[] last = [1, 1, 1, 1, 1]; // 4 count: 4; the sixth 9 may not be swapped in
        Assert.Equal(49m, rule.Counted([.. first, .. last], 11));
        Assert.Equal(6, rule.FirstBlockSize(11));
    }

    [Fact]
    public void Split_PartialSeasonUsesTheBlockBoundaryOfTheWholeSeason()
    {
        // Eight rounds played of 16, 7 + 7: all eight are in the first block, so only seven of them count.
        var rule = ResultsCountingRule.Parse("best_14_split_7_and_7");

        Assert.Equal(7 * 9m, rule.Counted([9m, 9m, 9m, 9m, 9m, 9m, 9m, 9m], 16));
        // Nine played: the ninth is the first of the second block and counts in full.
        Assert.Equal(7 * 9m + 4m, rule.Counted([9m, 9m, 9m, 9m, 9m, 9m, 9m, 9m, 4m], 16));
    }

    // Season sizes are the ones the timeline notes describe ("five from the first six races and four from the last five", ...).
    [Theory]
    [InlineData(1967, 11, 6)]
    [InlineData(1968, 12, 6)]
    [InlineData(1969, 11, 6)]
    [InlineData(1970, 13, 7)]
    [InlineData(1971, 11, 6)]
    [InlineData(1972, 12, 6)]
    [InlineData(1973, 15, 8)]
    [InlineData(1974, 15, 8)]
    [InlineData(1975, 14, 7)]
    [InlineData(1976, 16, 8)]
    [InlineData(1977, 17, 9)]
    [InlineData(1978, 16, 8)]
    [InlineData(1980, 14, 7)]
    public void FirstBlockMatchesTheSplitsTheTimelineDescribes(int season, int rounds, int expectedFirstBlock)
    {
        var rule = Rules(season).ResultsCounting;

        Assert.Equal(ResultsCountingKind.Split, rule.Kind);
        Assert.Equal(expectedFirstBlock, rule.FirstBlockSize(rounds));
    }

    [Fact]
    public void Season1979_FifteenRounds_IsAnEstimatedSevenPlusEight()
    {
        // ESTIMATE, not in the data: the first block of an even split is floor(rounds / 2).
        var rule = Rules(1979).ResultsCounting;

        Assert.Equal("best_8_split_4_and_4", Real.RuleSetFor(1979).Value("results_counted"));
        Assert.Equal(7, rule.FirstBlockSize(15));
    }

    [Fact]
    public void ABlockRuleHasNoBlocksToAskAbout()
    {
        Assert.Throws<InvalidOperationException>(() => ResultsCountingRule.All.FirstBlockSize(10));
    }
}
