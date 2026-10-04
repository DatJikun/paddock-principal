using Paddock.Domain.Career;
using Paddock.Domain.People;
using Paddock.Domain.Pool;
using Paddock.Domain.Random;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Career;
using Paddock.Simulation.Pool;
using Paddock.Simulation.Time;

namespace Paddock.Tests.Pool;

/// <summary>
/// The pool over days and years: entry, development, lapse, fillers, signing, scouting and determinism.
/// Every person is a synthetic fixture. The sizes and ages asserted are the ESTIMATES in <see cref="PoolEstimates"/>, not facts.
/// </summary>
public class PoolDayTests
{
    private const ulong Seed = 11;

    [Fact]
    public void AScheduledRealDriverEntersOnHisDayAndAnEventSaysSo()
    {
        var spec = PoolKit.RealDriver("d_new", 1933);
        var run = Open([], [new ScheduledArrival(new GameDate(1952, 1, 1), spec)]);

        run.LiveUntil(new GameDate(1952, 1, 1));
        Assert.DoesNotContain(run.Pool.Members, member => member.Id.Value == "d_new");
        run.LiveDay();

        var member = Assert.Single(run.Pool.Members, member => member.Id.Value == "d_new");
        Assert.Equal(new GameDate(1952, 1, 1), member.EnteredOn);
        Assert.Contains(run.Events, e => e.TypeId == PoolEventTypes.Entered && e.Payload is MarkerPayload { Marker: "d_new" });
        Assert.Contains(run.World.Persons, person => person.Id.Value == "d_new" && person.IsReal);
    }

    [Fact]
    public void FillersTopThePoolUpToTheTargetFromTheSecondSeason()
    {
        var run = Open([], [], new TalentPoolOptions { TargetSize = 6 });
        run.LiveUntil(new GameDate(1951, 1, 1));
        Assert.Equal(0, run.Pool.Count);
        run.LiveDay();

        Assert.Equal(6, run.Pool.Count);
        Assert.All(run.Pool.Members, member => Assert.False(run.World.GetPerson(member.Id).IsReal));
        Assert.Equal(6, run.Events.Count(e => e.TypeId == PoolEventTypes.Entered));
    }

    [Fact]
    public void FillersEnterYoungAndTheMixIsNotAListOfFutureChampions()
    {
        var run = Open([], []);
        run.LiveUntil(new GameDate(1980, 1, 1));
        var stars = 0;
        var total = 0;
        foreach (var career in run.World.Persons)
        {
            var truth = run.World.TruthOf(career.Id);
            total++;
            if (GenerationEstimates.Overall(ToAttributes(truth, potential: true), 1960) >= GenerationEstimates.StarPotential)
            {
                stars++;
            }
        }

        Assert.True(total > 60);
        Assert.True(stars / (double)total <= 0.15, $"{stars} stars in {total}");
        Assert.True(stars > 0);

        var first = Open([], []);
        first.LiveUntil(new GameDate(1951, 1, 2));
        Assert.All(first.World.Persons, person =>
        {
            var age = 1951 - person.BirthDate.Year;
            Assert.InRange(age, PoolEstimates.FillerAgeMin, PoolEstimates.FillerAgeMaxExclusive - 1);
        });
    }

    [Fact]
    public void ARealDriverNobodySignsLapsesAndTheChronicleEventIsEmittedOnce()
    {
        var spec = PoolKit.RealDriver("d_unsigned", 1933);
        var run = Open([], [new ScheduledArrival(new GameDate(1952, 1, 1), spec)], new TalentPoolOptions { TargetSize = 0 });

        run.LiveUntil(new GameDate(1958, 1, 1));
        Assert.Contains(run.Pool.Members, member => member.Id.Value == "d_unsigned");
        run.LiveDay();

        Assert.DoesNotContain(run.Pool.Members, member => member.Id.Value == "d_unsigned");
        var lapse = Assert.Single(run.Pool.Lapsed);
        Assert.Equal("d_unsigned", lapse.Id.Value);
        Assert.Equal(new GameDate(1958, 1, 1), lapse.On);
        var emitted = Assert.Single(run.Events, e => e.TypeId == PoolEventTypes.CareerLapsed);
        Assert.Equal("d_unsigned", ((MarkerPayload)emitted.Payload).Marker);
        Assert.Equal(new GameDate(1958, 1, 1), emitted.Date);

        run.LiveUntil(new GameDate(1965, 1, 1));
        Assert.Single(run.Events, e => e.TypeId == PoolEventTypes.CareerLapsed);
        Assert.Contains(run.World.Persons, person => person.Id.Value == "d_unsigned");
        Assert.True(run.World.Ids.WasIssued("d_unsigned"));
    }

