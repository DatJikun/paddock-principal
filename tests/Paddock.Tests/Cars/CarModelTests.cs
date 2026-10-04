using System.Reflection;
using Paddock.Application.Access;
using Paddock.Application.Cars;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Managers;
using Paddock.Application.World;
using Paddock.Domain.Cars;
using Paddock.Domain.People;
using Paddock.Domain.Random;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;
using Paddock.Simulation.Cars;
using Paddock.Simulation.Racing.Pace;
using Paddock.Tests.Persistence;
using AccessManagerId = Paddock.Application.Access.ManagerId;

namespace Paddock.Tests.Cars;

public class CarModelTests
{
    private static readonly GameDate Opening = GameDate.SeasonStart(1955);
    private static readonly DateOnly Day = new(1955, 6, 1);
    private static readonly DriverAttributes Ten = new(10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10);

    [Fact]
    public void EachPoleWinsOneSituationAndLosesAnother()
    {
        var ceiling = 70d;
        var drag = ConceptMapping.Effects(Concept(aero: -1), ceiling);
        var downforce = ConceptMapping.Effects(Concept(aero: 1), ceiling);
        Assert.True(drag.Full.Power > downforce.Full.Power);
        Assert.True(downforce.Full.Downforce > drag.Full.Downforce);

        var margin = ConceptMapping.Effects(Concept(cooling: 1), ceiling);
        var edge = ConceptMapping.Effects(Concept(cooling: -1), ceiling);
        Assert.True(margin.Full.Reliability > edge.Full.Reliability);
        Assert.True(edge.Full.Power > margin.Full.Power);

        var wide = ConceptMapping.Effects(Concept(window: -1), ceiling);
        var narrow = ConceptMapping.Effects(Concept(window: 1), ceiling);
        Assert.True(wide.Stability > narrow.Stability);
        Assert.True(narrow.Peak > wide.Peak);

        var kind = ConceptMapping.Effects(Concept(tyre: 1), ceiling);
        var aggressive = ConceptMapping.Effects(Concept(tyre: -1), ceiling);
        Assert.True(kind.TyreWearMultiplier < aggressive.TyreWearMultiplier);
        Assert.True(aggressive.SingleLapAdvantage > kind.SingleLapAdvantage);

        var tailored = ConceptMapping.Effects(Concept(integration: -1), ceiling);
        var universal = ConceptMapping.Effects(Concept(integration: 1), ceiling);
        Assert.True(tailored.MatchedEngineBonus > universal.MatchedEngineBonus);
        Assert.True(universal.SupplierChangeCost < tailored.SupplierChangeCost);
    }

    [Fact]
    public void EvolutionStartsHigherAndEndsLowerThanRevolutionOverAThousandSeeds()
    {
        double evolutionStart = 0;
        double revolutionStart = 0;
        double evolutionEnd = 0;
        double revolutionEnd = 0;
        for (ulong seed = 1; seed <= 1000; seed++)
        {
            var evolution = ConceptMapping.DrawPotential(RngStreams.Derive(seed, RngStreamName.Development, 1955), -1, 1);
            var revolution = ConceptMapping.DrawPotential(RngStreams.Derive(seed, RngStreamName.Development, 1955), 1, 1);
            evolutionStart += evolution.StartLevel;
            revolutionStart += revolution.StartLevel;
            evolutionEnd += evolution.Ceiling;
            revolutionEnd += revolution.Ceiling;
        }

        Assert.True(evolutionStart > revolutionStart);
        Assert.True(evolutionEnd < revolutionEnd);
    }

    [Fact]
    public void DownforceAboveTheEraCapGivesNoGain()
    {
        var limits = new EraPerformanceLimits(40);
        var capped = Car("car:1", downforce: 40);
        var extra = Car("car:1", downforce: 95);
        var low = CarPerformanceFor.Resolve(capped, limits);
        var high = CarPerformanceFor.Resolve(extra, limits);
        Assert.Equal(40, low.Downforce);
        Assert.Equal(low.Downforce, high.Downforce);

        var track = new TrackLayout(
            "synthetic",
            "synthetic",
            5,
            new Dictionary<string, double> { ["straights"] = 0.25, ["high_speed"] = 0.25, ["low_speed"] = 0.25, ["braking"] = 0.25 },
            []);
        var driver = new DriverPace(50, 50, 50);
        double Lap(CarPerformance car) => LapTimeModel.Compute(new LapInputs
        {
            Track = track,
            Season = 1955,
            Limits = limits,
            Car = car,
            Driver = driver,
        }).TotalSeconds;
        Assert.Equal(Lap(low), Lap(high));
    }

