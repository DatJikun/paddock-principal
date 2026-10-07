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

    private static string TyreCatalogHardest() =>
        Paddock.Simulation.Racing.Tyres.TyreCompoundCatalog.Default.RaceCompounds(Season)[^1].Id;
}
