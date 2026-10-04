using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Application.World;
using Paddock.Domain.Contracts;
using Paddock.Domain.People;
using Paddock.Domain.Spy;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Time;
using Paddock.Tests.Persistence;

namespace Paddock.Tests.Contracts;

/// <summary>
/// Fixtures for the contract and negotiation tests. Everything here is a SYNTHETIC fixture: the teams, people, bands, salaries,
/// appeal values and personalities are invented to exercise the rules, not historical facts and not calibrated numbers.
/// The pay benchmark is a flat 100 000 so that arithmetic in the tests is easy to follow.
/// </summary>
internal static class ContractKit
{
    public const long Reference = 100_000;

    public static readonly GameDate Start = new(1955, 6, 1);

    public static readonly OrganizationId TeamA = OrganizationId.Real("fixture_team_a");

    public static readonly OrganizationId TeamB = OrganizationId.Real("fixture_team_b");

    public static readonly OrganizationId TeamC = OrganizationId.Real("fixture_team_c");

    public static readonly ManagerId Anna = new("mgr-anna");

    public static readonly ManagerId Bram = new("mgr-bram");

    public static readonly ManagerId Bot = new("mgr-bot");

    public static readonly PersonId DriverX = PersonId.Real("fixture_driver_x");

    public static readonly PersonId DriverY = PersonId.Real("fixture_driver_y");

    /// <summary>Under contract at team B until 31 December 1955 (213 days from the start).</summary>
    public static readonly PersonId Veteran = PersonId.Real("fixture_driver_veteran");

    /// <summary>Under contract at team B until the end of 1958.</summary>
    public static readonly PersonId Locked = PersonId.Real("fixture_driver_locked");

    public static readonly PersonId Strategist = PersonId.Real("fixture_staff_strategist");

    public static readonly PersonId PrincipalA = PersonId.Real("fixture_staff_principal_a");

    /// <summary>The personality most tests use: every hidden trait at 10, so no trait tilts a weight except the primary one.</summary>
    public static readonly PersonalityTraits Balanced = new(PrimaryPersonality.TeamPlayer, 10, 10, 10, 10, 10);

    public static string Reason(CommandResult result) =>
        Assert.IsType<CommandResult.Rejected>(result).Reason.Key;

    public static DateOnly Date(GameDate date) => new(date.Year, date.Month, date.Day);

    public static OfferTerms Terms(
        long salary = Reference,
        SeatStatus? seat = SeatStatus.Equal,
        int years = 2,
        long pointsBonus = 0,
        long winBonus = 0,
        long titleBonus = 0,
        OfferOption? option = null,
        ExitClause? exit = null) =>
        new(salary, pointsBonus, winBonus, titleBonus, years, seat, option, exit);

    public static PersonTruth Truth(int cornering, int rest = 12)
    {
        var now = new DriverAttributes(cornering, rest, rest, rest, rest, rest, rest, rest, rest, rest, rest);
        var ceiling = new DriverAttributes(
            Math.Max(cornering, 18),
            Math.Max(rest, 18),
            Math.Max(rest, 18),
            Math.Max(rest, 18),
            Math.Max(rest, 18),
            Math.Max(rest, 18),
            Math.Max(rest, 18),
            Math.Max(rest, 18),
            Math.Max(rest, 18),
            Math.Max(rest, 18),
            Math.Max(rest, 18));
        return PersonTruth.FromDriver(now, ceiling);
    }

    public static PersonTruth StaffTruth(StaffRole role, int value)
    {
        var keys = StaffCatalogue.AttributeKeys(role);
        var attributes = keys.Select(key => new NamedAttribute(key, value)).ToArray();
        var ceilings = keys.Select(key => new NamedAttribute(key, Math.Min(20, value + 2))).ToArray();
        return new PersonTruth(attributes, ceilings);
    }

