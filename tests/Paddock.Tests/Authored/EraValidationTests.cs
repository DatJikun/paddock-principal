using Paddock.Data.Authored;

namespace Paddock.Tests.Authored;

public class EraValidationTests
{
    [Fact]
    public void UnknownEraTimelineDimension_IsReported()
    {
        using var fixture = new TempAuthoredData(eraTimeline: EraSamples.UnknownDimensionTimeline);
        var errors = AuthoredDataValidator.Validate(AuthoredDataLoader.Load(fixture.Root));

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.EraUnknownDimension,
                    "era timeline period for 'not_real' (1950-open) uses a dimension that is not in the era catalog"),
            ],
            errors);
    }

    [Fact]
    public void EraEnumValueOutsideTheCatalogList_IsReported()
    {
        using var fixture = new TempAuthoredData(
            eraCatalog: EraSamples.ValueListCatalog,
            eraTimeline: EraSamples.BadEnumTimeline);
        var errors = AuthoredDataValidator.Validate(AuthoredDataLoader.Load(fixture.Root));

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.EraEnumValue,
                    "era dimension 'tobacco_advertising' value 'sometimes' in 1950-open is not in the era catalog value list"),
                new AuthoredDataError(
                    AuthoredDataValidator.EraEnumValue,
                    "era dimension 'hans_device' value 'optional' in 1950-open is not in the era catalog value list"),
            ],
            errors);
    }

    [Fact]
    public void EraGap_IsReported()
    {
        using var fixture = new TempAuthoredData(eraTimeline: EraSamples.GapTimeline);
        var errors = AuthoredDataValidator.Validate(AuthoredDataLoader.Load(fixture.Root));

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.EraGap,
                    "era dimension 'tobacco_advertising' does not cover 1961"),
            ],
            errors);
    }

    [Fact]
    public void EraOverlap_IsReported()
    {
        using var fixture = new TempAuthoredData(eraTimeline: EraSamples.OverlapTimeline);
        var errors = AuthoredDataValidator.Validate(AuthoredDataLoader.Load(fixture.Root));

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.EraOverlap,
                    "era dimension 'tobacco_advertising' overlaps in 1960"),
            ],
            errors);
    }

    [Fact]
    public void EraInvertedPeriod_IsReported()
    {
        using var fixture = new TempAuthoredData(eraTimeline: EraSamples.InvertedTimeline);
        var errors = AuthoredDataValidator.Validate(AuthoredDataLoader.Load(fixture.Root));

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.EraInvertedPeriod,
                    "era dimension 'tobacco_advertising' period 1970-1960 ends before it starts"),
            ],
            errors);
    }

    [Fact]
    public void EraSourceThatIsNotAnAbsoluteHttpsUrl_IsReported()
    {
        var cpi = EraSamples.ClosedCpi();
        cpi[0] = (cpi[0].Year, cpi[0].Cpi, cpi[0].Partial, "fixture");
        using var fixture = new TempAuthoredData(
            eraTimeline: EraSamples.HttpSourceTimeline,
            cpi: EraSamples.CpiJson(cpi));
        var errors = AuthoredDataValidator.Validate(AuthoredDataLoader.Load(fixture.Root));

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.EraSourceUrl,
                    "era timeline period for 'tobacco_advertising' (1950-open) source 'http://example.com/tobacco' is not an absolute https URL"),
                new AuthoredDataError(
                    AuthoredDataValidator.EraSourceUrl,
                    "cpi year 1950 source 'fixture' is not an absolute https URL"),
            ],
            errors);
    }

    [Fact]
    public void CpiYears_MissingDuplicateAndOutside_AreAllReported()
    {
        var cpi = EraSamples.ClosedCpi(includePartialFinalSeason: false);
        cpi.Add((1950, 1m, false, "https://example.com/cpi"));
        cpi.RemoveAll(row => row.Year == 1951);
        cpi.Add((1949, 1m, false, "https://example.com/cpi"));
        using var fixture = new TempAuthoredData(cpi: EraSamples.CpiJson(cpi));
        var errors = AuthoredDataValidator.Validate(AuthoredDataLoader.Load(fixture.Root));

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.EraCpiYear,
                    "cpi_us.json does not list 1951"),
                new AuthoredDataError(
                    AuthoredDataValidator.EraCpiYear,
                    "cpi_us.json lists 1950 more than once"),
                new AuthoredDataError(
                    AuthoredDataValidator.EraCpiYear,
                    "cpi_us.json lists 1949, outside 1950-2026"),
            ],
            errors);
    }

    [Fact]
    public void CpiFinalSeasonWithoutPartial_IsReported()
    {
        var cpi = EraSamples.ClosedCpi(includePartialFinalSeason: false);
        cpi.Add((AuthoredDataValidator.LastSeason, 77m, false, "https://example.com/cpi"));
        using var fixture = new TempAuthoredData(cpi: EraSamples.CpiJson(cpi));
        var errors = AuthoredDataValidator.Validate(AuthoredDataLoader.Load(fixture.Root));

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.EraCpiPartial,
                    "cpi_us.json year 2026 must have partial set to true"),
            ],
            errors);
    }

    [Fact]
    public void EraFailuresFromDifferentRules_AreReportedTogether()
    {
        var cpi = EraSamples.ClosedCpi(includePartialFinalSeason: false);
        cpi.Add((AuthoredDataValidator.LastSeason, 77m, false, "https://example.com/cpi"));
        using var fixture = new TempAuthoredData(
            eraTimeline: EraSamples.PlainSourceTimeline,
            cpi: EraSamples.CpiJson(cpi));
        var errors = AuthoredDataValidator.Validate(AuthoredDataLoader.Load(fixture.Root));

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.EraSourceUrl,
                    "era timeline period for 'tobacco_advertising' (1950-open) source 'fixture' is not an absolute https URL"),
                new AuthoredDataError(
                    AuthoredDataValidator.EraCpiPartial,
                    "cpi_us.json year 2026 must have partial set to true"),
            ],
            errors);
    }

    [Fact]
    public void UnknownEraJsonProperty_FailsTheLoad()
    {
        using var fixture = new TempAuthoredData(eraCatalog: EraSamples.UnknownPropertyCatalog);
        var ex = Assert.Throws<AuthoredDataLoadException>(() => AuthoredDataLoader.Load(fixture.Root));
        Assert.Contains("extra", ex.Message, StringComparison.Ordinal);
    }
}
