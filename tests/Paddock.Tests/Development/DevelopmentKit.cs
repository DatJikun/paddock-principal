using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Development;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Application.World;
using Paddock.Domain.Cars;
using Paddock.Domain.Contracts;
using Paddock.Domain.Development;
using Paddock.Domain.Finance;
using Paddock.Domain.People;
using Paddock.Domain.Spy;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Cars;
using Paddock.Simulation.Time;

namespace Paddock.Tests.Development;

/// <summary>Rules a test sets by year. Everything not set is unrestricted and unchanged.</summary>
internal sealed class FakeRules : IDevelopmentRules
{
    private readonly Dictionary<int, DevelopmentEra> _eras = [];

    public FakeRules Set(int year, DevelopmentEra era)
    {
        _eras[year] = era;
        return this;
    }

    public DevelopmentEra Era(int year) => _eras.TryGetValue(year, out var era) ? era : DevelopmentEra.Unrestricted;

    public static DevelopmentEra WithFingerprint(int changed, int total, string? testing = null)
    {
        var values = new Dictionary<string, string>();
        for (var i = 0; i < total; i++)
        {
            values["dim" + i] = i < changed ? "new" : "old";
        }

        return new DevelopmentEra(testing, null, values);
    }
}

/// <summary>SYNTHETIC fixture: two teams with engineers and cars, finance books, and a host loop for the development step.</summary>
internal sealed class DevelopmentKit
{
    public static readonly OrganizationId Alfa = OrganizationId.Real("alfa");

    public static readonly OrganizationId Beta = OrganizationId.Real("beta");

    public static readonly ManagerId Anna = new("human:anna");

    public static readonly ManagerId Bram = new("human:bram");

    public static readonly GameDate Opening = GameDate.SeasonStart(1955);

    private WorldState _world;

    public DevelopmentKit(
        ulong seed = 42UL,
        bool betaToo = true,
        FakeRules? rules = null,
        ITraceSink? sink = null,
        int skill = 14,
        bool staff = true,
        INextRaceSource? races = null)
    {
        Seed = seed;
        var (world, _) = WorldState.At(Opening).AddOrganization(Team(Alfa, "Alfa"));
        if (betaToo)
        {
            (world, _) = world.AddOrganization(Team(Beta, "Beta"));
        }

        if (staff)
        {
            world = Staff(world, Alfa, "a", skill);
            if (betaToo)
            {
                world = Staff(world, Beta, "b", skill);
            }
        }

        world = InitialCarFactory.Install(world, seed, 1955, fullyGenerated: false, EstimateCarStrength.Shared, []);
        var facts = new EraFinanceFacts("test", 200_000, 1_000_000, 3_000_000);
        var finance = FinanceSection.Empty.Open(Alfa, Opening, 5_000_000, facts);
        if (betaToo)
        {
            finance = finance.Open(Beta, Opening, 5_000_000, facts);
        }

        _world = world.WithSection(finance);
        Rules = rules ?? new FakeRules();
        Sink = sink ?? NullSink.Instance;
        Control = new ControlTable().Assign(Anna, Alfa).Assign(Bram, Beta);
        Managers = new ManagerRegistry();
        Managers.Register(Anna, ManagerKind.Human, "Anna");
        Managers.Register(Bram, ManagerKind.Human, "Bram");
        Book = new DevelopmentBook(() => _world, next => _world = next, seed);
        Environment = new DevelopmentEnvironment(Rules, Control, Sink, races);
        Resolvers = new InboxResolvers();
        Inbox = new InboxBook(Resolvers);
        Dispatcher = new CommandDispatcher();
        DevelopmentRegistration.Register(Dispatcher, Resolvers, Book, Environment);
        Dispatcher.Register(new ResolveInboxItemHandler());
        Today = Opening;
    }

    public ulong Seed { get; }

    public FakeRules Rules { get; }

    public ITraceSink Sink { get; }

    public ControlTable Control { get; }

    public ManagerRegistry Managers { get; }

    public DevelopmentBook Book { get; }

    public DevelopmentEnvironment Environment { get; }

    public InboxResolvers Resolvers { get; }

    public InboxBook Inbox { get; }

    public CommandDispatcher Dispatcher { get; }

    public GameDate Today { get; private set; }

    public WorldState World => _world;

    public DevelopmentSection Section => Book.Section;

    public CarsSection Cars => Book.Cars;

    public FinanceSection Finance => Book.Finance;

    public IReadOnlyList<TeamCar> AlfaCars => Cars.Of(Alfa);

    public static DateOnly Day(GameDate date) => new(date.Year, date.Month, date.Day);

