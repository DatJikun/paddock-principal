using Paddock.Domain.People;
using Paddock.Domain.Random;

namespace Paddock.Tests.People;

public class StaffGeneratorTests
{
    [Fact]
    public void SameSeedIsByteIdenticalAndAnotherSeedDiffers()
    {
        string first = One(StaffRole.TechnicalDirector, 8, 1988);
        string second = One(StaffRole.TechnicalDirector, 8, 1988);
        string other = One(StaffRole.TechnicalDirector, 9, 1988);

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void StaffDependOnEarlierPeopleOnlyThroughTheIdCounter()
    {
        var ids = new StableIdAllocator();
        var names = new FixtureNameSource();
        RngStream stream = RngStream.Derive(4, RngStreamName.People, 1976);
        GenerationRequest request = Request(1976);
        var drivers = new DriverGenerator(ids, names);
        var staff = new StaffGenerator(ids, names);
        GeneratedDriver driver = drivers.Generate(stream, 1976, request);
        GeneratedStaff member = staff.Generate(stream, 1976, StaffRole.ChiefDesigner, request);

        var staffOnly = new StaffGenerator(new StableIdAllocator(2), names);
        GeneratedStaff alone = staffOnly.Generate(stream, 1976, StaffRole.ChiefDesigner, request);

        Assert.Equal("gen:1", driver.Id);
        Assert.Equal("gen:2", member.Id);
        Assert.Equal(PeopleCanonical.Staff(member), PeopleCanonical.Staff(alone));
    }

    [Fact]
    public void RolesArriveWithTheirEraAndKeepAttributeBounds()
    {
        var ids = new StableIdAllocator();
        var generator = new StaffGenerator(ids, new FixtureNameSource());
        RngStream stream = RngStream.Derive(2, RngStreamName.People, 1950);
        GenerationRequest early = Request(1950);
        Assert.Throws<ArgumentException>(() => generator.Generate(stream, 1950, StaffRole.Strategist, early));
        Assert.Throws<ArgumentException>(() => generator.Generate(stream, 1967, StaffRole.HeadOfAerodynamics, Request(1967)));
        Assert.Throws<ArgumentException>(() => generator.Generate(stream, 1969, StaffRole.RaceEngineer, Request(1969)));
        Assert.Equal(1, ids.NextSequence);

        GeneratedStaff designer = generator.Generate(stream, 1950, StaffRole.TechnicalDirector, early);
        Assert.Equal(2, ids.NextSequence);
        AssertStaffShape(designer, 1950);
        NamedAttribute innovation = Assert.Single(designer.Attributes, attribute => attribute.Key == StaffCatalogue.InnovationKey);
        Assert.Equal(innovation.Value, designer.Innovation);

        GeneratedStaff mechanic = generator.Generate(stream, 1950, StaffRole.ChiefMechanic, early);
        AssertStaffShape(mechanic, 1950);
        Assert.DoesNotContain(mechanic.Attributes, attribute => attribute.Key == StaffCatalogue.InnovationKey);
        Assert.InRange(mechanic.Innovation, GenerationEstimates.AttributeMin, GenerationEstimates.AttributeMax);

        GeneratedStaff strategist = generator.Generate(
            RngStream.Derive(2, RngStreamName.People, 1994),
            1994,
            StaffRole.Strategist,
            Request(1994));
        AssertStaffShape(strategist, 1994);
        Assert.Equal(["strategy", "reaction", "weather"], strategist.Attributes.Select(attribute => attribute.Key).ToArray());

        GeneratedStaff principal = generator.Generate(stream, 1950, StaffRole.TeamPrincipal, early);
        Assert.Equal(4, principal.Attributes.Count);
        AssertStaffShape(principal, 1950);
    }

    [Fact]
    public void KnownPersonRequestsAreNotStaff()
    {
        var ids = new StableIdAllocator();
        var generator = new StaffGenerator(ids, new FixtureNameSource());
        var person = new KnownPersonIdentity("newey", "Adrian", "Newey", new DateOnly(1958, 12, 26), "GBR");
        GenerationRequest request = GenerationRequest.RandomizeKnownPerson(person, 50, QualityBand.Contender, 1992);

        Assert.Throws<ArgumentException>(() => generator.Generate(RngStream.Derive(1, RngStreamName.People, 1992), 1992, StaffRole.ChiefDesigner, request));
        Assert.Equal(1, ids.NextSequence);
    }

    [Fact]
    public void ScoutKeepsTheTwoAttributesFromTheDesignTable()
    {
        IReadOnlyList<string> keys = StaffCatalogue.AttributeKeys(StaffRole.Scout);
        Assert.Equal(["talent_judgement", "contact_network"], keys);
    }

    private static void AssertStaffShape(GeneratedStaff staff, int season)
    {
        Assert.Equal(StaffCatalogue.AttributeKeys(staff.Role), staff.Attributes.Select(attribute => attribute.Key).ToArray());
        foreach (NamedAttribute attribute in staff.Attributes)
        {
            Assert.InRange(attribute.Value, GenerationEstimates.AttributeMin, GenerationEstimates.AttributeMax);
        }

        Assert.InRange(staff.Innovation, GenerationEstimates.AttributeMin, GenerationEstimates.AttributeMax);
        Assert.InRange(staff.Ambition, GenerationEstimates.AttributeMin, GenerationEstimates.AttributeMax);
        Assert.InRange(staff.Loyalty, GenerationEstimates.AttributeMin, GenerationEstimates.AttributeMax);
        Assert.True(staff.Curve.PeakAge > staff.Curve.GrowthStartAge);
        Assert.InRange(staff.Curve.GrowthStartAge, GenerationEstimates.StaffGrowthStartMin, GenerationEstimates.StaffGrowthStartMaxExclusive - 1);
        Assert.InRange(staff.Curve.PlateauYears, GenerationEstimates.StaffPlateauMin, GenerationEstimates.StaffPlateauMaxExclusive - 1);
        Assert.InRange(staff.Curve.DeclineMilliPerYear, GenerationEstimates.StaffDeclineMilliMin, GenerationEstimates.StaffDeclineMilliMaxExclusive - 1);
        Assert.InRange(season - staff.BirthDate.Year, GenerationEstimates.StaffAgeMin, GenerationEstimates.StaffAgeMaxExclusive - 1);
        Assert.InRange(staff.BirthDate.Day, 1, GenerationEstimates.BirthDayMaxExclusive - 1);
    }

    private static string One(StaffRole role, ulong seed, int season)
    {
        var generator = new StaffGenerator(new StableIdAllocator(), new FixtureNameSource());
        GeneratedStaff staff = generator.Generate(
            RngStream.Derive(seed, RngStreamName.People, season),
            season,
            role,
            Request(season));
        return PeopleCanonical.Staff(staff);
    }

    private static GenerationRequest Request(int era)
    {
        return GenerationRequest.ForNew(QualityBand.Solid, [new NationalityWeight("GBR", 1)], era);
    }
}
