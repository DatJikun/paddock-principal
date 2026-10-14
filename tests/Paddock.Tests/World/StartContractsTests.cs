using Paddock.Data.Authored;
using Paddock.Data.World;
using Paddock.Domain.Career;
using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Tests.World;

/// <summary>
/// The contracts of a career's first morning (#325), built from a small invented schedule: the end of a driver's or a staff member's
/// contract follows his real stint (with a fixture, never the Jolpica cache), the old option ends everything in the start year, and the
/// first and second driver of a team come from the ratings, the age and the authored roles.
/// </summary>
public class StartContractsTests
{
    private const ulong Seed = 325UL;
    private const int Year = 2000;
    private const string Source = "https://example.com/fixture";

    private static EngineEntry Engine(string constructor, int year) => new(constructor, year, "Maker", "Maker V10", "works", "high", Source);

    private static StaffMember Staff(string id, string role, string org, int from, int to) =>
        new(id, id, "1950-01-01", "GBR", [new StaffCareerStint(org, role, from, to, Source)], [], "high");

    private static AuthoredData Data(DriverRolesFile? roles = null) => new(
        [],
        [],
        [],
        new CircuitsFile("fixture", [], []),
        [],
        [],
        new EnginesFile("fixture", [], [Engine("red", Year), Engine("green", Year), Engine("gold", Year), Engine("silver", Year)]),
        new LineageFile(
            "fixture",
            [
                new ConstructorLineage(
                    "red-line",
                    [
                        new LineageEntry("red", 1990, 2002, "renamed", Source),
                        new LineageEntry("blue", 2003, null, null, Source),
                    ]),
            ]),
        new FoundersFile("fixture", []),
        [
            Staff("boss", "team_principal", "red", 1995, 2004),
            Staff("tech", "technical_director", "green", 1999, Year),
        ],
        [],
        [],
        [],
        driverRoles: roles);

    private static DriverSeat Seat(int season, string constructor, string role = "race", int starts = 10) =>
        new(season, constructor, 1, role, starts);

    private static RealDriverRecord Driver(string id, int born, params DriverSeat[] seats) =>
        new(id, id, "Fixture", new DateOnly(born, 6, 1), born, "British", 1995, 1993, seats);

    private static FixtureProvider Provider() => new(
        [
            // red: r_a follows the lineage to blue and ends with 2004; r_b moves to another team after the start year.
            Driver("r_a", 1970, Seat(Year, "red"), Seat(2001, "red"), Seat(2002, "red"), Seat(2003, "blue"), Seat(2004, "blue"), Seat(2006, "blue")),
            Driver("r_b", 1971, Seat(Year, "red"), Seat(2001, "gold")),
            // green: the two are equally rated. g_a leaves a gap in 2002, g_b is only a substitute after the start.
            Driver("g_a", 1970, Seat(Year, "green"), Seat(2001, "green"), Seat(2003, "green")),
            Driver("g_b", 1965, Seat(Year, "green"), Seat(2001, "green", "substitute", 1)),
            // gold: o_b is the better rated. The reserve's stint counts any seat.
            Driver("o_a", 1975, Seat(Year, "gold")),
            Driver("o_b", 1976, Seat(Year, "gold"), Seat(2001, "gold"), Seat(2002, "gold")),
            Driver("o_res", 1980, Seat(Year, "gold", "substitute", 1), Seat(2001, "gold", "substitute", 1), Seat(2002, "gold", "substitute", 1)),
        ],
        new Dictionary<string, DriverRating>(StringComparer.Ordinal)
        {
            ["r_a"] = WorldInitFixtures.Rating(15, 16),
            ["r_b"] = WorldInitFixtures.Rating(11, 12),
            ["g_a"] = WorldInitFixtures.Rating(12, 13),
            ["g_b"] = WorldInitFixtures.Rating(12, 13),
            ["o_a"] = WorldInitFixtures.Rating(9, 10),
            ["o_b"] = WorldInitFixtures.Rating(14, 15),
            ["o_res"] = WorldInitFixtures.Rating(8, 9),
        });

    private static CareerConfig Config(StartContracts contracts = StartContracts.Real) =>
        CareerConfig.FromPreset(CareerPreset.Balanced).WithStartYear(Year).WithPlayerTeam("red").WithStartContracts(contracts);

    private static WorldInitResult Create(StartContracts contracts = StartContracts.Real, DriverRolesFile? roles = null, IPeopleProvider? provider = null) =>
        WorldInitializer.Create(Config(contracts), Data(roles), provider ?? Provider(), Seed);