    [Fact]
    public void ADevelopmentDrawDoesNotMoveAnotherTeamOrThePeopleStream()
    {
        var people = RngStreams.Derive(7, RngStreamName.People, 1955).NextULong();
        var parent = RngStream.Derive(7, RngStreamName.Development, 1955);
        var other = parent.DeriveChild("ferrari").NextULong();
        var own = parent.DeriveChild("mercedes");
        for (var i = 0; i < 40; i++)
        {
            _ = own.NextULong();
        }

        Assert.Equal(other, parent.DeriveChild("ferrari").NextULong());
        Assert.NotEqual(people, RngStreams.Derive(7, RngStreamName.Development, 1955).NextULong());
        Assert.Equal(people, RngStreams.Derive(7, RngStreamName.People, 1955).NextULong());
        Assert.Contains(RngStreamName.Development, RngStreamName.All);
    }

    [Fact]
    public void OpeningCarsAreTwoPerTeamAndMercedesIsStrongerIn1955()
    {
        var world = Grid();
        var cars = world.Section<CarsSection>(CarsSection.SectionName)!;
        var mercedes = cars.Of(OrganizationId.Real("mercedes"));
        var ferrari = cars.Of(OrganizationId.Real("ferrari"));
        Assert.Equal(2, mercedes.Count);
        Assert.Equal(2, ferrari.Count);
        Assert.Equal(78, mercedes[0].ConceptCeiling);
        Assert.Equal(mercedes[0].ConceptCeiling, mercedes[1].ConceptCeiling);
        Assert.Equal(50, ferrari[0].ConceptCeiling);
        Assert.True(mercedes[0].ConceptCeiling > ferrari[0].ConceptCeiling);
        Assert.Equal([PersonId.Real("moss"), PersonId.Real("collins")], mercedes.Select(car => car.Driver));
        Assert.DoesNotContain(mercedes, car => car.Driver == PersonId.Real("behra"));
    }

    [Fact]
    public void AGeneratedWorldIgnoresTheHistoricalStrengthTable()
    {
        var source = new MapCarStrengthSource(("mercedes", 1955, 99));
        var historical = InitialCarFactory.Install(TeamOnly(), 5, 1955, fullyGenerated: false, source, []);
        var generated = InitialCarFactory.Install(TeamOnly(), 5, 1955, fullyGenerated: true, source, []);
        var again = InitialCarFactory.Install(TeamOnly(), 5, 1955, fullyGenerated: true, source, []);
        Assert.Equal(99, Ceiling(historical));
        Assert.NotEqual(99, Ceiling(generated));
        Assert.Equal(generated.StateHash(), again.StateHash());
        Assert.NotEqual(generated.StateHash(), InitialCarFactory.Install(TeamOnly(), 6, 1955, true, source, []).StateHash());
    }

    [Fact]
    public void ApprovingAConceptKeepsTheDriversAndRepeats()
    {
        var first = Play();
        var second = Play();
        Assert.Equal(first, second);

        static string Play()
        {
            var lab = new Lab(Grid());
            var before = lab.Cars.Of(OrganizationId.Real("mercedes")).Select(car => car.Driver).ToArray();
            var result = lab.Submit(new ApproveConceptCommand
            {
                ManagerId = lab.Anna,
                IssuedOn = Day,
                OrganizationId = "mercedes",
                AeroMilli = 1000,
                PhilosophyMilli = -1000,
            });
            Assert.IsType<CommandResult.Accepted>(result);
            var after = lab.Cars.Of(OrganizationId.Real("mercedes"));
            Assert.Equal(before, after.Select(car => car.Driver));
            Assert.Equal(1000, CarEstimates.Milli(after[0].Concept.Aero));
            Assert.Equal(CarEstimates.NewConceptUnderstanding, after[0].Understanding);
            Assert.Equal(after[0].ConceptCeiling, after[1].ConceptCeiling);
            return lab.World.StateHash();
        }
    }

