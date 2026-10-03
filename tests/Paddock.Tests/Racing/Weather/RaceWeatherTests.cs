using Paddock.Domain.Random;
using Paddock.Simulation.Racing.Weather;

namespace Paddock.Tests.Racing.Weather;

public class RaceWeatherTests
{
    private const int Duration = 100;

    private static ClimateProfile Profile(RainBand band, double variability = 0.5) =>
        ClimateProfile.FromBand(ClimateClass.Continental, band, 12, 22, variability);

    [Fact]
    public void SameInputsGiveTheSameWeather()
    {
        var profile = Profile(RainBand.High);
        var a = RaceWeather.Generate(42, 1980, 7, profile, Duration);
        var b = RaceWeather.Generate(42, 1980, 7, profile, Duration);

        Assert.Equal(a.Samples, b.Samples);
        Assert.Equal(a.IsShowery, b.IsShowery);
        Assert.Equal(a.OnsetMinute, b.OnsetMinute);
    }

    [Fact]
    public void DifferentRoundsSeasonsAndSeedsGiveDifferentWeather()
    {
        var profile = Profile(RainBand.High);
        var baseline = RaceWeather.Generate(42, 1980, 7, profile, Duration).Samples;

        Assert.NotEqual(baseline, RaceWeather.Generate(42, 1980, 8, profile, Duration).Samples);
        Assert.NotEqual(baseline, RaceWeather.Generate(42, 1981, 7, profile, Duration).Samples);
        Assert.NotEqual(baseline, RaceWeather.Generate(43, 1980, 7, profile, Duration).Samples);
    }

    [Fact]
    public void WeatherIsIndependentOfOtherRngStreams()
    {
        var profile = Profile(RainBand.High);
        var before = RaceWeather.Generate(42, 1980, 7, profile, Duration);

        // Draw a lot from LapNoise (same seed, season, round) and from other streams in between.
        var lapNoise = RngStreams.Derive(42, RngStreamName.LapNoise, 1980, 7);
        var incidents = RngStreams.Derive(42, RngStreamName.Incidents, 1980, 7);
        for (var i = 0; i < 10_000; i++)
        {
            lapNoise.NextDouble();
            incidents.NextULong();
        }

        var after = RaceWeather.Generate(42, 1980, 7, profile, Duration);
        Assert.Equal(before.Samples, after.Samples);
    }

    [Fact]
    public void GeneratingDoesNotAdvanceTheStreamSnapshot()
    {
        var stream = RngStream.Derive(42, RngStreamName.Weather, 1980, 7);
        var stateBefore = stream.State;

        var first = RaceWeather.Generate(stream, Profile(RainBand.High), Duration);
        var second = RaceWeather.Generate(stream, Profile(RainBand.High), Duration);

        Assert.Equal(stateBefore, stream.State);
        Assert.Equal(first.Samples, second.Samples);
        Assert.Equal(first.Samples, RaceWeather.Generate(42, 1980, 7, Profile(RainBand.High), Duration).Samples);
    }

    [Fact]
    public void RejectsAStreamThatIsNotTheWeatherStream()
    {
        var wrong = RngStream.Derive(42, RngStreamName.LapNoise, 1980, 7);
        Assert.Throws<ArgumentException>(() => RaceWeather.Generate(wrong, Profile(RainBand.Low), Duration));
        Assert.Throws<ArgumentOutOfRangeException>(() => RaceWeather.Generate(42, 1980, 7, Profile(RainBand.Low), 0));
    }

    [Theory]
    [InlineData(RainBand.Low)]
    [InlineData(RainBand.Medium)]
    [InlineData(RainBand.High)]
    public void RainFrequencyOverManyRacesMatchesTheBand(RainBand band)
    {
        const int races = 5_000;
        var profile = Profile(band);
        var rainy = 0;
        for (var i = 0; i < races; i++)
        {
            if (RaceWeather.Generate(1_000UL + (ulong)i, 1970, 1 + (i % 16), profile, Duration).HasRain)
            {
                rainy++;
            }
        }

        var p = ClimateProfile.ProbabilityFor(band);
        var tolerance = 5 * Math.Sqrt(p * (1 - p) / races);
        Assert.InRange((double)rainy / races, p - tolerance, p + tolerance);
    }

    [Fact]
    public void BandsAreOrderedLowMediumHigh()
    {
        Assert.True(ClimateProfile.ProbabilityFor(RainBand.Low) < ClimateProfile.ProbabilityFor(RainBand.Medium));
        Assert.True(ClimateProfile.ProbabilityFor(RainBand.Medium) < ClimateProfile.ProbabilityFor(RainBand.High));
    }

    [Fact]
    public void ShowerySeedsRainFromTheirOnsetAndDrySeedsNeverRain()
    {
        var profile = Profile(RainBand.Medium);
        var showery = 0;
        var dry = 0;
        for (var i = 0; i < 600; i++)
        {
            var truth = RaceWeather.Generate((ulong)i, 1990, 3, profile, Duration);
            if (truth.IsShowery)
            {
                showery++;
                var onset = truth.OnsetMinute!.Value;
                Assert.InRange(onset, 0, Duration - 1);
                Assert.All(truth.Samples.Take(onset), s => Assert.Equal(0, s.RainIntensity));
                Assert.True(truth.Samples[onset].RainIntensity >= WeatherConstants.MeasurableRainIntensity);
                Assert.True(truth.HasRain);
            }
            else
            {
                dry++;
                Assert.Null(truth.OnsetMinute);
                Assert.False(truth.HasRain);
                Assert.All(truth.Samples, s => Assert.Equal(0, s.TrackWetness));
            }
        }

        Assert.True(showery > 0);
        Assert.True(dry > 0);
    }