    private static Contract DriverContract(WorldState world, string driver) =>
        world.Contracts.Single(contract => contract.PersonId == PersonId.Real(driver) && contract.Role.IsDriver);

    private static SeatStatus SeatOf(WorldState world, string driver) => DriverContract(world, driver).Role.Seat;

    private static DriverRolesFile Roles(params DriverRoleEntry[] entries) => new("fixture", entries);

    [Fact]
    public void ADriverContractRunsToTheEndOfHisRealStintWithThatTeamAndFollowsTheLineage()
    {
        var world = Create().World;

        Assert.Equal(GameDate.SeasonEnd(2004), DriverContract(world, "r_a").End);
        Assert.Equal(GameDate.SeasonStart(Year), DriverContract(world, "r_a").Start);
        Assert.Equal(GameDate.SeasonEnd(Year), DriverContract(world, "r_b").End);
    }

    [Fact]
    public void AGapOrASubstituteSeasonEndsARaceStintButAReserveKeepsAnySeat()
    {
        var world = Create().World;

        Assert.Equal(GameDate.SeasonEnd(2001), DriverContract(world, "g_a").End);
        Assert.Equal(GameDate.SeasonEnd(Year), DriverContract(world, "g_b").End);
        Assert.Equal(GameDate.SeasonEnd(2002), DriverContract(world, "o_b").End);
        var reserve = DriverContract(world, "o_res");
        Assert.Equal(SeatStatus.Reserve, reserve.Role.Seat);
        Assert.Equal(GameDate.SeasonEnd(2002), reserve.End);
    }

    [Fact]
    public void AStaffContractRunsToTheEndOfTheAuthoredStintAndAGeneratedPersonKeepsTheDefault()
    {
        var world = Create().World;

        Assert.Equal(GameDate.SeasonEnd(2004), world.Contracts.Single(contract => contract.PersonId == PersonId.Real("boss") && contract.Role.IsStaff).End);
        Assert.Equal(GameDate.SeasonEnd(Year), world.Contracts.Single(contract => contract.PersonId == PersonId.Real("tech") && contract.Role.IsStaff).End);

        var generatedDriver = world.Contracts.Where(contract => contract.Role.IsDriver && contract.OrganizationId == OrganizationId.Real("silver")).ToArray();
        Assert.Equal(2, generatedDriver.Length);
        Assert.All(generatedDriver, contract => Assert.Equal(GameDate.SeasonEnd(Year), contract.End));
        var generatedStaff = world.Contracts.Where(contract => contract.Role.IsStaff && !world.GetPerson(contract.PersonId).IsReal).ToArray();
        Assert.NotEmpty(generatedStaff);
        Assert.All(generatedStaff, contract => Assert.Equal(GameDate.SeasonEnd(Year), contract.End));
    }

    [Fact]
    public void WithTheOldOptionEveryContractEndsInTheStartYear()
    {
        var world = Create(StartContracts.AllEndThisYear).World;

        Assert.NotEmpty(world.Contracts);
        Assert.All(world.Contracts, contract => Assert.Equal(GameDate.SeasonEnd(Year), contract.End));
    }

    [Fact]
    public void NoContractEndsBeforeTheStartYear()
    {
        foreach (var contracts in new[] { StartContracts.Real, StartContracts.AllEndThisYear })
        {
            Assert.All(Create(contracts).World.Contracts, contract => Assert.True(contract.End >= GameDate.SeasonEnd(Year)));
        }
    }

    [Fact]
    public void TheOptionChangesOnlyTheEndDatesNotThePeopleOrTheSeats()
    {
        var real = Create().World;
        var old = Create(StartContracts.AllEndThisYear).World;

        Assert.Equal(
            real.Contracts.Select(contract => (contract.PersonId, contract.OrganizationId, contract.Role.ToString(), contract.Start)).ToArray(),
            old.Contracts.Select(contract => (contract.PersonId, contract.OrganizationId, contract.Role.ToString(), contract.Start)).ToArray());
        Assert.NotEqual(real.StateHash(), old.StateHash());
    }

    [Fact]
    public void TheSameInputsGiveTheSameHashWhateverTheOrderOfTheProvider()
    {
        Assert.Equal(Create().World.StateHash(), Create().World.StateHash());
        Assert.Equal(Create().World.StateHash(), Create(provider: Provider().Reversed()).World.StateHash());
    }

