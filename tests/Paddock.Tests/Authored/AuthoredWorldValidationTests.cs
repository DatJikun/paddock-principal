using Paddock.Data.Authored;

namespace Paddock.Tests.Authored;

public class AuthoredWorldValidationTests
{
    [Fact]
    public void DuplicateTechnologyId_IsReported()
    {
        var errors = Validate(technologies: AuthoredWorldSamples.DuplicateTechnologies);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.DuplicateTechnology,
                    "technology id 'rear_engine' appears 2 times"),
            ],
            errors);
    }

    [Fact]
    public void TechnologyAreaOutsideTheAllowedSet_IsReported()
    {
        var errors = Validate(technologies: AuthoredWorldSamples.BadAreaTechnologies);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.TechnologyArea,
                    "technology 'rear_engine' area 'wings' is not one of aero, chassis, engine, electronics, safety, tyres, other"),
            ],
            errors);
    }

    [Fact]
    public void UnknownPrerequisite_IsReported()
    {
        var errors = Validate(technologies: AuthoredWorldSamples.UnknownPrerequisiteTechnologies);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.UnknownPrerequisite,
                    "technology 'rear_engine' prerequisite 'missing' does not exist"),
            ],
            errors);
    }

    [Fact]
    public void PrerequisiteCycle_IsReported()
    {
        var errors = Validate(technologies: AuthoredWorldSamples.CyclicTechnologies);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.PrerequisiteCycle,
                    "technologies 'alpha' -> 'beta' -> 'alpha' form a prerequisite cycle"),
            ],
            errors);
    }

    [Fact]
    public void TechnologySeasonOrder_IsReported()
    {
        var errors = Validate(technologies: AuthoredWorldSamples.SeasonOrderTechnologies);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.TechnologySeasonOrder,
                    "technology 'late' has earliest_plausible.season 1970 after first_used.season 1960"),
                new AuthoredDataError(
                    AuthoredDataValidator.TechnologySeasonOrder,
                    "technology 'late' has first_used.season 1960 after widespread_by 1950"),
                new AuthoredDataError(
                    AuthoredDataValidator.TechnologySeasonOrder,
                    "technology 'gap' has earliest_plausible.season 2010 after widespread_by 2000"),
            ],
            errors);
    }

    [Fact]
    public void InvertedBan_IsReported()
    {
        var errors = Validate(technologies: AuthoredWorldSamples.InvertedBanTechnologies);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.InvertedBan,
                    "technology 'rear_engine' ban 1980-1970 ends before it starts"),
            ],
            errors);
    }

    [Fact]
    public void UnknownTechnologyTeam_IsReported()
    {
        var errors = Validate(technologies: AuthoredWorldSamples.UnknownTeamTechnologies);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.UnknownConstructor,
                    "technology 'rear_engine' first_used.team 'not_a_team' is not a known constructor"),
            ],
            errors);
    }

    [Fact]
    public void EngineYearOutside1950To2026_IsReported()
    {
        var errors = Validate(engines: AuthoredWorldSamples.EngineYearTooEarly);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.EngineYear,
                    "engine 'cooper' 1949 is outside 1950-2026"),
            ],
            errors);
    }

    [Fact]
    public void EngineTypeOutsideSupplyTypes_IsReported()
    {
        var errors = Validate(engines: AuthoredWorldSamples.EngineTypeUnknown);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.EngineType,
                    "engine 'cooper' 1950 type 'nope' is not one of works, customer, partner, badged, unknown"),
            ],
            errors);
    }

    [Fact]
    public void EmptyEngineText_IsReported()
    {
        var errors = Validate(engines: AuthoredWorldSamples.EngineEmptySupplier);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.EmptyText,
                    "engine 'cooper' 1950 supplier is empty"),
            ],
            errors);
    }

    [Fact]
    public void DuplicateLineageId_IsReported()
    {
        var errors = Validate(lineage: AuthoredWorldSamples.DuplicateLineage);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.DuplicateLineage,
                    "lineage id 'alpha' appears 2 times"),
            ],
            errors);
    }

    [Fact]
    public void OverlappingLineageEntries_AreReported()
    {
        var errors = Validate(lineage: AuthoredWorldSamples.OverlappingLineage);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.LineageOverlap,
                    "lineage 'alpha' entries 'cooper' (1950-1952) and 'cooper' (1952-1954) overlap"),
            ],
            errors);
    }

    [Fact]
    public void UnknownLineageConstructor_IsReported()
    {
        var errors = Validate(lineage: AuthoredWorldSamples.UnknownLineageConstructor);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.UnknownConstructor,
                    "lineage 'alpha' constructor 'not_a_team' is not a known constructor"),
            ],
            errors);
    }

    [Fact]
    public void ConstructorYearInTwoLineages_IsReported()
    {
        var errors = Validate(lineage: AuthoredWorldSamples.SharedConstructorYear);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.ConstructorYear,
                    "constructor 'cooper' in 1950 belongs to lineages 'alpha' and 'beta'"),
            ],
            errors);
    }

    [Fact]
    public void DuplicateOrganizationId_IsReported()
    {
        var errors = Validate(founders: AuthoredWorldSamples.DuplicateFounders);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.DuplicateOrganization,
                    "organization id 'alpha' appears 2 times"),
            ],
            errors);
    }

    [Fact]
    public void UnknownFounderConstructor_IsReported()
    {
        var errors = Validate(founders: AuthoredWorldSamples.UnknownFounderConstructor);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.UnknownConstructor,
                    "organization 'alpha' constructor 'not_a_team' is not a known constructor"),
            ],
            errors);
    }

    [Fact]
    public void InvertedFounderSpan_IsReported()
    {
        var errors = Validate(founders: AuthoredWorldSamples.InvertedFounderSpan);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.InvertedSpan,
                    "organization 'alpha' constructor 'cooper' period 1960-1950 ends before it starts"),
            ],
            errors);
    }

    [Fact]
    public void FoundedAfterConstructorEntry_IsReported()
    {
        var errors = Validate(founders: AuthoredWorldSamples.FoundedAfterEntry);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.FoundedAfterEntry,
                    "organization 'alpha' founded 1960 is after constructor 'cooper' entry 1950-1955"),
            ],
            errors);
    }

    [Fact]
    public void DuplicateStaffId_IsReported()
    {
        var errors = Validate(staff: AuthoredWorldSamples.DuplicateStaff);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.DuplicateStaff,
                    "staff id 'alpha' appears 2 times"),
            ],
            errors);
    }

    [Fact]
    public void StaffBornThatIsNotAnIsoDate_IsReported()
    {
        var errors = Validate(staff: AuthoredWorldSamples.BadBornStaff);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.StaffBorn,
                    "staff 'alpha' born '1920-13-01' is not an ISO date"),
            ],
            errors);
    }

    [Fact]
    public void InvertedStaffCareer_IsReported()
    {
        var errors = Validate(staff: AuthoredWorldSamples.InvertedCareerStaff);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.InvertedCareer,
                    "staff 'alpha' career at 'cooper' 1960-1950 ends before it starts"),
            ],
            errors);
    }

    [Fact]
    public void StaffRoleOutsideTheKnownSet_IsReported()
    {
        var errors = Validate(staff: AuthoredWorldSamples.BadRoleStaff);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.StaffRole,
                    "staff 'alpha' role 'mechanic' is not one of technical_director, team_principal, chief_designer, owner, designer, head_of_aero, race_engineer, engine_designer"),
            ],
            errors);
    }

    [Fact]
    public void F1StaffOrgMustBeAKnownConstructor_AndASeriesOrgIsFreeForm()
    {
        var errors = Validate(staff: AuthoredWorldSamples.UnknownStaffOrg);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.UnknownConstructor,
                    "staff 'alpha' F1 stint at 'not_a_team' is not a known constructor"),
            ],
            errors);
    }

    [Fact]
    public void SourceThatIsNotAnAbsoluteHttpsUrl_IsReported()
    {
        var errors = Validate(technologies: AuthoredWorldSamples.BadSourceTechnologies);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.SourceUrl,
                    "source 'http://example.com/nope' on technology 'rear_engine' first_used is not an absolute https URL"),
            ],
            errors);
    }

    [Fact]
    public void TechnologyAndEngineFailures_AreReportedTogether()
    {
        var errors = Validate(
            technologies: AuthoredWorldSamples.BadAreaTechnologies,
            engines: AuthoredWorldSamples.EngineYearTooEarly);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.TechnologyArea,
                    "technology 'rear_engine' area 'wings' is not one of aero, chassis, engine, electronics, safety, tyres, other"),
                new AuthoredDataError(
                    AuthoredDataValidator.EngineYear,
                    "engine 'cooper' 1949 is outside 1950-2026"),
            ],
            errors);
    }

    [Fact]
    public void UnknownTechnologyProperty_FailsTheLoad()
    {
        using var fixture = new TempAuthoredData(technologies: AuthoredWorldSamples.UnknownPropertyTechnologies);
        var ex = Assert.Throws<AuthoredDataLoadException>(() => AuthoredDataLoader.Load(fixture.Root));
        Assert.Contains("extra", ex.Message, StringComparison.Ordinal);
    }

    private static IReadOnlyList<AuthoredDataError> Validate(
        string? technologies = null,
        string? engines = null,
        string? lineage = null,
        string? founders = null,
        string? staff = null)
    {
        using var fixture = new TempAuthoredData(
            technologies: technologies,
            engines: engines,
            lineage: lineage,
            founders: founders,
            staff: staff);
        return AuthoredDataValidator.Validate(AuthoredDataLoader.Load(fixture.Root));
    }
}
