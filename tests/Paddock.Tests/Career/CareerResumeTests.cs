using System.Globalization;
using Paddock.Application.Career;
using Paddock.Application.Commands;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Domain.Career;
using Paddock.Domain.Codec;
using Paddock.Domain.People;
using Paddock.Domain.Random;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;
using Paddock.Simulation.Career;
using Paddock.Simulation.Time;
using Paddock.SimRunner;

namespace Paddock.Tests.Career;

/// <summary>
/// Issue #123: a saved career resumes to exactly the future an uninterrupted run lives. Every test compares a
/// session that ran through in one go with one that was written to a .paddock file, read back and resumed.
/// The fixture people, dates and the 3 generated drivers a season are SYNTHETIC ESTIMATES that exercise the rules;
/// they are not historical facts.
/// </summary>
public sealed class CareerResumeTests : IDisposable
{
    private const ulong Seed = 7;

    private const int IntakePerSeason = 3;

    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-resume-").FullName;

    private static readonly IReadOnlyDictionary<string, int> LastSeasons = new Dictionary<string, int>
    {
        ["d3"] = 1959,
        ["d4"] = 1950,
        ["r2"] = 1969,
        ["r3"] = 1968,
    };

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Theory]
    [InlineData(1950, 1970)]
    [InlineData(1955, 1970)]
    [InlineData(1960, 1970)]
    [InlineData(1969, 1970)]
    public void ASplitRunEqualsTheRunThatNeverStopped(int splitAfter, int last)
    {
        var whole = NewSession();
        CareerHost.Run(whole, last);

        var first = NewSession();
        var firstResult = CareerHost.Run(first, splitAfter);
        var path = Save(first, firstResult.Host, "split.paddock");
        var resumed = Resume(path, last);

        Assert.Equal(new GameDate(last + 1, 1, 1), resumed.Session.Date);
        Assert.Equal(Describe(whole), Describe(resumed.Session));
        Assert.Equal(whole.World.StateHash(), resumed.Session.World.StateHash());
        Assert.Equal(whole.Years, resumed.Session.Years);
        Assert.Equal(whole.TalentPool, resumed.Session.TalentPool);
        Assert.Equal(whole.Retired, resumed.Session.Retired);
        Assert.Equal(last - 1950 + 1, resumed.Session.Years.Count);
    }

    [Fact]
    public void TheFixtureReallyExercisesIntakeArrivalsRetirementsAndExpiries()
    {
        var whole = NewSession();
        CareerHost.Run(whole, 1970);

        Assert.Equal((IntakePerSeason * 20) + 3, whole.Intakes);
        Assert.True(whole.Retired.Count >= 8, "retired " + whole.Retired.Count);
        Assert.Contains(whole.Retired, id => id.Value == "r3");
        Assert.Contains(whole.World.Persons, person => person.Id.Value == "r1");
        Assert.True(whole.ContractExpiries >= 1);
        Assert.Contains(whole.Retired, id => id.Value == "d3");
    }

    [Fact]
    public void AChainOfSavesAndResumesStillEqualsTheRunThatNeverStopped()
    {
        var whole = NewSession();
        CareerHost.Run(whole, 1970);

        var first = NewSession();
        var result = CareerHost.Run(first, 1953);
        var second = Resume(Save(first, result.Host, "one.paddock"), 1961);
        var third = Resume(Save(second.Session, second.Host, "two.paddock"), 1964);
        var fourth = Resume(Save(third.Session, third.Host, "three.paddock"), 1970);

        Assert.Equal(Describe(whole), Describe(fourth.Session));
        Assert.Equal(whole.Years, fourth.Session.Years);
    }

    [Theory]
    [InlineData(1951, 7, 1, 1953)]
    [InlineData(1955, 3, 20, 1962)]
    [InlineData(1958, 12, 31, 1970)]
    public void ASaveInTheMiddleOfASeasonKeepsTheRngStreamsAndTheLastSeasonSchedule(int year, int month, int day, int last)
    {
        var whole = NewSession();
        LiveThrough(whole, new GameDate(last + 1, 1, 1));

        var first = NewSession();
        LiveThrough(first, new GameDate(year, month, day));
        var live = first.Clock.RngStates.Where(pair => pair.Key.Season == year).ToArray();
        var advanced = live.Where(pair => pair.Value != RngStreams.Derive(Seed, pair.Key.Name, year).State).ToArray();
        Assert.True(advanced.Length > 0 || month < 12, "late in the season a stream has been drawn from");

        var path = Save(first, FreshHost(), "mid.paddock");
        var loaded = CareerSaveReader.Read(path);
        Assert.Equal(first.Date, loaded.Session.Clock.Date);

        // A stream still in its derived state is the same as no stream (the clock derives it on first use), so it is not restored.
        foreach (var (slot, state) in advanced)
        {
            Assert.Equal(state, loaded.Session.Clock.RngStates[slot]);
        }

        Assert.Equal(advanced.Length, loaded.Session.Clock.RngStates.Count);

        var resumed = CareerSession.Resume(loaded.Session, PendingArrivals(loaded.Session.World.CurrentDate), Options());
        LiveThrough(resumed, new GameDate(last + 1, 1, 1));

        Assert.Equal(Describe(whole), Describe(resumed));
        Assert.Equal(whole.Years, resumed.Years);
    }

    [Fact]
    public void QueueCountersManagersAndCommandLogSurviveTheSaveExactly()
    {
        var session = NewSession();
        LiveThrough(session, new GameDate(1956, 1, 1));
        var queue = EventQueue.Empty
            .Enqueue(Scheduled("ev1", new GameDate(1957, 5, 1), new RaceSessionPayload(1957, 2, "spa-1954")))
            .Enqueue(Scheduled("ev2", new GameDate(1956, 9, 9), new MarkerPayload("later")))
            .Enqueue(Scheduled("ev3", new GameDate(1957, 5, 1), new MarkerPayload("same day")));
        queue = queue.TakeDue(new GameDate(1956, 9, 9)).Queue;
        var clock = session.Clock with { Queue = EventQueue.Restore(queue.Events, queue.NextSequence + 4), NextEventId = 90 };
        var withQueue = CareerSession.Resume(
            new CareerSessionResume(session.World, clock, session.OpenedYear, session.TalentPool, session.ContractExpiries, session.Intakes, session.Years),
            PendingArrivals(session.Date),
            Options());

        var managers = new ManagerRegistry();
        managers.Register(new ManagerId(CareerHost.AiManagerId), ManagerKind.Ai, "AI");
        managers.Register(new ManagerId("human:1"), ManagerKind.Human, "Ada");
        managers.PostBlockingItem(new ManagerId("human:1"), new BlockingItem("inbox.decision"));
        var log = new CommandLog();
        var day = new DateOnly(1955, 12, 31);
        log.Append(new ResolveInboxItemCommand { ManagerId = new ManagerId("human:1"), IssuedOn = day, ItemId = "inb:1", OptionId = "yes", SubmissionNumber = 1 });
        log.Append(new ExpireInboxItemCommand { ManagerId = new ManagerId(CareerHost.AiManagerId), IssuedOn = day, ItemId = "inb:2", SubmissionNumber = 2 });
        var host = new CareerHostState(managers, log, 3);

        var loaded = CareerSaveReader.Read(Save(withQueue, host, "queue.paddock"));

        Assert.Equal(WorldClockHash.Compute(WithoutOtherSeasons(withQueue.Clock)), WorldClockHash.Compute(WithoutOtherSeasons(loaded.Session.Clock)));
        Assert.Equal(withQueue.Clock.Queue.NextSequence, loaded.Session.Clock.Queue.NextSequence);
        Assert.Equal(withQueue.Clock.NextEventId, loaded.Session.Clock.NextEventId);
        Assert.Equal(2, loaded.Session.Clock.Queue.Count);
        Assert.Equal(
            queue.Events.Select(item => item.Id.Value + "|" + item.Sequence),
            loaded.Session.Clock.Queue.Events.Select(item => item.Id.Value + "|" + item.Sequence));
        Assert.Equal(withQueue.Clock.Queue.Events.Select(item => item.Payload), loaded.Session.Clock.Queue.Events.Select(item => item.Payload));
        Assert.Equal(["ai:paddock", "human:1"], loaded.Host.Managers.All.Select(manager => manager.Id.Value));
        Assert.Equal("inbox.decision", loaded.Host.Managers.Get(new ManagerId("human:1")).BlockingItem?.Kind);
        Assert.Equal(log.Entries, loaded.Host.Log.Entries);
        Assert.Equal(3, loaded.Host.NextSubmissionNumber);
        Assert.Equal(new CommandQueue(3).NextSubmissionNumber, loaded.Host.NextSubmissionNumber);
    }

    [Fact]
    public void ASaveThatHoldsAnUnknownTagIsNotLoaded()
    {
        var session = NewSession();
        LiveThrough(session, new GameDate(1956, 1, 1));
        var path = Save(session, FreshHost(), "unknown.paddock");
        using (var save = SaveFile.Open(path))
        {
            var repository = new WorldRepository(save);
            var snapshot = repository.LoadAll();
            var tampered = new WorldSnapshot(
                snapshot.World,
                [new StoredEvent("e1", new GameDate(1957, 1, 1), 0, ScheduledEventType.Race, "marker/9", """{"marker":"x"}""")],
                snapshot.NextEventId,
                1,
                snapshot.Managers,
                snapshot.CommandLog,
                snapshot.NextSubmissionNumber)
            {
                Run = snapshot.Run,
                RngStates = snapshot.RngStates,
            };
            repository.SaveAll(tampered, snapshot.World.CurrentDate);
        }

        var failure = Assert.Throws<UnknownTagException>(() => CareerSaveReader.Read(path));
        Assert.Equal("marker/9", failure.Tag);

        var other = Save(session, FreshHost(), "unknown-command.paddock");
        using (var save = SaveFile.Open(other))
        {
            var repository = new WorldRepository(save);
            var snapshot = repository.LoadAll();
            var tampered = new WorldSnapshot(
                snapshot.World,
                [],
                snapshot.NextEventId,
                0,
                snapshot.Managers,
                [new StoredCommand(1, CareerHost.AiManagerId, new DateOnly(1955, 12, 31), "inbox.resolve/9", """{"itemId":"x","optionId":"y"}""")],
                2)
            {
                Run = snapshot.Run,
                RngStates = snapshot.RngStates,
            };
            repository.SaveAll(tampered, snapshot.World.CurrentDate);
        }

        Assert.Equal("inbox.resolve/9", Assert.Throws<UnknownTagException>(() => CareerSaveReader.Read(other)).Tag);
    }

    [Fact]
    public void ASaveWithoutRunStateOrRngStatesRefusesToResume()
    {
        var session = NewSession();
        LiveThrough(session, new GameDate(1956, 1, 1));
        var path = Save(session, FreshHost(), "stripped.paddock");
        using (var save = SaveFile.Open(path))
        {
            var repository = new WorldRepository(save);
            var snapshot = repository.LoadAll();
            repository.SaveAll(
                new WorldSnapshot(snapshot.World, [], snapshot.NextEventId, 0, snapshot.Managers, [], 1) { RngStates = snapshot.RngStates },
                snapshot.World.CurrentDate);
        }

        var noRun = Assert.Throws<SaveNotResumableException>(() => CareerSaveReader.Read(path));
        Assert.Contains("run state", noRun.Message, StringComparison.Ordinal);

        var noRng = Save(session, FreshHost(), "no-rng.paddock");
        using (var save = SaveFile.Open(noRng))
        {
            var repository = new WorldRepository(save);
            var snapshot = repository.LoadAll();
            repository.SaveAll(
                new WorldSnapshot(snapshot.World, [], snapshot.NextEventId, 0, snapshot.Managers, [], 1) { Run = snapshot.Run },
                snapshot.World.CurrentDate);
        }

        Assert.Throws<SaveNotResumableException>(() => CareerSaveReader.Read(noRng));
    }

    [Fact]
    public void ResumeRefusesStateThatDisagreesWithItself()
    {
        var session = NewSession();
        LiveThrough(session, new GameDate(1956, 1, 1));
        var loaded = CareerSaveReader.Read(Save(session, FreshHost(), "checks.paddock")).Session;
        var arrivals = PendingArrivals(loaded.World.CurrentDate);

        Assert.Equal(Describe(session, withRng: false), Describe(CareerSession.Resume(loaded, arrivals, Options()), withRng: false));
        Assert.Throws<ArgumentException>(() => CareerSession.Resume(loaded with { Clock = loaded.Clock with { Date = new GameDate(1957, 1, 1) } }, arrivals, Options()));
        Assert.Throws<ArgumentException>(() => CareerSession.Resume(loaded with { OpenedYear = 1960 }, arrivals, Options()));
        Assert.Throws<ArgumentException>(() => CareerSession.Resume(loaded with { Intakes = -1 }, arrivals, Options()));
        Assert.Throws<ArgumentException>(() => CareerSession.Resume(loaded with { Pool = [.. loaded.Pool, loaded.Pool[0]] }, arrivals, Options()));
        Assert.Throws<ArgumentException>(() => CareerSession.Resume(loaded with { Years = [.. loaded.Years, loaded.Years[0]] }, arrivals, Options()));
        Assert.Throws<ArgumentException>(() => CareerSession.Resume(loaded with { Years = [new CareerYearSummary(1956, 0, 0, 0, 0, "x")] }, arrivals, Options()));
        var retired = loaded.World.Persons.First(person => person.IsRetired).Id;
        Assert.Throws<ArgumentException>(() => CareerSession.Resume(loaded with { Pool = [retired] }, arrivals, Options()));
        Assert.Throws<ArgumentException>(
            () => CareerSession.Resume(loaded, [.. arrivals, new ScheduledArrival(new GameDate(1960, 1, 1), SpecFor("d1", "D", 1920, driver: true))], Options()));
    }

    private static CareerSessionOptions Options() => new()
    {
        GeneratedIntakePerSeason = IntakePerSeason,
        LastSeasons = LastSeasons,
    };

    private static IReadOnlyList<ScheduledArrival> Arrivals() =>
    [
        new ScheduledArrival(new GameDate(1953, 1, 1), SpecFor("r1", "Rookie", 1932, driver: true)),
        new ScheduledArrival(new GameDate(1962, 1, 1), SpecFor("r2", "Later", 1940, driver: true)),
        new ScheduledArrival(new GameDate(1966, 1, 1), SpecFor("r3", "Last", 1944, driver: true)),
    ];

    private static IReadOnlyList<ScheduledArrival> PendingArrivals(GameDate date) =>
        [.. Arrivals().Where(arrival => arrival.On >= date)];

    private static CareerSession NewSession() =>
        new(World(), Seed, [PersonId.Real("d1"), PersonId.Real("d2"), PersonId.Real("d7")], Arrivals(), Options());

    private static CareerHostState FreshHost()
    {
        var managers = new ManagerRegistry();
        managers.Register(new ManagerId(CareerHost.AiManagerId), ManagerKind.Ai, "AI");
        return new CareerHostState(managers, new CommandLog(), 1);
    }

    private static void LiveThrough(CareerSession session, GameDate until)
    {
        while (session.Date < until)
        {
            session.LiveDay();
        }
    }

    private static ScheduledEvent Scheduled(string id, GameDate date, EventPayload payload) =>
        new(new EventId(id), date, ScheduledEventType.Race, payload);

    private static WorldClockState WithoutOtherSeasons(WorldClockState clock)
    {
        var season = clock.Date.Year;
        return clock with
        {
            RngStates = clock.RngStates.Where(pair => pair.Key.Season == season).ToDictionary(pair => pair.Key, pair => pair.Value),
        };
    }

    private string Save(CareerSession session, CareerHostState host, string name)
    {
        var path = Path.Combine(_directory, name);
        CareerSaveWriter.Write(
            path,
            session,
            CareerConfig.FromPreset(CareerPreset.Chaos).WithStartYear(1950),
            "alpha",
            new string('a', 64),
            "fixture 1950",
            host);
        return path;
    }

    private static ResumedRun Resume(string path, int last)
    {
        var loaded = CareerSaveReader.Read(path);
        var session = CareerSession.Resume(loaded.Session, PendingArrivals(loaded.Session.World.CurrentDate), Options());
        var result = CareerHost.Run(session, last, loaded.Host);
        return new ResumedRun(result.Session, result.Host);
    }

    private sealed record ResumedRun(CareerSession Session, CareerHostState Host);

    /// <summary>Everything a run keeps, as text: world hash, date, pool, retirements, tallies, summaries, clock counters, last season's streams.</summary>
    private static string Describe(CareerSession session, bool withRng = true)
    {
        var lines = new List<string>
        {
            "world " + session.World.StateHash(),
            "date " + session.Date,
            "opened " + session.OpenedYear.ToString(CultureInfo.InvariantCulture),
            "pool " + string.Join(',', session.TalentPool.Select(id => id.Value)),
            "retired " + string.Join(',', session.Retired.Select(id => id.Value)),
            "intakes " + session.Intakes.ToString(CultureInfo.InvariantCulture),
            "expiries " + session.ContractExpiries.ToString(CultureInfo.InvariantCulture),
            "eventId " + session.Clock.NextEventId.ToString(CultureInfo.InvariantCulture),
            "sequence " + session.Clock.Queue.NextSequence.ToString(CultureInfo.InvariantCulture),
        };
        lines.AddRange(session.Years.Select(year => string.Join(
            ' ',
            "year",
            year.Year.ToString(CultureInfo.InvariantCulture),
            year.Alive.ToString(CultureInfo.InvariantCulture),
            year.Retired.ToString(CultureInfo.InvariantCulture),
            year.Pool.ToString(CultureInfo.InvariantCulture),
            year.Contracts.ToString(CultureInfo.InvariantCulture),
            year.StateHash)));
        var season = session.Date.Year - 1;
        foreach (var name in RngStreamName.All)
        {
            if (withRng && session.Clock.RngStates.TryGetValue(new RngStreamSlot(name, season), out var state))
            {
                lines.Add("rng " + name + " " + state.S0 + " " + state.S1 + " " + state.S2 + " " + state.S3);
            }
        }

        return string.Join('\n', lines);
    }

    private static WorldState World()
    {
        var world = WorldState.At(new GameDate(1950, 1, 1));
        (world, var team) = world.AddOrganization(new OrganizationSpec(
            OrganizationKind.Team,
            true,
            "alpha",
            new GameDate(1950, 1, 1),
            null,
            0,
            [new OrganizationNameSpan("Alpha", new GameDate(1950, 1, 1), null)]));
        (world, _) = world.AddPerson(SpecFor("vet", "Vet", 1900, driver: true));
        for (var index = 1; index <= 10; index++)
        {
            (world, _) = world.AddPerson(SpecFor("d" + index.ToString(CultureInfo.InvariantCulture), "Driver", 1908 + (index * 2), driver: true));
        }

        (world, _) = world.AddPerson(SpecFor("chief", "Chief", 1870, driver: false));
        (world, _) = world.AddPerson(SpecFor("designer", "Designer", 1890, driver: false));
        (world, _) = world.AddPerson(SpecFor("junior", "Junior", 1912, driver: false));
        foreach (var (person, end) in new[] { ("d1", 1952), ("d5", 1955), ("d7", 1958), ("d9", 1964) })
        {
            (world, _) = world.AddContract(new ContractSpec(
                PersonId.Real(person),
                team,
                ContractRole.Driver(SeatStatus.Equal),
                new GameDate(1950, 1, 1),
                new GameDate(end, 12, 31),
                0,
                true,
                null,
                null));
        }

        return world;
    }

    private static PersonSpec SpecFor(string id, string family, int birthYear, bool driver)
    {
        PersonTruth truth;
        PersonRole[] roles;
        if (driver)
        {
            var flat = new DriverAttributes(10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10);
            truth = PersonTruth.FromDriver(flat, flat);
            roles = [PersonRole.Driver];
        }
        else
        {
            var keys = StaffCatalogue.AttributeKeys(StaffRole.ChiefDesigner);
            var attributes = keys.Select(key => new NamedAttribute(key, 10)).ToArray();
            truth = new PersonTruth(attributes, attributes);
            roles = [PersonRole.Staff(StaffRole.ChiefDesigner)];
        }

        return new PersonSpec(id, family, new GameDate(birthYear, 1 + (birthYear % 12), 1 + (birthYear % 27)), "GBR", true, id, roles, truth);
    }
}
