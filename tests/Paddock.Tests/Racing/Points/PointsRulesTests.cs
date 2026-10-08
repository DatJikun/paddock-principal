using Paddock.Simulation.Racing.Points;
using static Paddock.Tests.Racing.Points.PointsTestKit;

namespace Paddock.Tests.Racing.Points;

/// <summary>
/// The expected values below are read from data/authored/regulations/f1_timeline.json (and checked by the
/// first assertion of each case), not guessed.
/// </summary>
public class PointsRulesTests
{
    [Fact]
    public void Season1950_FiveScoringPlaces_SharedDrives_BestFour_ConstructorsClassificationStillRuns()
    {
        var rules = Rules(1950);

        Assert.Equal("top5_8_6_4_3_2", Real.RuleSetFor(1950).Value("points_scale"));
        Assert.Equal([8, 6, 4, 3, 2], rules.PositionPoints.ToArray());
        Assert.Equal(FastestLapRule.OnePointSharedIfTied, rules.FastestLap);
        Assert.Equal(SharedDriveRule.SharedEqually, rules.SharedDrive);
        // The data has no official title, but the game always classifies constructors (#264).
        Assert.Equal("no_championship", Real.RuleSetFor(1950).Value("constructors_points_counting"));
        Assert.Equal(ConstructorCounting.BestFinishingCarOnly, rules.ConstructorCounting);
        Assert.Equal(ResultsCountingRule.Best(4), rules.ResultsCounting);
        Assert.Equal(ClassificationRule.RunningAtFlag, rules.Classification);
        Assert.False(rules.DoublePointsFinale);
        Assert.False(rules.FastestLapCountsForConstructors);
    }

    [Fact]
    public void Season1960_SixPlacesEightForAWin_NoFastestLapPoint_BestSix_BestCarOnly()
    {
        var rules = Rules(1960);

        Assert.Equal("top6_8_6_4_3_2_1", Real.RuleSetFor(1960).Value("points_scale"));
        Assert.Equal([8, 6, 4, 3, 2, 1], rules.PositionPoints.ToArray());
        Assert.Equal(FastestLapRule.None, rules.FastestLap);
        Assert.Equal(SharedDriveRule.NotShared, rules.SharedDrive);
        Assert.Equal(ConstructorCounting.BestFinishingCarOnly, rules.ConstructorCounting);
        Assert.Equal(ResultsCountingRule.Best(6), rules.ResultsCounting);
    }

    [Fact]
    public void Season1961_NineForAWin()
    {
        Assert.Equal([9, 6, 4, 3, 2, 1], Rules(1961).PositionPoints.ToArray());
        Assert.Equal([9, 6, 4, 3, 2, 1], Rules(1990).PositionPoints.ToArray());
    }

    [Fact]
    public void Season1991_TenForAWin_EveryResultCounts_AllCarsScoreForConstructors()
    {
        var rules = Rules(1991);

        Assert.Equal([10, 6, 4, 3, 2, 1], rules.PositionPoints.ToArray());
        Assert.Equal(ResultsCountingRule.All, rules.ResultsCounting);
        Assert.Equal(ConstructorCounting.AllCars, rules.ConstructorCounting);
        Assert.Equal(ClassificationRule.RunningAtFlag, rules.Classification);
    }

    [Fact]
    public void Season2003_EightScoringPlaces_NinetyPercentRule()
    {
        var rules = Rules(2003);

        Assert.Equal([10, 8, 6, 5, 4, 3, 2, 1], rules.PositionPoints.ToArray());
        Assert.Equal(ClassificationRule.NinetyPercentOfWinnerLaps, rules.Classification);
    }

    [Fact]
    public void Season2010_TwentyFiveForAWin()
    {
        var rules = Rules(2010);

        Assert.Equal([25, 18, 15, 12, 10, 8, 6, 4, 2, 1], rules.PositionPoints.ToArray());
        Assert.Equal(FastestLapRule.None, rules.FastestLap);
    }

    [Fact]
    public void Season2014_DoublePointsFinale_Only()
    {
        Assert.False(Rules(2013).DoublePointsFinale);
        Assert.True(Rules(2014).DoublePointsFinale);
        Assert.False(Rules(2015).DoublePointsFinale);
    }

