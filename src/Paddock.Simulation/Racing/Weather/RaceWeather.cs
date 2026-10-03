using Paddock.Domain.Random;

namespace Paddock.Simulation.Racing.Weather;

/// <summary>
/// Generates the true weather of a race. Documented process (all numbers ESTIMATES, see <see cref="WeatherConstants"/>):
/// <list type="number">
/// <item>Race level: with probability <see cref="ClimateProfile.RaceRainProbability"/> the race is "showery".
/// A dry race never rains. A showery race gets its first rain onset at a uniformly drawn minute.</item>
/// <item>Rain: from the onset a two-state Markov chain (rain / dry, per minute) runs. Rain ends with probability
/// 1 / meanSpell and restarts with probability 1 / meanDry per minute, both interpolated by the climate's
/// variability. Every spell draws a base intensity (mostly light); the intensity then follows that base with jitter,
/// and moves toward its target at most <see cref="WeatherConstants.IntensityRampPerMinute"/> per minute.</item>
/// <item>Temperature: a race-day base drawn in the climate's range, plus an AR(1) wander, minus rain cooling. Track
/// temperature adds a solar gain (race-day sun level) that rain and a wet surface suppress.</item>
/// <item>Wetness: <see cref="TrackWetnessModel"/>.</item>
/// </list>
/// Randomness comes only from the <see cref="RngStreamName.Weather"/> stream derived for (season, round). The number
/// of draws is fixed by the race length, never by outcomes, and no other stream is touched (INV-004).
/// </summary>
public static class RaceWeather
{
    /// <summary>Weather of one race, from the career master seed. Deterministic per (seed, season, round, profile, duration).</summary>
    public static TruthWeather Generate(ulong masterSeed, int season, int round, ClimateProfile climate, int durationMinutes) =>
        Generate(RngStream.Derive(masterSeed, RngStreamName.Weather, season, round), climate, durationMinutes);

    /// <summary>Weather of one race from an already derived Weather stream snapshot. The snapshot is not advanced.</summary>
    public static TruthWeather Generate(RngStream weatherStream, ClimateProfile climate, int durationMinutes)
    {
        ArgumentNullException.ThrowIfNull(weatherStream);
        ArgumentNullException.ThrowIfNull(climate);
        if (weatherStream.Name != RngStreamName.Weather)
        {
            throw new ArgumentException("Weather must be generated from the Weather stream.", nameof(weatherStream));
        }

        if (durationMinutes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(durationMinutes), "Race must last at least a minute.");
        }

        var rng = weatherStream.DeriveChild("race");

        // Race-level draws, always made.
        var uShowery = rng.NextDouble();
        var uOnset = rng.NextDouble();
        var uTemp = rng.NextDouble();
        var uSun = rng.NextDouble();
        var showery = uShowery < climate.RaceRainProbability;
        var onset = Math.Min((int)(uOnset * durationMinutes), durationMinutes - 1);
        var dayBaseC = climate.AirTempMinC + (uTemp * (climate.AirTempMaxC - climate.AirTempMinC));
        var solarGainC = WeatherConstants.SolarGainMaxC
            * (WeatherConstants.SunLevelMin + ((1 - WeatherConstants.SunLevelMin) * uSun));

        var v = climate.Variability;
        var pStop = 1.0 / Lerp(WeatherConstants.MeanSpellMinutesAtLowVariability, WeatherConstants.MeanSpellMinutesAtHighVariability, v);
        var pStart = 1.0 / Lerp(WeatherConstants.MeanDryMinutesAtLowVariability, WeatherConstants.MeanDryMinutesAtHighVariability, v);
        var wanderSd = WeatherConstants.AirTempWanderSdC * (0.5 + v);
        var rho = WeatherConstants.AirTempAutocorrelation;
        var innovationSd = wanderSd * Math.Sqrt(1 - (rho * rho));

        var samples = new WeatherSample[durationMinutes + 1];
        var raining = false;
        var spellBase = 0.0;
        var intensity = 0.0;
        var wetness = 0.0;
        var wander = 0.0;

        for (var minute = 0; minute <= durationMinutes; minute++)
        {
            // Per-minute draws, always made in the same order.
            var uTrans = rng.NextDouble();
            var uSpell = rng.NextDouble();
            var uJitter = rng.NextDouble();
            var uNormalA = rng.NextDouble();
            var uNormalB = rng.NextDouble();

            var startsSpell = false;
            if (!showery || minute < onset)
            {
                raining = false;
            }
            else if (minute == onset)
            {
                raining = true;
                startsSpell = true;
            }
            else if (raining)
            {
                if (uTrans < pStop)
                {
                    raining = false;
                }
            }
            else if (uTrans < pStart)
            {
                raining = true;
                startsSpell = true;
            }

            if (startsSpell)
            {
                spellBase = WeatherConstants.SpellBaseIntensityMin
                    + ((1 - WeatherConstants.SpellBaseIntensityMin) * uSpell * uSpell);
            }

            var target = raining
                ? Math.Clamp(spellBase + ((uJitter - 0.5) * WeatherConstants.IntensityJitter), WeatherConstants.SpellBaseIntensityMin / 2, 1)
                : 0.0;
            intensity += Math.Clamp(
                target - intensity,
                -WeatherConstants.IntensityRampPerMinute,
                WeatherConstants.IntensityRampPerMinute);

            wander = (rho * wander) + (innovationSd * StandardNormal(uNormalA, uNormalB));
            var airC = dayBaseC + wander - (WeatherConstants.RainAirCoolingC * intensity);
            var trackC = airC
                + (solarGainC
                    * (1 - (WeatherConstants.RainSolarBlock * intensity))
                    * (1 - (WeatherConstants.WetSolarBlock * wetness)))
                - (WeatherConstants.WetEvaporativeCoolingC * wetness);
            wetness = TrackWetnessModel.Step(wetness, intensity, trackC);

            samples[minute] = new WeatherSample(minute, airC, trackC, intensity, wetness);
        }

        return new TruthWeather(weatherStream, climate, showery, onset, samples);
    }

    /// <summary>Box-Muller from two uniforms in [0, 1); the first is shifted off zero so the log is finite.</summary>
    internal static double StandardNormal(double u1, double u2) =>
        Math.Sqrt(-2.0 * Math.Log(1.0 - u1)) * Math.Cos(2.0 * Math.PI * u2);

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);
}
