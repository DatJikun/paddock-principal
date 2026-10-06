using Paddock.Application.Career;
using Paddock.Application.Racing;
using Paddock.Career;
using Paddock.Data.Historical;
using Paddock.Domain.Career;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;
using Paddock.Simulation.Career;
using Paddock.Simulation.Time;
using Paddock.Tests.Career;
using Paddock.Tests.Persistence;

namespace Paddock.Tests.Racing;

/// <summary>
/// #229: real race dates when the host has them, even spacing otherwise, and a plan that stays as stored. The tests use small
/// fakes; none reads <c>data/cache</c> (PP-041).
/// </summary>
public sealed class RealRaceDatesTests : IDisposable
{
    private const ulong Seed = 7;
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-v26-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void RealDatesPlaceEachWeekendOnTheRealRaceDay()
    {
        var book = RaceDateBook.Create([(1950, 1, new GameDate(1950, 5, 13)), (1950, 2, new GameDate(1950, 5, 21))]);

        var plan = SeasonCalendar.Plan(1950, Layouts(), Assignments(), book);

        Assert.Equal(
            [
                (ScheduledEventType.Practice, new GameDate(1950, 5, 11), 1),
                (ScheduledEventType.Qualifying, new GameDate(1950, 5, 12), 1),
                (ScheduledEventType.Race, new GameDate(1950, 5, 13), 1),
                (ScheduledEventType.Practice, new GameDate(1950, 5, 19), 2),
                (ScheduledEventType.Qualifying, new GameDate(1950, 5, 20), 2),
                (ScheduledEventType.Race, new GameDate(1950, 5, 21), 2),
            ],
            plan.Select(item => (item.TypeId, item.Date, item.Round)).ToArray());
    }

    [Fact]
    public void ASeasonMissingARoundDateKeepsTheEvenSpacing()
    {
        var book = RaceDateBook.Create([(1950, 1, new GameDate(1950, 5, 13))]);

        var plan = SeasonCalendar.Plan(1950, Layouts(), Assignments(), book);

        Assert.Equal(SeasonCalendar.Plan(1950, Layouts(), Assignments()), plan);
        Assert.Equal(new GameDate(1950, 3, 3), plan[2].Date);
    }

    [Fact]
    public void DatesTooCloseForAWeekendKeepTheEvenSpacing()
    {
        var book = RaceDateBook.Create([(1950, 1, new GameDate(1950, 5, 30)), (1950, 2, new GameDate(1950, 5, 31))]);

        Assert.Equal(
            SeasonCalendar.Plan(1950, Layouts(), Assignments()),
            SeasonCalendar.Plan(1950, Layouts(), Assignments(), book));
    }

    [Fact]
    public void NoBookAndAnEmptyBookPlanTheSame()
    {
        Assert.Equal(
            SeasonCalendar.Plan(1950, Layouts(), Assignments()),
            SeasonCalendar.Plan(1950, Layouts(), Assignments(), RaceDateBook.Empty));
    }

    [Fact]
    public void TheLoaderReadsTheNormalizedRacesFileAndTreatsAMissingCacheAsEmpty()
    {
        Assert.Equal(0, RaceDateLoader.Load(_directory).Count);

        var folder = Path.GetDirectoryName(RaceDateLoader.RacesPath(_directory))!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            RaceDateLoader.RacesPath(_directory),
            """
            {"schemaVersion":1,"races":[
              {"season":1950,"round":1,"name":"A","circuitId":"a","date":"1950-05-13","time":null,"url":"u","isIndianapolis500":false},
              {"season":1950,"round":2,"name":"B","circuitId":"b","date":"not-a-date","time":null,"url":"u","isIndianapolis500":false}
            ]}
            """);

        var book = RaceDateLoader.Load(_directory);