    [Fact]
    public void Season2019_FastestLapPointInTheTopTen()
    {
        var rules = Rules(2019);

        Assert.Equal(FastestLapRule.OnePointIfTopTen, rules.FastestLap);
        Assert.True(rules.FastestLapCountsForConstructors);
        Assert.Equal(FastestLapRule.OnePointIfTopTenAndHalfDistance, Rules(2022).FastestLap);
    }

    [Fact]
    public void Season2025_TwentyFiveScale_FastestLapPointAbolished()
    {
        var rules = Rules(2025);

        Assert.Equal(FastestLapRule.None, rules.FastestLap);
        Assert.Equal([25, 18, 15, 12, 10, 8, 6, 4, 2, 1], rules.PositionPoints.ToArray());
        Assert.Equal(ResultsCountingRule.All, rules.ResultsCounting);
    }

    [Theory]
    [InlineData(1950, 24)] // 23 plus the fastest-lap point of the era
    [InlineData(1960, 24)]
    [InlineData(1991, 26)]
    [InlineData(2003, 39)]
    [InlineData(2010, 101)]
    [InlineData(2019, 102)] // the scale's 101 plus the fastest-lap point
    [InlineData(2025, 101)]
    public void FullClassifiedGridPaysTheScaleTotalFromTheData(int season, int expectedTotal)
    {
        // Totals are the sums of the scales in the data: 8+6+4+3+2, 8+..+1, 10+6+4+3+2+1, 10+8+..+1, 25+18+..+1;
        // the car in second place sets the fastest lap, so eras that pay the point add one.
        var rules = Rules(season);
        var race = RaceClassifier.Classify(rules, Field(22, fastestLapIndex: 1), new RaceContext(50));

        Assert.Equal(expectedTotal, race.DriverScores.Sum(score => score.Points));
    }

    [Fact]
    public void EverySeasonOfTheCatalogBuildsRules()
    {
        for (var season = 1950; season <= 2026; season++)
        {
            var rules = Rules(season);
            Assert.Equal(season, rules.Season);
            Assert.True(rules.PositionPoints.Length > 0);
        }
    }

    [Fact]
    public void EveryResultsCountedValueOfTheCatalogParses()
    {
        var values = Real.Catalog.Single(entry => entry.Id == "results_counted").Values;

        Assert.NotNull(values);
        Assert.Equal(16, values.Count);
        foreach (var value in values)
        {
            var rule = ResultsCountingRule.Parse(value);
            Assert.Equal(value == "all" ? 0 : int.Parse(value.Split('_')[1], System.Globalization.CultureInfo.InvariantCulture), rule.TotalCounted);
        }
    }

    [Theory]
    [InlineData(1950, 9)] // 8 for a win plus the fastest-lap point
    [InlineData(1991, 10)]
    [InlineData(2019, 26)]
    [InlineData(2025, 25)]
    public void MaxDriverPointsPerRoundIsAWinPlusTheFastestLapPoint(int season, int expected) =>
        Assert.Equal(expected, Rules(season).MaxDriverPointsPerRound(isFinalRound: false));

    [Fact]
    public void MaxPointsPerRoundDoubleOnlyInTheDoublePointsFinale()
    {
        var rules = Rules(2014);

        Assert.Equal(25m, rules.MaxDriverPointsPerRound(isFinalRound: false));
        Assert.Equal(50m, rules.MaxDriverPointsPerRound(isFinalRound: true));
        Assert.Equal(43m, rules.MaxConstructorPointsPerRound(isFinalRound: false)); // 25 + 18, two cars
        Assert.Equal(86m, rules.MaxConstructorPointsPerRound(isFinalRound: true));
    }

    [Theory]
    [InlineData(1955, 8)] // no official title yet, but the game classifies constructors by the best car (#264)
    [InlineData(1970, 9)] // best car only
    [InlineData(1985, 15)] // two cars: 9 + 6
    public void MaxConstructorPointsPerRoundFollowsTheCountingRule(int season, int expected) =>
        Assert.Equal(expected, Rules(season).MaxConstructorPointsPerRound(isFinalRound: false));
}
