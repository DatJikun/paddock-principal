using System.Globalization;
using System.Text.Json;
using Paddock.Data.Authored;
using Paddock.Data.Historical;
using Paddock.DataPipeline;

namespace Paddock.Tests.DataPipeline;

/// <summary>
/// The starting-data step (#271) on a synthetic three-season fixture: every id, effect and result is invented for the test.
/// </summary>
public class StartingDataBuilderTests
{
    private const string Source = "https://example.com/fixture";

    [Fact]
    public void StrengthFollowsTheSeasonsCarEffectAndTheMeanCarGetsTheMeanStrength()
    {
        var report = Build();
        var season = Season(report, 1970);

        Assert.Equal(StartingStrengthSources.CarEffect, Constructor(season, "alpha").StrengthSource);
        Assert.True(Constructor(season, "alpha").Strength > Constructor(season, "bravo").Strength);
        Assert.True(Constructor(season, "bravo").Strength > Constructor(season, "charlie").Strength);

        // Effects 1, 0, -1: bravo is the mean, one standard deviation is sqrt(2/3).
        Assert.Equal(StartingDataEstimates.StrengthMean, Constructor(season, "bravo").Strength);
        var oneSd = StartingDataEstimates.StrengthPerSd / Math.Sqrt(2d / 3d);
        Assert.Equal(Math.Round(StartingDataEstimates.StrengthMean + oneSd, 1), Constructor(season, "alpha").Strength);
        Assert.Equal(1.0, Constructor(season, "alpha").CarEffect);
    }

    [Fact]
    public void ALineageSuccessorBorrowsTheLatestEarlierEffectAndANewTeamGetsTheNewEntrantStrength()
    {
        var report = Build();
        var season = Season(report, 1971);

        // bravo_new races in 1971 under the bravo lineage, which has no 1971 effect: it borrows bravo's 1970 effect.
        var successor = Constructor(season, "bravo_new");
        Assert.Equal(StartingStrengthSources.EarlierSeason, successor.StrengthSource);
        Assert.Equal(Constructor(Season(report, 1970), "bravo").Strength, successor.Strength);
        Assert.Null(successor.CarEffect);

        // delta is only in the engine book: it never raced, so it starts at the season's new-entrant strength.
        var newcomer = Constructor(season, "delta");
        Assert.Equal(StartingStrengthSources.NewEntrant, newcomer.StrengthSource);
        Assert.Equal(season.NewEntrantStrength, newcomer.Strength);
        Assert.Equal("Aurum", newcomer.EngineSupplier);
        Assert.True(newcomer.Strength < Constructor(season, "alpha").Strength);
    }

    [Fact]
    public void TheEngineIsTheFirstBookRowBySupplierAndAnUnknownSupplierIsLeftEmpty()
    {
        var season = Season(Build(), 1970);

        Assert.Equal("Aurum", Constructor(season, "alpha").EngineSupplier);
        Assert.Equal("Aurum V8", Constructor(season, "alpha").EngineName);
        Assert.Null(Constructor(season, "charlie").EngineSupplier);
    }

    [Fact]
    public void BeforeTheAllCarsRuleOnlyTheBestCarScoresAndATieGoesToTheBetterFinish()
    {
        HistoricalResult Row(int season, int round, string driver, string team, int position, decimal points) =>
            new(season, round, driver, team, "1", position, position.ToString(CultureInfo.InvariantCulture), "Finished", points, position, 50, null, null, true, false, false);

        var early = new List<HistoricalResult>
        {
            Row(1970, 1, "a1", "alpha", 2, 6),
            Row(1970, 1, "a2", "alpha", 3, 4),
            Row(1970, 1, "b1", "bravo", 1, 9),
            Row(1970, 1, "c1", "charlie", 4, 3),
            Row(1970, 2, "c1", "charlie", 1, 9),
            Row(1970, 2, "b1", "bravo", 5, 0),
        };

        // Best car only: alpha 6, bravo 9, charlie 12.
        Assert.Equal(["charlie", "bravo", "alpha"], StartingDataBuilder.Standings(early, 1970).Select(standing => standing.ConstructorId));

        // Every car scores: alpha 6 + 4 = 10 equals bravo 9 + 1 = 10, bravo's win breaks the tie.
        var late = new List<HistoricalResult>
        {
            Row(1979, 1, "a1", "alpha", 2, 6),
            Row(1979, 1, "a2", "alpha", 3, 4),
            Row(1979, 1, "b1", "bravo", 1, 9),
            Row(1979, 1, "b2", "bravo", 6, 1),
        };
        var standings = StartingDataBuilder.Standings(late, 1979);
        Assert.Equal(["bravo", "alpha"], standings.Select(standing => standing.ConstructorId));
        Assert.Equal([1, 2], standings.Select(standing => standing.Position));
        Assert.Equal(10m, standings[0].Points);
    }