    [Fact]
    public void ACustomerCarIsRefusedAndTheWorldDoesNotChange()
    {
        Assert.Equal(CarKeys.CustomerNotInMvp, CustomerChassisRules.Refusal(mvpAllows: false, regulationAllows: true));
        Assert.Equal(CarKeys.CustomerForbidden, CustomerChassisRules.Refusal(mvpAllows: true, regulationAllows: false));
        Assert.Null(CustomerChassisRules.Refusal(mvpAllows: true, regulationAllows: true));

        var lab = new Lab(Grid());
        var before = lab.World.StateHash();
        var refused = lab.Submit(new AcquireCustomerCarCommand
        {
            ManagerId = lab.Anna,
            IssuedOn = Day,
            OrganizationId = "mercedes",
            SellerId = "ferrari",
        });
        var rejected = Assert.IsType<CommandResult.Rejected>(refused);
        Assert.Equal(CarKeys.CustomerNotInMvp, rejected.Reason.Key);
        Assert.Equal(before, lab.World.StateHash());
        Assert.Empty(lab.Dispatcher.Log.Entries);
    }

    [Fact]
    public void ARivalViewHasNoCeilingAndBandsShrinkWithABetterTechnicalDirector()
    {
        var weak = CarKnowledgeBands.Around(60, technicalDirectorVision: 8, aeroHead: 10);
        var strong = CarKnowledgeBands.Around(60, technicalDirectorVision: 18, aeroHead: 10);
        Assert.True(strong.High - strong.Low < weak.High - weak.Low);
        Assert.True(strong.High > strong.Low);

        var lab = new Lab(Grid());
        var hash = lab.World.StateHash();
        var manager = lab.Query.View(AccessContext.ForManager(new AccessManagerId(lab.Anna.Value)));
        var ai = lab.Query.View(AccessContext.ForAi(new AccessManagerId(lab.Anna.Value)));
        Assert.Equal(hash, lab.World.StateHash());
        Assert.Equal(2, manager.Own.Count);
        Assert.Equal(manager.Own.Select(car => car.CarId), ai.Own.Select(car => car.CarId));
        Assert.All(manager.Own, car => Assert.True(car.Ceiling.High > car.Ceiling.Low));
        Assert.Equal(["ferrari"], manager.Rivals.Select(car => car.OrganizationId).Distinct());
        Assert.DoesNotContain(typeof(RivalCarView).GetProperties(), property => property.Name.Contains("Ceiling", StringComparison.Ordinal));
        Walk(typeof(ManagerCarRoster));
        Walk(typeof(OwnCarView));
        Walk(typeof(RivalCarView));
    }

    [Fact]
    public void AMatchingCarIsFasterAndCalmerThanAMismatch()
    {
        var driver = new DriverFitProfile(
            PersonId.Real("moss"),
            new DriverHandlingPreferences(0, 0, BrakingStyle.Normal),
            new DriverExperience(10, 2, 3, [new TrackLaps("monza", 40)]));
        var car = Car("car:1", downforce: 50);
        var match = DriverCarFit.Evaluate(driver, car);
        var mismatch = DriverCarFit.Evaluate(
            new DriverFitProfile(driver.Person, new DriverHandlingPreferences(-1, 1, BrakingStyle.Late), driver.Experience),
            car);
        Assert.Equal(0, match.PaceModifierSeconds);
        Assert.True(match.ConfidenceDelta > mismatch.ConfidenceDelta);
        Assert.True(match.PaceModifierSeconds > mismatch.PaceModifierSeconds);
    }

    [Fact]
    public void TheEraBalanceRatioIsAnEstimateFromTheRatingsModel()
    {
        Assert.Contains("RatingsModel", DriverCarBalance.Source, StringComparison.Ordinal);
        Assert.Contains("ESTIMATE", DriverCarBalance.Source, StringComparison.Ordinal);
        Assert.Equal(1.15, DriverCarBalance.CarToDriverSpread(1955));
        Assert.Equal(0.85, DriverCarBalance.CarToDriverSpread(2000));
        Assert.Equal(DriverCarBalance.CarToDriverSpread(2022), DriverCarBalance.CarToDriverSpread(2026));
    }