    /// <summary>
    /// The fixture world: three teams, free drivers X and Y, a veteran and a locked driver at team B, a free strategist and
    /// team A's principal. Each team believes a band of three points around the true value for every person it could hire.
    /// <paramref name="hiddenCornering"/> is the one true attribute that tests vary: it stays inside the believed band.
    /// </summary>
    public static WorldState BuildWorld(int hiddenCornering = 12)
    {
        var world = WorldState.At(Start);
        foreach (var (key, id) in new[] { ("fixture_team_a", TeamA), ("fixture_team_b", TeamB), ("fixture_team_c", TeamC) })
        {
            (world, _) = world.AddOrganization(new OrganizationSpec(
                OrganizationKind.Team,
                true,
                key,
                new GameDate(1950, 1, 1),
                null,
                1_000_000,
                [new OrganizationNameSpan("Fixture " + key, new GameDate(1950, 1, 1), null)]));
        }

        var drivers = new[]
        {
            (DriverX, "Ada", "Xavier", 1927, Truth(hiddenCornering)),
            (DriverY, "Bo", "Yates", 1926, Truth(12)),
            (Veteran, "Vera", "Veteran", 1925, Truth(13)),
            (Locked, "Lars", "Locked", 1928, Truth(11)),
        };
        foreach (var (id, given, family, born, truth) in drivers)
        {
            (world, _) = world.AddPerson(new PersonSpec(given, family, new GameDate(born, 3, 14), "GBR", true, id.Value, [PersonRole.Driver], truth));
        }

        (world, _) = world.AddPerson(new PersonSpec(
            "Sam", "Strategist", new GameDate(1920, 5, 5), "GBR", true, Strategist.Value, [PersonRole.Staff(StaffRole.Strategist)], StaffTruth(StaffRole.Strategist, 11)));
        (world, _) = world.AddPerson(new PersonSpec(
            "Pat", "Principal", new GameDate(1915, 2, 2), "GBR", true, PrincipalA.Value, [PersonRole.TeamPrincipal], StaffTruth(StaffRole.TeamPrincipal, 14)));

        (world, _) = world.AddContract(new ContractSpec(
            Veteran, TeamB, ContractRole.Driver(SeatStatus.Equal), new GameDate(1955, 1, 1), new GameDate(1955, 12, 31), 100_000, true, null, null));
        (world, _) = world.AddContract(new ContractSpec(
            Locked, TeamB, ContractRole.Driver(SeatStatus.NumberOne), new GameDate(1955, 1, 1), new GameDate(1958, 12, 31), 100_000, true, null, null));
        (world, _) = world.AddContract(new ContractSpec(
            PrincipalA, TeamA, ContractRole.Staff(StaffRole.TeamPrincipal), new GameDate(1955, 1, 1), new GameDate(1957, 12, 31), 0, true, null, null));

        foreach (var team in new[] { TeamA, TeamB, TeamC })
        {
            foreach (var person in world.Persons.ToArray())
            {
                // The team's belief never moves with the hidden value: driver X is believed to be a 12 whatever the truth is.
                var believed = person.Id == DriverX ? Truth(12) : person.Truth;
                var attributes = believed.Attributes
                    .Select(attribute => new KnownAttribute(
                        attribute.Key,
                        new AttributeBand(Math.Max(1, attribute.Value - 1), Math.Min(20, attribute.Value + 1))))
                    .ToArray();
                world = world.SetKnowledge(new PersonKnowledge(team, person.Id, attributes, null));
            }
        }

        return world;
    }

    /// <summary>Pay that does not depend on stars, so the reference salary is always <see cref="Reference"/>.</summary>
    public sealed class FlatPay : IPayBenchmark
    {
        public long Reference(int season, NegotiationSubject subject, double stars) => ContractKit.Reference;
    }

    public sealed class FakeAppeal : IOrganizationAppealSource
    {
        private readonly Dictionary<string, OrganizationAppeal> _byOrganization = new(StringComparer.Ordinal);

        public void Set(OrganizationId organization, OrganizationAppeal appeal) => _byOrganization[organization.Value] = appeal;

        public OrganizationAppeal Appeal(OrganizationId organization, GameDate on) =>
            _byOrganization.TryGetValue(organization.Value, out var appeal) ? appeal : OrganizationAppeal.Neutral;
    }

    public sealed class FakePersonality : IPersonalitySource
    {
        private readonly Dictionary<string, PersonalityTraits> _byPerson = new(StringComparer.Ordinal);

        public PersonalityTraits Default { get; set; } = Balanced;

        public void Set(PersonId person, PersonalityTraits traits) => _byPerson[person.Value] = traits;

