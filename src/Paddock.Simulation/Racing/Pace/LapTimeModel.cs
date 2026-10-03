using System.Globalization;
using Paddock.Domain.World;

namespace Paddock.Simulation.Racing.Pace;

/// <summary>
/// Lap time = track base + the sum of layers (DESIGN 7), in seconds. A pure function of <see cref="LapInputs"/>:
/// no state, no clock and no RNG (INV-002, INV-004); the noise sample comes from the caller. All numbers are
/// uncalibrated ESTIMATES in <see cref="PaceConstants"/>.
/// </summary>
public static class LapTimeModel
{
    public static LapTimeBreakdown Compute(LapInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        Validate(inputs);

        var baseSeconds = TrackBaseSeconds(inputs.Track, inputs.Season);

        var layers = new LapLayerValue[]
        {
            new(LapLayer.TrackBase, baseSeconds),
            new(LapLayer.CarFit, CarFitSeconds(inputs, baseSeconds)),
            new(LapLayer.Driver, -(inputs.Driver.Pace - PaceConstants.RatingReference) * PaceConstants.DriverSensitivity * baseSeconds),
            new(
                LapLayer.DriverAffinity,
                -Math.Clamp(inputs.Driver.AffinitySeconds, -PaceConstants.MaxAffinitySeconds, PaceConstants.MaxAffinitySeconds)),
            new(LapLayer.Fuel, inputs.FuelMassKg * PaceConstants.FuelSecondsPerKg),
            new(LapLayer.Tyres, inputs.TyreWearFactor),
            new(LapLayer.Traffic, TrafficSeconds(inputs.TrafficGapAheadSeconds)),
            new(LapLayer.DirtyAir, inputs.DirtyAirLevel * PaceConstants.DirtyAirMaxLossSeconds),
            new(LapLayer.Wetness, WetnessSeconds(inputs, baseSeconds)),
            new(LapLayer.Noise, inputs.NoiseDraw * NoiseSigma(inputs.Driver.Consistency)),
        };

        return new LapTimeBreakdown(layers);
    }

    /// <summary>
    /// Base lap: length / (era average speed * character factor). The era speed is a smooth function of the
    /// season; the character factor is the product of the layout's tag factors, clamped.
    /// </summary>
    public static double TrackBaseSeconds(TrackLayout track, int season)
    {
        ArgumentNullException.ThrowIfNull(track);
        var speedKph = EraCurve.Evaluate(PaceConstants.EraAverageSpeedKph, season) * CharacterFactor(track);
        return track.LengthKm / speedKph * 3600d;
    }

    private static double CharacterFactor(TrackLayout track)
    {
        var factor = 1d;
        foreach (var tag in track.CharacterTags)
        {
            if (PaceConstants.CharacterSpeedFactor.TryGetValue(tag, out var tagFactor))
            {
                factor *= tagFactor;
            }
        }

        return Math.Clamp(factor, PaceConstants.CharacterSpeedFactorMin, PaceConstants.CharacterSpeedFactorMax);
    }

    private static double CarFitSeconds(LapInputs inputs, double baseSeconds)
    {
        var car = inputs.Car;
        var downforce = Math.Min(car.Downforce, inputs.Limits.DownforceCap);
        double[] attributes = [car.Power, downforce, car.MechanicalGrip, car.Braking];

        // Iterate the fixed matrix order, not the dictionary: the result must not depend on enumeration order.
        var weighted = 0d;
        var weightSum = 0d;
        foreach (var (key, row) in PaceConstants.CarFitMatrix.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!inputs.Track.ProfileWeights.TryGetValue(key, out var weight) || weight <= 0d)
            {
                continue;
            }

            var score = 0d;
            for (var i = 0; i < attributes.Length; i++)
            {
                score += row[i] * attributes[i];
            }

            weighted += weight * score;
            weightSum += weight;
        }

