using System.Diagnostics;
using Paddock.Application.Cars;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Development;
using Paddock.Application.Finance;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Application.Objectives;
using Paddock.Application.Pool;
using Paddock.Application.Principals;
using Paddock.Application.Sponsors;
using Paddock.Application.Supply;
using Paddock.Application.World;
using Paddock.Data.Authored;
using Paddock.Domain.Contracts;
using Paddock.Domain.Development;
using Paddock.Domain.Finance;
using Paddock.Domain.Objectives;
using Paddock.Domain.Pool;
using Paddock.Domain.Spy;
using Paddock.Domain.Supply;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Career;
using Paddock.Simulation.Pool;
using Paddock.Simulation.Time;
using Paddock.Tests.Contracts;
using Paddock.Tests.Supply;
using HostManagerId = Paddock.Application.Managers.ManagerId;

namespace Paddock.Tests.Principals;

/// <summary>Collects every decision trace in order. A passive observer: it changes nothing (INV-006).</summary>
internal sealed class RecordingSink : ITraceSink
{
    public List<DecisionTrace> Traces { get; } = [];

    public bool IsEnabled => true;

    public void Record(DecisionTrace trace) => Traces.Add(trace);
}

internal sealed record PrincipalKitOptions
{
    public ulong Seed { get; init; } = 7UL;

    public PrincipalWorldOptions World { get; init; } = new();

    /// <summary>Where the AI decision traces and the people's decisions are written. Null: no sink.</summary>
    public ITraceSink? Sink { get; init; }

    /// <summary>The talent pool and the scouting of it. Off for the metamorphic test, which must not let scouting read the truth.</summary>
    public bool Pool { get; init; } = true;

    /// <summary>Index of a team a human manager runs, or -1 for an AI-only world.</summary>
    public int HumanTeam { get; init; } = -1;

    public bool Sponsors { get; init; } = true;

    /// <summary>Yearly revenue as a share of the typical budget, posted monthly (a stand-in for prize and start money: no races run here).</summary>
    public double RevenueShare { get; init; } = 1.1;
}

/// <summary>
/// An AI-only career wired the way the host will wire it: the session and its day handlers, the contract engine, supply, development, sponsors,
/// the pool and finance, the command pipeline, and the principal director that files each morning's commands. The synthetic world has
/// no races, so a monthly revenue handler stands in for the money races bring (it is part of this fixture, not of the game).
/// </summary>
internal sealed class PrincipalKit
{
    private static readonly Lazy<AuthoredData> Authored = new(() => AuthoredDataLoader.Load(Path.Combine(RepoPaths.Root(), "data")));

    private static readonly Lazy<SponsorsFile> SponsorFile = new(() => SponsorsLoader.Load(Path.Combine(RepoPaths.Root(), "data")));

    private readonly ContractBook _contracts;
    private readonly InboxBook _inbox;
    private readonly KitWorld _clockWorld;
    private readonly CommandContext _context;
    private readonly Stopwatch _clock = new();