    [Fact]
    public void AMemberWhoIsTooOldLapsesAfterAFullSeason()
    {
        var world = PoolKit.EmptyWorld();
        (world, var old) = PoolKit.Add(world, PoolKit.RealDriver("d_old", 1915));
        (world, var young) = PoolKit.Add(world, PoolKit.RealDriver("d_young", 1932));
        var run = new PoolKit.Run(world, Seed, [old, young], [], new TalentPoolOptions { TargetSize = 0 });

        run.LiveUntil(new GameDate(1951, 1, 2));

        Assert.False(run.Pool.Contains(old));
        Assert.True(run.Pool.HasLapsed(old));
        Assert.True(run.Pool.Contains(young));
    }

    [Fact]
    public void ASignedMemberLeavesThePoolAndDoesNotLapse()
    {
        var world = PoolKit.EmptyWorld();
        (world, var signed) = PoolKit.Add(world, PoolKit.RealDriver("d_signed", 1932));
        (world, var other) = PoolKit.Add(world, PoolKit.RealDriver("d_other", 1932));
        var run = new PoolKit.Run(world, Seed, [signed, other], [], new TalentPoolOptions { TargetSize = 0 });

        run.LiveUntil(new GameDate(1950, 3, 1));
        run.World = PoolKit.Sign(run.World, signed, PoolKit.Alpha, new GameDate(1950, 3, 1), new GameDate(1953, 12, 31));
        run.LiveDay();

        Assert.False(run.Pool.Contains(signed));
        Assert.True(run.Pool.Contains(other));
        run.LiveUntil(new GameDate(1960, 1, 1));
        Assert.False(run.Pool.HasLapsed(signed));
        Assert.Single(run.Events, e => e.TypeId == PoolEventTypes.CareerLapsed);
    }

    [Fact]
    public void MembersDevelopTowardTheirPotentialEachSeasonAndNeverBeyond()
    {
        var world = PoolKit.EmptyWorld();
        (world, var id) = PoolKit.Add(world, PoolKit.RealDriver("d_grow", 1932, current: 6, extra: 9));
        var run = new PoolKit.Run(world, Seed, [id], [], new TalentPoolOptions { TargetSize = 0 });
        var before = run.World.TruthOf(id);

        run.LiveUntil(new GameDate(1950, 12, 31));
        Assert.Equal(before.Attributes, run.World.TruthOf(id).Attributes);
        run.LiveUntil(new GameDate(1951, 1, 2));
        var after = run.World.TruthOf(id);

        Assert.True(after.Attributes.Sum(a => a.Value) > before.Attributes.Sum(a => a.Value));
        for (var i = 0; i < after.Attributes.Count; i++)
        {
            Assert.True(after.Attributes[i].Value <= after.Potential[i].Value);
        }

        Assert.Equal(before.Potential, after.Potential);
    }

    [Fact]
    public void AFundedSeasonActsOnTheNextDevelopmentAndIsThenSpent()
    {
        PersonId Develop(JuniorProgramme? programme, out int total, out PoolKit.Run run)
        {
            var world = PoolKit.EmptyWorld();
            (world, var id) = PoolKit.Add(world, PoolKit.RealDriver("d_funded", 1932, current: 4, extra: 5));
            run = new PoolKit.Run(world, Seed, [id], [], new TalentPoolOptions { TargetSize = 0 });
            if (programme is { } chosen)
            {
                run.World = run.World.WithSection(run.Pool.Fund(id, new JuniorFunding(PoolKit.Alpha, chosen, 1950)));
            }

            run.LiveUntil(new GameDate(1951, 1, 2));
            total = run.World.TruthOf(id).Attributes.Sum(a => a.Value);
            return id;
        }

        _ = Develop(null, out var plain, out _);
        _ = Develop(JuniorProgramme.ExpensiveFast, out var fast, out var fastRun);

        Assert.True(fast > plain, $"{fast} was not above {plain}");
        Assert.All(fastRun.Pool.Members, member => Assert.Null(member.Funding));
    }