        public PersonalityTraits TraitsOf(PersonId person) => _byPerson.TryGetValue(person.Value, out var traits) ? traits : Default;
    }

    public sealed class FakeTrust : ITrustSource
    {
        private readonly Dictionary<string, double> _byOrganization = new(StringComparer.Ordinal);

        public void Set(OrganizationId organization, double trust) => _byOrganization[organization.Value] = trust;

        public double Trust(PersonId person, OrganizationId organization) =>
            _byOrganization.TryGetValue(organization.Value, out var trust) ? trust : NeutralTrustSource.Neutral;
    }

    public sealed class FakeStandings : IConstructorStandings
    {
        private readonly Dictionary<string, int> _positions = new(StringComparer.Ordinal);

        public void Set(OrganizationId organization, int position) => _positions[organization.Value] = position;

        public int? Position(OrganizationId organization, int season) =>
            _positions.TryGetValue(organization.Value, out var position) ? position : null;
    }

    public sealed class FakePayroll : IPayrollLedger
    {
        private readonly Dictionary<string, long> _budget = new(StringComparer.Ordinal);

        public List<(OrganizationId Payer, PersonId Payee, long Amount)> Compensation { get; } = [];

        public void Cap(OrganizationId organization, long annualBudget) => _budget[organization.Value] = annualBudget;

        public bool CanCommit(OrganizationId organization, long annualSalary, int years, GameDate on) =>
            !_budget.TryGetValue(organization.Value, out var cap) || annualSalary <= cap;

        public bool CanPay(OrganizationId organization, long amount, GameDate on) =>
            !_budget.TryGetValue(organization.Value, out var cap) || amount <= cap;

        public void RecordCompensation(OrganizationId payer, PersonId payee, long amount, GameDate on) =>
            Compensation.Add((payer, payee, amount));
    }

    /// <summary>
    /// A game in a box: the fixture world, three managers (Anna runs team A, Bram runs team B, both human; a bot runs team C),
    /// the contract book, the inbox with its resolvers, the command pipeline, the ready gate, and the day clock with the two
    /// contract handlers. Time only moves through <see cref="Gate"/> or <see cref="Advance"/>, as in a real host.
    /// </summary>
    public sealed class Lab
    {
        private readonly LabWorld _world;

        public Lab(
            int hiddenCornering = 12,
            ITraceSink? trace = null,
            ulong masterSeed = 7UL,
            Func<WorldState, WorldState>? customize = null,
            Func<ContractsSection, ContractsSection>? customizeSection = null)
        {
            var world = BuildWorld(hiddenCornering);
            if (customize is not null)
            {
                world = customize(world);
            }

            Appeal = new FakeAppeal();
            Personality = new FakePersonality();
            Standings = new FakeStandings();
            Trust = new FakeTrust();
            Payroll = new FakePayroll();
            Control = new ControlTable().Assign(Anna, TeamA).Assign(Bram, TeamB).Assign(Bot, TeamC);
            Environment = new ContractEnvironment(
                Personality,
                new FlatPay(),
                Control,
                Appeal,
                Trust,
                Payroll,
                Standings,
                null,
                trace);
            Book = new ContractBook(world, Environment, customizeSection?.Invoke(ContractsSection.Empty));
            Managers = new ManagerRegistry();
            Managers.Register(Anna, ManagerKind.Human, "Anna");
            Managers.Register(Bram, ManagerKind.Human, "Bram");
            Managers.Register(Bot, ManagerKind.Ai, "Bot");
            var resolvers = new InboxResolvers();
            Inbox = new InboxBook(resolvers);
            Dispatcher = new CommandDispatcher();
            ContractRegistration.Register(Dispatcher, resolvers);
            Dispatcher.Register(new ResolveInboxItemHandler());
            Dispatcher.Register(new DismissInboxItemHandler());
            Dispatcher.Register(new ExpireInboxItemHandler());
            Queue = new CommandQueue();
            Clock = new WorldClockState(Start, masterSeed);
            _world = new LabWorld(this);
            Context = new CommandContext(_world, Managers, Inbox, Book);
            Engine = new ContractEngine(Book, Inbox, Managers);
            Handlers = new DayHandlerRegistry([new NegotiationDayHandler(Engine), new ContractLifecycleHandler(Engine)]);
            Query = new NegotiationQuery(Book);
            Gate = new ReadyGate();
        }

