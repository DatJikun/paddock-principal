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
    public void CareerArc_PeaksFromTheData_AndFadesOnlyAt36To40()
    {
        // Born 1950: debut at 22, best at 27, a relative dip late in the career (car or teammate, not age).
        int[] overall = [70, 78, 84, 88, 90, 90, 89, 85, 81, 77];
        var seasons = overall.Select((o, i) => new SeasonRating(1972 + i, 0.0, o)).ToList();

        var arc = RatingsCareerArc.Build(seasons, 1950, "someone");

        Assert.NotNull(arc);
        Assert.Equal(22, arc.DebutAge);
        Assert.Equal(22 - RatingsCareerArc.YearsOfGrowthBeforeDebut, arc.GrowthStartAge);
        Assert.Equal(26, arc.PeakAge);
        Assert.Equal(18.0, arc.PeakLevel);
        Assert.False(arc.RetiredAtPeak);
        Assert.InRange(arc.DeclineStartAge, RatingsCareerArc.DeclineStartMin, RatingsCareerArc.DeclineStartMax);
        Assert.Equal(arc.DeclineStartAge - arc.PeakAge, arc.PlateauYears);
        Assert.Equal(31, arc.LastSeasonAge);
    }

    [Fact]
    public void CareerArc_DeclineAgeIsStablePerDriver()
    {
        int[] overall = [80, 85, 90, 88, 86];
        var seasons = overall.Select((o, i) => new SeasonRating(2000 + i, 0.0, o)).ToList();

        var a = RatingsCareerArc.Build(seasons, 1978, "driver_a");
        var again = RatingsCareerArc.Build(seasons, 1978, "driver_a");

        Assert.Equal(a!.DeclineStartAge, again!.DeclineStartAge);
    }

    [Fact]
    public void CareerArc_EarlyPeakInTheData_IsMovedToTheUsualAge()
    {
        // Debut at 20 and best season at 21: the arc still peaks no earlier than 25.
        int[] overall = [85, 90, 88, 86, 84, 83];
        var seasons = overall.Select((o, i) => new SeasonRating(2000 + i, 0.0, o)).ToList();

        var arc = RatingsCareerArc.Build(seasons, 1980, "young");

        Assert.Equal(RatingsCareerArc.MinPeakAge, arc!.PeakAge);
    }

    [Fact]
    public void CareerArc_LateStarterStillAtPeak_FadesAfterHisLastSeason()
    {
        // Fangio-like: debut at 39, still at the top at 47.
        int[] overall = [94, 96, 98, 99, 99, 100, 100, 100];
        var seasons = overall.Select((o, i) => new SeasonRating(1950 + i, 0.0, o)).ToList();

        var arc = RatingsCareerArc.Build(seasons, 1911, "fangio_like");

        Assert.NotNull(arc);
        Assert.Equal(39, arc.PeakAge);
        Assert.True(arc.RetiredAtPeak);
        Assert.Equal(46, arc.DeclineStartAge);
        Assert.True(arc.GrowthStartAge < arc.PeakAge);
    }

    [Fact]
    public void CareerArc_NeedsABirthYearAndEnoughSeasons()
    {
        var four = Enumerable.Range(0, 4).Select(i => new SeasonRating(2000 + i, 0.0, 80)).ToList();
        Assert.Null(RatingsCareerArc.Build(four, null, "x"));
        Assert.Null(RatingsCareerArc.Build(four.Take(3).ToList(), 1980, "x"));
    }

    [Fact]
    public void Level_MapsTheFieldAverageTo12_AndStarsAreLevelOver4()
    {
        Assert.Equal(12.0, RatingsMapping.Level(0.0));
        Assert.Equal(1.0, RatingsMapping.Level(-5.0));
        Assert.Equal(5.0, RatingsMapping.StarsFromLevel(20.0));
        Assert.Equal(2.5, RatingsMapping.StarsFromLevel(10.0));
        Assert.Equal(4.7, RatingsMapping.StarsFromLevel(18.8));
        Assert.Equal(100, RatingsMapping.OverallFromLevel(20.0));
    }

    [Fact]
    public void Level_ApproachesTwentyWithoutFlatteningTheGreats()
    {
        double[] z = [0.5, 1.0, 1.5, 2.0, 2.5, 4.0];
        var levels = z.Select(RatingsMapping.Level).ToList();
        for (var i = 1; i < levels.Count; i++)
        {
            Assert.True(levels[i] > levels[i - 1], $"z={z[i]} level {levels[i]} not above {levels[i - 1]}");
        }

        Assert.True(levels[^1] < 20.0);
        Assert.InRange(RatingsMapping.Level(1.2), 18.5, 19.0);
    }

    [Fact]
    public void Shrink_PullsThinCareersTowardTheFieldMuchMoreThanLongOnes()
    {
        var thin = new FittedDriverMetrics("thin", 0.0, 0.0, string.Empty, 40, 20, 20, false, 1, [(1955, 2.0, 0.1)]);
        var thick = new FittedDriverMetrics("thick", 0.0, 0.0, string.Empty, 400, 200, 200, false, 1, [(1955, 2.0, 0.1)]);
        var shrunk = RatingsEraScale.Shrink(new Dictionary<string, FittedDriverMetrics> { ["thin"] = thin, ["thick"] = thick });

        var thinFactor = 40 / (40 + RatingsEraScale.ShrinkHalfDuels);
        var thickFactor = 400 / (400 + RatingsEraScale.ShrinkHalfDuels);
        Assert.Equal(2.0 * thinFactor, shrunk["thin"].Seasons.Single().Skill, 6);
        Assert.Equal(2.0 * thickFactor, shrunk["thick"].Seasons.Single().Skill, 6);
        Assert.True(thickFactor > 0.8 && thinFactor < 0.5);
    }
}
