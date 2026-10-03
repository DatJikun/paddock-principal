using Paddock.Domain.World;
using Paddock.Simulation.Racing.Pace;

namespace Paddock.Tests.Racing.Pace;

/// <summary>
/// The layout below is a synthetic fixture (not a real circuit). All expected ranges are ESTIMATES that follow the
/// uncalibrated constants in <see cref="PaceConstants"/>; they guard plausibility and direction, not real data.
/// </summary>
public class LapTimeModelTests
{
    private static readonly IReadOnlyDictionary<string, double> BalancedProfile = new Dictionary<string, double>
    {
        ["straights"] = 0.25,
        ["high_speed"] = 0.25,
        ["low_speed"] = 0.25,
        ["braking"] = 0.25,
    };

    private static TrackLayout Layout(
        double lengthKm = 6.0,
        IReadOnlyDictionary<string, double>? profile = null,
        params string[] tags) =>
        new("synthetic_layout", "synthetic_circuit", lengthKm, profile ?? BalancedProfile, tags);

    private static CarPerformance Car(double power = 50, double downforce = 50, double grip = 50, double braking = 50) =>
        new(power, downforce, grip, braking, 50);

    private static LapInputs Inputs(
        int season = 1965,
        TrackLayout? track = null,
        CarPerformance? car = null,
        DriverPace? driver = null,
        EraPerformanceLimits? limits = null) =>
        new()
        {
            Track = track ?? Layout(),
            Season = season,
            Limits = limits ?? new EraPerformanceLimits(100),
            Car = car ?? Car(),
            Driver = driver ?? new DriverPace(50, 50, 50),
        };

    [Fact]
    public void MidSixtiesSixKmCircuit_ReferenceCarAndDriver_IsInAPlausibleRange()
    {
        // Synthetic ~6.0 km layout, season 1965, reference (50) car and driver, no fuel, clean air, dry, no noise.
        // ESTIMATE: 175 km/h average -> 6.0 / 175 * 3600 = 123.4 s. Plausible band for the fixture: 105..140 s.
        var lap = LapTimeModel.Compute(Inputs()).TotalSeconds;

        Assert.InRange(lap, 105d, 140d);
        Assert.Equal(6.0 / 175.0 * 3600.0, lap, 6);
    }

    [Fact]
    public void HigherDriverPace_IsFaster()
    {
        var slow = LapTimeModel.Compute(Inputs(driver: new DriverPace(50, 50, 50))).TotalSeconds;
        var fast = LapTimeModel.Compute(Inputs(driver: new DriverPace(51, 50, 50))).TotalSeconds;

        Assert.True(fast < slow);
    }

    [Fact]
    public void MoreFuel_IsSlower()
    {
        var light = Inputs() with { FuelMassKg = 20 };
        var heavy = Inputs() with { FuelMassKg = 80 };

        Assert.True(LapTimeModel.Compute(heavy).TotalSeconds > LapTimeModel.Compute(light).TotalSeconds);
        Assert.Equal(60 * PaceConstants.FuelSecondsPerKg, LapTimeModel.Compute(heavy).TotalSeconds - LapTimeModel.Compute(light).TotalSeconds, 9);
    }

    [Fact]
    public void DownforceAboveTheEraCap_GivesNoGain()
    {
        var limits = new EraPerformanceLimits(70);
        var atCap = LapTimeModel.Compute(Inputs(car: Car(downforce: 70), limits: limits)).TotalSeconds;
        var aboveCap = LapTimeModel.Compute(Inputs(car: Car(downforce: 100), limits: limits)).TotalSeconds;
        var belowCap = LapTimeModel.Compute(Inputs(car: Car(downforce: 60), limits: limits)).TotalSeconds;

        Assert.Equal(atCap, aboveCap);
        Assert.True(atCap < belowCap, "downforce below the cap still pays off");
    }