        public FakeAppeal Appeal { get; }

        public FakePersonality Personality { get; }

        public FakeStandings Standings { get; }

        public FakeTrust Trust { get; }

        public FakePayroll Payroll { get; }

        public ControlTable Control { get; }

        public ContractEnvironment Environment { get; }

        public ContractBook Book { get; }

        public ManagerRegistry Managers { get; }

        public InboxBook Inbox { get; }

        public CommandDispatcher Dispatcher { get; }

        public CommandQueue Queue { get; }

        public CommandContext Context { get; }

        public ContractEngine Engine { get; }

        public DayHandlerRegistry Handlers { get; }

        public NegotiationQuery Query { get; }

        public ReadyGate Gate { get; }

        public WorldClockState Clock { get; set; }

        public GameDate Today => Clock.Date;

        public List<DomainEvent> Events { get; } = [];

        public ContractsSection Section => Book.Section;

        public WorldState World => Book.World;

        public string Hash() =>
            Inbox.Into(Book.Into()).StateHash() + ":" + WorldClockHash.Compute(Clock).ToString("x16", System.Globalization.CultureInfo.InvariantCulture);

        public CommandResult Submit(ICommand command)
        {
            Queue.Enqueue(command);
            return Dispatcher.DispatchAll(Queue, Context).Single();
        }

        public CommandResult Open(ManagerId manager, OrganizationId organization, PersonId person, NegotiationSubject? subject = null, int? deadlineDays = null) =>
            Submit(new OpenNegotiationCommand
            {
                ManagerId = manager,
                IssuedOn = Date(Today),
                Organization = organization,
                Person = person,
                Subject = subject ?? NegotiationSubject.DriverSeat,
                Deadline = deadlineDays is int days ? Date(Today.AddDays(days)) : null,
            });

        /// <summary>Opens a negotiation and returns its id.</summary>
        public string OpenOk(ManagerId manager, OrganizationId organization, PersonId person, NegotiationSubject? subject = null, int? deadlineDays = null)
        {
            var before = Section.NextNegotiation;
            var result = Open(manager, organization, person, subject, deadlineDays);
            Assert.IsType<CommandResult.Accepted>(result);
            return Negotiation.IdOf(before);
        }

        public CommandResult Offer(ManagerId manager, string negotiationId, OfferTerms terms) =>
            Submit(new SubmitOfferCommand { ManagerId = manager, IssuedOn = Date(Today), NegotiationId = negotiationId, Terms = terms });

        public CommandResult Accept(ManagerId manager, string negotiationId) =>
            Submit(new AcceptCounterOfferCommand { ManagerId = manager, IssuedOn = Date(Today), NegotiationId = negotiationId });

        public CommandResult Walk(ManagerId manager, string negotiationId) =>
            Submit(new WalkAwayCommand { ManagerId = manager, IssuedOn = Date(Today), NegotiationId = negotiationId });

        public Negotiation Find(string negotiationId) => Section.Find(negotiationId)!;

        /// <summary>Lives days until the negotiation is no longer waiting for an answer, at most <paramref name="limit"/> days.</summary>
        public Negotiation AdvanceUntilAnswered(string negotiationId, int limit = 20)
        {
            for (var i = 0; i < limit; i++)
            {
                Advance(1);
                if (Find(negotiationId).Status != NegotiationStatus.AwaitingResponse)
                {
                    break;
                }
            }

            return Find(negotiationId);
        }

        /// <summary>Lives one day directly, bypassing the gate (the gate is tested on its own).</summary>
        public void LiveDay()
        {
            var step = WorldClock.AdvanceDay(Clock, Handlers);
            Clock = step.State;
            Events.AddRange(step.Events);
            Book.UseWorld(Book.World.WithDate(Clock.Date));
        }

        public void Advance(int days)
        {
            for (var i = 0; i < days; i++)
            {
                LiveDay();
            }
        }

        public IReadOnlyList<DomainEvent> EventsOf(string typeId) => Events.Where(e => e.TypeId == typeId).ToArray();

        public ManagerRegistry Registry => Managers;

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
