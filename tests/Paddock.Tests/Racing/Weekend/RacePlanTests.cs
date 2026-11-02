using System.Collections.Immutable;
using Paddock.Domain.Racing;
using Paddock.Simulation.Racing;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Tyres;
using Paddock.Simulation.Racing.Weekend;

namespace Paddock.Tests.Racing.Weekend;

/// <summary>
/// The pre-race plan (#316): data the engine reads before lap 1, so another plan is another race and the same plan the same race.
/// Practice and qualifying come from the engine's sessions and never change the race.
/// </summary>
public class RacePlanTests
{
    private const int Season = 2010;

    private static RaceWeekendInput Input() => WeekendTestKit.Input(Season);

    private static string Winner(RaceWeekendResult result) => result.Tape.Events.OfType<Finished>().First().DriverId;

    private static string[] Dry() => [.. TyreCompoundCatalog.Default.RaceCompounds(Season).Select(c => c.Id)];

    [Fact]
    public void APlanThatSetsNothingIsTheSameRace()
    {
        var plain = WeekendTestKit.Run(Input());
        var planned = WeekendTestKit.Run(Input() with { Plans = [new RacePlan(Winner(plain))] });

        Assert.Equal(plain.Tape.Hash, planned.Tape.Hash);
    }

    [Fact]
    public void AnotherPlanIsAnotherRaceAndTheSamePlanTheSameRace()
    {
        var plain = WeekendTestKit.Run(Input());
        var car = Winner(plain);
        var dry = Dry();
        var plan = new RacePlan(car, dry[^1], PaceMode.Push, EngineMode.Full, [new PlannedStop(20, dry[0])]);

        var first = WeekendTestKit.Run(Input() with { Plans = [plan] });
        var again = WeekendTestKit.Run(Input() with { Plans = [plan] });

        Assert.NotEqual(plain.Tape.Hash, first.Tape.Hash);
        Assert.Equal(first.Tape.Hash, again.Tape.Hash);
        var lap1 = first.PitWall.Single(lap => lap.CarId == car && lap.Lap == 1);
        Assert.Equal(dry[^1], lap1.CompoundId);
        Assert.Equal(PaceMode.Push, lap1.Pace);
        Assert.True(lap1.Manual);
        Assert.Equal(EngineMode.Full, lap1.Engine);
    }

    [Fact]
    public void PlannedStopsAreTheOnlyDryStopsTheCarMakes()
    {
        var plain = WeekendTestKit.Run(Input());
        var car = Winner(plain);
        var dry = Dry();
        var plan = new RacePlan(car, dry[0], Stops: [new PlannedStop(15, dry[^1]), new PlannedStop(35, dry[^1])]);

        var planned = WeekendTestKit.Run(Input() with { Plans = [plan] });

        var stops = planned.Tape.Events.OfType<PitStop>()
            .Where(stop => stop.DriverId == car && stop.Phase == PitLanePhase.Out && stop.Tyres is { } tyres && dry.Contains(tyres))
            .Select(stop => stop.Lap)
            .ToArray();
        Assert.Equal([15, 35], stops);
    }

    [Fact]
    public void TheStartTyresOfAPlanMustBeARaceCompoundOfTheSeason()
    {
        var plain = WeekendTestKit.Run(Input());
        var car = Winner(plain);
        var planned = WeekendTestKit.Run(Input() with { Plans = [new RacePlan(car, "no-such-tyre")] });

        Assert.Equal(plain.Tape.Hash, planned.Tape.Hash);
    }

    [Fact]
    public void QualifyingFromTheSessionsIsTheGridTheRaceStartsFrom()
    {
        var input = Input();
        var race = WeekendTestKit.Run(input);
        var sessions = RaceSimulators.Sessions(RaceEngineKind.Lap);

        var qualifying = sessions.Qualifying(input);

        Assert.Equal(race.Qualifying.GridOrder, qualifying.GridOrder);
        Assert.Equal(race.Qualifying.Grid.Select(slot => slot.BestLapSeconds), qualifying.Grid.Select(slot => slot.BestLapSeconds));
    }

    [Fact]
    public void PracticeIsDeterministicReadsEveryDryCompoundAndChangesNothingInTheRace()
    {
        var input = Input();
        var before = WeekendTestKit.Run(input);
        var sessions = RaceSimulators.Sessions(RaceEngineKind.Lap);

        var practice = sessions.Practice(input);
        var again = sessions.Practice(input);

        Assert.Equal(input.Entries.Length, practice.Cars.Length);
        Assert.Equal(practice, again, PracticeComparer.Instance);
        Assert.True(practice.Cars.Zip(practice.Cars.Skip(1)).All(pair => pair.First.BestLapSeconds <= pair.Second.BestLapSeconds));
        Assert.All(practice.Cars, car =>
        {
            Assert.Equal(Dry(), car.Tyres.Select(read => read.CompoundId));
            Assert.All(car.Tyres, read => Assert.True(read.LowLaps >= 1 && read.LowLaps <= read.HighLaps));
            Assert.True(car.LongRunSeconds > car.BestLapSeconds);
        });
        Assert.Equal(before.Tape.Hash, WeekendTestKit.Run(input).Tape.Hash);
    }

    [Fact]
    public void ABetterStrategistReadsTheTyresMoreNarrowly()
    {
        var input = Input();
        var sharp = input with { Entries = [.. input.Entries.Select(entry => entry with { StrategistSkill = 100 })] };
        var blunt = input with { Entries = [.. input.Entries.Select(entry => entry with { StrategistSkill = 0 })] };
        var sessions = RaceSimulators.Sessions(RaceEngineKind.Lap);

        static double Width(Paddock.Simulation.Racing.Practice.PracticeFacts facts) =>
            facts.Cars.SelectMany(car => car.Tyres).Average(read => read.HighLaps - read.LowLaps);

        Assert.True(Width(sessions.Practice(sharp)) < Width(sessions.Practice(blunt)));
    }

    private sealed class PracticeComparer : IEqualityComparer<Paddock.Simulation.Racing.Practice.PracticeFacts>
    {
        public static PracticeComparer Instance { get; } = new();

        public bool Equals(Paddock.Simulation.Racing.Practice.PracticeFacts? x, Paddock.Simulation.Racing.Practice.PracticeFacts? y) =>
            x is not null && y is not null && x.Cars.Length == y.Cars.Length
            && x.Cars.Zip(y.Cars).All(pair => pair.First.CarId == pair.Second.CarId
                && pair.First.BestLapSeconds == pair.Second.BestLapSeconds
                && pair.First.LongRunSeconds == pair.Second.LongRunSeconds
                && pair.First.Tyres.SequenceEqual(pair.Second.Tyres));

        public int GetHashCode(Paddock.Simulation.Racing.Practice.PracticeFacts obj) => obj.Cars.Length;
    }
}
