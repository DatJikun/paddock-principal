using Paddock.Domain.Racing;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Weekend;

namespace Paddock.Tests.Racing.Weekend;

/// <summary>Orders from the pit wall during the race (#286): data with a lap, deterministic, and they never rewrite what was driven.</summary>
public class PitWallOrderTests
{
    private const int Season = 1976;

    private static (RaceWeekendResult Plain, string Car) Baseline()
    {
        var plain = WeekendTestKit.Run(WeekendTestKit.Input(Season));
        // The winner: a car that runs the whole race, so every lap of the orders below exists.
        var car = plain.Tape.Events.OfType<Finished>().First().DriverId;
        return (plain, car);
    }

    private static RaceWeekendResult With(params PitWallOrder[] orders) =>
        WeekendTestKit.Run(WeekendTestKit.Input(Season) with { Orders = [.. orders] });

    private static long LapStartMs(RaceWeekendResult result, string car, int lap) =>
        result.PitWall.Single(l => l.CarId == car && l.Lap == lap).StartMs;

    [Fact]
    public void AStopCalledByThePitWallIsMadeAtTheEndOfItsLapOnTheTyresAskedFor()
    {
        var (plain, car) = Baseline();
        var hard = TyreCatalogHardest();

        var steered = With(new PitWallOrder(car, 10, PitWallOrderKind.Pit, CompoundId: hard));

        var exit = Assert.Single(steered.Tape.Events.OfType<PitStop>(), p => p.DriverId == car && p.Lap == 10 && p.Phase == PitLanePhase.Out);
        Assert.Equal(hard, exit.Tyres);
        Assert.DoesNotContain(plain.Tape.Events.OfType<PitStop>(), p => p.DriverId == car && p.Lap == 10);
    }

    [Fact]
    public void AnOrderLeavesEverythingBeforeItsLapAsItWas()
    {
        var (plain, car) = Baseline();

        var steered = With(
            new PitWallOrder(car, 10, PitWallOrderKind.Pit, CompoundId: TyreCatalogHardest()),
            new PitWallOrder(car, 12, PitWallOrderKind.Pace, PaceMode.Push));

        var cut = LapStartMs(plain, car, 10);
        Assert.Equal(cut, LapStartMs(steered, car, 10));
        Assert.True(plain.Tape.SameBefore(steered.Tape, cut));
        Assert.False(plain.Tape.SameBefore(steered.Tape, steered.Tape.Events[^1].RaceTime + 1));
        Assert.NotEqual(plain.Tape.Hash, steered.Tape.Hash);
    }

    [Fact]
    public void PushingIsFasterButBurnsMoreFuelAndTheStrategistGetsTheCarBackOnAuto()
    {
        var (plain, car) = Baseline();

        var steered = With(
            new PitWallOrder(car, 3, PitWallOrderKind.Pace, PaceMode.Push),
            new PitWallOrder(car, 9, PitWallOrderKind.Auto));

        var pushed = steered.PitWall.Where(l => l.CarId == car && l.Lap is >= 3 and < 9).ToArray();
        Assert.All(pushed, l => Assert.Equal(PaceMode.Push, l.Pace));
        Assert.All(pushed, l => Assert.True(l.Manual));
        Assert.All(steered.PitWall.Where(l => l.CarId == car && l.Lap >= 9), l => Assert.False(l.Manual));

        var fuel = (RaceWeekendResult r) => r.PitWall.Single(l => l.CarId == car && l.Lap == 9).FuelKg;
        Assert.True(fuel(steered) < fuel(plain), "pushing burns more fuel");
        var lapTimes = (RaceWeekendResult r) => r.LapRecords.Where(l => l.DriverId == car && l.Lap is >= 3 and < 9 && !l.Neutralised).Sum(l => l.LapSeconds);
        Assert.True(lapTimes(steered) < lapTimes(plain), "pushing is faster over the stint");
    }

    [Fact]
    public void TheSameOrdersGiveTheSameRace()
    {
        var (_, car) = Baseline();
        PitWallOrder[] orders = [new(car, 5, PitWallOrderKind.Pace, PaceMode.Save), new(car, 14, PitWallOrderKind.Pit, CompoundId: TyreCatalogHardest())];

        Assert.Equal(With(orders).Digest(), With(orders).Digest());
    }

    [Fact]
    public void TheRadioSaysTheTyresGoOffAsTheyWear()
    {
        var (plain, _) = Baseline();

        Assert.Contains(plain.PitWall, l => l.Feel == TyreFeel.Good);
        Assert.Contains(plain.PitWall, l => l.Feel != TyreFeel.Good);
        Assert.All(plain.PitWall.Where(l => l.TyreAgeLaps == 0), l => Assert.Equal(TyreFeel.Good, l.Feel));
    }

    [Fact]
    public void QualifyingPaceIsTheFastestAndTheDearestConserveTheSlowestAndTheCheapest()
    {
        var (plain, car) = Baseline();
        RaceWeekendResult At(PaceMode pace) => With(new PitWallOrder(car, 2, PitWallOrderKind.Pace, pace));
        double Stint(RaceWeekendResult r) => r.LapRecords.Where(l => l.DriverId == car && l.Lap is >= 2 and < 8 && !l.Neutralised).Sum(l => l.LapSeconds);
        double FuelAt8(RaceWeekendResult r) => r.PitWall.Single(l => l.CarId == car && l.Lap == 8).FuelKg;

        var quali = At(PaceMode.Qualifying);
        var push = At(PaceMode.Push);
        var conserve = At(PaceMode.Conserve);
        var save = At(PaceMode.Save);

        Assert.True(Stint(quali) < Stint(push), "qualifying pace beats push");
        Assert.True(Stint(conserve) > Stint(save), "conserve is slower than save");
        Assert.True(FuelAt8(quali) < FuelAt8(push) && FuelAt8(conserve) > FuelAt8(save), "fuel follows the pace");

        // The driver feels the tyres go off sooner the harder the car is driven.
        int FirstWorn(RaceWeekendResult r) => r.PitWall.Where(l => l.CarId == car && l.Feel != TyreFeel.Good).Select(l => l.Lap).DefaultIfEmpty(int.MaxValue).Min();
        Assert.True(FirstWorn(quali) < FirstWorn(plain), "the tyres go off earlier at qualifying pace");
        Assert.True(FirstWorn(conserve) >= FirstWorn(plain));
    }

