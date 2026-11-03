using System.Collections.Immutable;
using System.Globalization;
using Paddock.Simulation.Racing.Pace;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Practice;
using Paddock.Simulation.Racing.Qualifying;
using Paddock.Simulation.Racing.Tyres;

namespace Paddock.Simulation.Racing.Weekend;

/// <summary>
/// Practice and qualifying on their own (#316): the sessions before the race, for the weekend's steps. Qualifying is the one the
/// race runs (same streams, same grid). Practice draws only children of the race's <c>LapNoise</c> stream tagged
/// <c>practice:</c>, which no race lap reads, so running it never changes the race (INV-004).
/// </summary>
internal sealed partial class WeekendRun
{
    public QualifyingResult Qualify() => RunQualifying();

    public PracticeFacts Practice()
    {
        var raceCompounds = _catalog.RaceCompounds(_in.Season);
        var middle = raceCompounds[raceCompounds.Length / 2];
        var cars = new List<PracticeCar>(_in.Entries.Length);
        foreach (var entry in _in.Entries)
        {
            var driver = entry.Primary;
            var halfTank = _fuel.StartLoadKg(_in.Season, entry.Engine, _in.Track.LengthKm, _in.TotalLaps) / 2d;
            var weight = FuelModel.CarWeightFactor(halfTank);
            var conditions = new TyreConditions(_abrasiveness, driver.Smoothness / 100d, weight);

            var best = double.PositiveInfinity;
            for (var run = 1; run <= PracticeConstants.FastLaps; run++)
            {
                best = Math.Min(best, PracticeLap(entry, WeekendConstants.QualifyingFuelKg, 0d, "fast", run));
            }

            var longRun = 0d;
            var own = entry.TyreSupplier.Apply(middle, entry.PartnerTuned);
            for (var lap = 1; lap <= PracticeConstants.LongRunLaps; lap++)
            {
                longRun += PracticeLap(entry, halfTank, TyreWear.LapLoss(own, lap, conditions), "long", lap);
            }

            var tyres = ImmutableArray.CreateBuilder<PracticeTyreRead>(raceCompounds.Length);
            foreach (var compound in raceCompounds)
            {
                var fitted = entry.TyreSupplier.Apply(compound, entry.PartnerTuned);
                var truth = fitted.CliffLap / TyreWear.WearMultiplier(conditions);
                var spread = PracticeConstants.TyreReadMinLaps
                    + (truth * PracticeConstants.TyreReadShare * (1d - (entry.StrategistSkill / 100d)));
                var rng = _lapNoiseStream.DeriveChild(Tag("tyres", entry.CarId, compound.Id));
                var read = truth + (Gaussian.FromUniforms(rng.NextDouble(), rng.NextDouble()) * spread / 2d);
                tyres.Add(new PracticeTyreRead(
                    compound.Id,
                    Math.Max(1, (int)Math.Floor(read - spread)),
                    Math.Max(1, (int)Math.Ceiling(read + spread))));
            }

            cars.Add(new PracticeCar(
                driver.DriverId,
                entry.ConstructorId,
                best,
                longRun / PracticeConstants.LongRunLaps,
                tyres.MoveToImmutable()));
        }

        return new PracticeFacts([.. cars.OrderBy(car => car.BestLapSeconds).ThenBy(car => car.CarId, StringComparer.Ordinal)]);
    }

    private double PracticeLap(RaceEntry entry, double fuelKg, double tyreLoss, string kind, int lap)
    {
        var rng = _lapNoiseStream.DeriveChild(Tag(kind, entry.CarId, lap.ToString(CultureInfo.InvariantCulture)));
        var inputs = new LapInputs
        {
            Track = _in.Track,
            Season = _in.Season,
            Limits = _limits,
            Car = entry.Car,
            Driver = entry.Primary.Pace,
            FuelMassKg = fuelKg,
            TyreWearFactor = Math.Max(0d, tyreLoss),
            NoiseDraw = Gaussian.FromUniforms(rng.NextDouble(), rng.NextDouble()),
        };
        return LapTimeModel.Compute(inputs).TotalSeconds;
    }

    private static string Tag(string kind, string carId, string detail) =>
        string.Create(CultureInfo.InvariantCulture, $"practice:{kind}:{carId}:{detail}");
}
