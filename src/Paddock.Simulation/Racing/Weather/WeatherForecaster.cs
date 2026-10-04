using System.Collections.Immutable;
using System.Globalization;

namespace Paddock.Simulation.Racing.Weather;

/// <summary>
/// One forecast lead time. This is KNOWLEDGE (INV-003): a noisy view of the truth, never the truth itself.
/// </summary>
/// <param name="HorizonMinutes">Minutes ahead of the forecast origin.</param>
/// <param name="RainProbability">Probability that rain (intensity at or above the measurable threshold) is falling then, 0..1.</param>
/// <param name="ExpectedRainIntensity">Best guess of the rain intensity then, clamped to 0..1.</param>
/// <param name="IntensityLow">Lower end of the uncertainty band, clamped to 0..1.</param>
/// <param name="IntensityHigh">Upper end of the uncertainty band, clamped to 0..1.</param>
/// <param name="UncertaintySd">Standard deviation of the forecast error at this horizon; grows with the horizon.</param>
/// <param name="RawIntensityEstimate">The unclamped estimate (truth plus zero-mean noise). Mean-unbiased by construction;
/// for tests and calibration, UI and AI should use <see cref="ExpectedRainIntensity"/>.</param>
public sealed record ForecastStep(
    int HorizonMinutes,
    double RainProbability,
    double ExpectedRainIntensity,
    double IntensityLow,
    double IntensityHigh,
    double UncertaintySd,
    double RawIntensityEstimate);

/// <summary>A forecast issued at <see cref="FromMinute"/>: one step per minute of horizon, up to the end of the race.</summary>
public sealed record WeatherForecast(int FromMinute, double ForecasterQuality, ImmutableArray<ForecastStep> Steps);

/// <summary>
/// Builds forecasts from the truth with an error that grows with the horizon and shrinks with the forecaster's quality.
/// <code>
/// sigma(h, q) = (ForecastSigmaFloor + ForecastSigmaPerSqrtMinute * sqrt(h)) * lerp(PoorForecasterScale, GoodForecasterScale, q)
/// raw         = trueIntensity(from + h) + sigma * z            // z ~ N(0, 1): zero-mean, so unbiased
/// P(rain)     = posterior of "rain" given raw, with the climatological prior (see <see cref="RainPosterior"/>)
/// band        = clamp(raw +- ForecastBandSigmas * sigma, 0, 1)
/// </code>
/// The noise z is hashed from the weather stream snapshot plus (origin, horizon, quality) with
/// <c>DeriveChild</c>: asking for a forecast consumes no RNG and changes no state (INV-005), and asking twice gives the
/// same answer. Forecasts issued at different origins have independent noise, so consecutive forecasts jitter (a
/// known simplification). ALL CONSTANTS ARE ESTIMATES.
/// </summary>
public static class WeatherForecaster
{
    /// <summary>Forecast error standard deviation for a lead time and forecaster quality (0 worst, 1 best).</summary>
    public static double Sigma(int horizonMinutes, double forecasterQuality)
    {
        if (horizonMinutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(horizonMinutes), "Must not be negative.");
        }

