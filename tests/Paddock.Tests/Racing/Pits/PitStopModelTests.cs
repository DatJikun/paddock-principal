using Paddock.Domain.Random;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Tyres;

namespace Paddock.Tests.Racing.Pits;

/// <summary>Stop times. The model's numbers are ESTIMATE, so the bounds are generous.</summary>
public class PitStopModelTests
{
    private static RngStream Stream(ulong seed = PitTestKit.Seed, int season = 2012, int round = 5) =>
        PitStopModel.DeriveRaceStream(seed, season, round);

    private static readonly PitStopRequest TyresOnly = new(ChangeTyres: true);

    [Fact]
    public void RaceStream_IsThePitStopsStream()
    {
        Assert.Equal(RngStreamName.PitStops, Stream().Name);
    }

    [Fact]
    public void Service_UsesT30TimesForTyresAndFuel_TheSlowestJobWins()
    {
        var crew = new PitCrew(50);

        // Tyres only: T30's stationary tyre change time.
        var modern = PitTestKit.Rules(2012);
        Assert.Equal(FuelModel.TyreChangeSeconds(2012), PitStopModel.ServiceSeconds(modern, TyresOnly, crew), 9);

        // 1960, gravity rig: 100 kg of fuel takes longer than the tyre change.
        var old = PitTestKit.Rules(1960);
        var request = new PitStopRequest(true, 100);
        Assert.Equal(100 / FuelModel.RefuelRateKgPerSecond(1960), PitStopModel.ServiceSeconds(old, request, crew), 9);
        Assert.True(100 / FuelModel.RefuelRateKgPerSecond(1960) > FuelModel.TyreChangeSeconds(1960));

        // 1996, fast rig: the same fuel is quick, the tyres decide.
        var fast = PitTestKit.Rules(1996);
        Assert.Equal(FuelModel.TyreChangeSeconds(1996), PitStopModel.ServiceSeconds(fast, new PitStopRequest(true, 60), crew), 9);
    }

    [Fact]
    public void DriverChange_TakesItsOwnTime_InSharedCarEras()
    {
        var rules = PitTestKit.Rules(1955);
        var driverOnly = PitStopModel.ServiceSeconds(rules, new PitStopRequest(false, 0, true), new PitCrew(50));

        Assert.Equal(PitConstants.DriverChangeSeconds, driverOnly, 9);
    }