    [Fact]
    public void TheIndianapolisRoundIsLeftOutOfTheOrder()
    {
        var season = Season(Build(), 1970);

        // echo only raced the Indianapolis round: it is not in the order, and not a constructor of the season either.
        Assert.DoesNotContain(season.Standings, standing => standing.ConstructorId == "echo");
        Assert.DoesNotContain(season.Constructors, constructor => constructor.ConstructorId == "echo");
        Assert.Equal("alpha", season.Standings[0].ConstructorId);
    }

    [Fact]
    public void TheReportIsDeterministicAndRoundTripsThroughTheLoader()
    {
        var first = JsonSerializer.Serialize(Build(), HistoricalJson.Options);
        var second = JsonSerializer.Serialize(Build(), HistoricalJson.Options);
        Assert.Equal(first, second);

        var parsed = StartingDataLoader.Parse(first);
        Assert.Equal(JsonSerializer.Serialize(parsed, HistoricalJson.Options), first);
        Assert.Equal([1970, 1971, 1972], parsed.Seasons.Select(season => season.Season));
        Assert.Contains("ESTIMATE", parsed.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void ASeasonWithNoCarEffectsUsesTheFallbackNewEntrantStrength()
    {
        var season = Season(Build(), 1972);

        Assert.Equal(StartingDataEstimates.NewEntrantFallback, season.NewEntrantStrength);
        Assert.All(season.Constructors, constructor => Assert.NotEqual(StartingStrengthSources.CarEffect, constructor.StrengthSource));
    }

    [Fact]
    public void TheCommandRefusesAMissingCache()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var empty = Directory.CreateTempSubdirectory("paddock-starting-data-").FullName;
        try
        {
            var code = StartingDataCommand.Execute(["starting-data", "--cache", Path.Combine(empty, "jolpica")], stdout, stderr);
            Assert.Equal(1, code);
            Assert.Contains("Run fetch and normalize first", stderr.ToString(), StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(empty, "reports")));
        }
        finally
        {
            Directory.Delete(empty, recursive: true);
        }
    }

    private static StartingDataReport Build()
    {
        var races = new List<HistoricalRace>
        {
            new(1970, 1, "GP", "c1", "1970-04-01", null, string.Empty, false),
            new(1970, 2, "Indy", "indianapolis", "1970-05-30", null, string.Empty, true),
            new(1971, 1, "GP", "c1", "1971-04-01", null, string.Empty, false),
            new(1972, 1, "GP", "c1", "1972-04-01", null, string.Empty, false),
        };

        HistoricalResult Row(int season, int round, string driver, string team, int position, decimal points) =>
            new(season, round, driver, team, "1", position, position.ToString(CultureInfo.InvariantCulture), "Finished", points, position, 50, null, null, true, false, false);

        var results = new List<HistoricalResult>
        {
            Row(1970, 1, "a1", "alpha", 1, 8),
            Row(1970, 1, "b1", "bravo", 2, 6),
            Row(1970, 1, "c1", "charlie", 3, 4),
            Row(1970, 2, "e1", "echo", 1, 8),
            Row(1971, 1, "a1", "alpha", 1, 9),
            Row(1971, 1, "b1", "bravo_new", 2, 6),
            Row(1972, 1, "a1", "alpha", 1, 9),
        };

        var effects = new List<CarEffect>
        {
            new("alpha", 1970, 1.0, 0.1),
            new("bravo-line", 1970, 0.0, 0.1),
            new("charlie", 1970, -1.0, 0.1),
            new("alpha", 1971, 0.5, 0.1),
            new("charlie", 1971, -0.5, 0.1),
        };

        var engines = new List<EngineEntry>
        {
            new("alpha", 1970, "Ferro", "Ferro 12", "customer", "high", Source),
            new("alpha", 1970, "Aurum", "Aurum V8", "works", "high", Source),
            new("charlie", 1970, "unknown", "unknown", "unknown", "low", Source),
            new("delta", 1971, "Aurum", "Aurum V8", "customer", "high", Source),
        };

        var lineage = new ConstructorLineageMap(
        [
            new Paddock.DataPipeline.LineageEntry("bravo-line", "bravo", 1960, 1970),
            new Paddock.DataPipeline.LineageEntry("bravo-line", "bravo_new", 1971, null),
        ]);

        return StartingDataBuilder.Build(races, results, effects, engines, lineage, 1970, 1972);
    }

    private static StartingSeason Season(StartingDataReport report, int year) => report.Seasons.Single(season => season.Season == year);

    private static StartingConstructor Constructor(StartingSeason season, string id) =>
        season.Constructors.Single(constructor => constructor.ConstructorId == id);
}
