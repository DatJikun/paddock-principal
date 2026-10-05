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
using Paddock.Application.Sponsors;
using Paddock.Application.Supply;
using Paddock.Domain.Career;
using Paddock.Domain.Contracts;
using Paddock.Domain.Development;
using Paddock.Domain.Objectives;
using Paddock.Domain.People;
using Paddock.Domain.Supply;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Career;
using Paddock.Simulation.Racing.Points;
using Paddock.Simulation.Time;
using AccessManagerId = Paddock.Application.Access.ManagerId;
using HostManagerId = Paddock.Application.Managers.ManagerId;

namespace Paddock.Desktop.Bridge;

/// <summary>
/// One career in this process. The morning, the queue and the ready gate are the same calls
/// <see cref="CareerHost"/> makes; this type does not add a rule of its own.
/// </summary>
public sealed class CareerBridge
{
    /// <summary>The first human <see cref="CareerShell"/> registers. The page sends this id.</summary>
    public const string HumanManagerId = "human:1";

    public const int DefaultYear = 1955;

    public const ulong DefaultSeed = 1;

    private readonly CareerShell _shell;
    private readonly CareerConfig _config;
    private readonly string _worldDataHash;
    private readonly string _careerName;
    private readonly string? _noticeKey;
    private readonly IReadOnlyList<TrackLayout> _layouts;
    private readonly IReadOnlyList<RaceAssignment> _assignments;
    private readonly IReadOnlyList<string> _dimensionIds;
    private readonly IReadOnlyList<RulePeriod> _periods;

    private CareerBridge(
        CareerShell shell,
        CareerConfig config,
        string worldDataHash,
        string careerName,
        string? noticeKey,
        IReadOnlyList<TrackLayout> layouts,
        IReadOnlyList<RaceAssignment> assignments,
        IReadOnlyList<string> dimensionIds,
        IReadOnlyList<RulePeriod> periods)
    {
        _shell = shell;
        _config = config;
        _worldDataHash = worldDataHash;
        _careerName = careerName;
        _noticeKey = noticeKey;
        _layouts = layouts;
        _assignments = assignments;
        _dimensionIds = dimensionIds;
        _periods = periods;
    }

    public string StateHash => _shell.WorldHash;

    public string DateText => _shell.Date.ToString()!;

    public string? NoticeKey => _noticeKey;

    public CareerConfig Config => _config;

    public string WorldDataHash => _worldDataHash;

    public string CareerName => _careerName;

    internal static CareerBridge Adopt(
        CareerShell shell,
        CareerConfig config,
        string worldDataHash,
        string careerName,
        string? noticeKey,
        IReadOnlyList<TrackLayout> layouts,
        IReadOnlyList<RaceAssignment> assignments,
        IReadOnlyList<string> dimensionIds,
        IReadOnlyList<RulePeriod> periods)
    {
        if (!string.Equals(shell.Player.Value, HumanManagerId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The career shell registered '" + shell.Player.Value + "' instead of '" + HumanManagerId + "'.");
        }

        return new CareerBridge(shell, config, worldDataHash, careerName, noticeKey, layouts, assignments, dimensionIds, periods);
    }

    public JsonNode? Query(string name)
    {
        var access = AccessContext.ForManager(new AccessManagerId(_shell.Player.Value));
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
            "board" => BridgeValues.ToNode(Board().View(access, _shell.Session.Date)),
            "pool" => BridgeValues.ToNode(Pool().View(access)),
            "supply" => BridgeValues.ToNode(ReadSupply(access)),
            "negotiations" => BridgeValues.ToNode(new NegotiationQuery(Contracts()).View(access)),
            "calendar" => BridgeValues.ToNode(ReadCalendar()),
            "standings" => BridgeValues.ToNode(ReadStandings()),
            "nextRace" => BridgeValues.ToNode(ReadNextRace()),
            "staff" => BridgeValues.ToNode(ReadStaff()),
            "market" => BridgeValues.ToNode(ReadMarket(access)),
            _ => throw new InvalidOperationException("Query '" + name + "' is registered but not implemented."),
        };
    }

