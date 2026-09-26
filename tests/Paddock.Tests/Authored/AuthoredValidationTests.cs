using Paddock.Data.Authored;

namespace Paddock.Tests.Authored;

public class AuthoredValidationTests
{
    [Fact]
    public void ValidFixture_HasNoErrors()
    {
        using var fixture = new TempAuthoredData();
        var errors = AuthoredDataValidator.Validate(AuthoredDataLoader.Load(fixture.Root));
        Assert.Empty(errors);
    }

    [Fact]
    public void UnknownTimelineDimension_IsReported()
    {
        using var fixture = new TempAuthoredData(timeline: AuthoredSamples.UnknownDimensionTimeline);
        var errors = AuthoredDataValidator.Validate(AuthoredDataLoader.Load(fixture.Root));

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.UnknownDimension,
                    "timeline period for 'not_real' (1950-open) uses a dimension that is not in the catalog"),
            ],
            errors);
    }

    [Fact]
    public void EnumValueOutsideTheCatalogList_IsReported()
    {
        using var fixture = new TempAuthoredData(
            catalog: AuthoredSamples.ValueListCatalog,
            timeline: AuthoredSamples.BadEnumTimeline);
        var errors = AuthoredDataValidator.Validate(AuthoredDataLoader.Load(fixture.Root));

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.EnumValue,
                    "dimension 'points_scale' value 'nope' in 1950-open is not in the catalog value list"),
                new AuthoredDataError(
                    AuthoredDataValidator.EnumValue,
                    "dimension 'refuelling' value 'sometimes' in 1950-open is not in the catalog value list"),
            ],
            errors);
    }

    [Fact]
    public void GapOverlapAndInvertedPeriod_AreAllReported()
    {
        using var fixture = new TempAuthoredData(timeline: AuthoredSamples.GapOverlapTimeline);
        var errors = AuthoredDataValidator.Validate(AuthoredDataLoader.Load(fixture.Root));

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.InvertedPeriod,
                    "dimension 'points_scale' period 1970-1960 ends before it starts"),
                new AuthoredDataError(
                    AuthoredDataValidator.Gap,
                    "dimension 'points_scale' does not cover 1961"),
                new AuthoredDataError(
                    AuthoredDataValidator.Overlap,
                    "dimension 'points_scale' overlaps in 1960"),
            ],
            errors);
    }

    [Fact]
    public void ProfileWeightsThatDoNotSumToOne_AreReported()
    {
        using var fixture = new TempAuthoredData(circuits: AuthoredSamples.BadProfileCircuits);
        var errors = AuthoredDataValidator.Validate(AuthoredDataLoader.Load(fixture.Root));

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.ProfileWeights,
                    "layout 'test_1950' profile weights sum to 2.000, expected 1 +/- 0.01"),
            ],
            errors);
    }

    [Fact]
    public void RaceMapEntryForAMissingLayout_IsReported()
    {
        using var fixture = new TempAuthoredData(raceMap: AuthoredSamples.MissingLayoutRaceMap);
        var errors = AuthoredDataValidator.Validate(AuthoredDataLoader.Load(fixture.Root));

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.UnknownLayout,
                    "race 1950 round 1 points at missing layout 'missing_layout'"),
            ],
            errors);
    }

    [Fact]
    public void FailuresFromDifferentRules_AreReportedTogether()
    {
        using var fixture = new TempAuthoredData(
            timeline: AuthoredSamples.UnknownDimensionTimeline,
            circuits: AuthoredSamples.BadProfileCircuits);
        var errors = AuthoredDataValidator.Validate(AuthoredDataLoader.Load(fixture.Root));

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.UnknownDimension,
                    "timeline period for 'not_real' (1950-open) uses a dimension that is not in the catalog"),
                new AuthoredDataError(
                    AuthoredDataValidator.ProfileWeights,
                    "layout 'test_1950' profile weights sum to 2.000, expected 1 +/- 0.01"),
            ],
            errors);
    }

    [Fact]
    public void UnknownJsonProperty_FailsTheLoad()
    {
        using var fixture = new TempAuthoredData(circuits: AuthoredSamples.UnknownPropertyCircuits);
        var ex = Assert.Throws<AuthoredDataLoadException>(() => AuthoredDataLoader.Load(fixture.Root));
        Assert.Contains("extra", ex.Message, StringComparison.Ordinal);
    }
}
