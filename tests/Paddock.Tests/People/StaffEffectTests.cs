using Paddock.Application.Sponsors;
using Paddock.Domain.Cars;
using Paddock.Domain.Development;
using Paddock.Domain.People;
using Paddock.Domain.Pool;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;
using Paddock.Tests.Persistence;

namespace Paddock.Tests.People;

public class StaffEffectTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "paddock-staff-" + Guid.NewGuid().ToString("N"));

    public StaffEffectTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, true);

    [Fact]
    public void ABetterEngineerScoutAndNegotiatorProduceABetterOutcome()
    {
        var skilled = Engineer(18);
        var ordinary = Engineer(8);
        var annual = 1_000_000L;
        var betterCar = EngineeringCapacity.Derive([skilled], 1955, 4, annual, annual);
        var worseCar = EngineeringCapacity.Derive([ordinary], 1955, 4, annual, annual);
        Assert.True(betterCar.Quality > worseCar.Quality);

        Assert.True(ScoutingModel.WidthFactor(18) < ScoutingModel.WidthFactor(PoolEstimates.DefaultScoutJudgement));
        Assert.True(ScoutingModel.BiasLimit(18) < ScoutingModel.BiasLimit(PoolEstimates.DefaultScoutJudgement));
        Assert.True(
            ScoutingModel.MonthlyMilli(ScoutFocusKind.Person, new ScoutProfile(18, 18), 0)
            > ScoutingModel.MonthlyMilli(ScoutFocusKind.Person, ScoutProfile.Unscouted, 0));

        var on = GameDate.SeasonStart(1955);
        var strong = Negotiator(on, "strong", 18);
        var weak = Negotiator(on, "weak", 8);
        var skills = new KnownNegotiatorSkills();
        Assert.True(skills.Skill(strong.World, strong.Team, on) > skills.Skill(weak.World, weak.Team, on));
    }

    [Fact]
    public void APairingRoundTripsThroughTheSaveAndGrowsOnlyWhileTheyStayTogether()
    {
        var on = GameDate.SeasonStart(1955);
        var world = WorldFixtures.Small();
        var team = world.Organizations[0].Id;
        var engineer = world.Persons[0].Id;
        var driver = world.Persons[1].Id;
        var section = StaffSection.Empty.With(new RaceEngineerLink(team, engineer, driver, StaffEstimates.RelationshipStart, 1955));
        world = world.WithSection(section);

        using (var file = SaveFile.Create(Path.Combine(_directory, "staff.paddock"), WorldFixtures.Meta()))
        {
            var repository = new WorldRepository(file);
            repository.SaveWorld(world, world.CurrentDate);
            var loaded = repository.LoadWorld();
            Assert.Equal(world.StateHash(), loaded.StateHash());
            Assert.Equal(section.Links, loaded.Section<StaffSection>(StaffSection.SectionName)!.Links);
        }
    }

    [Fact]
    public void TheRelationshipGrowsWhenThePairStaysAndStartsAgainWhenItDoesNot()
    {
        var on = GameDate.SeasonStart(1955);
        var world = WorldState.At(on);
        (world, var team) = world.AddOrganization(new OrganizationSpec(
            OrganizationKind.Team, true, "alpha", on, null, 0, [new OrganizationNameSpan("Alpha", on, null)]));
        (world, var engineer) = world.AddPerson(Person("eng", StaffRole.RaceEngineer));
        (world, var driver) = world.AddPerson(Person("drv", null));
        (world, _) = world.AddContract(StaffContract(engineer, team, StaffRole.RaceEngineer, on));
        (world, _) = world.AddContract(new ContractSpec(
            driver, team, ContractRole.Driver(SeatStatus.Equal), on, GameDate.SeasonEnd(1956), 0, true, null, null));
        var section = StaffSection.Empty.With(new RaceEngineerLink(team, engineer, driver, StaffEstimates.RelationshipStart, 1955));

        var stayed = StaffRoster.Advance(world, section, GameDate.SeasonStart(1956));
        Assert.Equal(StaffEstimates.RelationshipStart + StaffEstimates.RelationshipPerSeason, Assert.Single(stayed.Links).Relationship);

        var retired = world.RetirePerson(engineer, on);
        var broken = StaffRoster.Advance(retired, section, GameDate.SeasonStart(1956));
        Assert.Empty(broken.Links);
    }

    private static PersonSpec Person(string id, StaffRole? role) => new(
        "Pat",
        id,
        new GameDate(1920, 1, 1),
        "GBR",
        true,
        id,
        role is StaffRole staff ? [PersonRole.Staff(staff)] : [PersonRole.Driver],
        new PersonTruth([new NamedAttribute("pace", 10)], [new NamedAttribute("pace", 10)]));

    private static ContractSpec StaffContract(PersonId person, OrganizationId team, StaffRole role, GameDate on) => new(
        person, team, ContractRole.Staff(role), on, GameDate.SeasonEnd(1956), 0, false, null, null);

    private static Engineer Engineer(int skill)
    {
        var keys = StaffCatalogue.AttributeKeys(StaffRole.TechnicalDirector);
        var attributes = keys.ToDictionary(key => key, _ => skill, StringComparer.Ordinal);
        return new Engineer("eng", StaffRole.TechnicalDirector, attributes, 0.5, 0.5);
    }

    private static (WorldState World, OrganizationId Team) Negotiator(GameDate on, string id, int skill)
    {
        var world = WorldState.At(on);
        (world, var team) = world.AddOrganization(new OrganizationSpec(
            OrganizationKind.Team,
            true,
            id,
            on,
            null,
            0,
            [new OrganizationNameSpan(id, on, null)]));
        (world, var person) = world.AddPerson(new PersonSpec(
            "Pat",
            id,
            new GameDate(1920, 1, 1),
            "GBR",
            true,
            id + "_person",
            [PersonRole.Staff(StaffRole.CommercialDirector)],
            new PersonTruth(
                [new NamedAttribute("negotiation", skill), new NamedAttribute("marketing", skill)],
                [new NamedAttribute("negotiation", skill), new NamedAttribute("marketing", skill)])));
        (world, _) = world.AddContract(new ContractSpec(
            person,
            team,
            ContractRole.Staff(StaffRole.CommercialDirector),
            on,
            GameDate.SeasonEnd(1955),
            0,
            false,
            null,
            null));
        world = world.SetKnowledge(new PersonKnowledge(
            team,
            person,
            [new KnownAttribute("negotiation", new AttributeBand(skill, skill)), new KnownAttribute("marketing", new AttributeBand(skill, skill))],
            null));
        return (world, team);
    }
}
