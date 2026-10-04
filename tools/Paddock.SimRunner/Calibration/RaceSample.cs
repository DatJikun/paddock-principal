using Paddock.Simulation.Racing.Points;
using Paddock.Simulation.Racing.Incidents;
using Paddock.Simulation.Racing.Tyres;
using Paddock.Simulation.Racing.Weather;
using Paddock.Simulation.Racing.Weekend;

namespace Paddock.SimRunner.Calibration;

/// <summary>
/// What one simulated race contributes to the calibration report: plain counts, so races can be pooled per era band.
/// Built from a <see cref="RaceWeekendResult"/> only (read-only, INV-001, INV-005).
/// </summary>
public sealed record RaceSample(
    int Season,
    int Starters,
    int Finishers,
    int Mechanical,
    int Accident,
    int OtherRetired,
    int Stops,
    int Lapped,
    double? MarginSeconds,
    bool PoleWon,
    int LeadChanges,
    bool SafetyCar,
    bool DryDay,
    int CarsOnWetTyres,
    bool Rain,
    double StartAirTempC)
{
    public static RaceSample From(RaceWeekendResult result, int season)
    {
        ArgumentNullException.ThrowIfNull(result);
        var cars = result.CarResults;
        var winner = cars[0];
        var wetId = TyreCompoundCatalog.Default.WetCompound(season).Id;

        var finishers = cars.Count(c => c.Status == FinishStatus.Classified);
        var lapped = cars.Count(c => c.Status == FinishStatus.Classified && c.LapsCompleted < winner.LapsCompleted);
        double? margin = null;
        if (cars.Length > 1 && cars[1].Status == FinishStatus.Classified && cars[1].LapsCompleted == winner.LapsCompleted)
        {
            margin = cars[1].TotalSeconds - winner.TotalSeconds;
        }

        var leaders = result.LapRecords
            .Where(l => l.Position == 1)
            .OrderBy(l => l.Lap)
            .Select(l => l.CarId)
            .ToList();
        var changes = 0;
        for (var i = 1; i < leaders.Count; i++)
        {
            if (leaders[i] != leaders[i - 1])
            {
                changes++;
            }
        }

        var weather = result.TruthWeather;
        var dry = !weather.HasRain && weather.Samples.All(s => s.TrackWetness < WeatherConstants.DryBelow);
        return new RaceSample(
            season,
            cars.Length,
            finishers,
            cars.Count(c => c.Status == FinishStatus.Mechanical),
            cars.Count(c => c.Status == FinishStatus.Accident),
            cars.Count(c => c.Status is not (FinishStatus.Classified or FinishStatus.Mechanical or FinishStatus.Accident)),
            cars.Sum(c => c.Stops),
            lapped,
            margin,
            winner.GridPosition == 1,
            changes,
            result.Neutralisations.Any(n => n.Kind == NeutralisationKind.SafetyCar),
            dry,
            cars.Count(c => c.CompoundsUsed.Contains(wetId, StringComparer.Ordinal)),
            weather.HasRain,
            weather.Samples[0].AirTempC);
    }
}

/// <summary>Pooled figures of many <see cref="RaceSample"/>s (or of historical races, where some figures do not exist).</summary>
public sealed record EraFigures(
    int Races,
    int Starters,
    double FinishRate,
    double MechanicalShareOfRetirements,
    double AccidentShareOfRetirements,
    double? StopsPerCar,
    double? MedianMarginSeconds,
    double LappedShareOfFinishers,
    double PoleToWinRate,
    double? LeadChangesPerRace,
    double? SafetyCarRaceShare,
    double? WetTyreShareOnDryDays,
    double RainRaceShare)
{
    public static EraFigures FromSamples(IReadOnlyCollection<RaceSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var starters = samples.Sum(s => s.Starters);
        var finishers = samples.Sum(s => s.Finishers);
        var retired = starters - finishers;
        var mech = samples.Sum(s => s.Mechanical);
        var acc = samples.Sum(s => s.Accident);
        var dry = samples.Where(s => s.DryDay).ToList();
        var dryStarters = dry.Sum(s => s.Starters);
        return new EraFigures(
            samples.Count,
            starters,
            Ratio(finishers, starters) ?? 0,
            Ratio(mech, retired) ?? 0,
            Ratio(acc, retired) ?? 0,
            Ratio(samples.Sum(s => s.Stops), starters),
            Median(samples.Where(s => s.MarginSeconds is not null).Select(s => s.MarginSeconds!.Value).ToList()),
            Ratio(samples.Sum(s => s.Lapped), finishers) ?? 0,
            Ratio(samples.Count(s => s.PoleWon), samples.Count) ?? 0,
            samples.Count == 0 ? null : samples.Sum(s => s.LeadChanges) / (double)samples.Count,
            Ratio(samples.Count(s => s.SafetyCar), samples.Count),
            Ratio(dry.Sum(s => s.CarsOnWetTyres), dryStarters),
            Ratio(samples.Count(s => s.Rain), samples.Count) ?? 0);
    }

    public static double? Ratio(int part, int whole) => whole == 0 ? null : part / (double)whole;

    public static double? Median(List<double> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        values.Sort();
        var mid = values.Count / 2;
        return values.Count % 2 == 1 ? values[mid] : (values[mid - 1] + values[mid]) / 2d;
    }
}
