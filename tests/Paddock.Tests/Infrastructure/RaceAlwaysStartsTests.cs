using Paddock.Application.Career;
using Paddock.Application.Racing;
using Paddock.Domain.Cars;
using Paddock.Domain.Finance;
using Paddock.Domain.Infrastructure;
using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Cars;
using Paddock.Tests.Career;
using Paddock.Tests.Persistence;

namespace Paddock.Tests.Infrastructure;

/// <summary>The team always races (#264): cash is not a gate, the bill just pushes the balance below zero.</summary>
public sealed class RaceAlwaysStartsTests
{
    [Fact]
    public void ATeamWithoutCashStillStartsTheRace()
    {
        var today = GameDate.SeasonStart(1955);
        var world = WorldState.At(today);
        (world, var teamId) = world.AddOrganization(new OrganizationSpec(
            OrganizationKind.Team, true, "alfa", today, null, 1_000_000, [new("Alfa", today, null)]));
        (world, var driverId) = world.AddPerson(new PersonSpec(
            "Driver", "One", new GameDate(1920, 1, 1), "ITA", false, null, [PersonRole.Driver], DriverTruth(15)));
        (world, _) = world.AddContract(new ContractSpec(
            driverId, teamId, ContractRole.Driver(SeatStatus.NumberOne), today, new GameDate(1955, 12, 31), 100_000, true, null, null));
        world = world.WithSection(CarsSection.Empty.Add(new TeamCar(
            CarIds.Format(1),
            teamId,
            1955,
            CarConcept.Neutral,
            ConceptMapping.StartingLevels(CarConcept.Neutral, 60),
            60,
            CarEstimates.InitialUnderstanding,
            1.0,
            0,
            null,
            driverId)));

        const int races = 10;
        var facts = new EraFinanceFacts("test", 200_000, 1_000_000, 3_000_000);
        var finance = FinanceSection.Empty.Open(teamId, today, 0, facts);
        var typical = finance.TypicalCents;
        var transport = RaceFieldBuilder.TransportCost(
            teamId,
            typical,
            LogisticsMath.Argentina,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["alfa"] = "ITA" });
        Assert.True(transport > 0);
        world = world.WithSection(finance);
        Assert.Equal(0L, finance.BalanceOf(teamId));

        var field = RaceFieldBuilder.Build(
            world,
            today,
            CareerKit.Data.RuleSetFor(1955),
            races,
            null,
            null,
            circuitCountry: LogisticsMath.Argentina,
            teamCountries: new Dictionary<string, string>(StringComparer.Ordinal) { ["alfa"] = "ITA" });
        Assert.Single(field.Entries);
        Assert.Equal(teamId.Value, field.Entries[0].ConstructorId);
    }

    private static PersonTruth DriverTruth(int value)
    {
        var list = new List<NamedAttribute>
        {
            new("cornering", value),
            new("braking", value),
            new("smoothness", value),
            new("overtaking", value),
            new("defending", value),
            new("consistency", value),
            new("composure", value),
            new("adaptability", value),
            new("wet_weather", value),
            new("fitness", value),
            new("feedback", value),
        };
        return new PersonTruth(list, list);
    }
}
