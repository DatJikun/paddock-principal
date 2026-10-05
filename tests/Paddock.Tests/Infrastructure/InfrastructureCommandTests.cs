using Paddock.Application.Access;
using Paddock.Application.Commands;
using Paddock.Application.Infrastructure;
using Paddock.Domain.Finance;
using Paddock.Domain.Infrastructure;
using AccessManagerId = Paddock.Application.Access.ManagerId;
using static Paddock.Tests.Infrastructure.InfrastructureKit;

namespace Paddock.Tests.Infrastructure;

/// <summary>Upgrade, book-test, refusals, inbox on finish, and the manager query. SYNTHETIC fixtures.</summary>
public class InfrastructureCommandTests
{
    private static string? Key(CommandResult result) => (result as CommandResult.Rejected)?.Reason.Key;

    [Fact]
    public void EveryTeamStartsWithAFactoryAndNoDyno()
    {
        var kit = new InfrastructureKit();
        Assert.NotNull(kit.Book.Section.Find(Alfa, FacilityKind.Factory));
        Assert.Null(kit.Book.Section.Find(Alfa, FacilityKind.WindTunnel));
        Assert.Equal(50_000, kit.Book.Section.Find(Alfa, FacilityKind.Factory)!.QualityMilli);
        Assert.Equal(40_000, kit.Book.Section.Find(Beta, FacilityKind.Factory)!.QualityMilli);
    }

    [Fact]
    public void UpgradingTheFactorySpendsMoneyAndDegradesItUntilTheEndDay()
    {
        var kit = new InfrastructureKit();
        var before = kit.Book.Finance.BalanceOf(Alfa);
        var quality = kit.Book.Section.Find(Alfa, FacilityKind.Factory)!.QualityMilli;
        var cost = InfrastructureMath.UpgradeCostCents(quality, 1955, kit.Book.Finance.TypicalCents);
        var days = InfrastructureMath.UpgradeDays(quality, 1955);
        Assert.IsType<CommandResult.Accepted>(kit.Submit(new UpgradeFacilityCommand
        {
            ManagerId = Anna,
            IssuedOn = Day(Opening),
            OrganizationId = Alfa.Value,
            Kind = FacilityKindIds.Factory,
        }));
        var building = kit.Book.Section.Find(Alfa, FacilityKind.Factory)!;
        Assert.True(building.IsBuilding);
        Assert.Equal(before - cost, kit.Book.Finance.BalanceOf(Alfa));
        Assert.Contains(kit.Book.Finance.EntriesOf(Alfa), entry => entry.Category == LedgerCategories.Infrastructure);

        kit.Live(days + 1);
        var done = kit.Book.Section.Find(Alfa, FacilityKind.Factory)!;
        Assert.False(done.IsBuilding);
        Assert.True(done.QualityMilli > quality);
        Assert.Contains(kit.Inbox.Section.ItemsOf(Anna.Value), item => item.Kind == InfrastructureKeys.NoticeKind);
    }

    [Fact]
    public void AWindTunnelBeforeItsEraAndASecondBuildAreRefused()
    {
        var kit = new InfrastructureKit();
        Assert.Equal(InfrastructureKeys.EraNotReached, Key(kit.Submit(new UpgradeFacilityCommand
        {
            ManagerId = Anna,
            IssuedOn = Day(Opening),
            OrganizationId = Alfa.Value,
            Kind = FacilityKindIds.WindTunnel,
        })));
        Assert.IsType<CommandResult.Accepted>(kit.Submit(new UpgradeFacilityCommand
        {
            ManagerId = Anna,
            IssuedOn = Day(Opening),
            OrganizationId = Alfa.Value,
            Kind = FacilityKindIds.Factory,
        }));
        Assert.Equal(InfrastructureKeys.AlreadyBuilding, Key(kit.Submit(new UpgradeFacilityCommand
        {
            ManagerId = Anna,
            IssuedOn = Day(Opening),
            OrganizationId = Alfa.Value,
            Kind = FacilityKindIds.Factory,
        })));
        Assert.Equal(InfrastructureKeys.NoControl, Key(kit.Submit(new UpgradeFacilityCommand
        {
            ManagerId = Anna,
            IssuedOn = Day(Opening),
            OrganizationId = Beta.Value,
            Kind = FacilityKindIds.Factory,
        })));
    }

