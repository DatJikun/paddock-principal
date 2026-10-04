using System.Text.Json;
using Paddock.Data.Historical;
using Paddock.Data.World;
using Paddock.DataPipeline;
using Paddock.Domain.People;

namespace Paddock.Tests.World;

public class HistoricalRatingTableTests
{
    private static readonly string[] Keys = GenerationEstimates.DriverAttributeKeys;

    [Fact]
    public void AnUnratedDriverHasNoRating()
    {
        var table = Table(Driver("a", null, (1950, 50)));

        Assert.Null(table.RatingFor("nobody", 1950));
        Assert.Equal(1, table.Count);
    }

    [Theory]
    [InlineData(100, 20.0)]
    [InlineData(60, 12.0)]
    [InlineData(95, 19.0)]
    [InlineData(93, 18.6)]
    [InlineData(5, 1.0)]
    public void TheMeanAttributeIsTheSeasonLevel(int overall, double level)
    {
        var rating = Table(Driver("a", null, (1950, overall))).RatingFor("a", 1950)!;

        Assert.Equal(level, Mean(rating.Current), precision: 1);
    }

    [Fact]
    public void ARatedSeasonIsReadAsItIsAndGapSeasonsAreInterpolated()
    {
        var table = Table(Driver("a", null, (1950, 60), (1952, 80)));

        Assert.Equal(12.0, Mean(table.RatingFor("a", 1950)!.Current), precision: 1);
        Assert.Equal(14.0, Mean(table.RatingFor("a", 1951)!.Current), precision: 1);
        Assert.Equal(16.0, Mean(table.RatingFor("a", 1952)!.Current), precision: 1);
    }

    [Fact]
    public void WithoutAnArcTheLevelIsHeldBeforeTheFirstAndAfterTheLastSeason()
    {
        var table = Table(Driver("a", null, (1955, 70), (1956, 70)));

        Assert.Equal(14.0, Mean(table.RatingFor("a", 1950)!.Current), precision: 1);
        Assert.Equal(14.0, Mean(table.RatingFor("a", 1990)!.Current), precision: 1);
    }

    [Fact]
    public void BeforeTheDebutTheArcGrowsTheDriverTowardsTheFirstRatedLevel()
    {
        var arc = Arc(growthStartAge: 17, debutAge: 21, declineStartAge: 38, lastSeasonAge: 30);
        var table = Table(Driver("a", arc, (1955, 80), (1956, 80)));

        var early = Mean(table.RatingFor("a", 1951)!.Current);
        var late = Mean(table.RatingFor("a", 1954)!.Current);
        var debut = Mean(table.RatingFor("a", 1955)!.Current);

        Assert.Equal(WorldInitEstimates.RatedGrowthStartLevel, early, precision: 1);
        Assert.InRange(late, early, debut);
        Assert.Equal(16.0, debut, precision: 1);
        Assert.True(late > early);
    }

    [Fact]
    public void AfterTheLastSeasonTheLevelIsHeldUntilTheArcDeclineStartsAndThenFalls()
    {
        var arc = Arc(growthStartAge: 17, debutAge: 21, declineStartAge: 36, lastSeasonAge: 30, declineLevelsPerYear: 0.75);
        var table = Table(Driver("a", arc, (1950, 80), (1959, 80)));

        Assert.Equal(16.0, Mean(table.RatingFor("a", 1965)!.Current), precision: 1);
        Assert.Equal(16.0, Mean(table.RatingFor("a", 1965)!.Current), precision: 1);
        Assert.Equal(16.0 - 0.75 * 4, Mean(table.RatingFor("a", 1969)!.Current), precision: 1);
    }

    [Fact]
    public void ADriverWhoRetiredAtTheirPeakDoesNotDeclineBeforeTheArcSaysSo()
    {
        // The model sets the decline age to the last real age for such a driver (Fangio), so the fade starts after the career.
        var arc = Arc(growthStartAge: 25, debutAge: 38, declineStartAge: 46, lastSeasonAge: 46, retiredAtPeak: true);
        var table = Table(Driver("a", arc, (1950, 100), (1958, 100)));

        Assert.Equal(20.0, Mean(table.RatingFor("a", 1958)!.Current), precision: 1);
        Assert.True(Mean(table.RatingFor("a", 1960)!.Current) < 20.0);
    }