    [Fact]
    public void AFundingPaidThisYearWaitsForNextYearsRoll()
    {
        var world = PoolKit.EmptyWorld();
        (world, var id) = PoolKit.Add(world, PoolKit.RealDriver("d_late", 1932, current: 4, extra: 14));
        var run = new PoolKit.Run(world, Seed, [id], [], new TalentPoolOptions { TargetSize = 0 });
        run.LiveUntil(new GameDate(1951, 1, 1));
        run.World = run.World.WithSection(run.Pool.Fund(id, new JuniorFunding(PoolKit.Alpha, JuniorProgramme.CheapSlow, 1951)));

        run.LiveDay();
        Assert.NotNull(run.Pool.Find(id)!.Funding);
        run.LiveUntil(new GameDate(1952, 1, 2));
        Assert.Null(run.Pool.Find(id)!.Funding);
    }

    [Fact]
    public void SameSeedGivesTheSamePoolForTenYearsAndAnotherSeedDoesNot()
    {
        string Hash(ulong seed)
        {
            var session = Session(seed, 10);
            while (session.Date < new GameDate(1960, 1, 1))
            {
                session.LiveDay();
            }

            return PoolHash(session) + "|" + session.World.StateHash();
        }

        var first = Hash(Seed);
        Assert.Equal(first, Hash(Seed));
        Assert.NotEqual(first, Hash(Seed + 1));
    }

    [Fact]
    public void ThePoolStaysWithinTheExpectedSizeOverThirtyYears()
    {
        var (start, ids) = DriversInPool();
        var run = new PoolKit.Run(start, Seed, ids, [], new TalentPoolOptions());
        var sizes = new List<int>();
        var signed = 0;
        while (run.Date < new GameDate(1980, 1, 1))
        {
            run.LiveDay();
            if (run.Date.Day == 2 && run.Date.Year > 1950)
            {
                sizes.Add(run.Pool.Count);
            }

            if (run.Date.Month == 3 && run.Date.Day == 1 && run.Pool.Count > 3)
            {
                // Two members are signed every March, so the pool is also tested with people leaving it.
                foreach (var member in run.Pool.Members.Take(2))
                {
                    run.World = PoolKit.Sign(run.World, member.Id, PoolKit.Alpha, run.Date, new GameDate(run.Date.Year + 3, 12, 31));
                    signed++;
                }
            }
        }

        Assert.True(signed >= 50);
        Assert.True(sizes.Count >= 12 * 28);
        Assert.InRange(sizes.Min(), PoolEstimates.ExpectedMinSize, PoolEstimates.ExpectedMaxSize);
        Assert.InRange(sizes.Max(), PoolEstimates.ExpectedMinSize, PoolEstimates.ExpectedMaxSize);
    }

    [Fact]
    public void ExtraScoutingDoesNotShiftDevelopmentOrFillers()
    {
        var (world, ids) = DriversInPool();
        var plain = new PoolKit.Run(world, Seed, ids, [], new TalentPoolOptions());
        var watched = new PoolKit.Run(world, Seed, ids, [], new TalentPoolOptions());
        watched.SetFocus(PoolKit.Alpha, ScoutFocusKind.Pool);
        watched.SetFocus(PoolKit.Bravo, ScoutFocusKind.Pool);

        plain.LiveUntil(new GameDate(1965, 1, 1));
        watched.LiveUntil(new GameDate(1965, 1, 1));

        Assert.Equal(plain.World.Persons.Count, watched.World.Persons.Count);
        foreach (var person in plain.World.Persons)
        {
            var other = watched.World.GetPerson(person.Id);
            Assert.Equal(person.Name, other.Name);
            Assert.Equal(person.Truth.Attributes, other.Truth.Attributes);
            Assert.Equal(person.Truth.Potential, other.Truth.Potential);
        }

        Assert.Empty(plain.World.Knowledge);
        Assert.NotEmpty(watched.World.Knowledge);
        Assert.Equal(
            plain.Pool.Members.Select(member => member.Id.Value + member.Handle),
            watched.Pool.Members.Select(member => member.Id.Value + member.Handle));
    }