    public PrincipalKit(PrincipalKitOptions? options = null)
    {
        Options = options ?? new PrincipalKitOptions();
        var data = Authored.Value;
        var facts = EraFinance.ForYear(data.EraPeriods, PrincipalWorld.Opening.Year);
        var world = PrincipalWorld.Build(Options.World, facts, out _);
        Session = new CareerSession(
            world,
            Options.Seed,
            [],
            [],
            new CareerSessionOptions { Pool = new TalentPoolOptions { TargetSize = Options.Pool ? PoolEstimates.TargetSize : 0 } });
        Managers = new ManagerRegistry();
        Control = new ControlTable();
        Queue = new CommandQueue();
        Dispatcher = new CommandDispatcher();
        Trace = Options.Sink ?? NullSink.Instance;

        var pay = new EraPayBenchmark(season => EraSet.For(
            season,
            [EraPayBenchmark.MidfieldDimension, EraPayBenchmark.TopDimension],
            data.EraPeriods));
        var financeBook = FinanceBook.ForSession(Session);
        var environment = new ContractEnvironment(
            new DerivedPersonalitySource(Options.Seed),
            pay,
            Control,
            payroll: new FinancePayroll(financeBook),
            trace: Trace);
        var resolvers = new InboxResolvers();
        _inbox = InboxBook.From(Session.World, resolvers);
        _contracts = new ContractBook(Session.World, environment);
        Engine = new ContractEngine(_contracts, _inbox, Managers);
        ContractRegistration.Register(Dispatcher, resolvers);

        var supplyBook = SupplyBook.ForSession(Session);
        SupplyEnvironment = new SupplyEnvironment(
            Control,
            new PeriodSupplyEras(data.EraPeriods),
            new EstimateSupplierProfiles(PrincipalWorld.Opening.Year),
            trace: Trace);
        SupplyRegistration.Register(Dispatcher, supplyBook, SupplyEnvironment);

        var developmentBook = DevelopmentBook.ForSession(Session, Options.Seed);
        var developmentEnvironment = new DevelopmentEnvironment(new PeriodDevelopmentRules(data.EraPeriods), Control, Trace);
        DevelopmentRegistration.Register(Dispatcher, resolvers, developmentBook, developmentEnvironment);
        var carBook = new CarBook(() => Session.World, Session.StoreWorld, Options.Seed);

        var objectives = new ObjectiveFactRegistry();
        var sponsorBook = SponsorBook.ForSession(Session);
        var sponsorEnvironment = new SponsorEnvironment(
            SponsorsLoader.ToCatalog(SponsorFile.Value),
            new PeriodSponsorEras(data.EraPeriods),
            new EraPeriodFinance(data.EraPeriods),
            Control,
            objectives);
        if (Options.Sponsors)
        {
            Dispatcher.Register(new BeginSponsorTalksHandler(sponsorBook, sponsorEnvironment));
            Dispatcher.Register(new SignAtCurrentTermsHandler(sponsorBook, sponsorEnvironment));
            Dispatcher.Register(new WalkAwayFromTalksHandler(sponsorBook, sponsorEnvironment));
            Dispatcher.Register(new RespondToSponsorOfferHandler(sponsorBook, sponsorEnvironment));
        }

        var poolBook = PoolBook.ForSession(Session);
        var organizations = new PrincipalOrganizations();
        Dispatcher.Register(new AssignScoutFocusHandler(poolBook, organizations));

        if (Options.HumanTeam >= 0)
        {
            Human = new HostManagerId("human:anna");
            Managers.Register(Human, ManagerKind.Human, "Anna");
            Control.Assign(Human, PrincipalWorld.Team(Options.HumanTeam));
        }

        var principalsBook = PrincipalsBook.ForSession(Session);
        var principalEnvironment = new PrincipalEnvironment(Options.Seed, () => Session.World, Managers, [Control])
        {
            Contracts = Engine,
            Development = new DevelopmentSources(developmentBook, developmentEnvironment, carBook),
            Supply = new SupplySources(supplyBook, SupplyEnvironment),
            Sponsors = Options.Sponsors ? new SponsorSources(sponsorBook, sponsorEnvironment, new ObjectiveQuery(objectives, organizations)) : null,
            Pool = Options.Pool ? poolBook : null,
            Outlook = new DevelopmentRulesOutlook(new PeriodDevelopmentRules(data.EraPeriods)),
            Trace = Trace,
        };
        PrincipalEnvironment = principalEnvironment;
        Director = PrincipalRegistration.Register(Dispatcher, principalsBook, principalEnvironment);

        _clockWorld = new KitWorld(Session);
        _context = new CommandContext(_clockWorld, Managers, _inbox, _contracts);

        var handlers = new List<IDayHandler>
        {
            new Syncing(Session, _contracts, _inbox, new NegotiationDayHandler(Engine)),
            new Syncing(Session, _contracts, _inbox, new ContractLifecycleHandler(Engine)),
            new SupplyDayHandler(supplyBook, SupplyEnvironment, _inbox, Managers),
            new DevelopmentDayHandler(developmentBook, developmentEnvironment),
            new FinanceDayHandler(() => Session.World, Session.StoreWorld),
            new RevenueHandler(Session, data, Options.RevenueShare),
        };
        if (Options.Sponsors)
        {
            handlers.Add(new SponsorDayHandler(sponsorBook, sponsorEnvironment, _inbox, Managers));
        }

        Session.AttachHandlers(handlers);
    }

    public PrincipalKitOptions Options { get; }

    public CareerSession Session { get; }

    public ManagerRegistry Managers { get; }

    public ControlTable Control { get; }

    public CommandQueue Queue { get; }

    public CommandDispatcher Dispatcher { get; }

    public ContractEngine Engine { get; }

    public PrincipalDirector Director { get; }

    public PrincipalEnvironment PrincipalEnvironment { get; }

    public SupplyEnvironment SupplyEnvironment { get; }

    public ITraceSink Trace { get; }

    public HostManagerId Human { get; }