        Assert.Equal(1, book.Count);
        Assert.True(book.TryGet(1950, 1, out var date));
        Assert.Equal(new GameDate(1950, 5, 13), date);
        Assert.False(book.TryGet(1950, 2, out _));
    }

    [Fact]
    public void CareerDataFindsTheCacheUnderTheDataRoot()
    {
        var folder = Path.GetDirectoryName(RaceDateLoader.RacesPath(_directory))!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            RaceDateLoader.RacesPath(_directory),
            """{"schemaVersion":1,"races":[{"season":1955,"round":1,"name":"A","circuitId":"a","date":"1955-01-16","time":null,"url":"u","isIndianapolis500":false}]}""");

        Assert.True(CareerData.LoadRaceDates(_directory).TryGet(1955, 1, out var date));
        Assert.Equal(new GameDate(1955, 1, 16), date);
    }

    [Fact]
    public void ACareerPlansOnRealDatesAndKeepsThemWhenTheBookChanges()
    {
        var opened = CareerKit.Opened(CareerPreset.Chaos, 1955, Seed);
        var real = RealBook(1955);
        var session = opened.Session;
        var options = Options(opened, real);
        CareerHost.RunUntil(session, new GameDate(1955, 1, 2), null, options);

        var stored = session.World.Section<RaceCalendarSection>(RaceCalendarSection.SectionName);
        Assert.NotNull(stored);
        Assert.True(stored!.HasSeason(1955));
        var calendar = ChampionshipRead.Calendar(session, options.Inputs, new Dictionary<string, CircuitLabel>());
        Assert.Equal(real.RaceDay(1955, 1).ToString(), calendar.Rounds[0].Race);
        Assert.Equal(real.RaceDay(1955, 1).AddDays(-2).ToString(), calendar.Rounds[0].Practice);

        var other = Options(opened, RaceDateBook.Empty);
        var again = ChampionshipRead.Calendar(session, other.Inputs, new Dictionary<string, CircuitLabel>());
        Assert.Equal(calendar.Rounds, again.Rounds);
    }

    [Fact]
    public void ACareerWithoutDatesKeepsTheEvenSpacing()
    {
        var opened = CareerKit.Opened(CareerPreset.Chaos, 1955, Seed);
        var options = Options(opened, null);
        CareerHost.RunUntil(opened.Session, new GameDate(1955, 1, 2), null, options);

        var calendar = ChampionshipRead.Calendar(opened.Session, options.Inputs, new Dictionary<string, CircuitLabel>());

        Assert.Equal("1955-03-03", calendar.Rounds[0].Race);
    }

    [Fact]
    public void ThePlanSurvivesASaveAndLoad()
    {
        var opened = CareerKit.Opened(CareerPreset.Chaos, 1955, Seed);
        CareerHost.RunUntil(opened.Session, new GameDate(1955, 1, 2), null, Options(opened, RealBook(1955)));
        var path = Path.Combine(_directory, "plan.paddock");
        using var save = SaveFile.Create(path, WorldFixtures.Meta());
        var repo = new WorldRepository(save);
        repo.SaveWorld(opened.Session.World, opened.Session.Date);

        var loaded = repo.LoadWorld();

        var before = opened.Session.World.Section<RaceCalendarSection>(RaceCalendarSection.SectionName)!;
        var after = loaded.Section<RaceCalendarSection>(RaceCalendarSection.SectionName)!;
        Assert.Equal(before.Sessions, after.Sessions);
        Assert.Equal(opened.Session.World.StateHash(), loaded.StateHash());
    }

    [Fact]
    public void AV25SaveMigratesWithoutACalendarSection()
    {
        var path = Path.Combine(_directory, "v25.paddock");
        using (var created = SaveFile.Create(path, WorldFixtures.Meta(), [.. SaveMigrations.Production.Take(25)]))
        {
            Assert.Equal(25, created.ReadMeta().SchemaVersion);
        }

        using var opened = SaveFile.Open(path);

        Assert.Equal(SaveMigrations.CurrentVersion, opened.ReadMeta().SchemaVersion);
        Assert.True(SaveMigrations.CurrentVersion >= 26);
        using var command = opened.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM race_calendar_sessions";
        Assert.Equal(0L, command.ExecuteScalar());
    }

    [Fact]
    public void ASeasonWithNoStoredPlanIsReadWithTheEvenSpacing()
    {
        var career = CareerKit.Opened(CareerPreset.Chaos, 1955, Seed);

        var read = SeasonPlans.Read(career.Session.World, 1955, CareerKit.Data.Layouts, CareerKit.Data.RaceAssignments);

        Assert.Equal(SeasonCalendar.Plan(1955, CareerKit.Data.Layouts, CareerKit.Data.RaceAssignments), read);
    }

    [Fact]
    public void ARunWithRealDatesIsRepeatable()
    {
        string Hash()
        {
            var opened = CareerKit.Opened(CareerPreset.Chaos, 1955, Seed);
            CareerHost.RunUntil(opened.Session, new GameDate(1955, 4, 20), null, Options(opened, RealBook(1955)));
            return opened.Session.World.StateHash();
        }

        Assert.Equal(Hash(), Hash());
    }

    private static CareerRunOptions Options(OpenedCareer opened, RaceDateBook? dates) =>
        new() { Inputs = CareerInputsLoader.Load(CareerKit.DataRoot, CareerKit.Data, opened.Supplies, opened.Config, dates) };

    private static FakeBook RealBook(int season) => new(season);

    private static IReadOnlyList<TrackLayout> Layouts() =>
    [
        new TrackLayout("alpha", "alpha_circuit", 5.0, new Dictionary<string, double> { ["balance"] = 1.0 }, ["fast"]),
        new TrackLayout("beta", "beta_circuit", 3.3, new Dictionary<string, double> { ["balance"] = 1.0 }, ["street"]),
    ];

    private static IReadOnlyList<RaceAssignment> Assignments() =>
        [new RaceAssignment(1950, 1, "alpha"), new RaceAssignment(1950, 2, "beta")];
}

/// <summary>Fake real dates for the authored rounds of a season: one race every ten days from 10 April.</summary>
internal sealed class FakeBook
{
    private readonly RaceDateBook _book;

    public FakeBook(int season)
    {
        var rows = CareerKit.Data.RaceAssignments
            .Where(assignment => assignment.Season == season)
            .Select(assignment => (season, assignment.Round, Day(season, assignment.Round)))
            .ToArray();
        _book = RaceDateBook.Create(rows);
    }

    public GameDate RaceDay(int season, int round) => Day(season, round);

    public static implicit operator RaceDateBook(FakeBook fake) => fake._book;

    private static GameDate Day(int season, int round) => new GameDate(season, 4, 10).AddDays(10 * (round - 1));
}