    [Fact]
    public void RequestsTheRulesForbid_Throw()
    {
        var crew = new PitCrew(50);
        Assert.Throws<InvalidOperationException>(() => PitStopModel.ServiceSeconds(PitTestKit.Rules(2012), new PitStopRequest(true, 30), crew));
        Assert.Throws<InvalidOperationException>(() => PitStopModel.ServiceSeconds(PitTestKit.Rules(2005), TyresOnly, crew));
        Assert.Throws<InvalidOperationException>(() => PitStopModel.ServiceSeconds(PitTestKit.Rules(1975), new PitStopRequest(false, 0, true), crew));
        Assert.Throws<ArgumentOutOfRangeException>(() => PitStopModel.ServiceSeconds(PitTestKit.Rules(1995), new PitStopRequest(true, -1), crew));
        Assert.Throws<ArgumentOutOfRangeException>(() => PitStopModel.ServiceSeconds(PitTestKit.Rules(1995), TyresOnly, new PitCrew(101)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PitStopModel.ServiceSeconds(PitTestKit.Rules(1995), TyresOnly, new PitCrew(50, 0)));
    }

    [Fact]
    public void RefuellingOnlyStop_IsAllowedWhereTyreChangesAreNot()
    {
        var rules = PitTestKit.Rules(2005);
        var service = PitStopModel.ServiceSeconds(rules, new PitStopRequest(false, 40), new PitCrew(50));

        Assert.Equal(40 / FuelModel.RefuelRateKgPerSecond(2005), service, 9);
    }

    [Fact]
    public void MinimumStopTime_FloorsTheServiceTime()
    {
        var rules = PitTestKit.Rules(2020);

        Assert.Equal(PitConstants.MinimumStopSeconds, PitStopModel.ServiceSeconds(rules, new PitStopRequest(false), new PitCrew(50)));
        for (ulong seed = 0; seed < 500; seed++)
        {
            var stop = PitStopModel.Sample(Stream(seed), "a", 1, rules, 18, new PitStopRequest(false), new PitCrew(50));
            Assert.True(stop.StationarySeconds - stop.FaultSeconds >= PitConstants.MinimumStopSeconds - 1e-9);
        }
    }

    [Fact]
    public void Service_ScalesWithCrewQualityAndSize()
    {
        var rules = PitTestKit.Rules(2012);
        double Service(PitCrew crew) => PitStopModel.ServiceSeconds(rules, TyresOnly, crew);

        Assert.True(Service(new PitCrew(100)) < Service(new PitCrew(50)));
        Assert.True(Service(new PitCrew(50)) < Service(new PitCrew(0)));
        Assert.Equal(1 + PitConstants.CrewSpeedSpread, PitStopModel.CrewSpeedFactor(rules, new PitCrew(0)), 9);

        var standard = rules.StandardCrewSize;
        Assert.True(Service(new PitCrew(50, standard / 2)) > Service(new PitCrew(50)));
        Assert.True(Service(new PitCrew(50, standard * 2)) < Service(new PitCrew(50)));
        Assert.Equal(Service(new PitCrew(50)), Service(new PitCrew(50, standard)), 9);
    }

    [Fact]
    public void StopTimes_FallInPlausibleRanges_ModernAndOld()
    {
        // 2012, average crew: lane loss 15 s plus about 3 s, a slow stop at worst about 20 s on top.
        var modern = PitTestKit.Rules(2012);
        var lane = modern.PitLaneTimeLossSeconds();
        var totals = Enumerable.Range(0, 3000)
            .Select(i => PitStopModel.Sample(Stream((ulong)i), "a", 1, modern, lane, TyresOnly, new PitCrew(50)).TotalSeconds)
            .ToArray();
        Assert.InRange(totals.Min(), lane + 1.9, lane + 3.5);
        Assert.InRange(totals.Max(), lane + 3, lane + 30);
        Assert.InRange(totals.Average(), lane + 2.5, lane + 5);

        // 1952: thirty seconds for the tyres.
        var old = PitTestKit.Rules(1952);
        var oldLane = old.PitLaneTimeLossSeconds();
        var oldTotals = Enumerable.Range(0, 1000)
            .Select(i => PitStopModel.Sample(Stream((ulong)i, 1952, 3), "a", 1, old, oldLane, TyresOnly, new PitCrew(50)).TotalSeconds)
            .ToArray();
        Assert.InRange(oldTotals.Average(), oldLane + 25, oldLane + 45);
    }

    [Fact]
    public void Parts_AddUpToTheTotal()
    {
        var rules = PitTestKit.Rules(1996);
        var stop = PitStopModel.Sample(Stream(), "a", 1, rules, 12, new PitStopRequest(true, 50), new PitCrew(30));

        Assert.Equal(stop.LaneLossSeconds + stop.ServiceSeconds + stop.CrewVarianceSeconds + stop.FaultSeconds, stop.TotalSeconds, 9);
        Assert.Equal(12, stop.LaneLossSeconds);
    }

    [Fact]
    public void Variance_IsZeroMean_AndShrinksWithCrewQuality()
    {
        var rules = PitTestKit.Rules(2012);
        double[] Variances(double quality) => Enumerable.Range(0, 6000)
            .Select(i => PitStopModel.Sample(Stream((ulong)i), "a", 1, rules, 15, TyresOnly, new PitCrew(quality)).CrewVarianceSeconds)
            .ToArray();

        var poor = Variances(0);
        var good = Variances(100);

        Assert.InRange(poor.Average(), -0.05, 0.05);
        Assert.InRange(good.Average(), -0.05, 0.05);
        Assert.True(Sd(poor) > 2 * Sd(good));
        Assert.InRange(Sd(poor), 0.5 * PitStopModel.VarianceSd(3, 0), 1.5 * PitStopModel.VarianceSd(3, 0));
    }

    [Fact]
    public void SlowStopsAndErrors_AreRarerForBetterCrews()
    {
        var rules = PitTestKit.Rules(2012);
        const int n = 8000;
        (double Rate, int Errors, int Slow) Faults(double quality)
        {
            var stops = Enumerable.Range(0, n)
                .Select(i => PitStopModel.Sample(Stream((ulong)i), "a", 1, rules, 15, TyresOnly, new PitCrew(quality)))
                .ToArray();
            return (stops.Count(s => s.Fault != PitStopFault.None) / (double)n, stops.Count(s => s.Fault == PitStopFault.Error), stops.Count(s => s.Fault == PitStopFault.SlowStop));
        }

        var poor = Faults(0);
        var good = Faults(100);

        Assert.InRange(poor.Rate, PitConstants.FaultProbabilityAtQuality0 - 0.02, PitConstants.FaultProbabilityAtQuality0 + 0.02);
        Assert.InRange(good.Rate, PitConstants.FaultProbabilityAtQuality100 - 0.01, PitConstants.FaultProbabilityAtQuality100 + 0.01);
        Assert.True(poor.Errors > 0 && poor.Slow > poor.Errors);
        Assert.True(PitStopModel.FaultProbability(25) > PitStopModel.FaultProbability(75));
    }

    [Fact]
    public void FaultsAddTheirSeconds_WithinTheirRanges()
    {
        var rules = PitTestKit.Rules(2012);
        var stops = Enumerable.Range(0, 4000)
            .Select(i => PitStopModel.Sample(Stream((ulong)i), "a", 1, rules, 15, TyresOnly, new PitCrew(0)))
            .Where(s => s.Fault != PitStopFault.None)
            .ToArray();

        Assert.NotEmpty(stops);
        foreach (var stop in stops)
        {
            var (low, high) = stop.Fault == PitStopFault.Error
                ? (PitConstants.ErrorMinSeconds, PitConstants.ErrorMaxSeconds)
                : (PitConstants.SlowStopMinSeconds, PitConstants.SlowStopMaxSeconds);
            Assert.InRange(stop.FaultSeconds, low, high);
        }

        Assert.All(
            Enumerable.Range(0, 200).Select(i => PitStopModel.Sample(Stream((ulong)i), "a", 1, rules, 15, TyresOnly, new PitCrew(100))).Where(s => s.Fault == PitStopFault.None),
            s => Assert.Equal(0, s.FaultSeconds));
    }

    [Fact]
    public void ExpectedStopSeconds_MatchesTheMeanOfSamples()
    {
        var rules = PitTestKit.Rules(2012);
        var crew = new PitCrew(20);
        var expected = PitStopModel.ExpectedStopSeconds(rules, 15, TyresOnly, crew);
        var mean = Enumerable.Range(0, 20_000)
            .Select(i => PitStopModel.Sample(Stream((ulong)i), "a", 1, rules, 15, TyresOnly, crew).TotalSeconds)
            .Average();

        Assert.InRange(mean, expected - 0.25, expected + 0.25);
    }

    [Fact]
    public void SameInputs_GiveTheSameStop()
    {
        var rules = PitTestKit.Rules(2012);
        var a = PitStopModel.Sample(Stream(), "car-7", 2, rules, 15, TyresOnly, new PitCrew(40));
        var b = PitStopModel.Sample(Stream(), "car-7", 2, rules, 15, TyresOnly, new PitCrew(40));

        Assert.Equal(a, b);
        Assert.NotEqual(a, PitStopModel.Sample(Stream(PitTestKit.Seed + 1), "car-7", 2, rules, 15, TyresOnly, new PitCrew(40)));
    }

    [Fact]
    public void SamplingDoesNotAdvanceTheStream()
    {
        var stream = Stream();
        var before = stream.State;
        _ = PitStopModel.Sample(stream, "a", 1, PitTestKit.Rules(2012), 15, TyresOnly, new PitCrew(50));

        Assert.Equal(before, stream.State);
    }

    [Fact]
    public void CarsAndStopsHaveIndependentSubStreams()
    {
        var rules = PitTestKit.Rules(2012);
        var stream = Stream();
        PitStopTime Stop(string car, int number) => PitStopModel.Sample(stream, car, number, rules, 15, TyresOnly, new PitCrew(10));

        // Car A's second stop is the same whether or not car B (or A's first stop) was ever drawn, and in any order.
        var alone = Stop("A", 2);
        _ = Stop("B", 1);
        _ = Stop("B", 2);
        _ = Stop("A", 1);
        Assert.Equal(alone, Stop("A", 2));

        // Different cars and different stops differ somewhere over a run of stops.
        var differsByCar = Enumerable.Range(1, 20).Any(n => Stop("A", n) != Stop("B", n));
        var differsByStop = Enumerable.Range(1, 20).Any(n => Stop("A", n) != Stop("A", n + 1));
        Assert.True(differsByCar);
        Assert.True(differsByStop);

        // Adding cars to the race changes nothing for car A.
        var smallField = Enumerable.Range(1, 5).Select(n => Stop("A", n)).ToArray();
        for (var i = 0; i < 30; i++)
        {
            _ = Stop("X" + i, 1);
        }

        Assert.Equal(smallField, Enumerable.Range(1, 5).Select(n => Stop("A", n)).ToArray());
    }

    [Fact]
    public void SampleRejectsNonsense()
    {
        var rules = PitTestKit.Rules(2012);
        Assert.Throws<ArgumentOutOfRangeException>(() => PitStopModel.Sample(Stream(), "a", 0, rules, 15, TyresOnly, new PitCrew(50)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PitStopModel.Sample(Stream(), "a", 1, rules, -1, TyresOnly, new PitCrew(50)));
        Assert.Throws<ArgumentException>(() => PitStopModel.Sample(Stream(), " ", 1, rules, 15, TyresOnly, new PitCrew(50)));
    }

    private static double Sd(double[] values)
    {
        var mean = values.Average();
        return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Length);
    }
}
