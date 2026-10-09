using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Paddock.Application.Access;
using Paddock.Application.Board;
using Paddock.Application.Career;
using Paddock.Application.Cars;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Development;
using Paddock.Application.Finance;
using Paddock.Application.Inbox;
using Paddock.Application.Infrastructure;
using Paddock.Application.Managers;
using Paddock.Application.Objectives;
using Paddock.Application.Pool;
using Paddock.Application.Racing;
using Paddock.Application.Sponsors;
using Paddock.Application.Staff;
using Paddock.Application.Supply;
using Paddock.Application.World;
using Paddock.Data.Authored;
using Paddock.Data.World;
using Paddock.Domain.Career;
using Paddock.Domain.Contracts;
using Paddock.Domain.Development;
using Paddock.Domain.Objectives;
using Paddock.Domain.Supply;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Career;
using AccessManagerId = Paddock.Application.Access.ManagerId;
using HostManagerId = Paddock.Application.Managers.ManagerId;

namespace Paddock.Desktop.Bridge;

/// <summary>
/// One career in this process. The morning, the queue and the ready gate are the same calls
/// <see cref="CareerHost"/> makes; this type does not add a rule of its own.
/// </summary>
public sealed partial class CareerBridge
{
    public const string HumanManagerId = "human:player";

    public const int DefaultYear = 1955;

    public const ulong DefaultSeed = 1;

    private CareerSession? _fixedSession;
    private CareerModuleHost? _modules;
    private CommandDispatcher? _dispatcher;
    private CommandQueue? _queue;
    private readonly ReadyGate _gate = new();
    private SessionClock? _clock;
    private HostManagerId _human = new(HumanManagerId);
    private bool _morningDone;
    private CareerShell? _play;
    private string? _dataRoot;

    private CareerBridge()
    {
    }

    private CareerBridge(
        CareerSession session,
        CareerModuleHost modules,
        CommandDispatcher dispatcher,
        CommandQueue queue,
        HostManagerId human)
    {
        _fixedSession = session;
        _modules = modules;
        _dispatcher = dispatcher;
        _queue = queue;
        _human = human;
        _clock = new SessionClock(session);
    }

    /// <summary>A window with no career yet. The page starts or loads one. Year and seed are the start screen's defaults.</summary>
    public static CareerBridge Lobby(string dataRoot, int year = DefaultYear, ulong seed = DefaultSeed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        return new CareerBridge { _dataRoot = dataRoot, _suggestedYear = year, _suggestedSeed = seed };
    }

    public bool HasCareer => _play is not null || _fixedSession is not null;

    private CareerSession Session => _play?.Session ?? _fixedSession ?? throw new InvalidOperationException("No career is open.");

    private CareerModuleContext Box => _play?.Modules ?? _modules?.Context ?? throw new InvalidOperationException("No career is open.");

    public string StateHash => Session.World.StateHash();

    public string DateText => Session.Date.ToString()!;

    public static CareerBridge Open(string dataRoot, int year, ulong seed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        var data = AuthoredDataLoader.Load(dataRoot);
        var config = CareerConfig.FromPreset(CareerPreset.Chaos).WithStartYear(year);
        var provider = EmptyPeopleProvider.Instance;
        var created = WorldInitializer.Create(config, data, provider, seed);
        var arrivals = TalentIntakeSchedule.AfterStart(config, provider, created.World, seed);
        var session = new CareerSession(
            created.World,
            seed,
            created.TalentPool,
            arrivals,
            new CareerSessionOptions { LastSeasons = LastSeasons.From(provider) });

        var managers = new ManagerRegistry();
        var ai = new HostManagerId(CareerHost.AiManagerId);
        var human = new HostManagerId(HumanManagerId);
        managers.Register(ai, ManagerKind.Ai, "AI");
        managers.Register(human, ManagerKind.Human, "Principal");
        var dispatcher = new CommandDispatcher();
        var queue = new CommandQueue();
        var modules = CareerModuleHost.Attach(
            session,
            managers,
            dispatcher,
            ai,
            CareerModules.Default,
            LoadInputs(dataRoot, data, created.EngineSupplies),
            // Seat the player on his team, so the AI principal director (T44) never runs it.
            created.PlayerOrganization.IsAssigned
                ? [new CareerHuman(HumanManagerId, "Principal", created.PlayerOrganization.Value)]
                : null);

        if (created.PlayerOrganization.IsAssigned)
        {
            modules.Context.Require<BoardEngine>().AppointHuman(
                human,
                created.PlayerOrganization,
                session.Date,
                founder: false,
                BridgeKeys.CareerAppointed);
        }

        var bridge = new CareerBridge(session, modules, dispatcher, queue, human);
        bridge._dataRoot = dataRoot;
        bridge.RememberCircuits(data);
        bridge.BeginDay();
        return bridge;
    }

