using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Inbox;
using Paddock.Application.Infrastructure;
using Paddock.Application.Managers;
using Paddock.Application.World;
using Paddock.Domain.Cars;
using Paddock.Domain.Development;
using Paddock.Domain.Finance;
using Paddock.Domain.Infrastructure;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Time;

namespace Paddock.Tests.Infrastructure;

/// <summary>SYNTHETIC fixture: one team, a factory, finance books and optional cars.</summary>
internal sealed class InfrastructureKit
{
    public static readonly OrganizationId Alfa = OrganizationId.Real("alfa");

    public static readonly OrganizationId Beta = OrganizationId.Real("beta");

    public static readonly ManagerId Anna = new("human:anna");

    public static readonly GameDate Opening = GameDate.SeasonStart(1955);

    private WorldState _world;

    public InfrastructureKit(bool cars = true, string? testing = "unrestricted")
    {
        var (world, _) = WorldState.At(Opening).AddOrganization(Team(Alfa, "Alfa"));
        (world, _) = world.AddOrganization(Team(Beta, "Beta"));
        var facts = new EraFinanceFacts("test", 200_000, 1_000_000, 3_000_000);
        var finance = FinanceSection.Empty.Open(Alfa, Opening, 5_000_000, facts).Open(Beta, Opening, 5_000_000, facts);
        world = world.WithSection(finance);
        if (cars)
        {
            world = InitialCarFactory.Install(world, 42UL, 1955, fullyGenerated: false, null, []);
        }

        Catalog = new FacilityCatalog(
            [new FacilityKindSpec(FacilityKind.Factory, 1950), new FacilityKindSpec(FacilityKind.WindTunnel, 1968)],
            new Dictionary<FacilityKind, int> { [FacilityKind.Factory] = 40_000 },
            new Dictionary<string, IReadOnlyDictionary<FacilityKind, int>>(StringComparer.Ordinal)
            {
                ["alfa"] = new Dictionary<FacilityKind, int> { [FacilityKind.Factory] = 50_000 },
            });
        world = world.WithSection(InitialInfrastructureFactory.Install(world, Catalog, Opening));
        _world = world;
        Control = new ControlTable().Assign(Anna, Alfa);
        Managers = new ManagerRegistry();
        Managers.Register(Anna, ManagerKind.Human, "Anna");
        Book = new InfrastructureBook(() => _world, next => _world = next);
        var periods = testing is null
            ? Array.Empty<RulePeriod>()
            : new RulePeriod[] { new(PeriodDevelopmentRules.InSeasonTestingDimension, testing, 1950, null) };
        Environment = new InfrastructureEnvironment(
            Control,
            Catalog,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["alfa"] = "ITA", ["beta"] = "GBR" },
            periods);
        Inbox = new InboxBook();
        Dispatcher = new CommandDispatcher();
        InfrastructureRegistration.Register(Dispatcher, Book, Environment);
        Today = Opening;
    }

    public FacilityCatalog Catalog { get; }

    public ControlTable Control { get; }

    public ManagerRegistry Managers { get; }

    public InfrastructureBook Book { get; }

    public InfrastructureEnvironment Environment { get; }

    public InboxBook Inbox { get; }

    public CommandDispatcher Dispatcher { get; }

    public GameDate Today { get; private set; }

    public WorldState World => _world;

    public static DateOnly Day(GameDate date) => new(date.Year, date.Month, date.Day);

    public CommandResult Submit(ICommand command) =>
        Dispatcher.Dispatch(
            command.WithSubmissionNumber(1),
            new CommandContext(new Frozen(Day(Today)), Managers, Inbox));

    public void Live(int days)
    {
        var registry = new DayHandlerRegistry([new InfrastructureDayHandler(Book, Environment, Inbox, Managers)]);
        for (var step = 0; step < days; step++)
        {
            WorldClock.AdvanceDay(new WorldClockState(Today, 1UL), registry);
            Today = Today.AddDays(1);
            _world = _world.WithDate(Today);
        }
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
