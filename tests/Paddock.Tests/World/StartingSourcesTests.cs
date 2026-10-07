using System.Text.Json;
using Paddock.Data.Authored;
using Paddock.Data.Historical;
using Paddock.Data.World;
using Paddock.Domain.Cars;
using Paddock.Domain.Career;
using Paddock.Domain.Finance;
using Paddock.Domain.World;

namespace Paddock.Tests.World;

/// <summary>
/// The starting data of a career start (#271): the authored files win, only the start season is read, and the world initializer
/// builds its cars from the layered strengths. Every id and number is a fixture.
/// </summary>
public class StartingSourcesTests
{
    private const int Year = WorldInitFixtures.Year;

    [Fact]
    public void AnAuthoredStrengthWinsAndTheGeneratedOneFillsTheRest()
    {
        var authored = new MapCarStrengthSource([("bravo", Year, 55d, 40d)]);
        var sources = StartingSources.For(Year, Report(), authored, null);

        Assert.True(sources.FromGeneratedData);
        Assert.True(sources.CarStrength!.TryGet("bravo", Year, out var bravo));
        Assert.Equal(55, bravo);
        Assert.True(sources.CarStrength.TryGetReliability("bravo", Year, out var fragile));
        Assert.Equal(40, fragile);

        Assert.True(sources.CarStrength.TryGet("alpha", Year, out var alpha));
        Assert.Equal(70, alpha);
        Assert.False(sources.CarStrength.TryGetReliability("alpha", Year, out _));
    }

    [Fact]
    public void OnlyTheStartSeasonIsReadSoALaterSeasonNeverSeesTheRealOne()
    {
        var sources = StartingSources.For(Year, Report(), null, null);

        Assert.False(sources.CarStrength!.TryGet("alpha", Year + 1, out _));
        Assert.False(sources.CarStrength.TryGet("alpha", Year - 1, out _));
        Assert.False(sources.CarStrength.TryGet("aurum", Year, out _));

        // The order of the season before the start sets the tier of the start; a later start has no generated order.
        Assert.Equal(TeamTier.Top, sources.Tiers!.TierOf(OrganizationId.Real("echo"), Year));
        Assert.Equal(TeamTier.Typical, sources.Tiers.TierOf(OrganizationId.Real("alpha"), Year));
        Assert.Equal(TeamTier.Low, sources.Tiers.TierOf(OrganizationId.Real("bravo"), Year));
        Assert.Equal(TeamTier.Typical, sources.Tiers.TierOf(OrganizationId.Real("delta"), Year));
        Assert.Null(sources.Tiers.PreviousPlaceOf(OrganizationId.Real("echo"), Year + 1));
    }

    [Fact]
    public void AnAuthoredOrderOfThePreviousSeasonReplacesTheWholeGeneratedOrder()
    {
        var authored = new PreviousSeasonStandingTier([new ConstructorStandingFact(Year - 1, "bravo", 1)]);
        var sources = StartingSources.For(Year, Report(), null, authored);

        Assert.Same(authored, sources.Tiers);
        Assert.Equal(TeamTier.Top, sources.Tiers!.TierOf(OrganizationId.Real("bravo"), Year));
        Assert.Equal(TeamTier.Typical, sources.Tiers.TierOf(OrganizationId.Real("echo"), Year));
    }

    [Fact]
    public void WithoutGeneratedDataTheAuthoredSourcesAreUnchanged()
    {
        var authored = new MapCarStrengthSource(("bravo", Year, 55));
        var tiers = new PreviousSeasonStandingTier([]);

        var none = StartingSources.For(Year, null, authored, tiers);
        var otherYears = StartingSources.For(Year + 10, Report(), authored, tiers);

        Assert.False(none.FromGeneratedData);
        Assert.Same(authored, none.CarStrength);
        Assert.Same(tiers, none.Tiers);
        Assert.False(otherYears.FromGeneratedData);
        Assert.Same(authored, otherYears.CarStrength);
    }