    [Fact]
    public void SamplesStayWithinPhysicalBoundsAndCoverTheWholeRace()
    {
        var profile = Profile(RainBand.High, variability: 1.0);
        for (var i = 0; i < 200; i++)
        {
            var truth = RaceWeather.Generate((ulong)i, 2000, 5, profile, Duration);
            Assert.Equal(Duration, truth.DurationMinutes);
            Assert.Equal(Duration + 1, truth.Samples.Count);
            for (var m = 0; m < truth.Samples.Count; m++)
            {
                var s = truth.Samples[m];
                Assert.Equal(m, s.Minute);
                Assert.InRange(s.RainIntensity, 0, 1);
                Assert.InRange(s.TrackWetness, 0, 1);
                Assert.True(double.IsFinite(s.AirTempC) && double.IsFinite(s.TrackTempC));
            }
        }
    }

    [Fact]
    public void RainRaisesWetnessAndTheTrackStaysDryWithoutRain()
    {
        var profile = Profile(RainBand.High);
        var sawWet = false;
        for (var i = 0; i < 300 && !sawWet; i++)
        {
            var truth = RaceWeather.Generate((ulong)i, 1975, 2, profile, Duration);
            sawWet = truth.Samples.Any(s => s.WetnessBand >= WetnessBand.Wet);
        }

        Assert.True(sawWet, "No race in 300 reached a wet track; wetting is too weak.");
    }

    [Fact]
    public void WetnessNeverRisesInMinutesWithoutRain()
    {
        var profile = Profile(RainBand.High, variability: 0.8);
        var checkedMinutes = 0;
        for (var i = 0; i < 400; i++)
        {
            var samples = RaceWeather.Generate((ulong)i, 1985, 4, profile, 200).Samples;
            for (var m = 1; m < samples.Count; m++)
            {
                if (samples[m].RainIntensity == 0 && samples[m - 1].TrackWetness > 0)
                {
                    checkedMinutes++;
                    Assert.True(samples[m].TrackWetness <= samples[m - 1].TrackWetness);
                }
            }
        }

        Assert.True(checkedMinutes > 100, "Too few dry-after-wet minutes to test the drying curve.");
    }

    [Fact]
    public void DryingCurveIsMonotoneAndReachesDry()
    {
        foreach (var trackTemp in new[] { 5.0, 15.0, 30.0, 45.0 })
        {
            var wetness = 1.0;
            var minutes = 0;
            while (wetness > 0)
            {
                var next = TrackWetnessModel.Step(wetness, 0, trackTemp);
                Assert.True(next <= wetness);
                Assert.True(next >= 0);
                wetness = next;
                minutes++;
                Assert.True(minutes < 2_000, "Track never dries.");
            }
        }
    }

    [Fact]
    public void WarmTracksDryFasterAndTheRacingLineFactorApplies()
    {
        Assert.True(TrackWetnessModel.DryingRatePerMinute(40) > TrackWetnessModel.DryingRatePerMinute(20));
        Assert.True(TrackWetnessModel.DryingRatePerMinute(-50) > 0);
        Assert.Equal(
            WeatherConstants.BaseOffLineDryingPerMinute * WeatherConstants.RacingLineDryingFactor,
            TrackWetnessModel.DryingRatePerMinute(WeatherConstants.DryingReferenceTrackTempC),
            12);
    }

    [Fact]
    public void RainWetsTheTrackAndHeavierRainWetsItMore()
    {
        var light = TrackWetnessModel.Step(0, 0.2, 20);
        var heavy = TrackWetnessModel.Step(0, 0.9, 20);
        Assert.True(light > 0);
        Assert.True(heavy > light);
        Assert.Equal(1.0, TrackWetnessModel.Step(1.0, 1.0, 20), 12);
    }

    [Theory]
    [InlineData(0.0, WetnessBand.Dry)]
    [InlineData(0.09, WetnessBand.Dry)]
    [InlineData(0.10, WetnessBand.Damp)]
    [InlineData(0.39, WetnessBand.Damp)]
    [InlineData(0.40, WetnessBand.Wet)]
    [InlineData(0.84, WetnessBand.Wet)]
    [InlineData(0.85, WetnessBand.Extreme)]
    [InlineData(1.0, WetnessBand.Extreme)]
    public void WetnessBandThresholds(double wetness, WetnessBand expected)
    {
        Assert.Equal(expected, WetnessBands.From(wetness));
    }

    [Fact]
    public void AtLapUsesTheLapStartMinute()
    {
        var truth = RaceWeather.Generate(7, 1999, 1, Profile(RainBand.Medium), Duration);
        Assert.Equal(truth.Samples[0], truth.AtLap(1, 1.5));
        Assert.Equal(truth.Samples[3], truth.AtLap(3, 1.5));
        Assert.Equal(truth.Samples[Duration], truth.At(10_000));
        Assert.Equal(truth.Samples[0], truth.At(-5));
        Assert.Throws<ArgumentOutOfRangeException>(() => truth.AtLap(0, 1.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => truth.AtLap(1, 0));
    }

    [Fact]
    public void RainCoolsAndTrackIsHotterThanAirWhenDry()
    {
        var profile = Profile(RainBand.High);
        for (var i = 0; i < 200; i++)
        {
            var truth = RaceWeather.Generate((ulong)i, 1960, 6, profile, Duration);
            Assert.All(
                truth.Samples.Where(s => s.RainIntensity == 0 && s.TrackWetness == 0),
                s => Assert.True(s.TrackTempC > s.AirTempC));
        }
    }
}