    [Fact]
    public void ScoutingAndPeopleAreDifferentStreams()
    {
        Assert.NotEqual(RngStreamName.People, RngStreamName.Scouting);
        Assert.Contains(RngStreamName.Scouting, RngStreamName.All);
        Assert.NotEqual(
            RngStreams.Derive(Seed, RngStreamName.People, 1950).NextULong(),
            RngStreams.Derive(Seed, RngStreamName.Scouting, 1950).NextULong());
    }

    [Fact]
    public void ScoutingNarrowsABandMonthByMonthAndTheScoutDecidesTheJudgement()
    {
        var world = PoolKit.EmptyWorld();
        (world, var id) = PoolKit.Add(world, PoolKit.RealDriver("d_watched", 1932, current: 11, extra: 5));
        (world, var scout) = PoolKit.Add(world, PoolKit.Scout("s_best", 20, 20));
        world = PoolKit.SignScout(world, scout, PoolKit.Alpha);
        var run = new PoolKit.Run(world, Seed, [id], [], new TalentPoolOptions { TargetSize = 0 });
        run.SetFocus(PoolKit.Alpha, ScoutFocusKind.Person, id);

        var spans = new List<int>();
        var truthInside = true;
        for (var month = 0; month < 30; month++)
        {
            run.LiveUntil(new GameDate(1950, 1, 1).AddDays(31 * (month + 1)));
            var belief = run.World.KnowledgeOf(PoolKit.Alpha, id);
            if (belief is not { } view)
            {
                continue;
            }

            spans.Add(view.Attributes.Sum(known => known.Band.High - known.Band.Low));
            truthInside &= view.Attributes.All(known =>
            {
                var truth = run.World.TruthOf(id).Value(known.Key);
                return truth >= known.Band.Low && truth <= known.Band.High;
            });
        }

        Assert.True(spans.Count >= 25);
        Assert.True(spans[^1] < spans[0]);
        Assert.True(truthInside, "a perfect scout's band missed the truth while the member developed");
        Assert.Null(run.World.KnowledgeOf(PoolKit.Bravo, id));
        Assert.True(run.Pool.ObservedMilli(PoolKit.Alpha, id) > 0);
    }

    [Fact]
    public void AnOrganizationWithoutAFocusLearnsNothing()
    {
        var (world, ids) = DriversInPool();
        var run = new PoolKit.Run(world, Seed, ids, [], new TalentPoolOptions());
        run.LiveUntil(new GameDate(1955, 1, 1));
        Assert.Empty(run.World.Knowledge);
    }

    [Fact]
    public void ABestScoutNarrowsFasterThanTheDefaultEye()
    {
        int Span(bool withScout)
        {
            var world = PoolKit.EmptyWorld();
            (world, var id) = PoolKit.Add(world, PoolKit.RealDriver("d_watched", 1932, current: 11, extra: 5));
            if (withScout)
            {
                (world, var scout) = PoolKit.Add(world, PoolKit.Scout("s_best", 20, 20));
                world = PoolKit.SignScout(world, scout, PoolKit.Alpha);
            }

            var run = new PoolKit.Run(world, Seed, [id], [], new TalentPoolOptions { TargetSize = 0 });
            run.SetFocus(PoolKit.Alpha, ScoutFocusKind.Person, id);
            run.LiveUntil(new GameDate(1950, 7, 2));
            return run.World.KnowledgeOf(PoolKit.Alpha, id)!.Value.Attributes.Sum(known => known.Band.High - known.Band.Low);
        }

        Assert.True(Span(withScout: true) < Span(withScout: false), Span(withScout: true) + " vs " + Span(withScout: false));
    }

    [Fact]
    public void SharedRacesNarrowBandsFaster()
    {
        int Span(ISharedRaceSource source)
        {
            var world = PoolKit.EmptyWorld();
            (world, var id) = PoolKit.Add(world, PoolKit.RealDriver("d_watched", 1932, current: 11, extra: 5));
            var run = new PoolKit.Run(world, Seed, [id], [], new TalentPoolOptions { TargetSize = 0, SharedRaces = source });
            run.SetFocus(PoolKit.Alpha, ScoutFocusKind.Pool);
            run.LiveUntil(new GameDate(1950, 4, 2));
            return run.World.KnowledgeOf(PoolKit.Alpha, id)!.Value.Attributes.Sum(known => known.Band.High - known.Band.Low);
        }

        Assert.True(Span(new FixedRaces(3)) < Span(NoSharedRaces.Instance));
    }

