using Paddock.Simulation.Racing.Incidents;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Points;
using Paddock.Simulation.Racing.Weekend;

namespace Paddock.Tests.Racing.Weekend;

/// <summary>
/// What the eras do differently, over 200 seeded races each on the synthetic fixture field. Every bound is a generous
/// ESTIMATE: the numbers behind the race are uncalibrated, so these tests pin the shape of the eras (more breakdowns in 1955,
/// no safety car before 1993, a mandatory mix in 2012), not the values. The strategist searches with at most one further stop
/// (a cost limit of the tests, see <see cref="WeekendTestKit.CheapSearch"/>).
/// </summary>
public class WeekendEraTests
{
    private const int RacesPerEra = 200;

    private static readonly Lazy<RaceWeekendResult[]> Races1955 = new(() => WeekendTestKit.Races(1955, RacesPerEra));
    private static readonly Lazy<RaceWeekendResult[]> Races1988 = new(() => WeekendTestKit.Races(1988, RacesPerEra));
    private static readonly Lazy<RaceWeekendResult[]> Races2012 = new(() => WeekendTestKit.Races(2012, RacesPerEra));

    private static double MechanicalShare(RaceWeekendResult[] races) =>
        (double)races.Sum(r => r.CarResults.Count(c => c.Status == FinishStatus.Mechanical)) / races.Sum(r => r.CarResults.Length);

    private static double StopsPerCar(RaceWeekendResult[] races) =>
        (double)races.Sum(r => r.CarResults.Sum(c => c.Stops)) / races.Sum(r => r.CarResults.Length);

    private static IEnumerable<NeutralisationKind> Kinds(RaceWeekendResult[] races) =>
        races.SelectMany(r => r.Neutralisations.Select(n => n.Kind));

    [Fact]
    public void In1955_NoStopIsMadeForFuel_AndOnlyOneDryCompoundExists()
    {
        var races = Races1955.Value;

        // The data allows refuelling in 1955, but a tank holds the whole race: nobody stops for fuel, a stop is for tyres,
        // a driver change or a repair (a top-up may ride along with a tyre change).
        var stops = races.SelectMany(r => r.PitStops).ToList();
        Assert.NotEmpty(stops);
        Assert.All(stops, s => Assert.True(s.ChangedTyres || s.DriverSwap || s.IsRepair, "a stop for fuel alone"));
        var refuelled = stops.Sum(s => s.RefuelKg);
        var startFuel = races.Length * 18 * 150d; // about 18 starters, ESTIMATE of 150 kg each
        Assert.True(refuelled < 0.1 * startFuel, $"top-ups {refuelled:F0} kg against about {startFuel:F0} kg started with");

        var compounds = races.SelectMany(r => r.CarResults.SelectMany(c => c.CompoundsUsed)).Where(c => c != "wet").Distinct().ToList();
        Assert.Equal(["treaded.hard"], compounds);
    }

    [Fact]
    public void In1955_ThereAreFewerStopsThanIn2012()
    {
        var stops1955 = StopsPerCar(Races1955.Value);
        var stops2012 = StopsPerCar(Races2012.Value);

        Assert.True(stops1955 < stops2012 - 0.4, $"stops per car 1955 {stops1955:F2}, 2012 {stops2012:F2}");
        Assert.True(stops1955 < 1.0);
    }

    [Fact]
    public void MechanicalRetirements_AreMoreCommonIn1955Than1988Than2012()
    {
        var share1955 = MechanicalShare(Races1955.Value);
        var share1988 = MechanicalShare(Races1988.Value);
        var share2012 = MechanicalShare(Races2012.Value);

        Assert.True(share1955 > share1988 + 0.05, $"1955 {share1955:P1}, 1988 {share1988:P1}");
        Assert.True(share1988 > share2012 + 0.05, $"1988 {share1988:P1}, 2012 {share2012:P1}");
    }

