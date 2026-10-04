using Paddock.Application.Board;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Application.Objectives;
using Paddock.Application.World;
using Paddock.Domain.Board;
using Paddock.Domain.Inbox;
using Paddock.Domain.Objectives;
using Paddock.Domain.People;
using Paddock.Domain.Spy;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Objectives;
using Paddock.Simulation.Time;
using Paddock.Tests.Contracts;

namespace Paddock.Tests.Board;

/// <summary>
/// Fixtures for the board tests. Everything here is a SYNTHETIC fixture: five invented teams with budgets in a fixed order, a few
/// principals, the standings the tests set by hand, and a flat pay benchmark. None of it is a historical fact, and every threshold
/// the tests exercise is an uncalibrated ESTIMATE of <see cref="BoardEstimates"/>.
/// </summary>
internal static class BoardKit
{
    public const string DecisionKind = "test.decision";

    public static readonly GameDate Start = new(1955, 1, 1);

    public static readonly OrganizationId T1 = OrganizationId.Real("fixture_team_1");

    public static readonly OrganizationId T2 = OrganizationId.Real("fixture_team_2");

    public static readonly OrganizationId T3 = OrganizationId.Real("fixture_team_3");

    public static readonly OrganizationId T4 = OrganizationId.Real("fixture_team_4");

    public static readonly OrganizationId T5 = OrganizationId.Real("fixture_team_5");

    public static readonly OrganizationId[] Teams = [T1, T2, T3, T4, T5];

    public static readonly ManagerId Pam = new("mgr-pam");

    public static readonly ManagerId Quinn = new("mgr-quinn");

    public static PersonId Principal(int number) => PersonId.Real("fixture_principal_" + number);

    public static DateOnly Date(GameDate date) => new(date.Year, date.Month, date.Day);

    /// <summary>
    /// Five teams founded in 1950 with budgets from 5 million (team 1) down to 1 million (team 5). Principals 1 to 3 run teams 1 to 3
    /// under contracts to the end of 1960; teams 4 and 5 have none. <paramref name="free"/> more principals are free to hire.
    /// </summary>
    public static WorldState BuildWorld(int free)
    {
        var world = WorldState.At(Start);
        var budget = 5_000_000L;
        foreach (var team in Teams)
        {
            (world, _) = world.AddOrganization(new OrganizationSpec(
                OrganizationKind.Team,
                true,
                team.Value,
                new GameDate(1950, 1, 1),
                null,
                budget,
                [new OrganizationNameSpan("Fixture " + team.Value, new GameDate(1950, 1, 1), null)]));
            budget -= 1_000_000;
        }

        for (var i = 1; i <= 3 + free; i++)
        {
            var id = Principal(i);
            (world, _) = world.AddPerson(new PersonSpec(
                "Pat" + i,
                "Principal",
                new GameDate(1910 + i, 2, 2),
                "GBR",
                true,
                id.Value,
                [PersonRole.TeamPrincipal],
                ContractKit.StaffTruth(StaffRole.TeamPrincipal, 12)));
            if (i <= 3)
            {
                (world, _) = world.AddContract(new ContractSpec(
                    id, Teams[i - 1], ContractRole.Staff(StaffRole.TeamPrincipal), new GameDate(1950, 1, 1), new GameDate(1960, 12, 31), 0, true, null, null));
            }
        }

        return world;
    }

    /// <summary>The standings and money the tests set by hand. An unset value is unknown to the board.</summary>
    public sealed class FakeFacts : IObjectiveFacts
    {
        private readonly Dictionary<string, decimal> _position = new(StringComparer.Ordinal);
        private readonly Dictionary<string, decimal> _cash = new(StringComparer.Ordinal);
        private readonly Dictionary<string, decimal> _people = new(StringComparer.Ordinal);

        public void Position(OrganizationId team, int position) => _position[team.Value] = position;

        public void AllPositions(int position)
        {
            foreach (var team in Teams)
            {
                Position(team, position);
            }
        }