    [Fact]
    public void LayerSum_EqualsTotal_InSummationOrder()
    {
        var inputs = Inputs(
            track: Layout(tags: ["technical", "street"]),
            car: Car(power: 70, downforce: 60, grip: 40, braking: 55),
            driver: new DriverPace(65, 40, 30, 0.1)) with
        {
            FuelMassKg = 55,
            TyreWearFactor = 0.8,
            TrafficGapAheadSeconds = 0.5,
            DirtyAirLevel = 0.6,
            WetnessLevel = 0.4,
            TyreFitMismatch = 0.5,
            NoiseDraw = -0.7,
        };

        var breakdown = LapTimeModel.Compute(inputs);

        Assert.Equal(Enum.GetValues<LapLayer>(), breakdown.Layers.Select(layer => layer.Layer).ToArray());
        var sum = 0d;
        foreach (var layer in breakdown.Layers)
        {
            sum += layer.Seconds;
        }

        Assert.Equal(sum, breakdown.TotalSeconds);
        Assert.Equal(breakdown.Seconds(LapLayer.TrackBase), LapTimeModel.TrackBaseSeconds(inputs.Track, inputs.Season));
    }

    [Fact]
    public void BaseLapTime_SameLayout_DecreasesWithEraSpeed_1955_1988_2020()
    {
        var track = Layout();
        var b1955 = LapTimeModel.TrackBaseSeconds(track, 1955);
        var b1988 = LapTimeModel.TrackBaseSeconds(track, 1988);
        var b2020 = LapTimeModel.TrackBaseSeconds(track, 2020);

        Assert.True(b1955 > b1988);
        Assert.True(b1988 > b2020);
    }

    [Fact]
    public void BaseLapTime_NeverIncreasesFromYearToYear_AndHasNoJumps()
    {
        var track = Layout();
        var previous = LapTimeModel.TrackBaseSeconds(track, 1945);
        for (var season = 1946; season <= 2035; season++)
        {
            var current = LapTimeModel.TrackBaseSeconds(track, season);
            Assert.True(current <= previous + 1e-12, $"base lap increased in {season}");
            Assert.True(previous - current < 2.5, $"base lap jumped by {previous - current:0.00} s in {season}");
            previous = current;
        }
    }

    [Fact]
    public void BaseLap_UsesLengthAndCharacter()
    {
        var plain = LapTimeModel.TrackBaseSeconds(Layout(6.0), 1980);
        var longer = LapTimeModel.TrackBaseSeconds(Layout(12.0), 1980);
        var street = LapTimeModel.TrackBaseSeconds(Layout(6.0, tags: "street"), 1980);
        var fast = LapTimeModel.TrackBaseSeconds(Layout(6.0, tags: "high_speed"), 1980);
        var unknownTag = LapTimeModel.TrackBaseSeconds(Layout(6.0, tags: "no_such_tag"), 1980);

        Assert.Equal(plain * 2, longer, 9);
        Assert.True(street > plain);
        Assert.True(fast < plain);
        Assert.Equal(plain, unknownTag);
    }

    [Fact]
    public void CarFit_PowerHelpsMostOnStraights_DownforceHelpsMostOnHighSpeed()
    {
        var straights = Layout(profile: new Dictionary<string, double> { ["straights"] = 1.0 });
        var highSpeed = Layout(profile: new Dictionary<string, double> { ["high_speed"] = 1.0 });

        double Gain(TrackLayout track, CarPerformance better)
        {
            var reference = LapTimeModel.Compute(Inputs(1990, track, Car())).Seconds(LapLayer.CarFit);
            return reference - LapTimeModel.Compute(Inputs(1990, track, better)).Seconds(LapLayer.CarFit);
        }

        Assert.True(Gain(straights, Car(power: 80)) > Gain(highSpeed, Car(power: 80)));
        Assert.True(Gain(highSpeed, Car(downforce: 80)) > Gain(straights, Car(downforce: 80)));
    }

    [Fact]
    public void CarFit_ReferenceCar_AddsNothing_AndReliabilityNeverAffectsPace()
    {
        var reference = LapTimeModel.Compute(Inputs());
        var reliable = LapTimeModel.Compute(Inputs(car: new CarPerformance(50, 50, 50, 50, 100)));

        Assert.Equal(0d, reference.Seconds(LapLayer.CarFit), 12);
        Assert.Equal(reference.TotalSeconds, reliable.TotalSeconds);
    }

    [Fact]
    public void CarFit_SensitivityIsEraDependent_EarlyCarsDifferMore()
    {
        double Spread(int season)
        {
            var good = LapTimeModel.Compute(Inputs(season, car: Car(80, 80, 80, 80))).Seconds(LapLayer.CarFit);
            return good / LapTimeModel.TrackBaseSeconds(Layout(), season);
        }

        Assert.True(Spread(1955) < Spread(2020), "relative gain is more negative (bigger) in the early era");
    }

