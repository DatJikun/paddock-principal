namespace Paddock.Simulation.Racing.Weather;

/// <summary>
/// Every tunable number of the weather generator, in one place. ALL VALUES ARE ESTIMATES (guesses by the
/// author, not calibrated against measured data) until the R9 climate data and a calibration pass replace
/// them (TECH, "Numbers are estimates until calibrated").
/// </summary>
public static class WeatherConstants
{
    // ---- Climate: rain bands (ESTIMATE) -------------------------------------------------------------------
    /// <summary>ESTIMATE: probability that a race sees measurable rain at some point, band Low.</summary>
    public const double RaceRainProbabilityLow = 0.08;

    /// <summary>ESTIMATE: probability that a race sees measurable rain at some point, band Medium.</summary>
    public const double RaceRainProbabilityMedium = 0.20;

    /// <summary>ESTIMATE: probability that a race sees measurable rain at some point, band High.</summary>
    public const double RaceRainProbabilityHigh = 0.35;

    // ---- Climate: classes (ESTIMATE) ----------------------------------------------------------------------
    // Annual mean air temperature (C), seasonal half-amplitude (C), half-width of the race-day range (C), variability 0..1.
    public const double MaritimeMeanC = 11, MaritimeAmplitudeC = 8, MaritimeHalfRangeC = 4, MaritimeVariability = 0.8;
    public const double ContinentalMeanC = 12, ContinentalAmplitudeC = 13, ContinentalHalfRangeC = 5, ContinentalVariability = 0.6;
    public const double MediterraneanMeanC = 17, MediterraneanAmplitudeC = 9, MediterraneanHalfRangeC = 4, MediterraneanVariability = 0.4;
    public const double DesertMeanC = 27, DesertAmplitudeC = 10, DesertHalfRangeC = 4, DesertVariability = 0.2;
    public const double TropicalMeanC = 27, TropicalAmplitudeC = 2, TropicalHalfRangeC = 3, TropicalVariability = 0.7;

    /// <summary>ESTIMATE: month of the seasonal temperature peak in the northern hemisphere (July); southern is shifted by 6.</summary>
    public const int NorthernPeakMonth = 7;

    // ---- Rain process (ESTIMATE) --------------------------------------------------------------------------
    /// <summary>ESTIMATE: rain intensity at or above this counts as "rain" (HasRain, forecast probability threshold).</summary>
    public const double MeasurableRainIntensity = 0.05;

    /// <summary>ESTIMATE: mean length of a rain spell in minutes at variability 0 (steady) and 1 (changeable).</summary>
    public const double MeanSpellMinutesAtLowVariability = 40, MeanSpellMinutesAtHighVariability = 12;

    /// <summary>ESTIMATE: mean length of a dry gap between spells in minutes at variability 0 and 1.</summary>
    public const double MeanDryMinutesAtLowVariability = 50, MeanDryMinutesAtHighVariability = 15;

    /// <summary>ESTIMATE: lowest spell base intensity; the base is MIN + (1 - MIN) * u^2, so most spells are light.</summary>
    public const double SpellBaseIntensityMin = 0.10;

    /// <summary>ESTIMATE: width of the per-minute jitter around the spell base intensity.</summary>
    public const double IntensityJitter = 0.20;

    /// <summary>ESTIMATE: largest change of RainIntensity per minute (rain builds and fades, it does not jump).</summary>
    public const double IntensityRampPerMinute = 0.25;

    // ---- Temperatures (ESTIMATE) --------------------------------------------------------------------------
    /// <summary>ESTIMATE: lag-one autocorrelation of the minute-to-minute air temperature wander.</summary>
    public const double AirTempAutocorrelation = 0.97;

    /// <summary>ESTIMATE: stationary standard deviation of the air temperature wander at variability 0.5 (C).</summary>
    public const double AirTempWanderSdC = 0.6;

    /// <summary>ESTIMATE: air cooling at full rain intensity (C).</summary>
    public const double RainAirCoolingC = 3.0;

    /// <summary>ESTIMATE: lowest daily sun level (cloudiness); the race-day sun is uniform in [MIN, 1].</summary>
    public const double SunLevelMin = 0.3;

    /// <summary>ESTIMATE: track temperature above air temperature on a fully sunny dry day (C).</summary>
    public const double SolarGainMaxC = 14.0;

    /// <summary>ESTIMATE: share of the solar gain blocked at full rain intensity.</summary>
    public const double RainSolarBlock = 0.85;

    /// <summary>ESTIMATE: share of the solar gain lost on a fully wet track (wet tarmac, evaporation).</summary>
    public const double WetSolarBlock = 0.6;

    /// <summary>ESTIMATE: evaporative cooling of the track at wetness 1 (C).</summary>
    public const double WetEvaporativeCoolingC = 3.0;

    // ---- Track wetness (ESTIMATE) -------------------------------------------------------------------------
    /// <summary>ESTIMATE: wetness gained per minute at rain intensity 1 on a bone-dry track (scaled by intensity^exponent and by 1 - wetness).</summary>
    public const double WettingPerMinute = 0.25;

    /// <summary>ESTIMATE: exponent applied to rain intensity when wetting (drizzle wets disproportionately little).</summary>
    public const double WettingIntensityExponent = 1.5;

    /// <summary>ESTIMATE: base drying (wetness per minute) of the off-line track at the reference temperature.</summary>
    public const double BaseOffLineDryingPerMinute = 0.008;

    /// <summary>
    /// ESTIMATE: the racing line dries this many times faster than the rest of the track (rubber, heat, cars pushing
    /// water away). The modelled wetness is the wetness of the racing line, which is what tyres feel.
    /// </summary>
    public const double RacingLineDryingFactor = 1.5;

    /// <summary>ESTIMATE: track temperature at which drying runs at its base rate (C).</summary>
    public const double DryingReferenceTrackTempC = 30.0;

    /// <summary>ESTIMATE: relative change of the drying rate per degree of track temperature.</summary>
    public const double DryingTempSensitivityPerC = 0.04;

    /// <summary>ESTIMATE: floor of the temperature factor, so a cold track still dries eventually.</summary>
    public const double DryingTempFactorMin = 0.25;

    // ---- Wetness bands (ESTIMATE) -------------------------------------------------------------------------
    public const double DryBelow = 0.10;
    public const double DampBelow = 0.40;
    public const double WetBelow = 0.85;

    // ---- Forecast (ESTIMATE) ------------------------------------------------------------------------------
    /// <summary>ESTIMATE: forecast error sd at horizon 0 minutes, forecaster scale 1 (rain intensity units).</summary>
    public const double ForecastSigmaFloor = 0.03;

    /// <summary>ESTIMATE: growth of the forecast error sd with the square root of the horizon in minutes.</summary>
    public const double ForecastSigmaPerSqrtMinute = 0.04;

    /// <summary>ESTIMATE: error scale of a worst (quality 0) forecaster.</summary>
    public const double PoorForecasterScale = 1.6;

    /// <summary>ESTIMATE: error scale of a best (quality 1) forecaster.</summary>
    public const double GoodForecasterScale = 0.5;

    /// <summary>ESTIMATE: the uncertainty band is the noisy estimate +- this many sigmas (about a 90 per cent band).</summary>
    public const double ForecastBandSigmas = 1.645;
}