    /// <summary>Commands accepted and rejected so far, by command type name.</summary>
    public Dictionary<string, int> Accepted { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> Rejected { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> RejectedKeys { get; } = new(StringComparer.Ordinal);

    public int Filed { get; private set; }

    /// <summary>Wall-clock time spent in <see cref="PrincipalDirector.FileCommands"/>.</summary>
    public TimeSpan DirectorTime => _clock.Elapsed;

    public int Reviews => Accepted.GetValueOrDefault(nameof(RecordPrincipalReviewCommand));

    public string Hash => Session.World.StateHash();

    /// <summary>Lives one morning: the director files, the pipeline runs the queue, and the day is lived.</summary>
    public void Day(Action<GameDate>? beforeDispatch = null, Action<GameDate>? afterDispatch = null)
    {
        var today = Session.Date;
        _contracts.UseWorld(Session.World);
        _clock.Start();
        var filed = Director.FileCommands(Queue, today);
        _clock.Stop();
        Filed += filed;
        beforeDispatch?.Invoke(today);
        foreach (var command in Queue.DequeueAll())
        {
            _contracts.UseWorld(Session.World);
            var result = Dispatcher.Dispatch(command, _context);
            var name = command.GetType().Name;
            if (result is CommandResult.Rejected rejected)
            {
                Rejected[name] = Rejected.GetValueOrDefault(name) + 1;
                RejectedKeys[name + "|" + rejected.Reason.Key] = RejectedKeys.GetValueOrDefault(name + "|" + rejected.Reason.Key) + 1;
            }
            else
            {
                Accepted[name] = Accepted.GetValueOrDefault(name) + 1;
            }

            if (command.GetType().Namespace == typeof(OpenNegotiationCommand).Namespace)
            {
                Session.StoreWorld(_inbox.Into(_contracts.Into()));
            }
        }

        afterDispatch?.Invoke(today);
        _contracts.UseWorld(Session.World);
        Session.LiveDay();
    }

    public void RunUntil(GameDate end, Action<GameDate>? afterDay = null)
    {
        while (Session.Date < end)
        {
            var today = Session.Date;
            Day();
            afterDay?.Invoke(today);
        }
    }

    public void RunDays(int days)
    {
        for (var i = 0; i < days; i++)
        {
            Day();
        }
    }

    // ---------------------------------------------------------------- fixture parts

    private sealed class KitWorld : IWorldState
    {
        private readonly CareerSession _session;

        public KitWorld(CareerSession session) => _session = session;

        public DateOnly CurrentDate => new(_session.Date.Year, _session.Date.Month, _session.Date.Day);

        public void AdvanceDate() => _session.LiveDay();

        public string ContentHash() => _session.World.StateHash();
    }

    /// <summary>The contract day handlers read and write the contract book's copy of the world; this keeps it equal to the session's.</summary>
    private sealed class Syncing : IDayHandler
    {
        private readonly CareerSession _session;
        private readonly ContractBook _book;
        private readonly InboxBook _inbox;
        private readonly IDayHandler _inner;

        public Syncing(CareerSession session, ContractBook book, InboxBook inbox, IDayHandler inner)
        {
            _session = session;
            _book = book;
            _inbox = inbox;
            _inner = inner;
            Order = inner.Order;
        }

        public int Order { get; }

        public void OnDay(DayContext context)
        {
            _book.UseWorld(_session.World);
            _inner.OnDay(context);
            _session.StoreWorld(_inbox.Into(_book.Into()));
        }
    }

    /// <summary>SYNTHETIC stand-in for prize and start money: each first of the month every team with books is paid a share of the typical budget.</summary>
    private sealed class RevenueHandler : IDayHandler
    {
        private readonly CareerSession _session;
        private readonly AuthoredData _data;
        private readonly double _share;

        public RevenueHandler(CareerSession session, AuthoredData data, double share)
        {
            _session = session;
            _data = data;
            _share = share;
        }

        public int Order => 790;

        public void OnDay(DayContext context)
        {
            if (context.Today.Day != 1)
            {
                return;
            }

            var world = _session.World;
            var finance = world.Section<FinanceSection>(FinanceSection.SectionName);
            if (finance is null)
            {
                return;
            }

            var typical = EraFinance.ForYear(_data.EraPeriods, context.Today.Year).TypicalDollars;
            var monthly = Money.FromDollars(typical).Cents * (long)(_share * 1000) / 1000 / 12;
            foreach (var organization in world.Organizations)
            {
                if (organization.Kind == OrganizationKind.Team && finance.HasBook(organization.Id))
                {
                    finance = finance.Post(organization.Id, context.Today, LedgerCategories.PrizeMoney, null, monthly, FinanceReason.PrizeMoney);
                }
            }

            _session.StoreWorld(world.WithSection(finance));
        }
    }
}
