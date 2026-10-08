using System.Globalization;
using Paddock.Application.Career;
using Paddock.Data.World;
using Paddock.Domain.Career;
using Paddock.Domain.People;
using Paddock.Domain.Pool;
using Paddock.Domain.Random;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;
using Paddock.Simulation.Career;
using Paddock.Simulation.Pool;
using Paddock.Simulation.Time;
using Paddock.Career;
using Paddock.SimRunner;

namespace Paddock.Tests.Career;

/// <summary>
/// The stored world hash is the fixture career on the morning of 1961-01-01 (seed 7, seasons 1950 through 1960).
/// Update <see cref="StoredWorldHash"/> and <see cref="StoredRetired"/> only by editing them after a reviewed
/// change to the day rules or the fixture. This test prints the actual values and never writes them (there is no command that
/// rewrites a golden hash: TECH 6.2 says how a changed one is reviewed).
/// <para>
/// History of <see cref="StoredWorldHash"/>: it changed in #160, when the career modules joined the run. The fixture team now
/// has its two cars (concept approved by the team's AI manager, ceilings from the Development stream) and a board with its principal on
/// the record, which are world sections and so are in the hash. The fixture has no era data, so finance and sponsors stay out.
/// It changed again in #173 (B3): the ceiling draw is a child of Development tagged with the organization, the season and the
/// concept axes, and approval no longer advances <c>NextCeilingDraw</c>. The same concept cannot be re-rolled, so the stored
/// ceiling and the cars-section counter differ from the #160 hash. Two runs with one seed still match (INV-002). Seat sync (B4)
/// does not move this fixture: its one contracted driver stays seated.
/// It changed again in #184: the AI principal of the fixture team reviews and writes the <c>principals</c> section, and contract
/// renewals are that principal's commands instead of the renewal placeholder. The gate manager <c>ai:paddock</c> stays, and the
/// team adds <c>ai:alpha</c>.
/// It changed again for the season-target choice (#197): an AI team still takes the expected finish, but the objective
/// records that choice, so the objectives section text differs.
/// It changed again in #194: the fixture driver is checked for a mid-contract raise, so the raises section is in the
/// hash. The retired string stayed chief,leap,vet. Two runs with one seed still match (INV-002).
/// It changed again in #199: empty staff chairs are filled and the race-engineer pairing is a world section, so both are
/// in the hash. The retired string stayed chief,leap,vet. Two runs with one seed still match (INV-002).
/// It changed again in #227: an opening car races at its strength (its levels are the strength, the development ceiling is the strength
/// plus <c>CarEstimates.InitialHeadroom</c>) and a newly approved concept maps its full potential to the ceiling instead of about
/// half of it, so the cars section differs. The retired string stayed chief,leap,vet.
/// </para>
/// </summary>
public class CareerRunTests
{
    private const ulong Seed = 7;

    private const string StoredWorldHash = "2f80e1c9d21c7e8e92e22c7462a38ba7aeb41a76ac64e14df92099796297490f";

    private const string StoredRetired = "chief,leap,vet";

    [Fact]
    public void CurveDoesNotDrawOutsideTheWindowAndAgreesWithItself()
    {
        var below = RngStreams.Derive(Seed, RngStreamName.LifeEvents, 1950);
        var before = below.State;
        Assert.False(RetirementCurve.Retires(CareerDayEstimates.DriverRetirementFromAge - 1, true, below.NextDouble));
        Assert.Equal(before, below.State);

        var certain = RngStreams.Derive(Seed, RngStreamName.LifeEvents, 1950);
        var certainBefore = certain.State;
        Assert.True(RetirementCurve.Retires(CareerDayEstimates.DriverRetirementCertainAge, true, certain.NextDouble));
        Assert.Equal(certainBefore, certain.State);

        var first = RngStreams.Derive(Seed, RngStreamName.LifeEvents, 1951);
        var second = RngStreams.Derive(Seed, RngStreamName.LifeEvents, 1951);
        var age = CareerDayEstimates.DriverRetirementFromAge;
        Assert.Equal(
            RetirementCurve.Retires(age, true, first.NextDouble),
            RetirementCurve.Retires(age, true, second.NextDouble));
        Assert.NotEqual(RngStreams.Derive(Seed, RngStreamName.LifeEvents, 1951).State, first.State);
    }