        var q = CheckQuality(forecasterQuality);
        var scale = WeatherConstants.PoorForecasterScale
            + ((WeatherConstants.GoodForecasterScale - WeatherConstants.PoorForecasterScale) * q);
        return (WeatherConstants.ForecastSigmaFloor + (WeatherConstants.ForecastSigmaPerSqrtMinute * Math.Sqrt(horizonMinutes))) * scale;
    }

    /// <summary>
    /// Forecast for minutes <c>fromMinute + 1 .. fromMinute + horizonMinutes</c>, cut off at the end of the race.
    /// </summary>
    public static WeatherForecast ForecastFor(TruthWeather truth, int fromMinute, int horizonMinutes, double forecasterQuality)
    {
        ArgumentNullException.ThrowIfNull(truth);
        if (fromMinute < 0 || fromMinute > truth.DurationMinutes)
        {
            throw new ArgumentOutOfRangeException(nameof(fromMinute), "Origin must lie within the race.");
        }

        if (horizonMinutes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(horizonMinutes), "Horizon must be at least a minute.");
        }

        var q = CheckQuality(forecasterQuality);
        var prior = truth.Climate.RaceRainProbability * WeatherConstants.RainMinuteShareGivenRain;
        var qualityKey = q.ToString("R", CultureInfo.InvariantCulture);
        var last = Math.Min(horizonMinutes, truth.DurationMinutes - fromMinute);
        var steps = ImmutableArray.CreateBuilder<ForecastStep>(last);
        for (var h = 1; h <= last; h++)
        {
            var sigma = Sigma(h, q);
            var rng = truth.Stream.DeriveChild(
                string.Create(CultureInfo.InvariantCulture, $"forecast|q={qualityKey}|from={fromMinute}|h={h}"));
            var z = RaceWeather.StandardNormal(rng.NextDouble(), rng.NextDouble());
            var raw = truth.Samples[fromMinute + h].RainIntensity + (sigma * z);
            var probability = RainPosterior(raw, sigma, prior);
            var band = WeatherConstants.ForecastBandSigmas * sigma;
            steps.Add(new ForecastStep(
                h,
                probability,
                Math.Clamp(raw, 0, 1),
                Math.Clamp(raw - band, 0, 1),
                Math.Clamp(raw + band, 0, 1),
                sigma,
                raw));
        }

        return new WeatherForecast(fromMinute, q, steps.MoveToImmutable());
    }

    /// <summary>
    /// P(rain falling | noisy estimate), with a prior from the climate: a minute is dry (intensity exactly 0) with probability
    /// <c>1 - prior</c>, and rainy with probability <c>prior</c>, in which case the intensity is uniform between the measurable
    /// threshold and 1. Bayes: <c>prior * L1 / (prior * L1 + (1 - prior) * L0)</c>, where L0 is the density of the estimate
    /// around 0 and L1 its density around a uniform intensity. A flat prior (the old <c>Phi((raw - threshold) / sigma)</c>) read
    /// every noisy dry-day estimate as likely rain, so the pit wall fitted wet tyres on dry days (issue #122). ESTIMATE
    /// (the prior comes from <see cref="WeatherConstants.RainMinuteShareGivenRain"/> and the climate).
    /// </summary>
    public static double RainPosterior(double rawEstimate, double sigma, double prior)
    {
        var threshold = WeatherConstants.MeasurableRainIntensity;
        var dry = NormalPdf(rawEstimate / sigma) / sigma;
        var rainy = (NormalCdf((rawEstimate - threshold) / sigma) - NormalCdf((rawEstimate - 1.0) / sigma)) / (1.0 - threshold);
        var numerator = prior * rainy;
        var denominator = numerator + ((1.0 - prior) * dry);
        return denominator <= 0 ? (rawEstimate >= threshold ? 1.0 : 0.0) : Math.Clamp(numerator / denominator, 0.0, 1.0);
    }

    private static double NormalPdf(double x) => Math.Exp(-0.5 * x * x) / Math.Sqrt(2.0 * Math.PI);

    private static double CheckQuality(double quality)
    {
        if (double.IsNaN(quality) || quality is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quality), "Forecaster quality must be in [0, 1].");
        }

        return quality;
    }

    /// <summary>Standard normal CDF via the Abramowitz-Stegun 7.1.26 erf approximation (abs. error below 1.5e-7).</summary>
    public static double NormalCdf(double x)
    {
        var t = 1.0 / (1.0 + (0.3275911 * Math.Abs(x) / Math.Sqrt(2.0)));
        var poly = t * (0.254829592 + (t * (-0.284496736 + (t * (1.421413741 + (t * (-1.453152027 + (t * 1.061405429))))))));
        var erf = 1.0 - (poly * Math.Exp(-x * x / 2.0));
        return x >= 0 ? 0.5 * (1.0 + erf) : 0.5 * (1.0 - erf);
    }
}
