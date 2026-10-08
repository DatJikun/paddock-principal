using Paddock.Application.Contracts;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Application.Racing;
using Paddock.Application.Regulation;
using Paddock.Data.Authored;
using Paddock.Domain.Career;
using Paddock.Domain.Finance;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Regulation;
using Paddock.Tests.Career;

namespace Paddock.Tests.Regulation;

/// <summary>Two racing series in one world: the first five teams race in "f1", the rest in "f2".</summary>
internal sealed class TwoSeries : ISeriesDirectory
{
    public const string First = "f1";

    public const string Second = "f2";

    public IReadOnlyList<string> SeriesIds { get; } = [First, Second];

    public IReadOnlyList<Organization> TeamsOf(string seriesId, WorldState world, GameDate today)
    {
        var all = Paddock.Application.Career.CareerTeams.Active(world, today);
        return seriesId == First ? [.. all.Take(5)] : [.. all.Skip(5)];
    }
}

internal sealed record HarnessOptions
{
    public int TeamCount { get; init; } = 10;

    /// <summary>Team ids a human runs. The others are AI. Null means the first team only.</summary>
    public IReadOnlyList<string>? Humans { get; init; }

    public bool AllHuman { get; init; }

    public VoteMode Mode { get; init; } = VoteMode.OneVoteEach;

    public ulong Seed { get; init; } = 7;

    public int StartYear { get; init; } = 1955;

    public ISeriesDirectory? Directory { get; init; }

    public bool WithCalendar { get; init; }

    public bool WithInbox { get; init; }

    /// <summary>Opening capital in dollars per team index (default: 60000 for everybody).</summary>
    public Func<int, long>? Capital { get; init; }

    public IReadOnlyDictionary<string, string>? TeamCountries { get; init; }

    /// <summary>The rules that can never be voted on. Null bans nothing.</summary>
    public BannedRules? BannedRules { get; init; }

    /// <summary>Real race dates, so the weekends of a laid out season are the ones the host would plan. Null means even spacing.</summary>
    public RaceDateBook? RaceDates { get; init; }

    /// <summary>Lays the seasons out in the <c>race-calendar</c> section the way a career does on 31 December, so the political year reads a stored plan.</summary>
    public bool StoreCalendar { get; init; }
}

/// <summary>
/// A small world with ten teams, real ledgers and the real regulation catalog, stepped day by day through the political life of the
/// series without a race: the proposals, the ballots and the counts are what the tests look at.
/// </summary>
internal sealed class PoliticsHarness
{
    public const string Series = SeriesIds.WorldChampionship;

    private WorldState _world;
    private bool _storeCalendar;

    private PoliticsHarness(WorldState world, RegulationPolitics politics, RegulationEnvironment environment, IReadOnlyList<string> teams, InboxBook? inbox, ManagerRegistry managers)
    {
        _world = world;
        Politics = politics;
        Environment = environment;
        Teams = teams;
        Inbox = inbox;
        Managers = managers;
    }

    public RegulationPolitics Politics { get; }

    public RegulationEnvironment Environment { get; }

    public IReadOnlyList<string> Teams { get; }

    public InboxBook? Inbox { get; }

    public ManagerRegistry Managers { get; }

    public GameDate Today { get; private set; }

    public WorldState World => _world;

    public RegulationsSection Section => _world.Section<RegulationsSection>(RegulationsSection.SectionName)!;

    public SeriesRegulations Of(string seriesId = Series) => Section.Find(seriesId)!;

    public FinanceSection Finance => _world.Section<FinanceSection>(FinanceSection.SectionName)!;

    public static AuthoredData Data => CareerKit.Data;

    public static string TeamId(int index) => "t" + (index + 1).ToString("00", System.Globalization.CultureInfo.InvariantCulture);

    public static OrganizationId Org(string teamId) => OrganizationId.Real(teamId);

