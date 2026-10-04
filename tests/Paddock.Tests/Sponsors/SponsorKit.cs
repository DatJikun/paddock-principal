using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Finance;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Application.Objectives;
using Paddock.Application.Sponsors;
using Paddock.Application.World;
using Paddock.Data.Authored;
using Paddock.Domain.Contracts;
using Paddock.Domain.Finance;
using Paddock.Domain.Objectives;
using Paddock.Domain.Sponsors;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Objectives;
using Paddock.Simulation.Time;

namespace Paddock.Tests.Sponsors;

/// <summary>Two teams with books, the real authored sponsors and era data, and a host loop for the day handlers.</summary>
internal sealed class SponsorKit
{
    public static readonly OrganizationId Alfa = OrganizationId.Real("alfa");

    public static readonly OrganizationId Beta = OrganizationId.Real("beta");

    public static readonly ManagerId Anna = new("human:anna");

    public static readonly ManagerId Bram = new("human:bram");

    private static readonly Lazy<AuthoredData> Authored = new(() => AuthoredDataLoader.Load(Path.Combine(RepoPaths.Root(), "data")));

    private static readonly Lazy<SponsorsFile> File = new(() => SponsorsLoader.Load(Path.Combine(RepoPaths.Root(), "data")));

    private WorldState _world;

    public SponsorKit(
        GameDate start,
        SponsorCatalog? catalog = null,
        int skill = 0,
        double prestige = 0.5,
        ulong seed = 42UL,
        bool betaToo = true)
    {
        Start = start;
        Seed = seed;
        var (world, _) = WorldState.At(start).AddOrganization(Team(Alfa, "Alfa Romeo", start));
        if (betaToo)
        {
            (world, _) = world.AddOrganization(Team(Beta, "Beta", start));
        }

        var facts = EraFinance.ForYear(Authored.Value.EraPeriods, start.Year);
        var finance = FinanceSection.Empty.Open(Alfa, start, facts.TypicalDollars, facts);
        if (betaToo)
        {
            finance = finance.Open(Beta, start, facts.TypicalDollars, facts);
        }

        _world = world.WithSection(finance);
        Book = new SponsorBook(() => _world, next => _world = next);
        Control = new ControlTable().Assign(Anna, Alfa).Assign(Bram, Beta);
        Managers = new ManagerRegistry();
        Managers.Register(Anna, ManagerKind.Human, "Anna");
        Managers.Register(Bram, ManagerKind.Human, "Bram");
        Inbox = new InboxBook();
        Objectives = new ObjectiveFactRegistry();
        Objectives.RegisterNumber(ObjectiveFactKeys.SeasonPodiums, _ => Podiums);
        Objectives.RegisterNumber(ObjectiveFactKeys.SeasonPoints, _ => Points);
        Objectives.RegisterNumber(ObjectiveFactKeys.ChampionshipPosition, _ => Position);
        Objectives.RegisterFlag(ObjectiveFactKeys.LineupDriverNationality, (_, nationality) => Nationalities.Contains(nationality));
        Environment = new SponsorEnvironment(
            catalog ?? SponsorsLoader.ToCatalog(File.Value),
            new PeriodSponsorEras(Authored.Value.EraPeriods),
            new EraPeriodFinance(Authored.Value.EraPeriods),
            Control,
            Objectives,
            new FixedAppeal(prestige),
            new FixedSkill(skill));
        Context = new CommandContext(new StubWorldState(new DateOnly(start.Year, start.Month, start.Day)), Managers, Inbox);
    }

    public GameDate Start { get; }

    public ulong Seed { get; }

    public SponsorBook Book { get; }

    public IOrganizationControl Control { get; }

    public ManagerRegistry Managers { get; }

    public InboxBook Inbox { get; }

    public ObjectiveFactRegistry Objectives { get; }

    public SponsorEnvironment Environment { get; }

    public CommandContext Context { get; }

    public WorldState World => _world;

    public decimal Podiums { get; set; }

    public decimal Points { get; set; }

    public decimal Position { get; set; } = 10m;

    public HashSet<string> Nationalities { get; } = [];

    public AuthoredData Data => Authored.Value;

    public SponsorsFile SponsorsFile => File.Value;

    public static DateOnly Day(GameDate date) => new(date.Year, date.Month, date.Day);