    [Fact]
    public void TheSessionKeepsThePoolInTheWorldAndRetirementTakesAMemberOut()
    {
        var world = PoolKit.EmptyWorld();
        (world, var old) = PoolKit.Add(world, PoolKit.RealDriver("d_vet", 1900));
        (world, var young) = PoolKit.Add(world, PoolKit.RealDriver("d_kid", 1932));
        var session = new CareerSession(world, Seed, [old, young], [], new CareerSessionOptions { Pool = new TalentPoolOptions { TargetSize = 0 } });

        Assert.Equal(2, session.TalentPool.Count);
        Assert.NotNull(session.World.Section(TalentPoolSection.SectionName));
        while (session.Date < new GameDate(1950, 7, 1))
        {
            session.LiveDay();
        }

        Assert.Equal([young], session.TalentPool);
        Assert.Contains(old, session.Retired);
        Assert.Throws<ArgumentException>(() => new CareerSession(world, Seed, [old, old], []));
    }

    [Fact]
    public void ACommandCannotMoveTheDateOfTheSession()
    {
        var session = new CareerSession(PoolKit.EmptyWorld(), Seed, [], []);
        Assert.Throws<InvalidOperationException>(() => session.StoreWorld(session.World.WithDate(new GameDate(1950, 1, 2))));
        session.StoreWorld(session.World.WithSection(session.Pool));
    }

    // Helpers.

    private static PoolKit.Run Open(
        IEnumerable<PersonId> members,
        IEnumerable<ScheduledArrival> arrivals,
        TalentPoolOptions? options = null) =>
        new(PoolKit.EmptyWorld(), Seed, members, arrivals, options);

    private static (WorldState World, List<PersonId> Ids) DriversInPool()
    {
        var world = PoolKit.EmptyWorld();
        var ids = new List<PersonId>();
        for (var i = 0; i < 8; i++)
        {
            (world, var id) = PoolKit.Add(world, PoolKit.RealDriver("d_start" + i, 1930 + (i % 3), current: 6 + i, extra: 6));
            ids.Add(id);
        }

        return (world, ids);
    }

    private static CareerSession Session(ulong seed, int years)
    {
        _ = years;
        var world = PoolKit.EmptyWorld();
        var ids = new List<PersonId>();
        for (var i = 0; i < 6; i++)
        {
            (world, var id) = PoolKit.Add(world, PoolKit.RealDriver("d_session" + i, 1931 + i, current: 7 + i, extra: 5));
            ids.Add(id);
        }

        var arrivals = new[]
        {
            new ScheduledArrival(new GameDate(1953, 1, 1), PoolKit.RealDriver("d_late1", 1936)),
            new ScheduledArrival(new GameDate(1955, 1, 1), PoolKit.RealDriver("d_late2", 1938)),
        };
        var session = new CareerSession(world, seed, ids, arrivals);
        return session;
    }

    private static string PoolHash(CareerSession session)
    {
        var writer = new CanonicalWriter();
        session.Pool.WriteCanonical(writer);
        return writer.ToString();
    }

    private static DriverAttributes ToAttributes(PersonTruth truth, bool potential)
    {
        var source = potential ? truth.Potential : truth.Attributes;
        int Get(string key) => source.Single(attribute => attribute.Key == key).Value;
        return new DriverAttributes(
            Get("cornering"),
            Get("braking"),
            Get("smoothness"),
            Get("overtaking"),
            Get("defending"),
            Get("consistency"),
            Get("composure"),
            Get("adaptability"),
            Get("wet_weather"),
            Get("fitness"),
            Get("feedback"));
    }

    private sealed class FixedRaces : ISharedRaceSource
    {
        private readonly int _count;

        public FixedRaces(int count) => _count = count;

        public int SharedRaces(OrganizationId organization, PersonId person, GameDate on) => _count;
    }
}
