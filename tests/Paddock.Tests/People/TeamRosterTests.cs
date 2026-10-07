using Paddock.Application.Staff;
using Paddock.Domain.People;
using Paddock.Domain.Random;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Tests.People;

/// <summary>#265: a team has exactly two race engineers, and only roles something reads are on the roster.</summary>
public class TeamRosterTests
{
    private static (WorldState World, OrganizationId Team) TeamWithFourDrivers(GameDate on)
    {
        var world = WorldState.At(on);
        (world, var team) = world.AddOrganization(new OrganizationSpec(
            OrganizationKind.Team, true, "alpha", on, null, 0, [new OrganizationNameSpan("Alpha", on, null)]));
        var seats = new[] { SeatStatus.Equal, SeatStatus.Equal, SeatStatus.Reserve, SeatStatus.Reserve };
        for (var i = 0; i < seats.Length; i++)
        {
            var truth = PersonTruth.FromDriver(
                new DriverAttributes(12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12),
                new DriverAttributes(14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14));
            (world, var driver) = world.AddPerson(new PersonSpec("Dan", "Driver" + i, new GameDate(1930, 1, 1), "GBR", false, null, [PersonRole.Driver], truth));
            (world, _) = world.AddContract(new ContractSpec(driver, team, ContractRole.Driver(seats[i]), on, GameDate.SeasonEnd(on.Year), 0, true, null, null));
        }

        return (world, team);
    }

    [Fact]
    public void AReserveDriverDoesNotGetARaceEngineerSoATeamHasTwo()
    {
        var on = GameDate.SeasonStart(1955);
        var (world, team) = TeamWithFourDrivers(on);

        var filled = StaffRoster.Fill(world, RngStream.Derive(3, RngStreamName.People, 1955), new FixtureNameSource(), null, on);

        var engineers = filled.World.Contracts.Count(contract =>
            contract.OrganizationId == team && contract.Role.IsStaff && contract.Role.StaffRole == StaffRole.RaceEngineer);
        Assert.Equal(2, engineers);
        Assert.False(StaffRoster.HasVacancy(filled.World, on));
    }

    [Fact]
    public void RolesNothingReadsAreNeitherHiredNorShown()
    {
        var on = GameDate.SeasonStart(1955);
        var (world, team) = TeamWithFourDrivers(on);

        var filled = StaffRoster.Fill(world, RngStream.Derive(3, RngStreamName.People, 1955), new FixtureNameSource(), null, on);

        var roles = filled.World.Contracts
            .Where(contract => contract.OrganizationId == team && contract.Role.IsStaff)
            .Select(contract => contract.Role.StaffRole)
            .ToHashSet();
        Assert.DoesNotContain(StaffRole.Strategist, roles);
        Assert.DoesNotContain(StaffRole.ChiefMechanic, roles);
        Assert.Equal(
            [StaffRole.TechnicalDirector, StaffRole.ChiefDesigner, StaffRole.HeadOfAerodynamics, StaffRole.HeadOfVehicleDynamics, StaffRole.RaceEngineer, StaffRole.Scout, StaffRole.CommercialDirector],
            StaffCatalogue.TeamRoster);

        var listed = StaffQuery.Of(filled.World, team, on);
        Assert.DoesNotContain(listed, person => person.Role is nameof(StaffRole.Strategist) or nameof(StaffRole.ChiefMechanic));
        Assert.All(listed, person => Assert.NotNull(person.Overall));
    }
}
