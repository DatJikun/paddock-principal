using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Tests.Persistence;

namespace Paddock.Tests.World;

/// <summary>Retirement as a domain fact (T18): the person stays, with the date, and the contracts end.</summary>
public class PersonRetirementTests
{
    private static readonly GameDate Opening = GameDate.SeasonStart(1970);

    private static (WorldState World, PersonId Driver, PersonId Other, OrganizationId Team, ContractId Contract) Build()
    {
        var world = WorldState.At(Opening);
        (world, var team) = world.AddOrganization(new OrganizationSpec(
            OrganizationKind.Team,
            true,
            "ret_team",
            new GameDate(1950, 1, 1),
            null,
            0,
            [new OrganizationNameSpan("Retirement Team", new GameDate(1950, 1, 1), null)]));
        (world, var driver) = world.AddPerson(Spec("Old", "Driver", new GameDate(1930, 5, 5), "ret_old_driver"));
        (world, var other) = world.AddPerson(Spec("Young", "Driver", new GameDate(1950, 5, 5), "ret_young_driver"));
        (world, var contract) = world.AddContract(Contract(driver, team));
        (world, _) = world.AddContract(Contract(other, team));
        return (world, driver, other, team, contract);
    }

    [Fact]
    public void RetiringKeepsThePersonWithTheDateAndEndsTheirContracts()
    {
        var (world, driver, other, _, contract) = Build();
        var before = world.StateHash();

        var after = world.RetirePerson(driver, new GameDate(1970, 12, 31));

        var kept = after.GetPerson(driver);
        Assert.Equal(new GameDate(1970, 12, 31), kept.RetiredOn);
        Assert.True(kept.IsRetired);
        Assert.Equal(world.GetPerson(driver).Truth, kept.Truth);
        Assert.Null(after.GetPerson(other).RetiredOn);
        Assert.DoesNotContain(after.Contracts, item => item.Id == contract);
        Assert.Single(after.Contracts);
        Assert.True(after.Ids.WasIssued(driver.Value));
        Assert.Equal(before, world.StateHash());
        Assert.NotEqual(before, after.StateHash());
    }

    [Fact]
    public void TheRetirementDateIsPartOfTheDigest()
    {
        var (world, driver, _, _, _) = Build();
        var retired = world.RetirePerson(driver, new GameDate(1970, 12, 31));
        var later = world.RetirePerson(driver, new GameDate(1971, 12, 31));

        Assert.NotEqual(world.StateHash(), retired.StateHash());
        Assert.NotEqual(retired.StateHash(), later.StateHash());
    }

    [Fact]
    public void ARetiredPersonCannotRetireAgainOrSignAContract()
    {
        var (world, driver, _, team, _) = Build();
        var retired = world.RetirePerson(driver, new GameDate(1970, 12, 31));

        Assert.Throws<InvalidOperationException>(() => retired.RetirePerson(driver, new GameDate(1971, 12, 31)));
        Assert.Throws<InvalidOperationException>(() => retired.AddContract(Contract(driver, team)));
    }

    [Fact]
    public void RetiringBeforeBirthIsRefused()
    {
        var (world, driver, _, _, _) = Build();

        Assert.Throws<ArgumentOutOfRangeException>(() => world.RetirePerson(driver, new GameDate(1929, 1, 1)));
    }

    [Fact]
    public void RestoreKeepsRetirementAndRefusesAContractHeldByARetiredPerson()
    {
        var (world, driver, _, _, _) = Build();
        var retired = world.RetirePerson(driver, new GameDate(1970, 12, 31));

        var restored = WorldState.Restore(
            retired.CurrentDate,
            retired.Ids,
            retired.Persons,
            retired.Organizations,
            retired.Contracts,
            retired.Knowledge);
        Assert.Equal(retired.StateHash(), restored.StateHash());

        Assert.Throws<InvalidOperationException>(() => WorldState.Restore(
            retired.CurrentDate,
            retired.Ids,
            retired.Persons,
            retired.Organizations,
            world.Contracts,
            retired.Knowledge));
    }

    private static PersonSpec Spec(string given, string family, GameDate born, string realId) =>
        new(given, family, born, "GBR", true, realId, [PersonRole.Driver], WorldFixtures.DriverTruth(8, 14));

    private static ContractSpec Contract(PersonId person, OrganizationId team) =>
        new(person, team, ContractRole.Driver(SeatStatus.Equal), Opening, GameDate.SeasonEnd(1972), 0, true, null, null);
}