        public void Cash(OrganizationId team, decimal cash) => _cash[team.Value] = cash;

        public void People(OrganizationId team, decimal value) => _people[team.Value] = value;

        public decimal? Number(OrganizationId owner, string factKey)
        {
            var map = factKey switch
            {
                ObjectiveFactKeys.ChampionshipPosition => _position,
                ObjectiveFactKeys.Cash => _cash,
                BoardFactKeys.PeopleDevelopment => _people,
                _ => null,
            };
            return map is not null && map.TryGetValue(owner.Value, out var value) ? value : null;
        }

        public bool? Flag(OrganizationId owner, string factKey, string argument) => null;
    }

    public sealed class FakeHistory : IBoardHistory
    {
        private readonly Dictionary<(string Team, int Season), int> _positions = [];

        public void Set(OrganizationId team, int season, int position) => _positions[(team.Value, season)] = position;

        public int? FinalPosition(OrganizationId organization, int season) =>
            _positions.TryGetValue((organization.Value, season), out var position) ? position : null;
    }

    private sealed class AckResolver : IInboxResolver
    {
        public string Kind => DecisionKind;

        public TranslationMessage? Validate(InboxItem item, string optionId, CommandContext context) => null;

        public IReadOnlyList<IDomainEvent> Execute(InboxItem item, string optionId, CommandContext context) => [];
    }

    /// <summary>
    /// A game in a box: the fixture world, two human managers (Pam and Quinn, no team until the test gives them one), the contract
    /// book with the board's reputation hook, the inbox with its resolvers, the command pipeline, and the day clock with the
    /// objective and board handlers. After each lived day it applies the objective outcomes and runs the lapsed inbox items as
    /// commands, as a real host does.
    /// </summary>
    public sealed class Lab
    {
        private readonly LabWorld _world;

        public Lab(
            ulong masterSeed = 11UL,
            int free = 3,
            ITraceSink? trace = null,
            IBoardHistory? history = null,
            bool noRaces = false,
            IBoardSeverance? severance = null)
        {
            Appeal = new ContractKit.FakeAppeal();
            Facts = new FakeFacts();
            Environment = new ContractEnvironment(
                new ContractKit.FakePersonality(),
                new ContractKit.FlatPay(),
                new TenureControl(() => Board!.Section),
                Appeal,
                null,
                null,
                null,
                null,
                trace,
                new BoardReputationSource(() => Board!.Section));
            Contracts = new ContractBook(BuildWorld(free), Environment);
            Board = new BoardBook(Contracts, Facts, masterSeed, history, severance: severance);
            Managers = new ManagerRegistry();
            Managers.Register(Pam, ManagerKind.Human, "Pam");
            Managers.Register(Quinn, ManagerKind.Human, "Quinn");
            var resolvers = new InboxResolvers();
            resolvers.Register(new AckResolver());
            Inbox = new InboxBook(resolvers);
            Dispatcher = new CommandDispatcher();
            Dispatcher.Register(new ResolveInboxItemHandler());
            Dispatcher.Register(new DismissInboxItemHandler());
            Dispatcher.Register(new ExpireInboxItemHandler());
            BoardRegistration.Register(Dispatcher, resolvers);
            Queue = new CommandQueue();
            Clock = new WorldClockState(Start, masterSeed);
            _world = new LabWorld(this);
            Context = new CommandContext(_world, Managers, Inbox, Contracts, Board);
            Engine = new BoardEngine(Board, Inbox, Managers);
            Handlers = new DayHandlerRegistry([new ObjectiveDayHandler(() => Board.Objectives, Facts), new BoardDayHandler(Engine)]);
            Gate = new ReadyGate();
            Query = new BoardQuery(Board, Inbox, new ObjectiveQuery(Facts, Board.Organizations()));
            if (!noRaces)
            {
                ScheduleRaces(1955, 1958);
            }
        }

        public ContractKit.FakeAppeal Appeal { get; }

        public FakeFacts Facts { get; }

        public ContractEnvironment Environment { get; }

        public ContractBook Contracts { get; }

