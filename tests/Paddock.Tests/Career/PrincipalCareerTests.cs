using Paddock.Application.Career;
using Paddock.Application.Principals;
using Paddock.Domain.Cars;
using Paddock.Domain.People;
using Paddock.Domain.Pool;
using Paddock.Domain.Principals;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Ai;
using Paddock.Simulation.Career;
using Paddock.Simulation.Pool;
using Paddock.Simulation.Time;

namespace Paddock.Tests.Career;

/// <summary>#184: the AI principals file the morning's commands, skip a human's team, and a retirement pulls the next review.</summary>
public sealed class PrincipalCareerTests
{
    private const ulong Seed = 7;

    [Fact]
    public void ARetirementOfAContractedPersonPullsThatTeamsReviewForward()
    {
        var session = Session(WithContract("vet"), principals: Parked("alpha"));

        var result = CareerHost.RunUntil(session, new GameDate(1950, 1, 3), null, new CareerRunOptions());

        var reviews = result.Host.Log.Entries.OfType<RecordPrincipalReviewCommand>().ToArray();
        Assert.DoesNotContain(reviews, review => review.IssuedOn == new DateOnly(1950, 1, 1));
        var pulled = Assert.Single(reviews);
        Assert.Equal(new DateOnly(1950, 1, 2), pulled.IssuedOn);
        Assert.Equal("alpha", pulled.OrganizationId);
        Assert.Equal("ai:alpha", pulled.ManagerId.Value);
    }

    [Fact]
    public void AHumanTeamIsNotReviewedAndTwoRunsAgree()
    {
        var options = new CareerRunOptions { Humans = [new CareerHuman("human:beta", "Bea", "beta")] };
        var first = Session(TwoTeams());
        var second = Session(TwoTeams());

        // A human who has not pressed ready blocks the day. The morning has already filed and flushed.
        Assert.Throws<InvalidOperationException>(() => CareerHost.RunUntil(first, new GameDate(1950, 1, 2), null, options));
        Assert.Throws<InvalidOperationException>(() => CareerHost.RunUntil(second, new GameDate(1950, 1, 2), null, options));

        Assert.Equal(first.World.StateHash(), second.World.StateHash());
        var principals = first.World.Section<PrincipalsSection>(PrincipalsSection.SectionName);
        Assert.NotNull(principals);
        Assert.NotNull(principals.Of(OrganizationId.Real("alpha")));
        Assert.Null(principals.Of(OrganizationId.Real("beta")));
        var cars = first.World.Section<CarsSection>(CarsSection.SectionName);
        Assert.NotNull(cars);
        Assert.NotEmpty(cars.Of(OrganizationId.Real("alpha")));
        Assert.Empty(cars.Of(OrganizationId.Real("beta")));
    }

    [Fact]
    public void TriggersNameTheTeamThatLostSomeoneAndTheWholeGridForARuleChangeOrARace()
    {
        var seats = new Dictionary<string, List<OrganizationId>>(StringComparer.Ordinal)
        {
            ["vet"] = [OrganizationId.Real("alpha")],
        };
        var outlook = new StepOutlook();

        var retired = PrincipalTriggers.Planned(
            [Event(CareerEventType.Retired, new MarkerPayload("vet"))],
            outlook,
            seats);
        var one = Assert.Single(retired);
        Assert.Equal(ReviewTrigger.Vacancy, one.Trigger);
        Assert.Equal(OrganizationId.Real("alpha"), one.Organization);

        Assert.Empty(PrincipalTriggers.Planned(
            [Event(CareerEventType.Retired, new MarkerPayload("stranger"))],
            outlook,
            seats));

        var rules = Assert.Single(PrincipalTriggers.Planned(
            [Event(CareerEventType.SeasonChanged, new MarkerPayload("1956"))],
            outlook,
            seats));
        Assert.Equal(ReviewTrigger.RegulationChange, rules.Trigger);
        Assert.Null(rules.Organization);
        Assert.Empty(PrincipalTriggers.Planned(
            [Event(CareerEventType.SeasonChanged, new MarkerPayload("1950"))],
            outlook,
            seats));

        var race = Assert.Single(PrincipalTriggers.Planned(
            [Event(ScheduledEventType.Race, new MarkerPayload("round"))],
            outlook,
            seats));
        Assert.Equal(ReviewTrigger.RaceResult, race.Trigger);
        Assert.Null(race.Organization);
    }

    private sealed class StepOutlook : IRegulationOutlook
    {
        public double ChangeAfter(int season) => season == 1955 ? 0.4 : 0;
    }

    private static DomainEvent Event(string type, EventPayload payload) =>
        new(EventId.FromCounter(1), new GameDate(1956, 1, 1), type, payload);

    private static CareerSession Session(WorldState world, PrincipalsSection? principals = null)
    {
        if (principals is not null)
        {
            world = world.WithSection(principals);
        }

        return new CareerSession(world, Seed, [PersonId.Real("vet")], [], new CareerSessionOptions
        {
            Pool = new TalentPoolOptions { TargetSize = 0 },
        });
    }

    private static PrincipalsSection Parked(string team) =>
        PrincipalsSection.Empty.With(new AiPrincipalRecord(
            OrganizationId.Real(team),
            "Contender",
            null,
            new GameDate(1949, 12, 1),
            new GameDate(1949, 12, 1),
            new GameDate(1950, 6, 1),
            0,
            0,
            string.Empty));

    private static WorldState WithContract(string person)
    {
        var world = WorldState.At(new GameDate(1950, 1, 1));
        (world, var team) = world.AddOrganization(Team("alpha", "Alpha"));
        (world, _) = world.AddPerson(Person(person, new GameDate(1900, 1, 1)));
        (world, _) = world.AddContract(new ContractSpec(
            PersonId.Real(person),
            team,
            ContractRole.Driver(SeatStatus.Equal),
            new GameDate(1950, 1, 1),
            new GameDate(1950, 12, 31),
            0,
            true,
            null,
            null));
        return world;
    }

    private static WorldState TwoTeams()
    {
        var world = WorldState.At(new GameDate(1950, 1, 1));
        (world, _) = world.AddOrganization(Team("alpha", "Alpha"));
        (world, _) = world.AddOrganization(Team("beta", "Beta"));
        (world, _) = world.AddPerson(Person("vet", new GameDate(1900, 1, 1)));
        return world;
    }

    private static OrganizationSpec Team(string id, string name) =>
        new(
            OrganizationKind.Team,
            true,
            id,
            new GameDate(1950, 1, 1),
            null,
            0,
            [new OrganizationNameSpan(name, new GameDate(1950, 1, 1), null)]);

    private static PersonSpec Person(string id, GameDate birth)
    {
        var flat = new DriverAttributes(10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10);
        return new PersonSpec(id, id, birth, "GBR", true, id, [PersonRole.Driver], PersonTruth.FromDriver(flat, flat));
    }
}