        // No usable profile: a flat track that rewards every attribute equally.
        var fit = weightSum > 0d ? weighted / weightSum : attributes.Average();
        var sensitivity = EraCurve.Evaluate(PaceConstants.CarFitSensitivity, inputs.Season);
        return -(fit - PaceConstants.RatingReference) * sensitivity * baseSeconds;
    }

    private static double TrafficSeconds(double gapSeconds)
    {
        if (gapSeconds >= PaceConstants.TrafficGapThresholdSeconds)
        {
            return 0d;
        }

        return PaceConstants.TrafficMaxLossSeconds * (1d - (gapSeconds / PaceConstants.TrafficGapThresholdSeconds));
    }

    private static double WetnessSeconds(LapInputs inputs, double baseSeconds)
    {
        var wet = inputs.WetnessLevel;
        var mismatch = wet * inputs.TyreFitMismatch;
        var fraction = (PaceConstants.WetBaseFraction * wet * wet)
            + (PaceConstants.WetMismatchFraction * mismatch * mismatch);

        var composure = inputs.Driver.Composure / PaceConstants.RatingMax;
        var multiplier = PaceConstants.WetComposureMultiplierAtZero
            + ((PaceConstants.WetComposureMultiplierAtMax - PaceConstants.WetComposureMultiplierAtZero) * composure);

        return fraction * multiplier * baseSeconds;
    }

    private static double NoiseSigma(double consistency)
    {
        var t = consistency / PaceConstants.RatingMax;
        return PaceConstants.NoiseSigmaAtZeroConsistency
            + ((PaceConstants.NoiseSigmaAtMaxConsistency - PaceConstants.NoiseSigmaAtZeroConsistency) * t);
    }

    private static void Validate(LapInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs.Track);
        ArgumentNullException.ThrowIfNull(inputs.Limits);

        Positive(inputs.Track.LengthKm, "Track.LengthKm");
        foreach (var (key, weight) in inputs.Track.ProfileWeights)
        {
            Finite(weight, $"Track.ProfileWeights[{key}]");
        }

        Rating(inputs.Car.Power, "Car.Power");
        Rating(inputs.Car.Downforce, "Car.Downforce");
        Rating(inputs.Car.MechanicalGrip, "Car.MechanicalGrip");
        Rating(inputs.Car.Braking, "Car.Braking");
        Rating(inputs.Car.Reliability, "Car.Reliability");
        Rating(inputs.Driver.Pace, "Driver.Pace");
        Rating(inputs.Driver.Consistency, "Driver.Consistency");
        Rating(inputs.Driver.Composure, "Driver.Composure");
        Finite(inputs.Driver.AffinitySeconds, "Driver.AffinitySeconds");
        Finite(inputs.Limits.DownforceCap, "Limits.DownforceCap");
        NotNegative(inputs.FuelMassKg, "FuelMassKg");
        NotNegative(inputs.TyreWearFactor, "TyreWearFactor");
        if (double.IsNaN(inputs.TrafficGapAheadSeconds) || inputs.TrafficGapAheadSeconds < 0d)
        {
            throw Out("TrafficGapAheadSeconds", inputs.TrafficGapAheadSeconds, "a gap in seconds >= 0 (or +infinity)");
        }

        Unit(inputs.DirtyAirLevel, "DirtyAirLevel");
        Unit(inputs.WetnessLevel, "WetnessLevel");
        Unit(inputs.TyreFitMismatch, "TyreFitMismatch");
        Finite(inputs.NoiseDraw, "NoiseDraw");
    }

    private static void Rating(double value, string name)
    {
        if (!double.IsFinite(value) || value < PaceConstants.RatingMin || value > PaceConstants.RatingMax)
        {
            throw Out(name, value, "a rating in 0..100");
        }
    }

    private static void Unit(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0d || value > 1d)
        {
            throw Out(name, value, "a value in 0..1");
        }
    }

    private static void NotNegative(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0d)
        {
            throw Out(name, value, "a finite value >= 0");
        }
    }

    private static void Positive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0d)
        {
            throw Out(name, value, "a finite value > 0");
        }
    }

    private static void Finite(double value, string name)
    {
        if (!double.IsFinite(value))
        {
            throw Out(name, value, "a finite number");
        }
    }

    private static ArgumentOutOfRangeException Out(string name, double value, string expected) =>
        new(name, value, $"Expected {expected}, got {value.ToString("R", CultureInfo.InvariantCulture)}.");
}