    public static PoliticsHarness Create(HarnessOptions? options = null)
    {
        options ??= new HarnessOptions();
        var opening = new GameDate(options.StartYear, 1, 1);
        var world = WorldState.At(opening);
        var teams = new List<string>();
        for (var i = 0; i < options.TeamCount; i++)
        {
            var id = TeamId(i);
            teams.Add(id);
            (world, _) = world.AddOrganization(new OrganizationSpec(
                OrganizationKind.Team,
                true,
                id,
                opening,
                null,
                0,
                [new OrganizationNameSpan("Team " + id, opening, null)]));
        }

        var facts = new EraFinanceFacts(FinanceEstimates.PromoterModel, 20_000, 60_000, 250_000);
        var finance = FinanceSection.Empty;
        for (var i = 0; i < teams.Count; i++)
        {
            finance = finance.Open(Org(teams[i]), opening, options.Capital?.Invoke(i) ?? 60_000, facts);
        }

        world = world.WithSection(finance);

        var control = new ControlTable();
        var managers = new ManagerRegistry();
        var humans = options.AllHuman ? teams : options.Humans ?? [teams[0]];
        var humanOrganizations = new List<OrganizationId>();
        foreach (var team in humans)
        {
            var manager = new ManagerId("human:" + team);
            managers.Register(manager, ManagerKind.Human, team);
            control.Assign(manager, Org(team));
            humanOrganizations.Add(Org(team));
        }

        var data = Data;
        var inbox = options.WithInbox ? new InboxBook(new InboxResolvers()) : null;
        var environment = new RegulationEnvironment(
            RulesSource.VotedEachSeason,
            options.Mode,
            options.Seed,
            RuleCatalog.ToSpecs(data.Catalog),
            data.DimensionIds,
            data.Periods,
            control,
            managers,
            options.Directory ?? new SingleSeriesDirectory(),
            options.WithCalendar || options.StoreCalendar ? data.Layouts : null,
            options.WithCalendar || options.StoreCalendar ? data.RaceAssignments : null,
            options.TeamCountries,
            humanOrganizations,
            options.RaceDates,
            options.BannedRules);

        PoliticsHarness? self = null;
        var book = new RegulationBook(() => self!._world, next => self!._world = next);
        var politics = new RegulationPolitics(book, environment, inbox);
        self = new PoliticsHarness(world, politics, environment, teams, inbox, managers) { Today = opening };
        self._world = self._world.WithSection(politics.Open(self._world, opening));
        self._storeCalendar = options.StoreCalendar;
        if (options.StoreCalendar)
        {
            self.LayOut(options.StartYear);
        }

        return self;
    }

    /// <summary>Lives every day from the one after <see cref="Today"/> up to and including <paramref name="to"/>.</summary>
    public void LiveTo(GameDate to)
    {
        while (Today < to)
        {
            Today = Today.AddDays(1);
            if (_storeCalendar && Today.IsSeasonEnd)
            {
                LayOut(Today.Year + 1);
            }

            Politics.OnDay(Today);
        }
    }

    /// <summary>Stores the plan of a season the way the career does when it schedules the season (with the calendar policy voted so far).</summary>
    public void LayOut(int season) =>
        _world = SeasonPlans.Ensure(_world, season, Data.Layouts, Data.RaceAssignments, Environment.RaceDates).World;

    /// <summary>The stored political year of a series (after the first day of the season has been lived).</summary>
    public PoliticalSchedule ScheduleOf(string seriesId = Series) =>
        Of(seriesId).Schedule ?? throw new InvalidOperationException("The first day of the season has not been lived yet.");

    /// <summary>Lives up to the day the teams' ballot opens: the proposals of the season have become ballot items.</summary>
    public void LiveToTeamBallot(string seriesId = Series) => LiveTo(ScheduleOf(seriesId).TeamsSlot!.Opens);

    /// <summary>Lives up to the day the FIA's <paramref name="index"/>-th vote of the season opens.</summary>
    public void LiveToFia(int index = 0, string seriesId = Series) => LiveTo(ScheduleOf(seriesId).FiaSlots[index].Opens);

    /// <summary>Lives the first day too (the opening morning is a day of its own).</summary>
    public void LiveFrom(GameDate first, GameDate to)
    {
        Today = first.AddDays(-1);
        LiveTo(to);
    }

    public ManagerId HumanOf(string teamId) => new("human:" + teamId);

    public IReadOnlyList<BallotItem> Items(string seriesId = Series) => Of(seriesId).Ballot;

    public void SetFinance(FinanceSection finance) => _world = _world.WithSection(finance);

    public void SetSection(RegulationsSection section) => _world = _world.WithSection(section);

    public void SetWorld(WorldState world) => _world = world;

    /// <summary>Continues from a world that was saved and loaded, on the day it was saved.</summary>
    public void Resume(WorldState world, GameDate today)
    {
        _world = world;
        Today = today;
    }

    /// <summary>Posts a revenue line dated in the given year, so the fee of the following season has something to be a share of.</summary>
    public void PostRevenue(string teamId, int year, long cents)
    {
        SetFinance(Finance.Post(Org(teamId), new GameDate(year, 7, 1), LedgerCategories.PrizeMoney, null, cents, FinanceReason.PrizeMoney));
    }
}
