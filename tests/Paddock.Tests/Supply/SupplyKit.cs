using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Finance;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Application.Supply;
using Paddock.Application.World;
using Paddock.Data.Authored;
using Paddock.Domain.Cars;
using Paddock.Domain.Finance;
using Paddock.Domain.Spy;
using Paddock.Domain.Supply;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Time;
using Paddock.Tests.Sponsors;

namespace Paddock.Tests.Supply;

/// <summary>Two teams with books and two cars each, two engine suppliers, the real era data, and a host loop for the day handler.</summary>
internal sealed class SupplyKit
{
    public static readonly OrganizationId Alfa = OrganizationId.Real("alfa");

    public static readonly OrganizationId Beta = OrganizationId.Real("beta");

    public static readonly OrganizationId Acme = OrganizationId.Real("supplier:acme");

    public static readonly OrganizationId Bolt = OrganizationId.Real("supplier:bolt");

    public static readonly ManagerId Anna = new("human:anna");

    public static readonly ManagerId Bram = new("human:bram");

    private static readonly Lazy<AuthoredData> Authored = new(() => AuthoredDataLoader.Load(Path.Combine(RepoPaths.Root(), "data")));

    private WorldState _world;

    public SupplyKit(GameDate start, ulong seed = 42UL, IEngineProgrammes? programmes = null, ITraceSink? trace = null, bool supplierBooks = true)
    {
        Start = start;
        Seed = seed;
        var world = WorldState.At(start);
        foreach (var (id, kind, name) in new[]
                 {
                     (Alfa, OrganizationKind.Team, "Alfa"),
                     (Beta, OrganizationKind.Team, "Beta"),
                     (Acme, OrganizationKind.EngineSupplier, "Acme Motors"),
                     (Bolt, OrganizationKind.EngineSupplier, "Bolt Engines"),
                 })
        {
            (world, _) = world.AddOrganization(new OrganizationSpec(kind, true, id.Value, start, null, 0, [new OrganizationNameSpan(name, start, null)]));
        }

        var facts = EraFinance.ForYear(Authored.Value.EraPeriods, start.Year);
        var finance = FinanceSection.Empty.Open(Alfa, start, facts.TypicalDollars, facts).Open(Beta, start, facts.TypicalDollars, facts);
        if (supplierBooks)
        {
            finance = finance.Open(Acme, start, 0, facts);
        }

        var cars = new List<TeamCar>();
        long next = 1;
        foreach (var team in new[] { Alfa, Beta })
        {
            for (var seat = 0; seat < CarEstimates.CarsPerTeam; seat++)
            {
                var effects = ConceptMapping.Effects(CarConcept.Neutral, 60);
                cars.Add(new TeamCar(
                    CarIds.Format(next++),
                    team,
                    start.Year,
                    CarConcept.Neutral,
                    ConceptMapping.StartingLevels(CarConcept.Neutral, 60),
                    60,
                    CarEstimates.InitialUnderstanding,
                    effects.TyreWearMultiplier,
                    effects.SupplierChangeCost,
                    null,
                    null));
            }
        }

        _world = world.WithSection(finance).WithSection(CarsSection.Restore(next, 1, cars, []));
        Book = new SupplyBook(() => _world, updated => _world = updated);
        Control = new ControlTable().Assign(Anna, Alfa).Assign(Bram, Beta);
        Managers = new ManagerRegistry();
        Managers.Register(Anna, ManagerKind.Human, "Anna");
        Managers.Register(Bram, ManagerKind.Human, "Bram");
        Inbox = new InboxBook();
        Environment = new SupplyEnvironment(
            Control,
            new PeriodSupplyEras(Authored.Value.EraPeriods),
            new EstimateSupplierProfiles(start.Year, null, seed),
            programmes,
            trace);
        Context = new CommandContext(new StubWorldState(new DateOnly(start.Year, start.Month, start.Day)), Managers, Inbox);
    }

    public GameDate Start { get; }

    public ulong Seed { get; }

    public SupplyBook Book { get; }

    public IOrganizationControl Control { get; }

    public ManagerRegistry Managers { get; }

    public InboxBook Inbox { get; }

    public SupplyEnvironment Environment { get; }

    public CommandContext Context { get; }

    public WorldState World => _world;

    public AuthoredData Data => Authored.Value;

    public static DateOnly Day(GameDate date) => new(date.Year, date.Month, date.Day);

    /// <summary>The era budget yardstick for the start season, in cents.</summary>
    public long Budget => Environment.Eras.ReferenceBudgetCents(Start.Year);

    /// <summary>The least a supplier asks for a one-season deal of this shape, in cents.</summary>
    public long Floor(SupplyItem item, SupplyKind kind, int seasons = 1, bool exclusive = false) =>
        SupplyPricing.FloorCents(item, kind, new SupplyTerms(1, seasons, exclusive), Budget);

    public string? Run(ICommand command)
    {
        ICommandHandler handler = command switch
        {
            ProposeSupplyDealCommand => new ProposeSupplyDealHandler(Book, Environment),
            RespondToSupplyOfferCommand => new RespondToSupplyOfferHandler(Book, Environment),
            _ => throw new ArgumentException("Not a supply command."),
        };
        var rejection = handler.Validate(command, Context);
        if (rejection is not null)
        {
            return rejection.Key;
        }

        handler.Execute(command, Context);
        return null;
    }

    public ProposeSupplyDealCommand Proposal(
        ManagerId manager,
        OrganizationId customer,
        OrganizationId supplier,
        long priceCents,
        GameDate on,
        SupplyItem item = SupplyItem.Engine,
        SupplyKind kind = SupplyKind.Customer,
        int seasons = 1,
        bool exclusive = false,
        string negotiation = "") =>
        new()
        {
            ManagerId = manager,
            IssuedOn = Day(on),
            OrganizationId = customer.Value,
            SupplierId = supplier.Value,
            Item = item,
            Kind = kind,
            FirstSeason = on.Year,
            AnnualPriceCents = priceCents,
            Seasons = seasons,
            Exclusive = exclusive,
            NegotiationId = negotiation,
        };

    public SupplyNegotiation Propose(ManagerId manager, OrganizationId customer, OrganizationId supplier, long priceCents, GameDate on, SupplyItem item = SupplyItem.Engine, SupplyKind kind = SupplyKind.Customer, int seasons = 1, bool exclusive = false)
    {
        Assert.Null(Run(Proposal(manager, customer, supplier, priceCents, on, item, kind, seasons, exclusive)));
        return Book.Section.Negotiations.Last();
    }

    /// <summary>Lives <paramref name="days"/> days from <paramref name="from"/> with the supply day handler (and finance when asked).</summary>
    public void Live(GameDate from, int days, bool includeFinance = false)
    {
        var handlers = new List<IDayHandler> { new SupplyDayHandler(Book, Environment, Inbox, Managers) };
        if (includeFinance)
        {
            handlers.Add(new FinanceDayHandler(() => Book.World, next => _world = next));
        }

        var registry = new DayHandlerRegistry(handlers);
        var state = new WorldClockState(from, Seed);
        for (var step = 0; step < days; step++)
        {
            state = WorldClock.AdvanceDay(state, registry).State;
        }
    }

    public IReadOnlyList<LedgerEntry> SupplyEntries(OrganizationId organization) =>
        Book.Finance.EntriesOf(organization).Where(entry => entry.Category == LedgerCategories.Supply).ToArray();

    public IReadOnlyList<string> InboxSubjects(ManagerId manager) =>
        Inbox.Section.ItemsOf(manager.Value).Select(item => item.Draft.SubjectKey).ToArray();
}