    [Fact]
    public void BookingATestCostsMoneyAndRaisesUnderstanding()
    {
        var kit = new InfrastructureKit();
        var car = kit.Book.Cars.Of(Alfa)[0];
        var lowered = car.WithDesign(car.Season, car.Concept, car.Levels, car.ConceptCeiling, 10d, car.TyreWearMultiplier, car.SupplierChangeCost);
        kit.Book.Write(kit.Book.Section, cars: kit.Book.Cars.Replace(lowered));
        var beforeCash = kit.Book.Finance.BalanceOf(Alfa);
        var before = kit.Book.Cars.Of(Alfa)[0].Understanding;
        Assert.IsType<CommandResult.Accepted>(kit.Submit(new BookTestCommand
        {
            ManagerId = Anna,
            IssuedOn = Day(Opening),
            OrganizationId = Alfa.Value,
        }));
        Assert.True(kit.Book.Finance.BalanceOf(Alfa) < beforeCash);
        Assert.True(kit.Book.Cars.Of(Alfa)[0].Understanding > before);
        Assert.Equal(1, kit.Book.Section.TestsUsed(Alfa, 1955));
    }

    [Fact]
    public void BannedTestingAndAMissingCarAreRefused()
    {
        var banned = new InfrastructureKit(testing: "in_season_banned");
        Assert.Equal(InfrastructureKeys.TestsBanned, Key(banned.Submit(new BookTestCommand
        {
            ManagerId = Anna,
            IssuedOn = Day(Opening),
            OrganizationId = Alfa.Value,
        })));

        var noCars = new InfrastructureKit(cars: false);
        Assert.Equal(InfrastructureKeys.NoCars, Key(noCars.Submit(new BookTestCommand
        {
            ManagerId = Anna,
            IssuedOn = Day(Opening),
            OrganizationId = Alfa.Value,
        })));
    }

    [Fact]
    public void TheQueryShowsOnlyTheManagersFactoryAndThePostedTestCost()
    {
        var kit = new InfrastructureKit();
        var view = new InfrastructureQuery(kit.Book, kit.Environment).View(
            AccessContext.ForManager(new AccessManagerId(Anna.Value)),
            nextCircuitCountry: LogisticsMath.Argentina);
        var own = Assert.Single(view.Own);
        Assert.Equal(Alfa.Value, own.OrganizationId);
        Assert.Contains(own.Facilities, row => row.Kind == FacilityKindIds.Factory && row.Eligible);
        Assert.True(own.Tests.Allowed);
        Assert.True(own.Tests.CostCents > 0);
        Assert.Equal("ITA", own.HomeCountry);
        Assert.Equal("ship", own.NextTransport!.Mode);
        Assert.Equal(InfrastructureEstimates.LogisticsShipDays, own.NextTransport.Days);
    }

    [Fact]
    public void UpgradeAndBookTestRoundTripThroughTheCodec()
    {
        var manager = Anna;
        var issued = Day(Opening);
        ICommand[] commands =
        [
            new UpgradeFacilityCommand { ManagerId = manager, IssuedOn = issued, OrganizationId = Alfa.Value, Kind = FacilityKindIds.Factory, SubmissionNumber = 3 },
            new BookTestCommand { ManagerId = manager, IssuedOn = issued, OrganizationId = Alfa.Value, SubmissionNumber = 4 },
        ];
        foreach (var command in commands)
        {
            var encoded = CommandCodec.Production.Encode(command);
            var decoded = CommandCodec.Production.Decode(encoded.Tag, encoded.Text, command.ManagerId, command.SubmissionNumber, command.IssuedOn);
            Assert.Equal(command, decoded);
        }
    }
}