        public BoardBook Board { get; }

        public ManagerRegistry Managers { get; }

        public InboxBook Inbox { get; }

        public CommandDispatcher Dispatcher { get; }

        public CommandQueue Queue { get; }

        public CommandContext Context { get; }

        public BoardEngine Engine { get; }

        public DayHandlerRegistry Handlers { get; }

        public BoardQuery Query { get; }

        public ReadyGate Gate { get; }

        public IWorldState Time => _world;

        public WorldClockState Clock { get; set; }

        public GameDate Today => Clock.Date;

        public List<DomainEvent> Events { get; } = [];

        public BoardSection Section => Board.Section;

        public WorldState World => Board.World;

        public string Hash() =>
            Inbox.Into(Board.Into()).StateHash() + ":" + WorldClockHash.Compute(Clock).ToString("x16", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Twenty races a season, two weeks apart from 1 March (SYNTHETIC calendar, not the historical one).</summary>
        public void ScheduleRaces(int fromSeason, int toSeason)
        {
            for (var season = fromSeason; season <= toSeason; season++)
            {
                for (var i = 0; i < 20; i++)
                {
                    Clock = Clock.Enqueue(new ScheduledEvent(
                        new EventId("race-" + season + "-" + i),
                        new GameDate(season, 3, 1).AddDays(i * 14),
                        ScheduledEventType.Race,
                        new RaceSessionPayload(season, i + 1, "fixture_layout")));
                }
            }
        }

        /// <summary>Boards exist, then the manager runs the team (a fresh appointment, protected like any new principal).</summary>
        public void Appoint(ManagerId manager, OrganizationId team, bool founder = false)
        {
            Engine.EnsureBoards(Today);
            Engine.AppointHuman(manager, team, Today, founder, BoardKeys.OfferSubject);
        }

        /// <summary>Puts a manager without a team into the section directly, with a given reputation.</summary>
        public void Dismissed(ManagerId manager, int reputationTenths, OrganizationId? former = null)
        {
            Engine.EnsureBoards(Today);
            Board.Update(Section
                .WithInitialReputation(manager.Value, reputationTenths)
                .WithUnemployed(new UnemployedRecord(manager.Value, Today, former, 0, 0, null)));
        }

        public CommandResult Submit(ICommand command)
        {
            Queue.Enqueue(command);
            return Dispatcher.DispatchAll(Queue, Context).Single();
        }

        public InboxItem[] OpenOffers(ManagerId manager) =>
            Inbox.Section.ItemsOf(manager.Value).Where(item => item.IsOpen && item.Kind == BoardEngine.OfferKind).ToArray();

        public InboxItem[] OffersEver(ManagerId manager) =>
            Inbox.Section.ItemsOf(manager.Value).Where(item => item.Kind == BoardEngine.OfferKind).ToArray();

        public void LiveDay()
        {
            var step = WorldClock.AdvanceDay(Clock, Handlers);
            Clock = step.State;
            Events.AddRange(step.Events);
            BoardOutcomes.Apply(Engine, step.Events);
            Contracts.UseWorld(Contracts.World.WithDate(Clock.Date));
            InboxExpiry.EnqueueDue(Queue, Inbox, Clock.Date);
            Dispatcher.DispatchAll(Queue, Context);
        }

        public void Advance(int days)
        {
            for (var i = 0; i < days; i++)
            {
                LiveDay();
            }
        }

        public void AdvanceTo(GameDate date)
        {
            while (Clock.Date < date)
            {
                LiveDay();
            }
        }

        public IReadOnlyList<DomainEvent> EventsOf(string typeId) => Events.Where(e => e.TypeId == typeId).ToArray();

        private sealed class LabWorld : IWorldState
        {
            private readonly Lab _lab;

            public LabWorld(Lab lab) => _lab = lab;

            public DateOnly CurrentDate => Date(_lab.Today);

            public void AdvanceDate() => _lab.LiveDay();

            public string ContentHash() => _lab.Hash();
        }
    }
}