    public JsonNode? Query(string name, JsonElement args)
    {
        var access = AccessContext.ForManager(new AccessManagerId(Human.Value));
        return name switch
        {
            "shell" => BridgeValues.ToNode(ReadShell()),
            "inbox" => BridgeValues.ToNode(new InboxQuery(Inbox()).View(access)),
            "team" => BridgeValues.ToNode(ReadTeam()),
            "drivers" => BridgeValues.ToNode(ReadDrivers(access)),
            "cars" => BridgeValues.ToNode(Cars().View(access)),
            "development" => BridgeValues.ToNode(ReadDevelopment(access)),
            "infrastructure" => BridgeValues.ToNode(ReadInfrastructure(access)),
            "sponsors" => BridgeValues.ToNode(ReadSponsors(access)),
            "finance" => BridgeValues.ToNode(ReadFinance(access, withLedger: true)),
            "board" => BridgeValues.ToNode(Board().View(access, Session.Date)),
            "pool" => BridgeValues.ToNode(Pool().View(access)),
            "supply" => BridgeValues.ToNode(ReadSupply(access)),
            "negotiations" => BridgeValues.ToNode(new NegotiationQuery(Contracts()).View(access)),
            "session" => BridgeValues.ToNode(ReadSession()),
            "teams" => BridgeValues.ToNode(ReadTeams(args)),
            "saves" => BridgeValues.ToNode(ReadSaves()),
            "calendar" => BridgeValues.ToNode(ChampionshipRead.Calendar(Session, Box.Inputs, Circuits)),
            "standings" => BridgeValues.ToNode(ChampionshipRead.Standings(Session, Box.Inputs)),
            "raceResult" => BridgeValues.ToNode(ReadRace(args)),
            "nextRace" => BridgeValues.ToNode(ChampionshipRead.Next(Session, Circuits)),
            "track" => BridgeValues.ToNode(TrackRead.Read(Watched()?.Session ?? Session, Tracks, TextOf(args, "layoutId"), PastRaces())),
            "quickRounds" => BridgeValues.ToNode(ReadQuickRounds(args)),
            "newspaper" => BridgeValues.ToNode(ReadNewspaper()),
            "seasonOverview" => BridgeValues.ToNode(SeasonOverviewRead.Read(Session, Box.Inputs, Circuits)),
            "staff" => BridgeValues.ToNode(ReadStaff()),
            "market" => BridgeValues.ToNode(ReadMarket(access)),
            "driver" => BridgeValues.ToNode(ReadDriver(args)),
            "manager" => BridgeValues.ToNode(ReadManager()),
            "liveRace" => BridgeValues.ToNode(ReadLiveRace()),
            "liveFrames" => BridgeValues.ToNode(ReadLiveFrames(args)),
            "liveClock" => BridgeValues.ToNode(ReadLiveClock()),
            _ => throw new InvalidOperationException("Query '" + name + "' is registered but not implemented."),
        };
    }

    public CommandResult Submit(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (_play is not null)
        {
            return _play.Submit(command);
        }

        _queue!.Enqueue(command);
        var results = _dispatcher!.DispatchAll(_queue, Context());
        return results.Count == 0
            ? throw new InvalidOperationException("The queue accepted a command and then dispatched nothing.")
            : results[^1];
    }

    public AdvanceOutcome Advance()
    {
        if (_play is not null)
        {
            return AdvancePlay();
        }

        Box.Managers.SetReady(_human, true);
        var step = _gate.RequestAdvance(Box.Managers, _clock!);
        if (step is AdvanceResult.Refused refused)
        {
            return new AdvanceOutcome(false, refused.Refusal.Reason, null);
        }

        _morningDone = false;
        BeginDay();
        return new AdvanceOutcome(true, null, DateText);
    }

    public DateOnly IssuedOn => new(Session.Date.Year, Session.Date.Month, Session.Date.Day);

    public bool IsHuman(string managerId) => string.Equals(managerId, Human.Value, StringComparison.Ordinal);

    public HostManagerId Human => _play?.Player ?? _human;

    private void BeginDay()
    {
        if (_morningDone)
        {
            return;
        }

        _modules!.BeginMorning(_queue!);
        _dispatcher!.DispatchAll(_queue!, Context());
        _modules.EndMorning();
        _morningDone = true;
    }

    private CommandContext Context() => _modules!.CommandContext(_clock!, Box.Managers);

