using Paddock.Simulation.Racing.Weather;

namespace Paddock.Tests.Racing.Weather;

public class WeatherForecasterTests
{
    private const int Duration = 120;

    private static IReadOnlyList<TruthWeather> ShowerySet(int count)
    {
        var profile = ClimateProfile.FromBand(ClimateClass.Maritime, RainBand.High, 8, 18, 0.6);
        var list = new List<TruthWeather>();
        for (ulong seed = 0; list.Count < count; seed++)
        {
            var truth = RaceWeather.Generate(seed, 1995, 9, profile, Duration);
            if (truth.IsShowery)
            {
                list.Add(truth);
            }
        }

        return list;
    }

    private static (double Bias, double Rmse, double BiasLimit) Errors(IReadOnlyList<TruthWeather> set, int from, int horizon, double quality)
    {
        double sum = 0;
        double sumSq = 0;
        foreach (var truth in set)
        {
            var step = WeatherForecaster.ForecastFor(truth, from, horizon, quality).Steps[horizon - 1];
            var error = step.RawIntensityEstimate - truth.Samples[from + horizon].RainIntensity;
            sum += error;
            sumSq += error * error;
        }

        var n = set.Count;
        var sigma = WeatherForecaster.Sigma(horizon, quality);
        return (sum / n, Math.Sqrt(sumSq / n), 5 * sigma / Math.Sqrt(n));
    }

    [Theory]
    [InlineData(5, 0.5)]
    [InlineData(30, 0.5)]
    [InlineData(60, 0.1)]
    [InlineData(60, 0.9)]
    public void ForecastsAreUnbiasedOnAverage(int horizon, double quality)
    {
        var set = ShowerySet(2_000);
        var (bias, _, limit) = Errors(set, from: 10, horizon, quality);
        Assert.InRange(bias, -limit, limit);
    }

    [Fact]
    public void ForecastsAreNoisyNotExact()
    {
        var truth = ShowerySet(1)[0];
        var forecast = WeatherForecaster.ForecastFor(truth, 0, 30, 1.0);

        // Even the best forecaster does not reproduce the truth.
        var exact = forecast.Steps.Count(s => s.RawIntensityEstimate == truth.Samples[s.HorizonMinutes].RainIntensity);
        Assert.Equal(0, exact);
        Assert.All(forecast.Steps, s => Assert.True(s.UncertaintySd > 0));
    }

    [Fact]
    public void ForecastErrorGrowsWithTheHorizon()
    {
        var set = ShowerySet(2_000);
        var near = Errors(set, from: 10, horizon: 5, quality: 0.5).Rmse;
        var mid = Errors(set, from: 10, horizon: 30, quality: 0.5).Rmse;
        var far = Errors(set, from: 10, horizon: 90, quality: 0.5).Rmse;

        Assert.True(near < mid, $"{near} < {mid}");
        Assert.True(mid < far, $"{mid} < {far}");
    }

    [Fact]
    public void BetterForecastersAreSharper()
    {
        var set = ShowerySet(2_000);
        var poor = Errors(set, from: 10, horizon: 30, quality: 0.0).Rmse;
        var good = Errors(set, from: 10, horizon: 30, quality: 1.0).Rmse;

        Assert.True(good < poor, $"{good} < {poor}");
    }

    [Fact]
    public void UncertaintyBandNarrowsWithShorterHorizonsAndBetterQuality()
    {
        var truth = ShowerySet(1)[0];
        var forecast = WeatherForecaster.ForecastFor(truth, 0, 60, 0.5);
        for (var i = 1; i < forecast.Steps.Length; i++)
        {
            Assert.True(forecast.Steps[i].UncertaintySd > forecast.Steps[i - 1].UncertaintySd);
        }

        Assert.True(WeatherForecaster.Sigma(20, 1.0) < WeatherForecaster.Sigma(20, 0.0));
        foreach (var step in forecast.Steps)
        {
            Assert.InRange(step.IntensityLow, 0, step.ExpectedRainIntensity);
            Assert.InRange(step.IntensityHigh, step.ExpectedRainIntensity, 1);
            Assert.InRange(step.RainProbability, 0, 1);
        }
    }

    [Fact]
    public void ForecastProbabilityIsInformativeAndSharperUpClose()
    {
        // When the truth is raining hard at the target minute, a short-horizon forecast should be more confident than a long one.
        var set = ShowerySet(2_000);
        double nearSum = 0;
        double farSum = 0;
        var n = 0;
        foreach (var truth in set)
        {
            if (truth.Samples[40].RainIntensity < 0.5 || truth.Samples[100].RainIntensity < 0.5)
            {
                continue;
            }

            n++;
            nearSum += WeatherForecaster.ForecastFor(truth, 39, 1, 0.5).Steps[0].RainProbability;
            farSum += WeatherForecaster.ForecastFor(truth, 0, 100, 0.5).Steps[99].RainProbability;
        }

        Assert.True(n > 10, "Too few heavy-rain races to compare.");
        Assert.True(nearSum / n > 0.95);
        Assert.True(nearSum / n >= farSum / n);
    }

    [Fact]
    public void AskingForAForecastIsPureAndRepeatable()
    {
        var truth = ShowerySet(1)[0];
        var samplesBefore = truth.Samples.ToArray();

        var first = WeatherForecaster.ForecastFor(truth, 15, 40, 0.7);
        var second = WeatherForecaster.ForecastFor(truth, 15, 40, 0.7);

        Assert.Equal(first.Steps.ToArray(), second.Steps.ToArray());
        Assert.Equal(samplesBefore, truth.Samples);
        Assert.NotEqual(first.Steps.ToArray(), WeatherForecaster.ForecastFor(truth, 15, 40, 0.3).Steps.ToArray());
    }