    [Fact]
    public void ASafetyCar_AppearsOnlyWhereTheEraAllowsIt()
    {
        foreach (var races in new[] { Races1955.Value, Races1988.Value })
        {
            Assert.DoesNotContain(Kinds(races), k => k is NeutralisationKind.SafetyCar or NeutralisationKind.VirtualSafetyCar);
            Assert.DoesNotContain(
                races.SelectMany(r => r.Tape.Events),
                e => e is Paddock.Domain.Racing.SafetyCar);
        }

        var races2012 = Races2012.Value;
        var withSafetyCar = races2012.Count(r => r.Neutralisations.Any(n => n.Kind == NeutralisationKind.SafetyCar));
        Assert.InRange(withSafetyCar, RacesPerEra / 20, RacesPerEra * 4 / 5);

        // 2012 has a physical safety car and no virtual one (the data says "physical").
        Assert.DoesNotContain(Kinds(races2012), k => k == NeutralisationKind.VirtualSafetyCar);
        foreach (var race in races2012.Where(r => r.Neutralisations.Any(n => n.Kind == NeutralisationKind.SafetyCar)).Take(20))
        {
            var deployed = race.Tape.Events.OfType<Paddock.Domain.Racing.SafetyCar>().Where(e => e.Phase == Paddock.Domain.Racing.SafetyCarPhase.Deployed).ToList();
            Assert.NotEmpty(deployed);
            Assert.All(deployed, e => Assert.False(e.IsVirtual));
            Assert.Contains(race.LapRecords, l => l.Neutralised);
        }
    }

    [Fact]
    public void In1955_SharedDrivesSplitThePointsAsTheClassifierDoes()
    {
        var races = Races1955.Value;
        var swapped = 0;
        foreach (var race in races)
        {
            foreach (var car in race.CarResults)
            {
                if (car.DriversWhoDrove.Length == 1)
                {
                    continue;
                }

                swapped++;
                var classified = race.Classification.Cars.Single(c => c.DriverIds[0] == car.DriverId);
                Assert.Equal(car.DriversWhoDrove.ToArray(), classified.DriverIds.ToArray());
                var shares = classified.DriverIds
                    .Select(id => race.Classification.DriverScores.Single(s => s.DriverId == id).Points)
                    .ToList();
                Assert.All(shares, share => Assert.Equal(classified.CarPoints / 2, share));
            }
        }

        Assert.True(swapped >= RacesPerEra / 4, $"only {swapped} shared drives in {RacesPerEra} races");
    }

    [Fact]
    public void In1988And2012_NobodySharesACar_BecauseTheRuleIsNotShared()
    {
        Assert.All(Races1988.Value.Concat(Races2012.Value), race => Assert.All(race.CarResults, car => Assert.Single(car.DriversWhoDrove)));
    }

    [Fact]
    public void In2012_ADryRaceIsRunOnTwoDifferentCompounds()
    {
        var rules = PitRules.For(WeekendTestKit.Data.RuleSetFor(2012));
        var finishers = Races2012.Value
            .Where(r => !r.TruthWeather.HasRain)
            .SelectMany(r => r.CarResults)
            .Where(c => c.Status == FinishStatus.Classified && !c.CompoundsUsed.Contains("wet"))
            .ToList();

        Assert.True(finishers.Count > 500);
        var legal = finishers.Count(c => rules.MixSatisfied(c.CompoundsUsed, _ => false, wetRace: false));
        // ESTIMATE bound: a strategist that believes a forecast of rain can waive the rule in its head and be caught short.
        Assert.True(legal >= 0.98 * finishers.Count, $"{legal} of {finishers.Count} finishers ran two compounds");
    }

    [Fact]
    public void EveryEraTape_IsValid_AndRacesEndWithAWinnerOnTheLeadLap()
    {
        foreach (var race in Races1955.Value.Concat(Races1988.Value).Concat(Races2012.Value))
        {
            Assert.IsType<Paddock.Domain.Racing.RaceEnded>(race.Tape.Events[^1]);
            Assert.Equal(FinishStatus.Classified, race.CarResults[0].Status);
            Assert.Equal(race.LapsRun, race.CarResults[0].LapsCompleted);
        }
    }
}