    [Fact]
    public void TheCeilingIsTheCareerPeakAndNeverBelowTheCurrentLevel()
    {
        var table = Table(Driver("a", Arc(peakLevel: 16.0), (1950, 60), (1951, 70), (1952, 80), (1953, 60)));

        var rating = table.RatingFor("a", 1950)!;

        Assert.Equal(16.0, Mean(rating.Potential), precision: 1);
        foreach (var key in Keys)
        {
            Assert.True(rating.Potential.Get(key) >= rating.Current.Get(key));
        }

        var older = table.RatingFor("a", 1953)!;
        Assert.True(Mean(older.Potential) >= Mean(older.Current));
    }

    [Fact]
    public void ArcPeakAboveTheRatedSeasonsRaisesTheCeiling()
    {
        var table = Table(Driver("a", Arc(peakLevel: 18.0), (1950, 60), (1951, 70)));

        Assert.Equal(18.0, Mean(table.RatingFor("a", 1950)!.Potential), precision: 1);
    }

    [Fact]
    public void AttributesStayInRangeAtBothEnds()
    {
        var table = Table(Driver("top", null, (1950, 100)), Driver("bottom", null, (1950, 1)));

        var top = table.RatingFor("top", 1950)!;
        var bottom = table.RatingFor("bottom", 1950)!;

        Assert.All(Keys, key => Assert.Equal(20, top.Current.Get(key)));
        Assert.All(Keys, key => Assert.Equal(1, bottom.Current.Get(key)));
    }

    [Fact]
    public void TheSameInputsAlwaysGiveTheSameRatingAndDriversDifferInWhichAttributesRoundUp()
    {
        var table = Table(Driver("a", null, (1950, 83)), Driver("b", null, (1950, 83)), Driver("c", null, (1950, 83)));

        Assert.Equal(table.RatingFor("a", 1950), table.RatingFor("a", 1950));
        var shapes = new[] { "a", "b", "c" }.Select(id => string.Join(",", Keys.Select(key => table.RatingFor(id, 1950)!.Current.Get(key)))).Distinct();
        Assert.True(shapes.Count() > 1);
    }

    [Fact]
    public void ThePipelineReportReadsBackIntoTheTable()
    {
        // The producer (DataPipeline) and the reader (Data) are separate types: this pins their JSON shape together.
        var entry = new DriverRatingEntry(
            1,
            "fangio",
            "Juan Fangio",
            99,
            4.9,
            0.99,
            2.0,
            null,
            [new SeasonRating(1950, 1.8, 98), new SeasonRating(1951, 1.9, 99)],
            new CareerArc(25, 38, 40, 19.8, 46, 6, 0.75, 46, true));
        var report = new RatingsReportDocument(
            new RatingsFitSummary(0, 0, 0, 0, 0, 0, true, 0, 0, 0, 1950, 1950, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, 0),
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [entry]);

        var json = JsonSerializer.Serialize(report, HistoricalJson.Options);
        var document = JsonSerializer.Deserialize<HistoricalRatingsDocument>(json, HistoricalJson.Options)!;
        var rating = new HistoricalRatingTable(document).RatingFor("fangio", 1950)!;

        Assert.Equal(19.6, Mean(rating.Current), precision: 1);
        Assert.Equal(19.8, Mean(rating.Potential), precision: 1);
    }

    private static double Mean(DriverAttributes attributes) => Keys.Average(key => (double)attributes.Get(key));

    private static HistoricalRatingTable Table(params HistoricalDriverRating[] drivers) => new(new HistoricalRatingsDocument(drivers));

    private static HistoricalDriverRating Driver(string id, HistoricalCareerArc? arc, params (int Season, int Overall)[] seasons) =>
        new(id, seasons.Select(s => new HistoricalSeasonRating(s.Season, s.Overall)).ToArray(), arc);

    private static HistoricalCareerArc Arc(
        int growthStartAge = 17,
        int debutAge = 21,
        int declineStartAge = 38,
        int lastSeasonAge = 30,
        double peakLevel = 16.0,
        double declineLevelsPerYear = 0.75,
        bool retiredAtPeak = false) =>
        new(growthStartAge, debutAge, 28, peakLevel, declineStartAge, declineStartAge - 28, declineLevelsPerYear, lastSeasonAge, retiredAtPeak);
}
