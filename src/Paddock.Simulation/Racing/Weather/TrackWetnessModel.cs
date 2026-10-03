namespace Paddock.Simulation.Racing.Weather;

/// <summary>How wet the racing line is, for tyre selection (consumed by the tyre and strategy code).</summary>
public enum WetnessBand
{
    Dry,
    Damp,
    Wet,
    Extreme,
}

public static class WetnessBands
{
    /// <summary>Wetness 0..1 to a band. Thresholds are ESTIMATES (<see cref="WeatherConstants"/>).</summary>
    public static WetnessBand From(double wetness) => wetness switch
    {
        < WeatherConstants.DryBelow => WetnessBand.Dry,
        < WeatherConstants.DampBelow => WetnessBand.Damp,
        < WeatherConstants.WetBelow => WetnessBand.Wet,
        _ => WetnessBand.Extreme,
    };
}

/// <summary>
/// Track (racing-line) wetness dynamics, one minute per step. Pure functions, no randomness.
/// <code>
/// wetting  = WettingPerMinute * intensity^WettingIntensityExponent * (1 - wetness)
/// drying   = DryingRatePerMinute(trackTemp) * (1 - intensity)        // rain halts drying
/// wetness' = clamp(wetness + wetting - drying, 0, 1)
/// DryingRatePerMinute(T) = BaseOffLineDryingPerMinute * RacingLineDryingFactor
///                          * max(DryingTempFactorMin, 1 + DryingTempSensitivityPerC * (T - DryingReferenceTrackTempC))
/// </code>
/// Without rain the wetness never increases. Puddles are out of scope. All constants are ESTIMATES.
/// </summary>
public static class TrackWetnessModel
{
    /// <summary>Wetness lost per minute with no rain on the racing line at the given track temperature.</summary>
    public static double DryingRatePerMinute(double trackTempC)
    {
        var tempFactor = Math.Max(
            WeatherConstants.DryingTempFactorMin,
            1 + (WeatherConstants.DryingTempSensitivityPerC * (trackTempC - WeatherConstants.DryingReferenceTrackTempC)));
        return WeatherConstants.BaseOffLineDryingPerMinute * WeatherConstants.RacingLineDryingFactor * tempFactor;
    }

    /// <summary>One-minute step of the wetness. <paramref name="rainIntensity"/> is clamped to [0, 1].</summary>
    public static double Step(double wetness, double rainIntensity, double trackTempC)
    {
        var intensity = Math.Clamp(rainIntensity, 0, 1);
        var wetting = WeatherConstants.WettingPerMinute
            * Math.Pow(intensity, WeatherConstants.WettingIntensityExponent)
            * (1 - wetness);
        var drying = DryingRatePerMinute(trackTempC) * (1 - intensity);
        return Math.Clamp(wetness + wetting - drying, 0, 1);
    }
}