    public CommandResult Submit(ICommand command) => _shell.Submit(command);

    public AdvanceOutcome Advance()
    {
        var year = _shell.Date.Year;
        _shell.Ready(_shell.Player);
        var step = _shell.Advance();
        if (step is AdvanceResult.Refused refused)
        {
            return new AdvanceOutcome(false, refused.Refusal.Reason, null, false);
        }

        return new AdvanceOutcome(true, null, DateText, _shell.Date.Year != year);
    }

    public DateOnly IssuedOn => new(_shell.Date.Year, _shell.Date.Month, _shell.Date.Day);

    public bool IsHuman(string managerId) => string.Equals(managerId, _shell.Player.Value, StringComparison.Ordinal);

    public HostManagerId Human => _shell.Player;

    public CareerShell Shell => _shell;

    public string? TeamId => _shell.TeamOf(_shell.Player) is { } team ? team.Value : null;

    private ShellView ReadShell()
    {
        var team = ReadTeam();
        var inbox = new InboxQuery(Inbox()).View(Access());
        var blocking = _shell.Modules.Managers.Get(_shell.Player).BlockingItem;
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
            _shell.Player.Value,
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
        var organization = _shell.Modules.Require<BoardBook>().Section.OrganizationOf(_shell.Player.Value);
        if (organization is not OrganizationId id)
        {
            return new OwnTeamView(null, null, null);
        }

        var name = _shell.Session.World.GetOrganization(id).NameOn(_shell.Session.Date);
        long? cash = ReadFinance(Access()) is FinanceView.Own own ? own.CashCents : null;
        return new OwnTeamView(id.Value, name, cash);
    }