    [Fact]
    public void CommandsRoundTripThroughTheCodec()
    {
        var manager = new Paddock.Application.Managers.ManagerId("human:anna");
        ICommand[] commands =
        [
            new ApproveConceptCommand
            {
                ManagerId = manager,
                IssuedOn = Day,
                OrganizationId = "mercedes",
                AeroMilli = -250,
                PhilosophyMilli = 1000,
                WindowMilli = 0,
                CoolingMilli = -1000,
                TyreMilli = 500,
                IntegrationMilli = 0,
                SubmissionNumber = 3,
            },
            new AcquireCustomerCarCommand
            {
                ManagerId = manager,
                IssuedOn = Day,
                OrganizationId = "ferrari",
                SellerId = "maserati",
                SubmissionNumber = 4,
            },
        ];
        foreach (var command in commands)
        {
            var encoded = CommandCodec.Production.Encode(command);
            var decoded = CommandCodec.Production.Decode(encoded.Tag, encoded.Text, command.ManagerId, command.SubmissionNumber, command.IssuedOn);
            Assert.Equal(command, decoded);
        }
    }

    private static CarConcept Concept(double aero = 0, double philosophy = 0, double window = 0, double cooling = 0, double tyre = 0, double integration = 0) =>
        new(aero, philosophy, window, cooling, tyre, integration);

    private static TeamCar Car(string id, double downforce) =>
        new(id, OrganizationId.Real("mercedes"), 1955, CarConcept.Neutral, PerformanceLevels.Of(50, downforce, 50, 50, 50), 70, 100, 1, 60, null, PersonId.Real("moss"));

    private static double Ceiling(WorldState world)
    {
        var cars = world.Section<CarsSection>(CarsSection.SectionName)!.Cars;
        Assert.Equal(2, cars.Count);
        Assert.Equal(cars[0].ConceptCeiling, cars[1].ConceptCeiling);
        return cars[0].ConceptCeiling;
    }

    private static WorldState TeamOnly()
    {
        var (world, _) = WorldState.At(Opening).AddOrganization(Team("mercedes", "Mercedes"));
        return world;
    }

    private static WorldState Grid()
    {
        var world = WorldState.At(Opening);
        (world, var mercedes) = world.AddOrganization(Team("mercedes", "Mercedes"));
        (world, var ferrari) = world.AddOrganization(Team("ferrari", "Ferrari"));
        (world, var moss) = world.AddPerson(Driver("moss", "Stirling", "Moss"));
        (world, var collins) = world.AddPerson(Driver("collins", "Peter", "Collins"));
        (world, var behra) = world.AddPerson(Driver("behra", "Jean", "Behra"));
        (world, var hawthorn) = world.AddPerson(Driver("hawthorn", "Mike", "Hawthorn"));
        (world, var castel) = world.AddPerson(Driver("castellotti", "Eugenio", "Castellotti"));
        (world, var director) = world.AddPerson(Staff("uff", "Uff", "Director", StaffRole.TechnicalDirector, "vision", 18));
        world = Seat(world, moss, mercedes, SeatStatus.NumberOne);
        world = Seat(world, collins, mercedes, SeatStatus.NumberTwo);
        world = Seat(world, behra, mercedes, SeatStatus.Reserve);
        world = Seat(world, hawthorn, ferrari, SeatStatus.NumberOne);
        world = Seat(world, castel, ferrari, SeatStatus.NumberTwo);
        world = Employ(world, director, mercedes, StaffRole.TechnicalDirector);
        return InitialCarFactory.Install(world, 11, 1955, fullyGenerated: false, EstimateCarStrength.Shared, []);
    }

    private static WorldState Seat(WorldState world, PersonId person, OrganizationId team, SeatStatus seat)
    {
        (world, _) = world.AddContract(new ContractSpec(person, team, ContractRole.Driver(seat), Opening, GameDate.SeasonEnd(1955), 1000, true, null, null));
        return world;
    }

    private static WorldState Employ(WorldState world, PersonId person, OrganizationId team, StaffRole role)
    {
        (world, _) = world.AddContract(new ContractSpec(person, team, ContractRole.Staff(role), Opening, GameDate.SeasonEnd(1955), 1000, true, null, null));
        return world;
    }

    private static OrganizationSpec Team(string id, string name) =>
        new(OrganizationKind.Team, true, id, Opening, null, 0, [new OrganizationNameSpan(name, Opening, null)]);