    [Fact]
    public void FullPowerIsFasterAndThirstierAndTheLeanEngineTheOtherWay()
    {
        var (plain, car) = Baseline();
        RaceWeekendResult At(EngineMode engine) => With(new PitWallOrder(car, 2, PitWallOrderKind.Engine, Engine: engine));
        double Stint(RaceWeekendResult r) => r.LapRecords.Where(l => l.DriverId == car && l.Lap is >= 2 and < 8 && !l.Neutralised).Sum(l => l.LapSeconds);
        double FuelAt8(RaceWeekendResult r) => r.PitWall.Single(l => l.CarId == car && l.Lap == 8).FuelKg;

        var full = At(EngineMode.Full);
        var lean = At(EngineMode.Lean);

        Assert.True(Stint(full) < Stint(plain) && Stint(lean) > Stint(plain));
        Assert.True(FuelAt8(full) < FuelAt8(plain) && FuelAt8(lean) > FuelAt8(plain));
        Assert.All(full.PitWall.Where(l => l.CarId == car && l.Lap >= 2), l => Assert.Equal(EngineMode.Full, l.Engine));
        Assert.Equal(plain.Digest(), At(EngineMode.Standard).Digest());
    }

    [Fact]
    public void AHarderEngineBringsTheEngineFailureForwardWithTheSameDraws()
    {
        var stream = Paddock.Simulation.Racing.Reliability.FailureSampler.DeriveRaceStream(WeekendTestKit.Seed, 1955, 1);
        var components = Paddock.Tests.Racing.Reliability.ReliabilityTestKit.ReferenceComponents();
        var inputs = Paddock.Tests.Racing.Reliability.ReliabilityTestKit.ReferenceInputs(1955);
        var earlier = 0;
        for (var i = 0; i < 200; i++)
        {
            var id = "car-" + i;
            var plain = Paddock.Simulation.Racing.Reliability.FailureSampler.Sample(stream, id, 80, components, inputs);
            var same = Paddock.Simulation.Racing.Reliability.FailureSampler.Sample(stream, id, 80, components, inputs, hazardScale: (_, _) => 1d);
            Assert.Equal(plain, same);

            var full = Paddock.Simulation.Racing.Reliability.FailureSampler.Sample(
                stream, id, 80, components, inputs, hazardScale: (_, part) => PaceEffects.Hazard(EngineMode.Full, part));
            var lean = Paddock.Simulation.Racing.Reliability.FailureSampler.Sample(
                stream, id, 80, components, inputs, hazardScale: (_, part) => PaceEffects.Hazard(EngineMode.Lean, part));
            int Lap(Paddock.Simulation.Racing.Reliability.FailureSample s) => s.Retirement?.Lap ?? int.MaxValue;
            Assert.True(Lap(full) <= Lap(plain) && Lap(plain) <= Lap(lean));
            if (Lap(full) < Lap(plain))
            {
                earlier++;
            }
        }

        Assert.True(earlier > 0, "full power retires some cars sooner");
    }

    [Fact]
    public void ATeamOrderLetsTheTeamMateRightBehindThrough()
    {
        var input = WeekendTestKit.Input(Season);
        var plain = WeekendTestKit.Run(input);
        var team = input.Entries.ToDictionary(e => e.CarId, e => e.ConstructorId, StringComparer.Ordinal);
        var tapeId = input.Entries.ToDictionary(e => e.CarId, e => e.Primary.DriverId, StringComparer.Ordinal);

        // A lap that ends with two team-mates nose to tail, the faster one behind.
        var pair = plain.LapRecords
            .Where(l => !l.Neutralised)
            .GroupBy(l => l.Lap)
            .Where(g => g.Key >= 2 && g.Key < input.TotalLaps - 1)
            .SelectMany(g =>
            {
                var order = g.OrderBy(l => l.Position).ToArray();
                return order.Zip(order.Skip(1), (ahead, behind) => (Lap: g.Key, Ahead: ahead, Behind: behind));
            })
            .First(p => team[p.Ahead.CarId] == team[p.Behind.CarId] && p.Behind.EndSeconds - p.Ahead.EndSeconds <= PitConstants.LetByGapSeconds);

        var lap = pair.Lap + 1;
        var steered = With(new PitWallOrder(tapeId[pair.Ahead.CarId], lap, PitWallOrderKind.LetBy, On: true));

        int Position(RaceWeekendResult r, string id) => r.LapRecords.Single(l => l.CarId == id && l.Lap == lap).Position;
        Assert.True(Position(plain, pair.Ahead.CarId) < Position(plain, pair.Behind.CarId));
        Assert.True(Position(steered, pair.Behind.CarId) < Position(steered, pair.Ahead.CarId), "the team-mate is through");
        Assert.True(plain.Tape.SameBefore(steered.Tape, (long)(pair.Ahead.EndSeconds * 1000) - 1));
    }

    private static string TyreCatalogHardest() =>
        Paddock.Simulation.Racing.Tyres.TyreCompoundCatalog.Default.RaceCompounds(Season)[^1].Id;
}
