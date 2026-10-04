using Paddock.DataPipeline;

namespace Paddock.Tests.DataPipeline;

public class RatingsEraTests
{
    private static FittedDriverMetrics Driver(string id, params (int Season, double Skill)[] seasons)
    {
        var list = seasons.Select(s => (s.Season, s.Skill, 0.1)).ToList();
        return new FittedDriverMetrics(id, 0.0, 0.0, string.Empty, 40, 20, 20, false, list.Count, list);
    }

    // Two eras, 20 years apart, so the ±1 window never pools them.
    private static Dictionary<string, FittedDriverMetrics> TwoEras()
    {
        var drivers = new Dictionary<string, FittedDriverMetrics>(StringComparer.Ordinal);

        // 1955: wide field (spread 1.0 per step), the star is 3 steps above the middle.
        drivers["star55"] = Driver("star55", (1955, 3.0));
        for (var i = 0; i < 9; i++)
        {
            drivers[$"f55_{i}"] = Driver($"f55_{i}", (1955, i - 4.0));
        }

        // 1975: the same field shape, ten times narrower and shifted up.
        drivers["star75"] = Driver("star75", (1975, 10.3));
        for (var i = 0; i < 9; i++)
        {
            drivers[$"f75_{i}"] = Driver($"f75_{i}", (1975, 10.0 + (i - 4.0) / 10.0));
        }

        return drivers;
    }

    [Fact]
    public void EraScale_PutsTheStarOfANarrowEraLevelWithTheStarOfAWideEra()
    {
        var drivers = TwoEras();
        var fields = RatingsEraScale.Fields(drivers.Values);
        var relative = RatingsEraScale.ToRelative(drivers, fields);

        var z55 = relative["star55"].Seasons.Single().Skill;
        var z75 = relative["star75"].Seasons.Single().Skill;
        Assert.True(z55 > 0.9, $"1955 star z={z55}");
        Assert.True(Math.Abs(z55 - z75) < 0.25, $"z55={z55}, z75={z75}");
    }

    [Fact]
    public void EraScale_KeepsDominance_ADriverFurtherAboveHisFieldScoresHigher()
    {
        var drivers = TwoEras();
        drivers["star75"] = Driver("star75", (1975, 10.6));
        var relative = RatingsEraScale.ToRelative(drivers, RatingsEraScale.Fields(drivers.Values));

        Assert.True(relative["star75"].Seasons.Single().Skill > relative["star55"].Seasons.Single().Skill);
    }

    [Fact]
    public void EraScale_SdNeverFallsBelowTheFloor()
    {
        var drivers = new Dictionary<string, FittedDriverMetrics>(StringComparer.Ordinal);
        for (var i = 0; i < 10; i++)
        {
            drivers[$"d{i}"] = Driver($"d{i}", (1990, 1.0));
        }

        var field = RatingsEraScale.Fields(drivers.Values)[1990];
        Assert.Equal(RatingsEraScale.MinSd, field.Sd);
    }

    [Fact]
    public void CareerArc_ReadsGrowthPeakPlateauAndDecline()
    {
        // Born 1950: debut at 22, rises to 90 at 27, holds two years, then loses 4 points a year.
        int[] overall = [70, 78, 84, 88, 90, 90, 89, 85, 81, 77];
        var seasons = overall.Select((o, i) => new SeasonRating(1972 + i, 0.0, o)).ToList();

        var arc = RatingsCareerArc.Build(seasons, 1950);

        Assert.NotNull(arc);
        Assert.Equal(22, arc.DebutAge);
        Assert.Equal(22 - RatingsCareerArc.YearsOfGrowthBeforeDebut, arc.GrowthStartAge);
        Assert.Equal(26, arc.PeakAge);
        Assert.Equal(90, arc.PeakOverall);
        Assert.Equal(2, arc.PlateauYears);
        Assert.False(arc.DeclineIsEstimate);
        Assert.Equal(4000, arc.DeclineMilliPerYear);
        Assert.Equal(31, arc.LastSeasonAge);
    }

    [Fact]
    public void CareerArc_EndingAtThePeak_UsesTheDefaultDecline()
    {
        int[] overall = [80, 85, 90, 95];
        var seasons = overall.Select((o, i) => new SeasonRating(2015 + i, 0.0, o)).ToList();

        var arc = RatingsCareerArc.Build(seasons, 1997);

        Assert.NotNull(arc);
        Assert.True(arc.DeclineIsEstimate);
        Assert.Equal(RatingsCareerArc.DefaultDeclineMilliPerYear, arc.DeclineMilliPerYear);
        Assert.Equal(0, arc.PlateauYears);
    }

    [Fact]
    public void CareerArc_NeedsABirthYearAndEnoughSeasons()
    {
        var four = Enumerable.Range(0, 4).Select(i => new SeasonRating(2000 + i, 0.0, 80)).ToList();
        Assert.Null(RatingsCareerArc.Build(four, null));
        Assert.Null(RatingsCareerArc.Build(four.Take(3).ToList(), 1980));
    }
}