    [Fact]
    public void CertainBirthdayRetiresAndLeavesThePool()
    {
        var session = Session(Seed, target: 0, arrivals: []);
        session.LiveDay();

        Assert.Contains(session.Retired, id => id.Value == "vet");
        Assert.DoesNotContain(session.TalentPool, id => id.Value == "vet");
        Assert.DoesNotContain(session.Retired, id => id.Value == "kid");
        Assert.Equal(new GameDate(1950, 1, 2), session.Date);
    }

    [Fact]
    public void ARealPersonWhoRetiresStaysInTheWorldAndRetirementShowsInTheStateHash()
    {
        // TECH 6.2: real people always stay. Retirement is a fact of the world, so it is in the hash (and the save).
        var opening = WorldAt(new GameDate(1950, 1, 1));
        var session = new CareerSession(
            opening,
            Seed,
            [PersonId.Real("vet")],
            [],
            new CareerSessionOptions { Pool = new TalentPoolOptions { TargetSize = 0 } });
        session.LiveDay();

        Assert.Contains(session.Retired, id => id.Value == "vet");
        Assert.Contains(session.World.Persons, person => person.Id.Value == "vet");
        Assert.NotEqual(
            opening.WithDate(session.World.CurrentDate).StateHash(),
            session.World.StateHash());
    }

    [Fact]
    public void LeapDayBirthdayIsObservedOn28FebruaryInACommonYear()
    {
        var session = Session(Seed, target: 0, arrivals: []);
        while (session.Date < new GameDate(1950, 2, 28))
        {
            session.LiveDay();
        }

        Assert.DoesNotContain(session.Retired, id => id.Value == "leap");
        session.LiveDay();
        Assert.Contains(session.Retired, id => id.Value == "leap");
    }

    [Fact]
    public void LeapDayBirthdayWaitsFor29FebruaryInALeapYear()
    {
        var world = WorldAt(new GameDate(1952, 2, 28));
        var session = new CareerSession(world, Seed, [], [], new CareerSessionOptions { Pool = new TalentPoolOptions { TargetSize = 0 } });
        session.LiveDay();
        Assert.DoesNotContain(session.Retired, id => id.Value == "leap");
        Assert.Equal(new GameDate(1952, 2, 29), session.Date);
        session.LiveDay();
        Assert.Contains(session.Retired, id => id.Value == "leap");
    }

    [Fact]
    public void ContractExpiryIsRecordedOnceAndTheContractStays()
    {
        var session = Session(Seed, target: 0, arrivals: []);
        while (session.Date < new GameDate(1950, 12, 31))
        {
            session.LiveDay();
        }

        Assert.Equal(0, session.ContractExpiries);
        session.LiveDay();
        Assert.Equal(1, session.ContractExpiries);
        session.LiveDay();
        Assert.Equal(1, session.ContractExpiries);
        Assert.Single(session.World.Contracts);
        Assert.Equal(new GameDate(1950, 12, 31), session.World.Contracts[0].End);
    }

    [Fact]
    public void ScheduledDriverEntersThePoolOnTheEntryDay()
    {
        var arrival = new ScheduledArrival(
            new GameDate(1950, 1, 3),
            Person("rookie", "Rookie", "Driver", new GameDate(1938, 4, 4), driver: true));
        var session = Session(Seed, target: 0, arrivals: [arrival]);
        session.LiveDay();
        session.LiveDay();
        Assert.Equal(0, session.Intakes);
        session.LiveDay();
        Assert.Equal(1, session.Intakes);
        Assert.Contains(session.World.Persons, person => person.Id.Value == "rookie");
        Assert.Contains(session.TalentPool, id => id.Value == "rookie");
    }

