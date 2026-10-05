using System.Globalization;
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
using Paddock.Application.Managers;
using Paddock.Application.Objectives;
using Paddock.Application.Pool;
using Paddock.Application.Racing;
using Paddock.Application.Sponsors;
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
public sealed class CareerBridge
{
    public const string HumanManagerId = "human:player";

    public const int DefaultYear = 1955;

    public const ulong DefaultSeed = 1;

    private readonly CareerSession _session;
    private readonly CareerModuleHost _modules;
    private readonly CommandDispatcher _dispatcher;
    private readonly CommandQueue _queue;
    private readonly ReadyGate _gate = new();
    private readonly SessionClock _clock;
    private readonly HostManagerId _human;
    private bool _morningDone;

    private CareerBridge(
        CareerSession session,
        CareerModuleHost modules,
        CommandDispatcher dispatcher,
        CommandQueue queue,
        HostManagerId human)
    {
        _session = session;
        _modules = modules;
        _dispatcher = dispatcher;
        _queue = queue;
        _human = human;
        _clock = new SessionClock(session);
    }

    public string StateHash => _session.World.StateHash();

    public string DateText => _session.Date.ToString()!;

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
            LoadInputs(dataRoot, data, created.EngineSupplies, config),
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
        bridge.BeginDay();
        return bridge;
    }

    public JsonNode? Query(string name)
    {
        var access = AccessContext.ForManager(new AccessManagerId(_human.Value));
        return name switch
        {
            "shell" => BridgeValues.ToNode(ReadShell()),
            "inbox" => BridgeValues.ToNode(new InboxQuery(Inbox()).View(access)),
            "team" => BridgeValues.ToNode(ReadTeam()),
            "drivers" => BridgeValues.ToNode(ReadDrivers(access)),
            "cars" => BridgeValues.ToNode(Cars().View(access)),
            "development" => BridgeValues.ToNode(ReadDevelopment(access)),
            "sponsors" => BridgeValues.ToNode(ReadSponsors(access)),
            "finance" => BridgeValues.ToNode(ReadFinance(access)),
            "board" => BridgeValues.ToNode(Board().View(access, _session.Date)),
            "pool" => BridgeValues.ToNode(Pool().View(access)),
            "supply" => BridgeValues.ToNode(ReadSupply(access)),
            "negotiations" => BridgeValues.ToNode(new NegotiationQuery(Contracts()).View(access)),
            "standings" => BridgeValues.ToNode(ReadStandings()),
            "raceResult" => BridgeValues.ToNode(ReadRaceResult()),
            "raceReport" => BridgeValues.ToNode(ReadRaceReport()),
            _ => throw new InvalidOperationException("Query '" + name + "' is registered but not implemented."),
        };
    }

    public CommandResult Submit(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _queue.Enqueue(command);
        var results = _dispatcher.DispatchAll(_queue, Context());
        return results.Count == 0
            ? throw new InvalidOperationException("The queue accepted a command and then dispatched nothing.")
            : results[^1];
    }

    /// <summary>The last race, once, for the <c>raceFinished</c> push. Later queries still read it.</summary>
    public bool TakeFinishedRace(out RaceResultView result)
    {
        result = null!;
        var watch = _modules.Context.TryGet<RaceWatch>();
        if (watch is null || !watch.TryTake(out var season, out var round, out var layoutId, out _, out var lines, out _))
        {
            return false;
        }

        result = ResultView(season, round, layoutId, lines);
        return true;
    }

    public AdvanceOutcome Advance()
    {
        _modules.Context.Managers.SetReady(_human, true);
        var step = _gate.RequestAdvance(_modules.Context.Managers, _clock);
        if (step is AdvanceResult.Refused refused)
        {
            return new AdvanceOutcome(false, refused.Refusal.Reason, null);
        }

        _morningDone = false;
        BeginDay();
        return new AdvanceOutcome(true, null, DateText);
    }

    public DateOnly IssuedOn => new(_session.Date.Year, _session.Date.Month, _session.Date.Day);

    public bool IsHuman(string managerId) => string.Equals(managerId, _human.Value, StringComparison.Ordinal);

    public HostManagerId Human => _human;

    private void BeginDay()
    {
        if (_morningDone)
        {
            return;
        }

        _modules.BeginMorning(_queue);
        _dispatcher.DispatchAll(_queue, Context());
        _modules.EndMorning();
        _morningDone = true;
    }

    private CommandContext Context() => _modules.CommandContext(_clock, _modules.Context.Managers);

    private ShellView ReadShell()
    {
        var team = ReadTeam();
        var inbox = new InboxQuery(Inbox()).View(Access());
        var blocking = _modules.Context.Managers.Get(_human).BlockingItem;
        InboxItemView? decision = null;
        foreach (var item in inbox.Items)
        {
            if (item.NeedsDecision && item.Status == Paddock.Domain.Inbox.InboxStatus.Open)
            {
                decision = item;
                break;
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
            decision?.Subject.Key,
            team.OrganizationId,
            team.Name);
    }

    private OwnTeamView ReadTeam()
    {
        var organization = _modules.Context.Require<BoardBook>().Section.OrganizationOf(_human.Value);
        if (organization is not OrganizationId id)
        {
            return new OwnTeamView(null, null, null);
        }

        var name = _session.World.GetOrganization(id).NameOn(_session.Date);
        long? cash = ReadFinance(Access()) is FinanceView.Own own ? own.CashCents : null;
        return new OwnTeamView(id.Value, name, cash);
    }

    private DriversView ReadDrivers(AccessContext access)
    {
        var organization = _modules.Context.Require<BoardBook>().Section.OrganizationOf(_human.Value);
        var own = new List<OwnDriverView>();
        if (organization is OrganizationId id)
        {
            foreach (var contract in _session.World.Contracts.OrderBy(item => item.Id.Value, StringComparer.Ordinal))
            {
                if (contract.OrganizationId != id || !contract.Role.IsDriver || !contract.IsActiveOn(_session.Date))
                {
                    continue;
                }

                var person = _session.World.GetPerson(contract.PersonId);
                own.Add(new OwnDriverView(
                    person.Id.Value,
                    person.Name,
                    person.Nationality,
                    contract.Role.Seat.ToString()!,
                    contract.End.ToString()!));
            }
        }

        var market = new List<MarketDriverView>();
        if (organization is OrganizationId observer)
        {
            foreach (var person in new FreeAgentQuery(Contracts()).List(access, observer, _session.Date, NegotiationSubject.DriverSeat))
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

    private FinanceView ReadFinance(AccessContext access)
    {
        var organization = _modules.Context.Require<BoardBook>().Section.OrganizationOf(_human.Value);
        if (organization is not OrganizationId id || _modules.Context.TryGet<FinanceBook>() is null)
        {
            return new FinanceView.Unknown(TranslationMessage.Of(FinanceKeys.ViewUnknown));
        }

        return FinanceQuery.Read(
            access,
            id,
            _session.World,
            _session.Date,
            _modules.Context.Require<IOrganizationControl>());
    }

    private object ReadDevelopment(AccessContext access)
    {
        if (_modules.Context.Inputs.RulePeriods is not { } periods)
        {
            return new DevelopmentOverview([]);
        }

        var book = DevelopmentBook.ForSession(_session, _session.Clock.MasterSeed);
        var environment = new DevelopmentEnvironment(
            new PeriodDevelopmentRules(periods),
            _modules.Context.Require<IOrganizationControl>());
        return new DevelopmentQuery(book, environment).View(access);
    }

    private SponsorView ReadSponsors(AccessContext access)
    {
        var organization = _modules.Context.Require<BoardBook>().Section.OrganizationOf(_human.Value);
        if (organization is not OrganizationId id
            || _modules.Context.TryGet<SponsorBook>() is not { } book
            || _modules.Context.TryGet<SponsorEnvironment>() is not { } environment)
        {
            return new SponsorView.Unknown(TranslationMessage.Of(SponsorKeys.ViewUnknown));
        }

        var objectives = new ObjectiveQuery(
            _modules.Context.Require<IObjectiveFacts>(),
            _modules.Context.Require<IManagerOrganizations>());
        return SponsorQuery.Read(access, id, book, environment, objectives, _session.Date);
    }

    private ManagerSupplyView ReadSupply(AccessContext access)
    {
        if (_modules.Context.TryGet<SupplyBook>() is not { } book
            || _modules.Context.TryGet<SupplyEnvironment>() is not { } environment)
        {
            return new ManagerSupplyView([], []);
        }

        return new SupplyQuery(book, environment).View(access);
    }

    private CarQuery Cars() => new(
        new CarBook(() => _session.World, _session.StoreWorld, _session.Clock.MasterSeed),
        _modules.Context.Require<IOrganizationControl>());

    private PoolQuery Pool() => new(PoolBook.ForSession(_session), _modules.Context.Require<IManagerOrganizations>());

    private InboxBook Inbox() => _modules.Context.Require<InboxBook>();

    private BoardQuery Board() => new(
        _modules.Context.Require<BoardBook>(),
        Inbox(),
        new ObjectiveQuery(_modules.Context.Require<IObjectiveFacts>(), _modules.Context.Require<IManagerOrganizations>()));

    private ContractBook Contracts() => _modules.Context.Require<ContractBook>();

    private AccessContext Access() => AccessContext.ForManager(new AccessManagerId(_human.Value));

    private static CareerInputs LoadInputs(string dataRoot, AuthoredData data, IReadOnlyList<EngineSupplyLink> supplies, CareerConfig career)
    {
        var sponsors = SponsorsLoader.ToCatalog(SponsorsLoader.Load(dataRoot));
        var tiers = TeamTiersLoader.ToSource(TeamTiersLoader.Load(dataRoot));
        var inputs = CareerInputs.From(data.EraPeriods, sponsors, new EraPayBenchmark(data.EraSetFor), tiers);
        return new CareerInputs
        {
            Eras = inputs.Eras,
            SponsorEras = inputs.SponsorEras,
            Sponsors = inputs.Sponsors,
            Pay = inputs.Pay,
            Tiers = inputs.Tiers,
            EraPeriods = inputs.EraPeriods,
            RulePeriods = data.Periods,
            SupplyLinks = supplies.Select(link => new SupplyLink(link.Constructor, link.Supplier, link.EngineName, link.SupplyType)).ToArray(),
            Layouts = data.Layouts,
            RaceAssignments = data.RaceAssignments,
            RegulationDimensionIds = data.DimensionIds,
            EraDimensionIds = data.EraDimensionIds,
            RegulationCatalog = RuleCatalog.ToSpecs(data.Catalog),
            Rules = career.RulesSource,
            Fatality = career.FatalityLevel,
        };
    }

    private StandingsView ReadStandings()
    {
        var table = ChampionshipFacts.Table(_session, _modules.Context.Inputs);
        if (table is null)
        {
            return new StandingsView(_session.Date.Year, 0, 0, [], []);
        }

        return new StandingsView(
            _session.Date.Year,
            table.RoundsCompleted,
            table.TotalRounds,
            Rows(table.Drivers()),
            Rows(table.Constructors()));
    }

    private static IReadOnlyList<StandingRowView> Rows(IEnumerable<Paddock.Simulation.Racing.Points.StandingsRow> rows)
    {
        var list = new List<StandingRowView>();
        foreach (var row in rows)
        {
            list.Add(new StandingRowView(row.Position, row.Id, (double)row.CountedPoints));
        }

        return list;
    }

    private RaceResultView ReadRaceResult()
    {
        var watch = _modules.Context.TryGet<RaceWatch>();
        if (watch is null || watch.Round == 0 || watch.Lines.Count == 0)
        {
            throw new BridgeQueryException(BridgeKeys.NoRace);
        }

        return ResultView(watch.Season, watch.Round, watch.LayoutId, watch.Lines);
    }

    private RaceReportView ReadRaceReport()
    {
        var watch = _modules.Context.TryGet<RaceWatch>();
        if (watch?.Report is not { } input)
        {
            throw new BridgeQueryException(BridgeKeys.NoRace);
        }

        var report = RaceReportBuilder.Build(input);
        return new RaceReportView(
            input.Season,
            input.Round,
            input.TrackId,
            Line(report.Title),
            report.Sections.Select(section => new RaceReportSectionView(Line(section.Title), section.Lines.Select(Line).ToArray())).ToArray());
    }

    private static RaceResultView ResultView(int season, int round, string layoutId, IReadOnlyList<RaceResultLine> lines) =>
        new(
            season,
            round,
            layoutId,
            lines.Select(line => new RaceResultRowView(line.Position, line.Classified, line.DriverId, line.TeamId, line.Points)).ToArray());

    private static RaceReportLineView Line(RaceReportLine line)
    {
        var args = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in line.Args)
        {
            args[pair.Key] = pair.Value?.ToString() ?? "";
        }

        return new RaceReportLineView(line.Key, args);
    }

    /// <summary>The same adapter <see cref="CareerHost"/> keeps private: advancing the gate lives the day.</summary>
    private sealed class SessionClock : IWorldState
    {
        private readonly CareerSession _session;

        public SessionClock(CareerSession session) => _session = session;

        public DateOnly CurrentDate => new(_session.Date.Year, _session.Date.Month, _session.Date.Day);

        public void AdvanceDate() => _session.LiveDay();

        public string ContentHash() => _session.World.StateHash();
    }
}

/// <summary>What <see cref="CareerBridge.Advance"/> did. A refusal carries the reason key and changes nothing.</summary>
public sealed record AdvanceOutcome(bool Advanced, TranslationMessage? Reason, string? Date);

/// <summary>A query that cannot be answered. The host turns <see cref="Key"/> into the bridge error.</summary>
public sealed class BridgeQueryException : Exception
{
    public BridgeQueryException(string key) => Key = key;

    public string Key { get; }
}