    [Fact]
    public void ForecastIsCutOffAtTheEndOfTheRace()
    {
        var truth = ShowerySet(1)[0];
        var forecast = WeatherForecaster.ForecastFor(truth, Duration - 3, 50, 0.5);
        Assert.Equal(3, forecast.Steps.Length);
        Assert.Equal(1, forecast.Steps[0].HorizonMinutes);
        Assert.Empty(WeatherForecaster.ForecastFor(truth, Duration, 10, 0.5).Steps);
    }

    [Fact]
    public void RejectsBadArguments()
    {
        var truth = ShowerySet(1)[0];
        Assert.Throws<ArgumentOutOfRangeException>(() => WeatherForecaster.ForecastFor(truth, -1, 10, 0.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => WeatherForecaster.ForecastFor(truth, Duration + 1, 10, 0.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => WeatherForecaster.ForecastFor(truth, 0, 0, 0.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => WeatherForecaster.ForecastFor(truth, 0, 10, 1.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => WeatherForecaster.ForecastFor(truth, 0, 10, double.NaN));
    }

    [Fact]
    public void ForecastTypesDoNotCarryTheTruth()
    {
        // INV-003: a forecast is knowledge. Its types must not expose the truth object or its samples.
        foreach (var type in new[] { typeof(WeatherForecast), typeof(ForecastStep) })
        {
            Assert.DoesNotContain(
                type.GetProperties(),
                p => p.PropertyType == typeof(TruthWeather) || p.PropertyType == typeof(WeatherSample));
        }
    }

    [Theory]
    [InlineData(0.0, 0.5)]
    [InlineData(1.0, 0.8413447)]
    [InlineData(-1.0, 0.1586553)]
    [InlineData(2.0, 0.9772499)]
    public void NormalCdfMatchesKnownValues(double x, double expected)
    {
        Assert.Equal(expected, WeatherForecaster.NormalCdf(x), 5);
    }

    // Issue #122: the forecast probability used to ignore climatology, so noisy dry-day estimates read as rain and the pit wall
    // fitted wet tyres on dry days (about 7 percent of 1988 cars, 15 percent of 2012 cars). The pit wall fits wet tyres when this
    // probability reaches Paddock.Simulation.Racing.Pits.PitConstants.WetRaceProbability, so that is the line the test pins.
    [Theory]
    [InlineData(ClimateClass.Maritime, RainBand.High, 0.0)]
    [InlineData(ClimateClass.Maritime, RainBand.High, 0.5)]
    [InlineData(ClimateClass.Maritime, RainBand.High, 1.0)]
    [InlineData(ClimateClass.Mediterranean, RainBand.Low, 0.0)]
    [InlineData(ClimateClass.Mediterranean, RainBand.Low, 1.0)]
    public void OnDryDays_TheForecastRarelyReachesTheWetTyreThreshold(ClimateClass climateClass, RainBand band, double quality)
    {
        var profile = ClimateProfile.FromBand(climateClass, band, 8, 18, 0.6);
        var steps = 0;
        var wet = 0;
        for (ulong seed = 0; seed < 300; seed++)
        {
            var truth = RaceWeather.Generate(seed, 1995, 9, profile, Duration);
            if (truth.HasRain)
            {
                continue;
            }

            foreach (var origin in new[] { 0, 30, 60 })
            {
                foreach (var step in WeatherForecaster.ForecastFor(truth, origin, 60, quality).Steps)
                {
                    steps++;
                    if (step.RainProbability >= Paddock.Simulation.Racing.Pits.PitConstants.WetRaceProbability)
                    {
                        wet++;
                    }
                }
            }
        }

        Assert.True(steps > 10_000, "the sample must be large enough to mean something");
        Assert.True(wet / (double)steps <= 0.02, $"{wet} of {steps} dry-day forecast steps reached the wet threshold.");
    }

    [Fact]
    public void WhenItIsRaining_TheNearForecastStillSaysRain()
    {
        var profile = ClimateProfile.FromBand(ClimateClass.Maritime, RainBand.High, 8, 18, 0.6);
        var heavy = 0;
        var called = 0;
        for (ulong seed = 0; seed < 400; seed++)
        {
            var truth = RaceWeather.Generate(seed, 1995, 9, profile, Duration);
            for (var minute = 1; minute < Duration - 5; minute += 7)
            {
                if (truth.Samples[minute + 1].RainIntensity < 0.4)
                {
                    continue;
                }

                heavy++;
                if (WeatherForecaster.ForecastFor(truth, minute, 5, 0.8).Steps[0].RainProbability >= 0.6)
                {
                    called++;
                }
            }
        }

        Assert.True(heavy > 50);
        Assert.True(called / (double)heavy >= 0.9, $"only {called} of {heavy} heavy-rain minutes were called.");
    }

    [Fact]
    public void RainPosterior_RisesWithTheEstimate_AndFallsWithALowerPrior()
    {
        Assert.True(WeatherForecaster.RainPosterior(0.5, 0.1, 0.1) > WeatherForecaster.RainPosterior(0.1, 0.1, 0.1));
        Assert.True(WeatherForecaster.RainPosterior(0.1, 0.1, 0.3) > WeatherForecaster.RainPosterior(0.1, 0.1, 0.03));
        Assert.InRange(WeatherForecaster.RainPosterior(0.0, 0.05, 0.1), 0, 0.1);
        Assert.InRange(WeatherForecaster.RainPosterior(0.7, 0.05, 0.1), 0.99, 1);
    }
}
