using Paddock.Application.Career;
using Paddock.Application.Cars;
using Paddock.Application.Racing;
using Paddock.Domain.Cars;
using Paddock.Domain.Finance;
using Paddock.Domain.Inbox;
using Paddock.Domain.People;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;
using Paddock.Simulation.Cars;
using Paddock.Simulation.Racing.Weekend;
using Paddock.Tests.Career;
using Paddock.Tests.Infrastructure;
using Paddock.Tests.Persistence;
using Paddock.Simulation.Career;
using Paddock.Domain.Career;
using Paddock.Domain.Spy;
using Paddock.Application.Access;
using static Paddock.Tests.Infrastructure.InfrastructureKit;

namespace Paddock.Tests.Cars;

/// <summary>Crash damage, repairs and spare chassis (#270). Every number under test is an ESTIMATE from <see cref="CarDamageEstimates"/>.</summary>
public sealed class CarDamageTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-damage-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Theory]
    [InlineData(DamageKind.Light)]
    [InlineData(DamageKind.Heavy)]
    [InlineData(DamageKind.Wrecked)]
    public void ARepairNeverTakesFewerThanFiveDaysAndGetsFasterWithTheEra(DamageKind kind)
    {
        var previous = int.MaxValue;
        foreach (var year in new[] { 1950, 1965, 1985, 2005, 2026 })
        {
            var days = CarDamageEstimates.Days(kind, year, 0);
            Assert.True(days >= CarDamageEstimates.MinDays);
            Assert.True(days <= previous);
            previous = days;
        }

        Assert.True(CarDamageEstimates.Days(DamageKind.Wrecked, 1950, 0) > CarDamageEstimates.Days(DamageKind.Light, 1950, 0));
        Assert.True(CarDamageEstimates.CostCents(kind, 100_000_000) > 0);
    }

    [Fact]
    public void ACrashThatHurtTheDriverIsNeverALightRepair()
    {
        for (var step = 0; step < 100; step++)
        {
            Assert.NotEqual(DamageKind.Light, CarDamageEstimates.Kind(step / 100d, 1955, driverHurt: true));
        }

        Assert.Equal(DamageKind.Light, CarDamageEstimates.Kind(0.99, 1955, driverHurt: false));
        Assert.Equal(DamageKind.Wrecked, CarDamageEstimates.Kind(0.0, 1955, driverHurt: false));
    }

    [Fact]
    public void TheDamageSectionRoundTripsThroughTheSaveWithTheSameHash()
    {
        var world = WorldFixtures.Small();
        var organization = world.Organizations[0].Id;
        var opening = WorldFixtures.Opening;
        var section = CarDamageSection.Empty
            .WithDamage(new CarDamage(CarIds.Format(3), organization, DamageKind.Heavy, DamageSource.Race, opening, opening.AddDays(14), 123_456))
            .WithSpare(new SpareChassis(organization, 1954, new CarConcept(0.1, -0.2, 0.3, 0, 0.5, -0.6), PerformanceLevels.Of(40, 41, 42, 43, 44)));
        world = world.WithSection(section);

        using var file = SaveFile.Create(Path.Combine(_directory, "damage.paddock"), WorldFixtures.Meta());
        var repository = new WorldRepository(file);
        repository.SaveWorld(world, opening);
        var loaded = repository.LoadWorld();

        Assert.Equal(world.StateHash(), loaded.StateHash());
        var restored = loaded.Section<CarDamageSection>(CarDamageSection.SectionName)!;
        Assert.Equal(DamageKind.Heavy, Assert.Single(restored.Damages).Kind);
        Assert.Equal(42d, restored.SpareOf(organization)!.Levels.MechanicalGrip);
    }

    [Fact]
    public void ACarRepairedBeforeTheRaceStartsAndOneStillInTheWorkshopWithoutASpareStaysOutWithAReason()
    {
        var (world, team) = TwoCarWorld();
        var raceDay = new GameDate(1955, 5, 22);
        var cars = world.Section<CarsSection>(CarsSection.SectionName)!.Of(team);
        var damage = CarDamageSection.Empty
            .WithDamage(new CarDamage(cars[0].Id, team, DamageKind.Light, DamageSource.Test, raceDay.AddDays(-10), raceDay, 1_000))
            .WithDamage(new CarDamage(cars[1].Id, team, DamageKind.Wrecked, DamageSource.Race, raceDay.AddDays(-10), raceDay.AddDays(20), 1_000));
        var field = RaceFieldBuilder.Build(world.WithSection(damage), raceDay, CareerKit.Data.RuleSetFor(1955), 10, null, null);

        Assert.Equal(cars[0].Id, Assert.Single(field.Entries).CarId);
        var absence = Assert.Single(field.Absences);
        Assert.Equal(cars[1].Id, absence.CarId);
        Assert.Equal(raceDay.AddDays(20), absence.ReadyOn);
        Assert.False(absence.HasSpare);
        Assert.True(field.SpareRuns.IsDefaultOrEmpty);
    }

    [Fact]
    public void WithASpareTheDamagedCarStartsSlowerAndTheOtherDamagedCarOfTheTeamStaysOut()
    {
        var (world, team) = TwoCarWorld();
        var raceDay = new GameDate(1955, 5, 22);
        var cars = world.Section<CarsSection>(CarsSection.SectionName)!.Of(team);
        var spare = new SpareChassis(team, 1954, cars[0].Concept, cars[0].Levels);
        var healthy = RaceFieldBuilder.Build(world, raceDay, CareerKit.Data.RuleSetFor(1955), 10, null, null);
        var damage = CarDamageSection.Empty
            .WithSpare(spare)
            .WithDamage(new CarDamage(cars[0].Id, team, DamageKind.Heavy, DamageSource.Race, raceDay.AddDays(-3), raceDay.AddDays(11), 1_000))
            .WithDamage(new CarDamage(cars[1].Id, team, DamageKind.Heavy, DamageSource.Race, raceDay.AddDays(-3), raceDay.AddDays(11), 1_000));
        var field = RaceFieldBuilder.Build(world.WithSection(damage), raceDay, CareerKit.Data.RuleSetFor(1955), 10, null, null);

        var run = Assert.Single(field.SpareRuns);
        Assert.Equal(cars[0].Id, run.CarId);
        var entry = Assert.Single(field.Entries);
        Assert.Equal(cars[0].Id, entry.CarId);
        var normal = healthy.Entries.First(item => item.CarId == cars[0].Id);
        Assert.True(entry.Car.Power < normal.Car.Power);
        var absence = Assert.Single(field.Absences);
        Assert.Equal(cars[1].Id, absence.CarId);
        Assert.True(absence.HasSpare);
    }

    [Fact]
    public void ATestCrashCostsMoneyAndKeepsTheCarOutAtLeastFiveDaysAndTheInboxSaysSo()
    {
        InfrastructureKit? hit = null;
        for (ulong seed = 1; seed < 200 && hit is null; seed++)
        {
            var kit = new InfrastructureKit(masterSeed: seed);
            kit.Submit(new Paddock.Application.Infrastructure.BookTestCommand { ManagerId = Anna, IssuedOn = Day(Opening), OrganizationId = Alfa.Value });
            kit.Live(Paddock.Domain.Infrastructure.InfrastructureEstimates.TestLeadDays + 1);
            if (kit.World.Section<CarDamageSection>(CarDamageSection.SectionName)?.Damages.Count > 0)
            {
                hit = kit;
            }
        }

        Assert.NotNull(hit);
        var section = hit.World.Section<CarDamageSection>(CarDamageSection.SectionName)!;
        var damage = section.Damages[0];
        Assert.Equal(DamageSource.Test, damage.Source);
        Assert.True(damage.CostCents > 0);
        Assert.True(damage.DamagedOn.DaysUntil(damage.ReadyOn) >= CarDamageEstimates.MinDays);
        Assert.Contains(hit.Book.Finance.EntriesOf(Alfa), entry => entry.Category == LedgerCategories.CarBuild && entry.AmountCents == -damage.CostCents);
        Assert.Contains(hit.Inbox.Section.ItemsOf(Anna.Value), item => item.Kind == CarDamageKeys.NoticeKind);
    }

    [Fact]
    public void ATestCrashIsTheSameForTheSameSeedAndTheTestIsNotRunOnAWorkshopCar()
    {
        string Run(ulong seed)
        {
            var kit = new InfrastructureKit(masterSeed: seed);
            kit.Submit(new Paddock.Application.Infrastructure.BookTestCommand { ManagerId = Anna, IssuedOn = Day(Opening), OrganizationId = Alfa.Value });
            kit.Live(Paddock.Domain.Infrastructure.InfrastructureEstimates.TestLeadDays + 1);
            return kit.World.StateHash();
        }

        Assert.Equal(Run(5), Run(5));

        var workshop = new InfrastructureKit();
        foreach (var car in workshop.Book.Cars.Of(Alfa))
        {
            var damaged = (workshop.World.Section<CarDamageSection>(CarDamageSection.SectionName) ?? CarDamageSection.Empty)
                .WithDamage(new CarDamage(car.Id, Alfa, DamageKind.Light, DamageSource.Race, Opening, Opening.AddDays(60), 1));
            workshop.Book.Store(workshop.World.WithSection(damaged));
        }

        var cash = workshop.Book.Finance.BalanceOf(Alfa);
        workshop.Submit(new Paddock.Application.Infrastructure.BookTestCommand { ManagerId = Anna, IssuedOn = Day(Opening), OrganizationId = Alfa.Value });
        workshop.Live(Paddock.Domain.Infrastructure.InfrastructureEstimates.TestLeadDays + 1);
        Assert.Contains(
            workshop.Inbox.Section.ItemsOf(Anna.Value),
            item => item.SubjectKey == Paddock.Application.Infrastructure.InfrastructureKeys.TestCarsDamagedSubject);
        Assert.DoesNotContain(
            workshop.Book.Finance.EntriesOf(Alfa),
            entry => entry.ReasonKey == Paddock.Application.Infrastructure.InfrastructureKeys.LedgerTest);
        Assert.True(workshop.Book.Finance.BalanceOf(Alfa) <= cash);
    }

    [Fact]
    public void ADamagedTeamThatCannotFieldACarIsToldWhyAndTheRaceStillRuns()
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1955, 7);
        var team = CareerTeams.Active(session.World, session.Date)[0].Id;
        var section = CarDamageSection.Empty;
        foreach (var car in session.World.Section<CarsSection>(CarsSection.SectionName)!.Of(team))
        {
            section = section.WithDamage(new CarDamage(car.Id, team, DamageKind.Wrecked, DamageSource.Race, session.Date, new GameDate(1955, 12, 1), 5_000));
        }

        session.StoreWorld(session.World.WithSection(section));
        CareerHost.RunUntil(session, new GameDate(1955, 3, 4), null, CareerKit.Options);

        var latest = session.World.Section<RaceResultsSection>(RaceResultsSection.SectionName)!.Latest()!;
        Assert.True(latest.Rows.Count > 1, "the race still ran");
        Assert.DoesNotContain(latest.Rows, row => row.TeamId == team.Value);
        var told = session.World.Section<InboxSection>(InboxSection.SectionName)!.Items
            .Where(item => item.Kind == CarDamageKeys.NoticeKind && item.Arguments["organization"] == team.Value)
            .ToArray();
        Assert.Equal(2, told.Count(item => item.SubjectKey == CarDamageKeys.MissedNoSpareSubject));
    }

    [Fact]
    public void WithASpareTheTeamRacesOnItAndIsToldItIsSlower()
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1955, 7);
        var team = CareerTeams.Active(session.World, session.Date)[0].Id;
        var cars = session.World.Section<CarsSection>(CarsSection.SectionName)!.Of(team);
        var section = CarDamageSection.Empty
            .WithSpare(new SpareChassis(team, 1954, cars[0].Concept, cars[0].Levels))
            .WithDamage(new CarDamage(cars[0].Id, team, DamageKind.Heavy, DamageSource.Race, session.Date, new GameDate(1955, 12, 1), 5_000));
        session.StoreWorld(session.World.WithSection(section));
        CareerHost.RunUntil(session, new GameDate(1955, 3, 4), null, CareerKit.Options);

        var latest = session.World.Section<RaceResultsSection>(RaceResultsSection.SectionName)!.Latest()!;
        Assert.Equal(2, latest.Rows.Count(row => row.TeamId == team.Value));
        Assert.Contains(
            session.World.Section<InboxSection>(InboxSection.SectionName)!.Items,
            item => item.SubjectKey == CarDamageKeys.SpareRunSubject && item.Arguments["organization"] == team.Value);
    }

    [Fact]
    public void ASeasonOfRacingPostsRepairBillsTellsTheTeamsKeepsSparesAndMatchesTheSameSeed()
    {
        string Season(out CareerSession session)
        {
            session = CareerKit.Open(CareerPreset.Chaos, 1955, 11);
            CareerHost.RunUntil(session, new GameDate(1956, 1, 2), null, CareerKit.Options);
            return session.World.StateHash();
        }

        var first = Season(out var run);
        Assert.Equal(first, Season(out _));

        var finance = run.World.Section<FinanceSection>(FinanceSection.SectionName)!;
        var bills = CareerTeams.Active(run.World, run.Date)
            .SelectMany(team => finance.EntriesOf(team.Id))
            .Where(entry => entry.Category == LedgerCategories.CarBuild && entry.ReasonKey is CarDamageKeys.LedgerRepair or CarDamageKeys.LedgerChassis)
            .ToArray();
        Assert.NotEmpty(bills);
        Assert.All(bills, bill => Assert.True(bill.AmountCents < 0));
        var told = run.World.Section<InboxSection>(InboxSection.SectionName)!.Items.Count(item => item.Kind == CarDamageKeys.NoticeKind);
        Assert.True(told >= bills.Length, "every bill has a notice for the team");
        var spares = run.World.Section<CarDamageSection>(CarDamageSection.SectionName)!.Spares;
        Assert.NotEmpty(spares);
    }

    private static (WorldState World, OrganizationId Team) TwoCarWorld()
    {
        var today = new GameDate(1955, 1, 1);
        var world = WorldState.At(today);
        OrganizationId team;
        (world, team) = world.AddOrganization(new OrganizationSpec(
            OrganizationKind.Team, true, "mercedes", today, null, 1_000_000, [new("Mercedes", today, null)]));
        PersonId one;
        PersonId two;
        (world, one) = world.AddPerson(new PersonSpec("Driver", "One", new GameDate(1920, 1, 1), "DEU", false, null, [PersonRole.Driver], Truth(15)));
        (world, two) = world.AddPerson(new PersonSpec("Driver", "Two", new GameDate(1922, 1, 1), "DEU", false, null, [PersonRole.Driver], Truth(14)));
        (world, _) = world.AddContract(new ContractSpec(one, team, ContractRole.Driver(SeatStatus.NumberOne), today, new GameDate(1955, 12, 31), 100_000, true, null, null));
        (world, _) = world.AddContract(new ContractSpec(two, team, ContractRole.Driver(SeatStatus.NumberTwo), today, new GameDate(1955, 12, 31), 100_000, true, null, null));
        var cars = CarsSection.Empty
            .Add(Car(CarIds.Format(1), team, one))
            .Add(Car(CarIds.Format(2), team, two));
        return (world.WithSection(cars), team);
    }

    private static TeamCar Car(string id, OrganizationId team, PersonId driver) =>
        new(id, team, 1955, CarConcept.Neutral, ConceptMapping.StartingLevels(CarConcept.Neutral, 60), 60, CarEstimates.InitialUnderstanding, 1.0, 0, null, driver);

    private static PersonTruth Truth(int value)
    {
        var list = new[] { "cornering", "braking", "smoothness", "overtaking", "defending", "consistency", "composure", "adaptability", "wet_weather", "fitness", "feedback" }
            .Select(name => new NamedAttribute(name, value))
            .ToList();
        return new PersonTruth(list, list);
    }
}