    /// <summary>Lives <paramref name="days"/> days, one development step per day, as a host loop would.</summary>
    public void Live(int days)
    {
        for (var step = 0; step < days; step++)
        {
            var outcome = DevelopmentEngine.Step(Book.Inputs(Today, Environment));
            if (outcome.Changed)
            {
                Book.Write(outcome);
            }

            Today = Today.AddDays(1);
            _world = _world.WithDate(Today);
        }
    }

    /// <summary>Lives <paramref name="days"/> days through the real day handler, which also asks the principals in the inbox (T42c).</summary>
    public void LiveWithInbox(int days)
    {
        var registry = new DayHandlerRegistry([new DevelopmentDayHandler(Book, Environment, inbox: Inbox, managers: Managers)]);
        for (var step = 0; step < days; step++)
        {
            WorldClock.AdvanceDay(new WorldClockState(Today, Seed), registry);
            Today = Today.AddDays(1);
            _world = _world.WithDate(Today);
        }
    }

    public void SetPlan(OrganizationId organization, int current, int account, int nextYear)
    {
        var plan = (Section.PlanOf(organization) ?? DevelopmentPlan.Default(organization)) with
        {
            CurrentPercent = current,
            AccountPercent = account,
            NextYearPercent = nextYear,
        };
        _world = _world.WithSection(Section.SetPlan(plan));
    }

    public void PutProject(DevProject project)
    {
        var section = Section;
        _world = _world.WithSection(section.AddProject(project with { Number = section.NextProject }));
    }

    public void PutAccount(DevelopmentAccount account) => _world = _world.WithSection(Section.SetAccount(account));

    public void ReplaceCar(TeamCar car) => _world = _world.WithSection(Cars.Replace(car));

    public DevProject Project(Func<DevProject, bool> where) => Section.Projects.First(where);

    public CommandResult Submit(ICommand command) =>
        Dispatcher.Dispatch(
            command.WithSubmissionNumber(1),
            new CommandContext(new Frozen(Day(Today)), Managers, Inbox));

    public double Level(OrganizationId organization, DevArea area) =>
        DevelopmentMath.LevelOf(Cars.Of(organization)[0].Levels, area);

    /// <summary>A hand-made upgrade of one area with no failure risk, so two kits can be compared on the same noise.</summary>
    public static DevProject Upgrade(OrganizationId organization, DevArea area, long cost, double share, int days) =>
        new(
            1,
            organization,
            DevKind.Upgrade,
            area,
            "person:a-td",
            Opening,
            days,
            0,
            cost,
            0,
            DevelopmentEstimates.Milli(share),
            0,
            null,
            ProjectStatus.Active,
            ConceptTiming.NextSeason,
            0,
            0,
            null);

    public static DevProject Concept(OrganizationId organization, long cost, double share, int days, int risk = 0) =>
        Upgrade(organization, DevArea.Aero, cost, share, days) with { Kind = DevKind.Concept, Area = null, RiskMilli = risk };

    private static WorldState Staff(WorldState world, OrganizationId team, string tag, int skill)
    {
        var roles = new (StaffRole Role, string[] Keys)[]
        {
            (StaffRole.TechnicalDirector, ["vision", "project_management", "innovation"]),
            (StaffRole.ChiefDesigner, ["chassis", "integration", "precision"]),
            (StaffRole.HeadOfVehicleDynamics, ["suspension", "tyres", "tyre_temperature"]),
        };
        foreach (var (role, keys) in roles)
        {
            NamedAttribute[] attributes = keys.Select(key => new NamedAttribute(key, skill)).ToArray();
            var spec = new PersonSpec(
                "Given",
                tag + "-" + role,
                new GameDate(1915, 1, 1),
                "GB",
                true,
                tag + "-" + role.ToString().ToLowerInvariant(),
                [PersonRole.Staff(role)],
                new PersonTruth(attributes, attributes));
            (world, var person) = world.AddPerson(spec);
            (world, _) = world.AddContract(new ContractSpec(person, team, ContractRole.Staff(role), Opening, GameDate.SeasonEnd(1958), 1000, true, null, null));
        }

        return world;
    }

    private static OrganizationSpec Team(OrganizationId id, string name) =>
        new(OrganizationKind.Team, true, id.Value, Opening, null, 0, [new OrganizationNameSpan(name, Opening, null)]);

    private sealed class Frozen(DateOnly date) : IWorldState
    {
        public DateOnly CurrentDate => date;

        public void AdvanceDate()
        {
        }

        public string ContentHash() => "unused";
    }
}