    private static PersonSpec Driver(string id, string given, string family) =>
        new(given, family, new GameDate(1924, 1, 1), "GB", true, id, [PersonRole.Driver], PersonTruth.FromDriver(Ten, Ten));

    private static PersonSpec Staff(string id, string given, string family, StaffRole role, string key, int value)
    {
        NamedAttribute[] attributes = [new(key, value), new("project_management", 10), new("innovation", 10)];
        return new PersonSpec(given, family, new GameDate(1910, 1, 1), "DE", true, id, [PersonRole.Staff(role)], new PersonTruth(attributes, attributes));
    }

    private static void Walk(Type type)
    {
        var forbidden = new HashSet<Type>
        {
            typeof(TeamCar),
            typeof(CarPerformance),
            typeof(PerformanceLevels),
            typeof(CarsSection),
            typeof(CarConcept),
        };
        var seen = new HashSet<Type>();
        void Visit(Type current)
        {
            if (!seen.Add(current))
            {
                return;
            }

            Assert.DoesNotContain(current, forbidden);
            if (current.IsGenericType)
            {
                foreach (var argument in current.GetGenericArguments())
                {
                    Visit(argument);
                }
            }

            if (current.HasElementType)
            {
                Visit(current.GetElementType()!);
            }

            if (current.Namespace is { } ns && ns.StartsWith("Paddock.", StringComparison.Ordinal))
            {
                foreach (var property in current.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    Visit(property.PropertyType);
                }
            }
        }

        Visit(type);
    }

    private sealed class Lab
    {
        public Lab(WorldState world)
        {
            World = world;
            Book = new CarBook(() => World, value => World = value, 42);
            Control = new ControlTable().Assign(Anna, OrganizationId.Real("mercedes"));
            Managers = new ManagerRegistry();
            Managers.Register(Anna, ManagerKind.Human, "Anna");
            Dispatcher = new CommandDispatcher();
            CarCommands.Register(Dispatcher, Book, Control);
            Query = new CarQuery(Book, Control);
        }

        public Paddock.Application.Managers.ManagerId Anna { get; } = new("human:anna");

        public WorldState World { get; private set; }

        public CarBook Book { get; }

        public ControlTable Control { get; }

        public ManagerRegistry Managers { get; }

        public CommandDispatcher Dispatcher { get; }

        public CarQuery Query { get; }

        public CarsSection Cars => Book.Section;

        public CommandResult Submit(ICommand command) =>
            Dispatcher.Dispatch(command.WithSubmissionNumber(1), new CommandContext(new FrozenWorld(Day), Managers));
    }

    private sealed class FrozenWorld(DateOnly date) : IWorldState
    {
        public DateOnly CurrentDate => date;

        public void AdvanceDate()
        {
        }

        public string ContentHash() => "unused";
    }
}

public class CarPersistenceTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-cars-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void ACarSectionRoundTripsWithTheSameHash()
    {
        var profile = new DriverFitProfile(
            PersonId.Real("moss"),
            new DriverHandlingPreferences(-0.25, 0.5, BrakingStyle.Late),
            new DriverExperience(8, 2, 1, [new TrackLaps("monza", 44)]));
        var (withTeam, _) = WorldState.At(WorldFixtures.Opening).AddOrganization(
            new OrganizationSpec(OrganizationKind.Team, true, "mercedes", WorldFixtures.Opening, null, 0, [new OrganizationNameSpan("Mercedes", WorldFixtures.Opening, null)]));
        var world = InitialCarFactory.Install(withTeam, 3, 1955, false, EstimateCarStrength.Shared, [profile]).WithDate(WorldFixtures.Opening);
        using var file = SaveFile.Create(Path.Combine(_directory, "cars.paddock"), WorldFixtures.Meta());
        var repository = new WorldRepository(file);
        repository.SaveWorld(world, WorldFixtures.Opening);
        var loaded = repository.LoadWorld();
        Assert.Equal(world.StateHash(), loaded.StateHash());
        var restored = loaded.Section<CarsSection>(CarsSection.SectionName)!;
        Assert.Equal(2, restored.Cars.Count);
        Assert.Equal(44, restored.FitOf(PersonId.Real("moss"))!.Experience.LapsByTrack[0].Laps);
        Assert.Equal(78, restored.Of(OrganizationId.Real("mercedes"))[0].ConceptCeiling);
    }
}