    [Fact]
    public void CarFit_TrackWithoutKnownProfile_FallsBackToAverageOfAttributes()
    {
        var track = Layout(profile: new Dictionary<string, double> { ["balance"] = 1.0 });
        var car = Car(power: 60, downforce: 40, grip: 70, braking: 50);
        var fallback = LapTimeModel.Compute(Inputs(1990, track, car)).Seconds(LapLayer.CarFit);
        var meanCar = Car(55, 55, 55, 55);

        Assert.Equal(LapTimeModel.Compute(Inputs(1990, track, meanCar)).Seconds(LapLayer.CarFit), fallback, 12);
    }

    [Fact]
    public void Affinity_PositiveIsFaster_AndIsClamped()
    {
        var none = LapTimeModel.Compute(Inputs()).TotalSeconds;
        var liked = LapTimeModel.Compute(Inputs(driver: new DriverPace(50, 50, 50, 0.1))).TotalSeconds;
        var huge = LapTimeModel.Compute(Inputs(driver: new DriverPace(50, 50, 50, 9))).Seconds(LapLayer.DriverAffinity);

        Assert.Equal(none - 0.1, liked, 9);
        Assert.Equal(-PaceConstants.MaxAffinitySeconds, huge);
    }

    [Fact]
    public void Traffic_CostsTimeInsideTheThreshold_AndNothingWhenClear()
    {
        double Traffic(double gap) =>
            LapTimeModel.Compute(Inputs() with { TrafficGapAheadSeconds = gap }).Seconds(LapLayer.Traffic);

        Assert.Equal(0d, Traffic(double.PositiveInfinity));
        Assert.Equal(0d, Traffic(PaceConstants.TrafficGapThresholdSeconds));
        Assert.True(Traffic(1.0) > 0d);
        Assert.True(Traffic(0.2) > Traffic(1.0));
        Assert.Equal(PaceConstants.TrafficMaxLossSeconds, Traffic(0d), 12);
    }

    [Fact]
    public void DirtyAirAndTyres_AreSimplePerInputTerms()
    {
        var clean = LapTimeModel.Compute(Inputs());
        var dirty = LapTimeModel.Compute(Inputs() with { DirtyAirLevel = 1d, TyreWearFactor = 1.25 });

        Assert.Equal(PaceConstants.DirtyAirMaxLossSeconds, dirty.Seconds(LapLayer.DirtyAir), 12);
        Assert.Equal(1.25, dirty.Seconds(LapLayer.Tyres), 12);
        Assert.Equal(0d, clean.Seconds(LapLayer.DirtyAir));
        Assert.Equal(0d, clean.Seconds(LapLayer.Tyres));
    }

    [Fact]
    public void Wetness_IsZeroWhenDry_EvenWithWrongTyres()
    {
        var lap = LapTimeModel.Compute(Inputs() with { WetnessLevel = 0, TyreFitMismatch = 1 });

        Assert.Equal(0d, lap.Seconds(LapLayer.Wetness));
    }

    [Fact]
    public void Wetness_IsConvex_InWetnessAndInMismatch()
    {
        double Wet(double wetness, double mismatch) =>
            LapTimeModel.Compute(Inputs() with { WetnessLevel = wetness, TyreFitMismatch = mismatch }).Seconds(LapLayer.Wetness);

        // Convex: the second half of the range costs more than the first half.
        Assert.True(Wet(1.0, 0.5) - Wet(0.5, 0.5) > Wet(0.5, 0.5) - Wet(0.0, 0.5));
        Assert.True(Wet(0.8, 1.0) - Wet(0.8, 0.5) > Wet(0.8, 0.5) - Wet(0.8, 0.0));
        Assert.True(Wet(0.8, 1.0) > Wet(0.8, 0.0));
    }

    [Fact]
    public void Wetness_IsMuchLargerForLowComposure()
    {
        double Wet(double composure) =>
            LapTimeModel.Compute(Inputs(driver: new DriverPace(50, 50, composure)) with { WetnessLevel = 0.9, TyreFitMismatch = 0.6 })
                .Seconds(LapLayer.Wetness);

        Assert.True(Wet(10) > Wet(50));
        Assert.True(Wet(50) > Wet(90));
        Assert.True(Wet(10) > 2.5 * Wet(90));
    }