    private ShellView ReadShell()
    {
        var team = ReadTeam();
        var inbox = new InboxQuery(Inbox()).View(Access());
        var blocking = Box.Managers.Get(_human).BlockingItem;
        InboxItemView? decision = null;
        InboxItemView? important = null;
        foreach (var item in inbox.Items)
        {
            if (item.Status != Paddock.Domain.Inbox.InboxStatus.Open)
            {
                continue;
            }

            if (decision is null && item.NeedsDecision)
            {
                decision = item;
            }

            if (item.Important)
            {
                important = item;
            }
        }

        return new ShellView(
            _human.Value,
            DateText,
            team.CashCents,
            inbox.OpenCount,
            inbox.OpenDecisionCount,
            blocking?.Kind,
            decision?.Id,
            decision?.Kind,
            decision?.Subject,
            team.OrganizationId,
            team.Name,
            important?.Id,
            important?.Kind,
            important?.Subject);
    }

    private OwnTeamView ReadTeam()
    {
        var organization = Box.Require<BoardBook>().Section.OrganizationOf(_human.Value);
        if (organization is not OrganizationId id)
        {
            return new OwnTeamView(null, null, null);
        }

        var name = Session.World.GetOrganization(id).NameOn(Session.Date);
        long? cash = ReadFinance(Access()) is FinanceView.Own own ? own.CashCents : null;
        return new OwnTeamView(id.Value, name, cash);
    }