    private DriversView ReadDrivers(AccessContext access)
    {
        var organization = _shell.Modules.Require<BoardBook>().Section.OrganizationOf(_shell.Player.Value);
        var own = new List<OwnDriverView>();
        if (organization is OrganizationId id)
        {
            foreach (var contract in _shell.Session.World.Contracts.OrderBy(item => item.Id.Value, StringComparer.Ordinal))
            {
                if (contract.OrganizationId != id || !contract.Role.IsDriver || !contract.IsActiveOn(_shell.Session.Date))
                {
                    continue;
                }

                var person = _shell.Session.World.GetPerson(contract.PersonId);
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
            foreach (var person in new FreeAgentQuery(Contracts()).List(access, observer, _shell.Session.Date, NegotiationSubject.DriverSeat))
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
        var organization = _shell.Modules.Require<BoardBook>().Section.OrganizationOf(_shell.Player.Value);
        if (organization is not OrganizationId id || _shell.Modules.TryGet<FinanceBook>() is null)
        {
            return new FinanceView.Unknown(TranslationMessage.Of(FinanceKeys.ViewUnknown));
        }

        return FinanceQuery.Read(
            access,
            id,
            _shell.Session.World,
            _shell.Session.Date,
            _shell.Modules.Require<IOrganizationControl>());
    }

    private object ReadDevelopment(AccessContext access)
    {
        if (_shell.Modules.Inputs.RulePeriods is not { } periods)
        {
            return new DevelopmentOverview([]);
        }

        var book = DevelopmentBook.ForSession(_shell.Session, _shell.Session.Clock.MasterSeed);
        var environment = new DevelopmentEnvironment(
            new PeriodDevelopmentRules(periods),
            _shell.Modules.Require<IOrganizationControl>());
        return new DevelopmentQuery(book, environment).View(access);
    }

    private SponsorView ReadSponsors(AccessContext access)
    {
        var organization = _shell.Modules.Require<BoardBook>().Section.OrganizationOf(_shell.Player.Value);
        if (organization is not OrganizationId id
            || _shell.Modules.TryGet<SponsorBook>() is not { } book
            || _shell.Modules.TryGet<SponsorEnvironment>() is not { } environment)
        {
            return new SponsorView.Unknown(TranslationMessage.Of(SponsorKeys.ViewUnknown));
        }

        var objectives = new ObjectiveQuery(
            _shell.Modules.Require<IObjectiveFacts>(),
            _shell.Modules.Require<IManagerOrganizations>());
        return SponsorQuery.Read(access, id, book, environment, objectives, _shell.Session.Date);
    }

    private ManagerSupplyView ReadSupply(AccessContext access)
    {
        if (_shell.Modules.TryGet<SupplyBook>() is not { } book
            || _shell.Modules.TryGet<SupplyEnvironment>() is not { } environment)
        {
            return new ManagerSupplyView([], []);
        }

        return new SupplyQuery(book, environment).View(access);
    }

    private CarQuery Cars() => new(
        new CarBook(() => _shell.Session.World, _shell.Session.StoreWorld, _shell.Session.Clock.MasterSeed),
        _shell.Modules.Require<IOrganizationControl>());

    private PoolQuery Pool() => new(PoolBook.ForSession(_shell.Session), _shell.Modules.Require<IManagerOrganizations>());

    private InboxBook Inbox() => _shell.Modules.Require<InboxBook>();

    private BoardQuery Board() => new(
        _shell.Modules.Require<BoardBook>(),
        Inbox(),
        new ObjectiveQuery(_shell.Modules.Require<IObjectiveFacts>(), _shell.Modules.Require<IManagerOrganizations>()));

    private ContractBook Contracts() => _shell.Modules.Require<ContractBook>();

    private AccessContext Access() => AccessContext.ForManager(new AccessManagerId(_shell.Player.Value));

    private CalendarView ReadCalendar()
    {
        var season = _shell.Date.Year;
        var rounds = new List<CalendarRoundView>();
        foreach (var session in WeekendSessions(season))
        {
            if (session.TypeId != ScheduledEventType.Race || session.Payload is not RaceSessionPayload race)
            {
                continue;
            }

            var layout = Layout(race.LayoutId);
            rounds.Add(new CalendarRoundView(
                race.Round,
                race.LayoutId,
                layout?.CircuitId ?? string.Empty,
                Iso(session.Date),
                Iso(SessionOn(season, race.Round, ScheduledEventType.Qualifying)),
                Iso(SessionOn(season, race.Round, ScheduledEventType.Practice))));
        }

        rounds.Sort(static (left, right) => left.Round.CompareTo(right.Round));
        return new CalendarView(season, rounds);
    }

    private StandingsView ReadStandings()
    {
        var calendar = ReadCalendar();
        var rules = PointsRules.For(RuleSet.For(_shell.Date.Year, _dimensionIds, _periods));
        var scale = new int[rules.PositionPoints.Length];
        for (var i = 0; i < scale.Length; i++)
        {
            scale[i] = rules.PositionPoints[i];
        }

        if (calendar.Rounds.Count == 0)
        {
            return new StandingsView(_shell.Date.Year, 0, scale, [], []);
        }

        var table = Standings.Start(rules, calendar.Rounds.Count);
        return new StandingsView(
            _shell.Date.Year,
            calendar.Rounds.Count,
            scale,
            Rows(table.Drivers()),
            Rows(table.Constructors()));
    }

    private NextRaceView ReadNextRace()
    {
        var today = _shell.Date;
        CalendarRoundView? next = null;
        foreach (var round in ReadCalendar().Rounds)
        {
            if (string.CompareOrdinal(round.RaceDate, today.ToString()) < 0)
            {
                continue;
            }

            if (next is null || round.Round < next.Round)
            {
                next = round;
            }
        }

        return next is null
            ? new NextRaceView(null, null, null, null)
            : new NextRaceView(next.Round, next.LayoutId, next.CircuitId, next.RaceDate);
    }

    private OwnStaffView ReadStaff()
    {
        var own = new List<StaffMemberView>();
        if (OwnOrganization() is not OrganizationId id)
        {
            return new OwnStaffView(own);
        }

        foreach (var contract in _shell.Session.World.Contracts.OrderBy(item => item.Id.Value, StringComparer.Ordinal))
        {
            if (contract.OrganizationId != id || !contract.Role.IsStaff || !contract.IsActiveOn(_shell.Date))
            {
                continue;
            }

            var person = _shell.Session.World.GetPerson(contract.PersonId);
            own.Add(new StaffMemberView(
                person.Id.Value,
                person.Name,
                person.Nationality,
                contract.Role.StaffRole.ToString()!,
                contract.End.ToString()!));
        }

        return new OwnStaffView(own);
    }

    private MarketView ReadMarket(AccessContext access)
    {
        var free = new List<MarketPersonView>();
        var contracted = new List<MarketPersonView>();
        if (OwnOrganization() is not OrganizationId observer)
        {
            return new MarketView(free, contracted);
        }

        foreach (var person in new FreeAgentQuery(Contracts()).List(access, observer, _shell.Date, NegotiationSubject.DriverSeat))
        {
            if (!person.IsDriver)
            {
                continue;
            }

            free.Add(new MarketPersonView(
                person.Person.Value,
                person.Name,
                person.Nationality,
                null,
                person.FreeSince?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                person.KnownAttributes));
        }

        foreach (var contract in _shell.Session.World.Contracts.OrderBy(item => item.PersonId.Value, StringComparer.Ordinal))
        {
            if (contract.OrganizationId == observer || !contract.Role.IsDriver || !contract.IsActiveOn(_shell.Date))
            {
                continue;
            }

            if (_shell.Session.World.KnowledgeOf(observer, contract.PersonId) is not PersonKnowledgeView belief)
            {
                continue;
            }

            var person = _shell.Session.World.GetPerson(contract.PersonId);
            contracted.Add(new MarketPersonView(
                person.Id.Value,
                person.Name,
                person.Nationality,
                contract.OrganizationId.Value,
                null,
                belief.Attributes.Select(attribute => new KnownAttributeView(attribute.Key, attribute.Band.Low, attribute.Band.High)).ToArray()));
        }

        return new MarketView(free, contracted);
    }

    private OrganizationId? OwnOrganization() => _shell.TeamOf(_shell.Player);

    private IReadOnlyList<ScheduledEvent> WeekendSessions(int season)
    {
        var clock = SeasonCalendar.Schedule(
            new WorldClockState(new GameDate(season, 1, 1), _shell.Session.Clock.MasterSeed),
            season,
            _layouts,
            _assignments);
        var sessions = new List<ScheduledEvent>();
        foreach (var scheduled in clock.Queue.Events)
        {
            if (scheduled.Payload is RaceSessionPayload race && race.Season == season)
            {
                sessions.Add(scheduled);
            }
        }

        return sessions;
    }

    private GameDate? SessionOn(int season, int round, string typeId)
    {
        foreach (var scheduled in WeekendSessions(season))
        {
            if (scheduled.TypeId == typeId && scheduled.Payload is RaceSessionPayload race && race.Round == round)
            {
                return scheduled.Date;
            }
        }

        return null;
    }

    private TrackLayout? Layout(string layoutId)
    {
        foreach (var layout in _layouts)
        {
            if (string.Equals(layout.Id, layoutId, StringComparison.Ordinal))
            {
                return layout;
            }
        }

        return null;
    }

    private static string Iso(GameDate? date) => date?.ToString() ?? string.Empty;

    private static StandingRowView[] Rows(IReadOnlyList<StandingsRow> rows)
    {
        var copy = new StandingRowView[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            copy[i] = new StandingRowView(row.Position, row.Id, row.CountedPoints.ToString(CultureInfo.InvariantCulture));
        }

        return copy;
    }
}

/// <summary>What <see cref="CareerBridge.Advance"/> did. A refusal carries the reason key and changes nothing.</summary>
public sealed record AdvanceOutcome(bool Advanced, TranslationMessage? Reason, string? Date, bool SeasonChanged);