    [Fact]
    public void GeneratedIntakeStartsTheSeasonAfterTheOpeningYear()
    {
        var session = Session(Seed, target: 1, arrivals: []);
        while (session.Date < new GameDate(1951, 1, 1))
        {
            session.LiveDay();
        }

        var before = session.World.Persons.Count;
        Assert.Equal(0, session.Intakes);
        session.LiveDay();
        Assert.Equal(before + 1, session.World.Persons.Count);
        Assert.Equal(1, session.Intakes);
        Assert.Contains(session.TalentPool, id => !id.IsReal);
    }

    [Fact]
    public void TwoRunsFrom1950Through1960MatchTheStoredHash()
    {
        var left = RunDecade(Seed);
        var right = RunDecade(Seed);
        var other = RunDecade(Seed + 1);

        Assert.Equal(left.Hash, right.Hash);
        Assert.Equal(left.Retired, right.Retired);
        Assert.NotEqual(left.Hash, other.Hash);
        Assert.Equal(new GameDate(1961, 1, 1), left.Date);
        Assert.Equal(11, left.Years);
        Assert.Equal(0, left.Humans);
        Assert.Equal(2, left.Ai);
        Assert.True(left.Commands > 0);
        Assert.True(
            left.Hash == StoredWorldHash && left.Retired == StoredRetired,
            "actual hash " + left.Hash + " retired " + left.Retired);
    }