    [Fact]
    public void TheBetterRatedDriverIsFirstAndTiesGoToTheOlderThenToTheId()
    {
        var world = Create().World;

        Assert.Equal(SeatStatus.NumberOne, SeatOf(world, "r_a"));
        Assert.Equal(SeatStatus.NumberTwo, SeatOf(world, "r_b"));
        Assert.Equal(SeatStatus.NumberOne, SeatOf(world, "o_b"));
        Assert.Equal(SeatStatus.NumberTwo, SeatOf(world, "o_a"));
        // Equal ratings: g_b was born in 1965, g_a in 1970.
        Assert.Equal(SeatStatus.NumberOne, SeatOf(world, "g_b"));
        Assert.Equal(SeatStatus.NumberTwo, SeatOf(world, "g_a"));
    }

    [Fact]
    public void WhenRatingAndAgeAreEqualTheLowerIdIsFirst()
    {
        var provider = new FixtureProvider(
            [
                Driver("z_twin", 1970, Seat(Year, "red")),
                Driver("a_twin", 1970, Seat(Year, "red")),
            ],
            new Dictionary<string, DriverRating>(StringComparer.Ordinal)
            {
                ["z_twin"] = WorldInitFixtures.Rating(12, 13),
                ["a_twin"] = WorldInitFixtures.Rating(12, 13),
            });

        var world = Create(provider: provider).World;

        Assert.Equal(SeatStatus.NumberOne, SeatOf(world, "a_twin"));
        Assert.Equal(SeatStatus.NumberTwo, SeatOf(world, "z_twin"));
    }

    [Fact]
    public void AnAuthoredRoleBeatsTheDefaultAndThePartnerGetsTheOppositeRole()
    {
        var world = Create(roles: Roles(new DriverRoleEntry(Year, "o_a", "first"), new DriverRoleEntry(Year, "r_a", "second"))).World;

        Assert.Equal(SeatStatus.NumberOne, SeatOf(world, "o_a"));
        Assert.Equal(SeatStatus.NumberTwo, SeatOf(world, "o_b"));
        Assert.Equal(SeatStatus.NumberTwo, SeatOf(world, "r_a"));
        Assert.Equal(SeatStatus.NumberOne, SeatOf(world, "r_b"));
        // A team with no entry keeps the default.
        Assert.Equal(SeatStatus.NumberOne, SeatOf(world, "g_b"));
    }

    [Fact]
    public void AnAuthoredRoleOfAnotherStartYearIsIgnoredAndTwoFirstsLeaveTheDefaultToDecide()
    {
        var elsewhere = Create(roles: Roles(new DriverRoleEntry(Year + 1, "o_a", "first"))).World;
        Assert.Equal(SeatStatus.NumberOne, SeatOf(elsewhere, "o_b"));

        var both = Create(roles: Roles(new DriverRoleEntry(Year, "o_a", "first"), new DriverRoleEntry(Year, "o_b", "first"))).World;
        Assert.Equal(SeatStatus.NumberOne, SeatOf(both, "o_b"));
        Assert.Equal(SeatStatus.NumberTwo, SeatOf(both, "o_a"));
    }

    [Fact]
    public void TheRolesDoNotDependOnTheStartContractsOptionAndTheDefaultFileChangesNothing()
    {
        var real = Create().World;
        var old = Create(StartContracts.AllEndThisYear).World;

        foreach (var driver in new[] { "r_a", "r_b", "g_a", "g_b", "o_a", "o_b" })
        {
            Assert.Equal(SeatOf(real, driver), SeatOf(old, driver));
        }

        Assert.Equal(real.StateHash(), Create(roles: Roles()).World.StateHash());
    }

    [Fact]
    public void ATeamOfTwoGeneratedDriversHasNoHierarchyAndKeepsEqualSeats()
    {
        var world = Create().World;

        var silver = world.Contracts.Where(contract => contract.Role.IsDriver && contract.OrganizationId == OrganizationId.Real("silver")).ToArray();
        Assert.Equal(2, silver.Length);
        Assert.All(silver, contract => Assert.Equal(SeatStatus.Equal, contract.Role.Seat));
    }

    [Fact]
    public void TheDefaultOptionOfACareerWithGeneratedPeopleBuildsTheSameWorldAsTheOldOption()
    {
        var config = CareerConfig.FromPreset(CareerPreset.Chaos).WithStartYear(Year).WithPlayerTeam("red");

        var real = WorldInitializer.Create(config, Data(), EmptyPeopleProvider.Instance, Seed).World;
        var old = WorldInitializer.Create(config.WithStartContracts(StartContracts.AllEndThisYear), Data(), EmptyPeopleProvider.Instance, Seed).World;

        Assert.Equal(real.StateHash(), old.StateHash());
    }
}