    [Fact]
    public void Noise_ScalesWithDrawAndShrinksWithConsistency()
    {
        double Noise(double consistency, double draw) =>
            LapTimeModel.Compute(Inputs(driver: new DriverPace(50, consistency, 50)) with { NoiseDraw = draw }).Seconds(LapLayer.Noise);

        Assert.Equal(0d, Noise(50, 0));
        Assert.Equal(-Noise(50, 1.3), Noise(50, -1.3), 12);
        Assert.Equal(2 * Noise(50, 1), Noise(50, 2), 12);
        Assert.Equal(PaceConstants.NoiseSigmaAtZeroConsistency, Noise(0, 1), 12);
        Assert.Equal(PaceConstants.NoiseSigmaAtMaxConsistency, Noise(100, 1), 12);
        Assert.True(Math.Abs(Noise(90, 1)) < Math.Abs(Noise(20, 1)));
    }

    [Fact]
    public void Compute_IsDeterministic_AndDoesNotMutateItsInputs()
    {
        var inputs = Inputs(2001, Layout(5.3, tags: ["flowing"]), Car(65, 70, 55, 60), new DriverPace(72, 66, 58, -0.05)) with
        {
            FuelMassKg = 33,
            TyreWearFactor = 0.4,
            TrafficGapAheadSeconds = 0.9,
            DirtyAirLevel = 0.3,
            WetnessLevel = 0.2,
            TyreFitMismatch = 0.1,
            NoiseDraw = 0.42,
        };
        var snapshot = inputs with { };

        var first = LapTimeModel.Compute(inputs);
        var second = LapTimeModel.Compute(inputs);

        Assert.Equal(first.Layers, second.Layers);
        Assert.Equal(first.TotalSeconds, second.TotalSeconds);
        Assert.Equal(snapshot, inputs);
    }

    [Fact]
    public void ProfileWeightEnumerationOrder_DoesNotChangeTheResult()
    {
        var forward = new Dictionary<string, double> { ["straights"] = 0.1, ["high_speed"] = 0.2, ["low_speed"] = 0.3, ["braking"] = 0.4 };
        var backward = new Dictionary<string, double> { ["braking"] = 0.4, ["low_speed"] = 0.3, ["high_speed"] = 0.2, ["straights"] = 0.1 };
        var car = Car(61, 47, 73, 38);

        Assert.Equal(
            LapTimeModel.Compute(Inputs(1977, Layout(profile: forward), car)).TotalSeconds,
            LapTimeModel.Compute(Inputs(1977, Layout(profile: backward), car)).TotalSeconds);
    }

    [Fact]
    public void EraLimitsEstimate_DownforceCapGrowsSmoothlyWithTheEra()
    {
        var previous = EraPerformanceLimits.EstimateFor(1945).DownforceCap;
        for (var season = 1946; season <= 2030; season++)
        {
            var cap = EraPerformanceLimits.EstimateFor(season).DownforceCap;
            Assert.True(cap >= previous - 1e-12);
            Assert.InRange(cap, 0d, 100d);
            previous = cap;
        }
    }

    [Theory]
    [InlineData("pace")]
    [InlineData("composure")]
    [InlineData("fuel")]
    [InlineData("wet")]
    [InlineData("noise")]
    public void InvalidInputs_AreRejected(string which)
    {
        var inputs = which switch
        {
            "pace" => Inputs(driver: new DriverPace(101, 50, 50)),
            "composure" => Inputs(driver: new DriverPace(50, 50, double.NaN)),
            "fuel" => Inputs() with { FuelMassKg = -1 },
            "wet" => Inputs() with { WetnessLevel = 1.5 },
            _ => Inputs() with { NoiseDraw = double.PositiveInfinity },
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => LapTimeModel.Compute(inputs));
    }

    [Fact]
    public void AllAuthoredStyleProfileRows_SumToOne()
    {
        foreach (var (key, row) in PaceConstants.CarFitMatrix)
        {
            Assert.Equal(1.0, row.Sum(), 9);
            Assert.Equal(4, row.Length);
            Assert.False(string.IsNullOrWhiteSpace(key));
        }
    }
}