    /// <summary>Runs a command through its handler. Returns the rejection key, or null after executing.</summary>
    public string? Run(ICommand command)
    {
        ICommandHandler handler = command switch
        {
            BeginSponsorTalksCommand => new BeginSponsorTalksHandler(Book, Environment),
            SignAtCurrentTermsCommand => new SignAtCurrentTermsHandler(Book, Environment),
            WalkAwayFromTalksCommand => new WalkAwayFromTalksHandler(Book, Environment),
            RespondToSponsorOfferCommand => new RespondToSponsorOfferHandler(Book, Environment),
            _ => throw new ArgumentException("Not a sponsor command."),
        };
        var rejection = handler.Validate(command, Context);
        if (rejection is not null)
        {
            return rejection.Key;
        }

        handler.Execute(command, Context);
        return null;
    }

    public string? Begin(ManagerId manager, OrganizationId organization, string sponsor, int slot, GameDate on) =>
        Run(new BeginSponsorTalksCommand { ManagerId = manager, IssuedOn = Day(on), OrganizationId = organization.Value, SponsorId = sponsor, Slot = slot });

    public SponsorTalk OpenTalk(string sponsor, int slot, GameDate on, OrganizationId? organization = null)
    {
        var org = organization ?? Alfa;
        var manager = org == Alfa ? Anna : Bram;
        Assert.Null(Begin(manager, org, sponsor, slot, on));
        return Book.Section.Talks.Last();
    }

    public SponsorDeal SignDeal(string sponsor, int slot, GameDate on)
    {
        var talk = OpenTalk(sponsor, slot, on);
        Assert.Null(Run(new SignAtCurrentTermsCommand { ManagerId = Anna, IssuedOn = Day(on), OrganizationId = Alfa.Value, TalkId = talk.Id }));
        return Book.Section.Deals.Last();
    }

    /// <summary>Lives <paramref name="days"/> days from <paramref name="from"/>, applying objective outcomes as a host would.</summary>
    public List<DomainEvent> Live(GameDate from, int days, bool includeFinance = false)
    {
        var handlers = new List<IDayHandler>
        {
            new SponsorDayHandler(Book, Environment, Inbox, Managers),
            new ObjectiveDayHandler(() => Book.Objectives, Objectives),
        };
        if (includeFinance)
        {
            handlers.Add(new FinanceDayHandler(() => Book.World, next => _world = next));
        }

        var registry = new DayHandlerRegistry(handlers);
        var state = new WorldClockState(from, Seed);
        var all = new List<DomainEvent>();
        for (var step = 0; step < days; step++)
        {
            var day = WorldClock.AdvanceDay(state, registry);
            state = day.State;
            all.AddRange(day.Events);
            LastState = state;
            if (day.Events.Count > 0)
            {
                SponsorOutcomes.Apply(Book, Environment, day.Events, Inbox, Managers);
                _world = _world.WithSection(ObjectiveOutcomes.Apply(Book.Objectives, day.Events));
            }
        }

        return all;
    }

    public WorldClockState? LastState { get; private set; }

    public long Cash(OrganizationId organization) => Book.Finance.BalanceOf(organization);

    public IReadOnlyList<LedgerEntry> SponsorEntries(OrganizationId organization) =>
        Book.Finance.EntriesOf(organization).Where(entry => entry.Category == LedgerCategories.Sponsor).ToArray();

    public IReadOnlyList<string> InboxSubjects(ManagerId manager) =>
        Inbox.Section.ItemsOf(manager.Value).Select(item => item.Draft.SubjectKey).ToArray();

    public ObjectiveQuery ObjectiveQuery() => new(Objectives, new KitManagers(Control));

    private static OrganizationSpec Team(OrganizationId organization, string name, GameDate start) => new(
        OrganizationKind.Team,
        true,
        organization.Value,
        start,
        null,
        0,
        [new OrganizationNameSpan(name, start, null)]);

    private sealed class FixedAppeal : IOrganizationAppealSource
    {
        private readonly double _prestige;

        public FixedAppeal(double prestige) => _prestige = prestige;

        public OrganizationAppeal Appeal(OrganizationId organization, GameDate on) => new(_prestige, 0.5, 0.5);
    }

    private sealed class FixedSkill : INegotiatorSkills
    {
        private readonly int _skill;

        public FixedSkill(int skill) => _skill = skill;

        public int Skill(WorldState world, OrganizationId organization, GameDate on) => _skill;
    }

    private sealed class KitManagers : IManagerOrganizations
    {
        private readonly IOrganizationControl _control;

        public KitManagers(IOrganizationControl control) => _control = control;

        public OrganizationId? OrganizationOf(string managerId)
        {
            foreach (var candidate in new[] { Alfa, Beta })
            {
                if (_control.Controls(new ManagerId(managerId), candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