    [Fact]
    public void TheWorldBuildsItsCarsFromTheLayeredStrengths()
    {
        var sources = StartingSources.For(Year, Report(), new MapCarStrengthSource(("bravo", Year, 55)), null);
        var config = CareerConfig.FromPreset(CareerPreset.MostHistorical).WithStartYear(Year);
        var options = new WorldInitOptions(CarStrength: sources.CarStrength, Tiers: sources.Tiers);

        var first = WorldInitializer.Create(config, WorldInitFixtures.Data(), WorldInitFixtures.Provider(), 11UL, options);
        var again = WorldInitializer.Create(config, WorldInitFixtures.Data(), WorldInitFixtures.Provider(), 11UL, options);
        var cars = first.World.Section<CarsSection>(CarsSection.SectionName)!.Cars;

        Assert.Equal(first.World.StateHash(), again.World.StateHash());
        Assert.All(cars.Where(car => car.Organization == OrganizationId.Real("alpha")), car => Assert.Equal(70, car.Levels.Downforce));
        Assert.All(cars.Where(car => car.Organization == OrganizationId.Real("bravo")), car => Assert.Equal(55, car.Levels.Downforce));
        Assert.All(cars.Where(car => car.Organization == OrganizationId.Real("echo")), car => Assert.Equal(38, car.Levels.Downforce));

        // charlie is in neither table: the tier fallback, as before.
        Assert.All(cars.Where(car => car.Organization == OrganizationId.Real("charlie")), car => Assert.Equal(CarEstimates.TierFallback, car.Levels.Downforce));
    }

    [Fact]
    public void ALoaderRefusesAnotherSchemaVersionAndAMissingFileIsNoData()
    {
        var json = JsonSerializer.Serialize(Report() with { SchemaVersion = 99 }, HistoricalJson.Options);
        Assert.Throws<JsonException>(() => StartingDataLoader.Parse(json));

        var root = Directory.CreateTempSubdirectory("paddock-starting-").FullName;
        try
        {
            Assert.Null(StartingDataLoader.TryLoad(root));
            Assert.Equal(Path.Combine(root, "cache", "reports", "starting_data.json"), StartingDataLoader.DefaultPath(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AStartYearIsAvailableWhenItsCarsHaveStartingDataAnd2026IsNot()
    {
        var withReport = StartYears.Of(WorldInitFixtures.Data(), Report());
        var authoredOnly = StartYears.Of(AuthoredDataLoader.Load(Paddock.Tests.Desktop.BridgeTestData.DataRoot), null);

        Assert.Equal(Enumerable.Range(1950, 77), withReport.Select(option => option.Year));
        Assert.True(withReport.Single(option => option.Year == Year).Available);
        Assert.Null(withReport.Single(option => option.Year == Year).ReasonKey);
        Assert.Equal(StartYears.NoStartingData, withReport.Single(option => option.Year == 1950).ReasonKey);
        Assert.Equal(StartYears.Procedural, withReport.Single(option => option.Year == 2026).ReasonKey);

        // Without the local data only the seasons the authored strengths cover can start.
        Assert.Equal([1954, 1955], authoredOnly.Where(option => option.Available).Select(option => option.Year));

        foreach (var language in new[] { "en", "pl" })
        {
            var strings = File.ReadAllText(Path.Combine(RepoPaths.Root(), "strings", language + ".json"));
            Assert.Contains("\"" + StartYears.NoStartingData + "\"", strings, StringComparison.Ordinal);
            Assert.Contains("\"" + StartYears.Procedural + "\"", strings, StringComparison.Ordinal);
        }
    }

    private static StartingDataReport Report() => new(
        StartingDataLoader.SchemaVersion,
        "ESTIMATE fixture",
        Year - 1,
        Year,
        [
            new StartingSeason(
                Year - 1,
                40,
                [],
                [new StartingStanding("echo", 1, 20), new StartingStanding("alpha", 2, 10), new StartingStanding("bravo", 4, 1)]),
            new StartingSeason(
                Year,
                40,
                [
                    new StartingConstructor("alpha", 70, StartingStrengthSources.CarEffect, 0.8, "Aurum", "Aurum V8"),
                    new StartingConstructor("bravo", 45, StartingStrengthSources.CarEffect, -0.2, "Aurum", "Aurum V8"),
                    new StartingConstructor("echo", 38, StartingStrengthSources.NewEntrant, null, "Ferro Motors", "Ferro 12"),
                ],
                []),
        ]);
}