    private DriversView ReadDrivers(AccessContext access)
    {
        var organization = Box.Require<BoardBook>().Section.OrganizationOf(_human.Value);
        var own = new List<OwnDriverView>();
        if (organization is OrganizationId id)
        {
            foreach (var contract in Session.World.Contracts.OrderBy(item => item.Id.Value, StringComparer.Ordinal))
            {
                if (contract.OrganizationId != id || !contract.Role.IsDriver || !contract.IsActiveOn(Session.Date))
                {
                    continue;
                }

                var person = Session.World.GetPerson(contract.PersonId);
                string? injury = null;
                string? returnRange = null;
                if (person.IsInjured(Session.Date))
                {
                    var upcoming = Session.Clock.Queue.Events
                        .Where(e => e.TypeId == Paddock.Simulation.Time.ScheduledEventType.Race && e.Date >= Session.Date)
                        .OrderBy(e => e.Date)
                        .Select(e => e.Date)
                        .ToList();

                    var isLight = upcoming.Count <= 1 || (person.InjuredUntil is GameDate until && upcoming.Count > 1 && until <= upcoming[0]);
                    if (isLight)
                    {
                        injury = "Light";
                        var nextRace = upcoming.Count > 0 ? upcoming[0] : Session.Date;
                        returnRange = $"{Session.Date:yyyy-MM-dd} .. {nextRace:yyyy-MM-dd}";
                    }
                    else
                    {
                        injury = "Serious";
                        var minRace = upcoming.Count > 1 ? upcoming[1] : (upcoming.Count > 0 ? upcoming[0] : Session.Date);
                        var maxRace = upcoming.Count >= 6 ? upcoming[5] : (upcoming.Count > 0 ? upcoming[^1] : Session.Date.AddDays(90));
                        returnRange = $"{minRace:yyyy-MM-dd} .. {maxRace:yyyy-MM-dd}";
                    }
                }

                own.Add(new OwnDriverView(
                    person.Id.Value,
                    person.Name,
                    person.Nationality,
                    contract.Role.Seat.ToString()!,
                    contract.End.ToString()!,
                    injury,
                    returnRange));
            }
        }

        var market = new List<MarketDriverView>();
        if (organization is OrganizationId observer)
        {
            foreach (var person in new FreeAgentQuery(Contracts()).List(access, observer, Session.Date, NegotiationSubject.DriverSeat))
            {
                if (!person.IsDriver)
                {
                    continue;
                }

                market.Add(new MarketDriverView(
                    person.Person.Value,
                    person.Name,
                    person.Nationality,
                    person.FreeSince?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
            }
        }

        return new DriversView(own, market);
    }

    private FinanceView ReadFinance(AccessContext access, bool withLedger = false)
    {
        var organization = Box.Require<BoardBook>().Section.OrganizationOf(_human.Value);
        if (organization is not OrganizationId id || Box.TryGet<FinanceBook>() is null)
        {
            return new FinanceView.Unknown(TranslationMessage.Of(FinanceKeys.ViewUnknown));
        }

        return FinanceQuery.Read(
            access,
            id,
            Session.World,
            Session.Date,
            Box.Require<IOrganizationControl>(),
            withLedger: withLedger);
    }

    private object ReadDevelopment(AccessContext access)
    {
        if (Box.Inputs.RulePeriods is not { } periods)
        {
            return new DevelopmentOverview([]);
        }

        var book = DevelopmentBook.ForSession(Session, Session.Clock.MasterSeed);
        var environment = new DevelopmentEnvironment(
            new PeriodDevelopmentRules(periods),
            Box.Require<IOrganizationControl>());
        return new DevelopmentQuery(book, environment).View(access);
    }

    private object ReadInfrastructure(AccessContext access)
    {
        if (Box.TryGet<InfrastructureBook>() is not { } book
            || Box.TryGet<InfrastructureEnvironment>() is not { } environment)
        {
            return new InfrastructureOverview([]);
        }

        return new InfrastructureQuery(book, environment).View(access, ChampionshipRead.Next(Session, Circuits).Country);
    }

    private SponsorView ReadSponsors(AccessContext access)
    {
        var organization = Box.Require<BoardBook>().Section.OrganizationOf(_human.Value);
        if (organization is not OrganizationId id
            || Box.TryGet<SponsorBook>() is not { } book
            || Box.TryGet<SponsorEnvironment>() is not { } environment)
        {
            return new SponsorView.Unknown(TranslationMessage.Of(SponsorKeys.ViewUnknown));
        }

        var objectives = new ObjectiveQuery(
            Box.Require<IObjectiveFacts>(),
            Box.Require<IManagerOrganizations>());
        return SponsorQuery.Read(access, id, book, environment, objectives, Session.Date);
    }

    private ManagerSupplyView ReadSupply(AccessContext access)
    {
        if (Box.TryGet<SupplyBook>() is not { } book
            || Box.TryGet<SupplyEnvironment>() is not { } environment)
        {
            return new ManagerSupplyView([], []);
        }

        return new SupplyQuery(book, environment).View(access);
    }

    private CarQuery Cars() => new(
        new CarBook(() => Session.World, Session.StoreWorld, Session.Clock.MasterSeed),
        Box.Require<IOrganizationControl>());

    private PoolQuery Pool() => new(PoolBook.ForSession(Session), Box.Require<IManagerOrganizations>(), Box.TryGet<IJuniorFunding>());

    private InboxBook Inbox() => Box.Require<InboxBook>();

    private BoardQuery Board() => new(
        Box.Require<BoardBook>(),
        Inbox(),
        new ObjectiveQuery(Box.Require<IObjectiveFacts>(), Box.Require<IManagerOrganizations>()));

    private ContractBook Contracts() => Box.Require<ContractBook>();

    private AccessContext Access() => AccessContext.ForManager(new AccessManagerId(_human.Value));

    private static CareerInputs LoadInputs(string dataRoot, AuthoredData data, IReadOnlyList<EngineSupplyLink> supplies)
    {
        var sponsors = SponsorsLoader.ToCatalog(SponsorsLoader.Load(dataRoot), localMarket: true);
        var tiers = TeamTiersLoader.ToSource(TeamTiersLoader.Load(dataRoot));
        var inputs = CareerInputs.From(data.EraPeriods, sponsors, new EraPayBenchmark(data.EraSetFor), tiers);
        return new CareerInputs
        {
            Eras = inputs.Eras,
            SponsorEras = inputs.SponsorEras,
            Sponsors = inputs.Sponsors,
            Pay = inputs.Pay,
            Tiers = inputs.Tiers,
            CarStrength = data.CarStrength,
            EraPeriods = inputs.EraPeriods,
            RulePeriods = data.Periods,
            SupplyLinks = supplies.Select(link => new SupplyLink(link.Constructor, link.Supplier, link.EngineName, link.SupplyType)).ToArray(),
        };
    }

    /// <summary>The same adapter <see cref="CareerHost"/> keeps private: advancing the gate lives the day.</summary>
    private sealed class SessionClock : IWorldState
    {
        private readonly CareerSession Session;

        public SessionClock(CareerSession session) => Session = session;

        public DateOnly CurrentDate => new(Session.Date.Year, Session.Date.Month, Session.Date.Day);

        public void AdvanceDate() => Session.LiveDay();

        public string ContentHash() => Session.World.StateHash();
    }
}

/// <summary>What <see cref="CareerBridge.Advance"/> did. A refusal carries the reason key and changes nothing.</summary>
public sealed record AdvanceOutcome(
    bool Advanced,
    TranslationMessage? Reason,
    string? Date,
    bool SeasonChanged = false,
    int? RaceSeason = null,
    int? RaceRound = null,
    string? RaceLayout = null);
