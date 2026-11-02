using Paddock.Domain.Contracts;
using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Tests.Career;

namespace Paddock.Tests.Contracts;

/// <summary>
/// The salary the world writes on a contract it makes itself (#318): the era scale at the stars of the person's attributes.
/// The scale is an ESTIMATE; these tests keep it in the range of the published 2017 figures (a top Ferrari driver about 30 million
/// dollars a year before bonuses) and check the rounding and that a paid contract is never repriced.
/// </summary>
public class StartingPayTests
{
    private static readonly EraPayBenchmark Pay = new(CareerKit.Data.EraSetFor);

    private static PersonTruth Flat(int level)
    {
        var attributes = GenerationEstimates.DriverAttributeKeys.Select(key => new NamedAttribute(key, level)).ToArray();
        return new PersonTruth(attributes, attributes);
    }

    [Fact]
    public void ATopDriverOf2017IsPaidTensOfMillionsAndAMidfieldDriverMillions()
    {
        Assert.InRange(StartingPay.For(Pay, 2017, NegotiationSubject.DriverSeat, Flat(18)), 20_000_000, 45_000_000);
        Assert.InRange(StartingPay.For(Pay, 2017, NegotiationSubject.DriverSeat, Flat(10)), 1_000_000, 10_000_000);
    }

    [Fact]
    public void AStarOf1955EarnsThousandsNotMillions()
    {
        Assert.InRange(StartingPay.For(Pay, 1955, NegotiationSubject.DriverSeat, Flat(19)), 10_000, 30_000);
    }

    [Fact]
    public void AStaffMemberEarnsTheStaffShareOfTheScale()
    {
        var engineer = new PersonTruth(
            StaffCatalogue.AttributeKeys(StaffRole.RaceEngineer).Select(key => new NamedAttribute(key, 10)).ToArray(),
            StaffCatalogue.AttributeKeys(StaffRole.RaceEngineer).Select(key => new NamedAttribute(key, 10)).ToArray());
        var driver = StartingPay.For(Pay, 2017, NegotiationSubject.DriverSeat, Flat(10));
        var staff = StartingPay.For(Pay, 2017, NegotiationSubject.Staff(StaffRole.RaceEngineer), engineer);
        Assert.Equal(StartingPay.Round((long)(driver * NegotiationEstimates.StaffPayShare)), staff);
    }

    [Theory]
    [InlineData(33_241_517L, 33_000_000L)]
    [InlineData(18_750L, 19_000L)]
    [InlineData(7L, 7L)]
    [InlineData(0L, 1L)]
    public void TheSalaryKeepsTwoSignificantDigits(long amount, long rounded) => Assert.Equal(rounded, StartingPay.Round(amount));

    [Fact]
    public void OnlyAnUnpaidContractIsPricedAndThePrincipalIsLeftToTheBoard()
    {
        var world = WorldState.At(GameDate.SeasonStart(2017));
        (world, var team) = world.AddOrganization(new OrganizationSpec(OrganizationKind.Team, true, "red", new GameDate(1950, 1, 1), null, 1_000_000, [new OrganizationNameSpan("Red", new GameDate(1950, 1, 1), null)]));
        (world, var driver) = world.AddPerson(new PersonSpec("A", "Driver", new GameDate(1990, 1, 1), "GBR", false, null, [PersonRole.Driver], Flat(15)));
        (world, var paid) = world.AddPerson(new PersonSpec("B", "Driver", new GameDate(1990, 1, 1), "GBR", false, null, [PersonRole.Driver], Flat(15)));
        var bossTruth = StaffCatalogue.AttributeKeys(StaffRole.TeamPrincipal).Select(key => new NamedAttribute(key, 10)).ToArray();
        (world, var boss) = world.AddPerson(new PersonSpec("C", "Boss", new GameDate(1960, 1, 1), "GBR", false, null, [PersonRole.Staff(StaffRole.TeamPrincipal)], new PersonTruth(bossTruth, bossTruth)));
        var start = GameDate.SeasonStart(2017);
        (world, var free) = world.AddContract(new ContractSpec(driver, team, ContractRole.Driver(SeatStatus.Equal), start, GameDate.SeasonEnd(2017), 0, true, null, null));
        (world, var kept) = world.AddContract(new ContractSpec(paid, team, ContractRole.Driver(SeatStatus.Equal), start, GameDate.SeasonEnd(2017), 123, true, null, null));
        (world, var principal) = world.AddContract(new ContractSpec(boss, team, ContractRole.Staff(StaffRole.TeamPrincipal), start, GameDate.SeasonEnd(2017), 0, false, null, null));

        var priced = StartingPay.PayUnpaid(world, Pay, start);

        Assert.True(priced.Contracts.Single(contract => contract.Id == free).Salary > 1_000_000);
        Assert.Equal(123, priced.Contracts.Single(contract => contract.Id == kept).Salary);
        Assert.Equal(0, priced.Contracts.Single(contract => contract.Id == principal).Salary);
        Assert.Equal(priced.StateHash(), StartingPay.PayUnpaid(world, Pay, start).StateHash());
    }
}