    [Fact]
    public void SaveRoundTripsTheWorldAndTheAiManager()
    {
        var session = Session(Seed, target: 0, arrivals: []);
        CareerHost.Run(session, 1950);
        var directory = Directory.CreateTempSubdirectory("paddock-run-");
        try
        {
            var path = Path.Combine(directory.FullName, "career.paddock");
            CareerSaveWriter.Write(
                path,
                session,
                CareerConfig.FromPreset(CareerPreset.Chaos).WithStartYear(1950),
                "alpha",
                new string('a', 64),
                "fixture 1950");
            using var save = SaveFile.Open(path);
            var loaded = new WorldRepository(save).LoadAll();
            Assert.Equal(session.World.StateHash(), loaded.World.StateHash());
            Assert.Equal(session.Date, loaded.World.CurrentDate);
            Assert.Equal(CareerHost.AiManagerId, Assert.Single(loaded.Managers).Id);
            Assert.Equal("Ai", loaded.Managers[0].Kind);
            Assert.Equal((long)session.Clock.NextEventId, loaded.NextEventId);
            Assert.Equal((long)session.Clock.Queue.NextSequence, loaded.NextEventSequence);
            Assert.Empty(loaded.CommandLog);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Fact]
    public void ARetireeKeepsTheirRecordAndTheirDateThroughASaveAndLoad()
    {
        var session = Session(Seed, target: 0, arrivals: []);
        CareerHost.Run(session, 1950);
        var directory = Directory.CreateTempSubdirectory("paddock-retiree-");
        try
        {
            var path = Path.Combine(directory.FullName, "career.paddock");
            CareerSaveWriter.Write(
                path,
                session,
                CareerConfig.FromPreset(CareerPreset.Chaos).WithStartYear(1950),
                "alpha",
                new string('a', 64),
                "fixture 1950");
            using var save = SaveFile.Open(path);
            var loaded = new WorldRepository(save).LoadWorld();

            Assert.Equal(session.World.StateHash(), loaded.StateHash());
            Assert.Equal(new GameDate(1950, 1, 1), loaded.GetPerson(PersonId.Real("vet")).RetiredOn);
            Assert.Null(loaded.GetPerson(PersonId.Real("kid")).RetiredOn);
            Assert.Equal(
                session.Retired.Select(id => id.Value),
                loaded.Persons.Where(person => person.IsRetired).Select(person => person.Id.Value));
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Fact]
    public void ARealPersonWithAKnownLastSeasonRetiresAtItsEndNotByTheAgeCurve()
    {
        var session = WithLastSeasons(new Dictionary<string, int> { ["vet"] = 1952, ["kid"] = 1951 }, arrivals: []);
        while (session.Date < new GameDate(1952, 12, 31))
        {
            session.LiveDay();
            Assert.DoesNotContain(session.Retired, id => id.Value == "vet");
        }

        Assert.Equal(new GameDate(1951, 12, 31), session.World.GetPerson(PersonId.Real("kid")).RetiredOn);
        session.LiveDay();
        Assert.Equal(new GameDate(1952, 12, 31), session.World.GetPerson(PersonId.Real("vet")).RetiredOn);
        Assert.DoesNotContain(session.TalentPool, id => id.Value == "vet");
        Assert.Contains(session.Retired, id => id.Value == "chief");
    }

    [Fact]
    public void ALastSeasonBeforeTheRunOpenedMeansLeavingAtTheEndOfTheFirstSeason()
    {
        var session = WithLastSeasons(new Dictionary<string, int> { ["kid"] = 1940 }, arrivals: []);
        while (session.Date < new GameDate(1950, 12, 31))
        {
            session.LiveDay();
        }

        Assert.Null(session.World.GetPerson(PersonId.Real("kid")).RetiredOn);
        session.LiveDay();
        Assert.Equal(new GameDate(1950, 12, 31), session.World.GetPerson(PersonId.Real("kid")).RetiredOn);
    }

    [Fact]
    public void ARealArrivalWithAKnownLastSeasonRetiresAtItsEnd()
    {
        var arrival = new ScheduledArrival(
            new GameDate(1951, 1, 1),
            Person("rookie", "Rookie", "Driver", new GameDate(1938, 4, 4), driver: true));
        var session = WithLastSeasons(new Dictionary<string, int> { ["rookie"] = 1952 }, [arrival]);
        while (session.Date < new GameDate(1953, 1, 1))
        {
            session.LiveDay();
        }

        Assert.Equal(new GameDate(1952, 12, 31), session.World.GetPerson(PersonId.Real("rookie")).RetiredOn);
        Assert.DoesNotContain(session.TalentPool, id => id.Value == "rookie");
    }

    [Fact]
    public void ARetireeLosesTheirContractAndItsExpiryIsNotRecorded()
    {
        var session = WithLastSeasons(new Dictionary<string, int> { ["kid"] = 1950 }, arrivals: []);
        while (session.Date < new GameDate(1951, 1, 2))
        {
            session.LiveDay();
        }

        Assert.Equal(new GameDate(1950, 12, 31), session.World.GetPerson(PersonId.Real("kid")).RetiredOn);
        Assert.Empty(session.World.Contracts);
        Assert.Equal(0, session.ContractExpiries);
    }

    [Fact]
    public void LastSeasonsComeFromTheSeatsAndSkipDriversWhoseLastSeatIsTheEndOfTheData()
    {
        var provider = new ListProvider(
            Seated("early", 1962, 1960),
            Seated("mid", 1965),
            Seated("end", 1970, 1971),
            Seated("pool"));

        var last = LastSeasons.From(provider);

        Assert.Equal(new Dictionary<string, int> { ["early"] = 1962, ["mid"] = 1965 }, last);
    }

    [Fact]
    public void ScheduleListsOnlyFutureRealDrivers()
    {
        var world = WorldState.At(new GameDate(1950, 1, 1));
        (world, _) = world.AddPerson(Person("already", "Already", "Here", new GameDate(1930, 1, 1), driver: true));
        var provider = new ListProvider(
            Driver("future", 1952, new DateOnly(1934, 5, 5)),
            Driver("already", 1952, new DateOnly(1930, 1, 1)),
            Driver("opening", 1950, new DateOnly(1928, 1, 1)),
            Driver("nameless", 1953, null));
        var config = CareerConfig.FromPreset(CareerPreset.Balanced).WithStartYear(1950);

        var arrivals = TalentIntakeSchedule.AfterStart(config, provider, world, Seed);

        var arrival = Assert.Single(arrivals);
        Assert.Equal(new GameDate(1952, CareerDayEstimates.PoolEntryMonth, CareerDayEstimates.PoolEntryDay), arrival.On);
        Assert.Equal("future", arrival.Spec.RealId);
        Assert.Empty(TalentIntakeSchedule.AfterStart(
            CareerConfig.FromPreset(CareerPreset.Chaos).WithStartYear(1950),
            provider,
            world,
            Seed));
    }

    [Fact]
    public void AnExitClauseEndIsCountedInTheRunTallies()
    {
        var session = Session(Seed, target: 0, arrivals: []);
        session.AttachHandlers([new ExitMarker()]);

        session.LiveDay();

        Assert.Equal(1, session.ContractExpiries);
        Assert.Equal(1, session.SeasonExpired);
    }

    private sealed class ExitMarker : IDayHandler
    {
        public int Order => 710;

        public void OnDay(DayContext context) =>
            context.Emit("contract.exitExercised", new MarkerPayload("con:1"));
    }

    private static CareerSession WithLastSeasons(IReadOnlyDictionary<string, int> lastSeasons, IReadOnlyList<ScheduledArrival> arrivals) =>
        new(WorldAt(new GameDate(1950, 1, 1)), Seed, [PersonId.Real("vet")], arrivals, new CareerSessionOptions
        {
            Pool = new TalentPoolOptions { TargetSize = 0 },
            LastSeasons = lastSeasons,
        });

    private static RealDriverRecord Seated(string id, params int[] seasons) =>
        new(id, "Given", "Family", new DateOnly(1930, 1, 1), 1930, "GBR", 1950, 1948, seasons.Select(season => new DriverSeat(season, "alpha", 1, "race")).ToArray());

    private static DecadeRun RunDecade(ulong seed)
    {
        var session = Session(seed, target: PoolEstimates.TargetSize, arrivals: []);
        var result = CareerHost.Run(session, 1960);
        return new DecadeRun(
            session.World.StateHash(),
            string.Join(',', session.Retired.Select(id => id.Value)),
            session.Date,
            session.Years.Count,
            result.HumanManagers,
            result.AiManagers,
            result.CommandsDispatched);
    }

    private static CareerSession Session(ulong seed, int target, IReadOnlyList<ScheduledArrival> arrivals) =>
        new(WorldAt(new GameDate(1950, 1, 1)), seed, [PersonId.Real("vet")], arrivals, new CareerSessionOptions
        {
            Pool = new TalentPoolOptions { TargetSize = target },
        });

    private static WorldState WorldAt(GameDate date)
    {
        var world = WorldState.At(date);
        (world, var team) = world.AddOrganization(new OrganizationSpec(
            OrganizationKind.Team,
            true,
            "alpha",
            new GameDate(1950, 1, 1),
            null,
            0,
            [new OrganizationNameSpan("Alpha", new GameDate(1950, 1, 1), null)]));
        (world, _) = world.AddPerson(Person("vet", "Vet", "Driver", new GameDate(1900, 1, 1), driver: true));
        (world, _) = world.AddPerson(Person("kid", "Kid", "Driver", new GameDate(1935, 7, 1), driver: true));
        (world, _) = world.AddPerson(Person("leap", "Leap", "Driver", new GameDate(1896, 2, 29), driver: true));
        (world, var chief) = world.AddPerson(Person("chief", "Chief", "Designer", new GameDate(1870, 3, 1), driver: false));
        (world, _) = world.AddContract(new ContractSpec(
            PersonId.Real("kid"),
            team,
            ContractRole.Driver(SeatStatus.Equal),
            new GameDate(1950, 1, 1),
            new GameDate(1950, 12, 31),
            0,
            true,
            null,
            null));
        _ = chief;
        return world;
    }

    private static PersonSpec Person(string id, string given, string family, GameDate birth, bool driver)
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

        return new PersonSpec(given, family, birth, "GBR", true, id, roles, truth);
    }

    private static RealDriverRecord Driver(string id, int entry, DateOnly? birth) =>
        new(id, "Given", "Family", birth, birth?.Year, "GBR", entry + 2, entry, []);

    private sealed record DecadeRun(string Hash, string Retired, GameDate Date, int Years, int Humans, int Ai, int Commands);

    private sealed class ListProvider : IPeopleProvider
    {
        public ListProvider(params RealDriverRecord[] drivers) => Drivers = drivers;

        public IReadOnlyList<RealDriverRecord> Drivers { get; }

        public DriverRating? RatingFor(string driverId, int season) => null;
    }
}
