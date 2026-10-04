using System.Globalization;
using Paddock.Simulation.Racing.Pace;
using Xunit.Abstractions;

namespace Paddock.Tests.Racing.Pace;

/// <summary>
/// Synthetic curvature only: circles and stadiums built in the test. No track database and no network.
/// Expected shapes follow the uncalibrated ESTIMATES in <see cref="SpeedTraceConstants"/>.
/// </summary>
public class SpeedTraceModelTests(ITestOutputHelper output)
{
    [Fact]
    public void ConstantRadiusCircle_MatchesTheAnalyticCornerSpeed()
    {
        const double radius = 100d;
        const double step = 5d;
        var car = new CarPhysics(
            MechanicalGripMs2: 14d,
            DownforcePerM: 0.002d,
            TopSpeedMs: 95d,
            AccelerationMs2: 8d,
            BrakingMs2: 30d);

        var expected = AnalyticCornerSpeed(radius, car);
        var trace = SpeedTraceModel.Compute(Circle(radius, step), step, car);

        Assert.True(expected < car.TopSpeedMs);
        foreach (var speed in trace.SpeedsMs)
        {
            Assert.Equal(expected, speed, 9);
        }

        Assert.Equal(expected, trace.TopSpeedMs, 9);
        Assert.Empty(trace.BrakingZones);
        Assert.Empty(trace.Corners);
    }

    [Fact]
    public void ConstantRadiusCircle_WithNoDownforce_IsSqrtOfGripTimesRadius()
    {
        const double radius = 80d;
        var car = new CarPhysics(12d, 0d, 90d, 7d, 25d);
        var trace = SpeedTraceModel.Compute(Circle(radius, 4d), 4d, car);

        var expected = Math.Sqrt(12d * radius);
        Assert.Equal(expected, trace.SpeedsMs[0], 9);
        Assert.All(trace.SpeedsMs, speed => Assert.Equal(expected, speed, 9));
    }

    [Fact]
    public void AeroSaturatedCorner_IsTakenAtTopSpeed()
    {
        // c·r = 0.005 * 250 = 1.25 ≥ 1, so aero alone holds the corner.
        const double radius = 250d;
        var car = new CarPhysics(15d, 0.005d, 88d, 9d, 40d);
        Assert.True(car.DownforcePerM * radius >= 1d);

        var trace = SpeedTraceModel.Compute(Circle(radius, 10d), 10d, car);

        Assert.All(trace.SpeedsMs, speed => Assert.Equal(car.TopSpeedMs, speed, 9));
    }

    [Fact]
    public void SignedCurvature_UsesTheAbsoluteRadius()
    {
        var car = new CarPhysics(13d, 0.001d, 90d, 8d, 28d);
        var positive = SpeedTraceModel.Compute(Circle(60d, 5d, sign: 1d), 5d, car);
        var negative = SpeedTraceModel.Compute(Circle(60d, 5d, sign: -1d), 5d, car);

        Assert.Equal(positive.SpeedsMs.Count, negative.SpeedsMs.Count);
        for (var i = 0; i < positive.SpeedsMs.Count; i++)
        {
            Assert.Equal(positive.SpeedsMs[i], negative.SpeedsMs[i], 12);
        }
    }

    [Fact]
    public void Stadium_TopSpeedRisesWithStraightLength_UntilVmax()
    {
        var car = new CarPhysics(12d, 0.001d, 80d, 4.5d, 22d);
        int[] straights = [15, 40, 120, 400, 1500];
        var peaks = new double[straights.Length];
        for (var i = 0; i < straights.Length; i++)
        {
            var curvature = Stadium(straights[i], ArcSamples(40d, 4d), 40d);
            peaks[i] = SpeedTraceModel.Compute(curvature, 4d, car).TopSpeedMs;
        }

        for (var i = 1; i < peaks.Length; i++)
        {
            Assert.True(peaks[i] >= peaks[i - 1] - 1e-6, $"straight {straights[i]} peaked at {peaks[i]}, previous {peaks[i - 1]}");
        }

        Assert.True(peaks[0] < car.TopSpeedMs * 0.55d);
        Assert.InRange(peaks[^1] / car.TopSpeedMs, 0.98d, 1d);
        Assert.True(peaks[^1] <= car.TopSpeedMs + 1e-9);
    }

    [Fact]
    public void Stadium_BrakingZoneLength_MatchesTheConstantDecelerationFormula()
    {
        const double step = 2d;
        const double radius = 50d;
        var car = new CarPhysics(13d, 0.0012d, 78d, 7d, 32d);
        var curvature = Stadium(straightSamples: 450, arcSamples: ArcSamples(radius, step), radius);
        var trace = SpeedTraceModel.Compute(curvature, step, car);

        Assert.Equal(2, trace.BrakingZones.Count);
        AssertBrakingZonesMatchFormula(trace, curvature.Length, step, car);

        // Shift the lap so one braking zone crosses the start/finish line.
        var entryIndex = (int)Math.Round(trace.BrakingZones[0].StartDistanceM / step);
        var shift = (entryIndex + 5) % curvature.Length;
        var rotated = new double[curvature.Length];
        for (var i = 0; i < curvature.Length; i++)
        {
            rotated[i] = curvature[(i + shift) % curvature.Length];
        }

        var wrapped = SpeedTraceModel.Compute(rotated, step, car);
        Assert.Contains(wrapped.BrakingZones, zone => zone.EndDistanceM < zone.StartDistanceM);
        AssertBrakingZonesMatchFormula(wrapped, curvature.Length, step, car);
    }

    private static void AssertBrakingZonesMatchFormula(SpeedTrace trace, int sampleCount, double step, CarPhysics car)
    {
        foreach (var zone in trace.BrakingZones)
        {
            Assert.True(zone.EntrySpeedMs > zone.ExitSpeedMs);
            var length = ZoneLength(zone, sampleCount, step);
            var expected = ((zone.EntrySpeedMs * zone.EntrySpeedMs) - (zone.ExitSpeedMs * zone.ExitSpeedMs))
                / (2d * car.BrakingMs2);
            var relativeError = Math.Abs(length - expected) / expected;
            Assert.True(relativeError < 0.02d, $"zone {length:F2} m vs formula {expected:F2} m ({relativeError:P2})");
        }
    }

    [Fact]
    public void MoreDownforce_HelpsAHighSpeedCornerMuchMoreThanAHairpin()
    {
        var low = new CarPhysics(14d, 0.0008d, 100d, 9d, 40d);
        var high = low with { DownforcePerM = 0.0035d };

        var hairpin = LapTimeGain(Circle(25d, 2d), 2d, low, high);
        var sweeper = LapTimeGain(Circle(160d, 4d), 4d, low, high);

        Assert.True(hairpin > 0d);
        Assert.True(sweeper > hairpin * 4d, $"sweeper gain {sweeper:P1}, hairpin gain {hairpin:P1}");
    }

    [Fact]
    public void MorePower_HelpsALongStraightMuchMoreThanATwistyLayout()
    {
        var low = new CarPhysics(14d, 0.0015d, 70d, 5d, 30d);
        var high = low with
        {
            AccelerationMs2 = 5d * 1.5d,
            TopSpeedMs = 70d * Math.Cbrt(1.5d),
        };

        const double step = 5d;
        const double radius = 30d;
        var arc = ArcSamples(radius, step);
        var twisty = LapTimeGain(Stadium(8, arc, radius), step, low, high);
        var open = LapTimeGain(Stadium(400, arc, radius), step, low, high);

        Assert.True(open > twisty * 3d, $"open gain {open:P1}, twisty gain {twisty:P1}");
    }

    [Fact]
    public void ClosedLoop_EndOfTheLapFeedsTheStart()
    {
        const double step = 4d;
        var car = new CarPhysics(13d, 0.002d, 85d, 8d, 30d);
        var curvature = Stadium(80, ArcSamples(55d, step), 55d);
        var trace = SpeedTraceModel.Compute(curvature, step, car);
        var speeds = trace.SpeedsMs;
        var n = speeds.Count;

        // The sample after the last one is the first one. Acceleration out of the last sample must be able
        // to reach the first, and braking into the first must be able to stop from the last.
        var vStart = speeds[0];
        var vEnd = speeds[n - 1];
        Assert.True(vStart <= SpeedAfterAcceleration(vEnd, step, car) + 1e-6);
        Assert.True(vEnd <= Math.Sqrt((vStart * vStart) + (2d * car.BrakingMs2 * step)) + 1e-6);

        // No privileged start index: rotating the curvature rotates the trace.
        var shift = n / 3;
        var rotated = new double[n];
        for (var i = 0; i < n; i++)
        {
            rotated[i] = curvature[(i + shift) % n];
        }

        var shifted = SpeedTraceModel.Compute(rotated, step, car);
        for (var i = 0; i < n; i++)
        {
            Assert.Equal(speeds[(i + shift) % n], shifted.SpeedsMs[i], 5);
        }
    }

    [Fact]
    public void SameInputs_ProduceTheSameTrace()
    {
        const double step = 5d;
        var car = EraCarPhysics.For(1988);
        var curvature = Stadium(60, ArcSamples(70d, step), 70d);

        var first = SpeedTraceModel.Compute(curvature, step, car);
        var second = SpeedTraceModel.Compute(curvature, step, car);

        Assert.Equal(first.LapSeconds, second.LapSeconds);
        Assert.Equal(first.TopSpeedMs, second.TopSpeedMs);
        Assert.Equal(first.SpeedsMs.Count, second.SpeedsMs.Count);
        for (var i = 0; i < first.SpeedsMs.Count; i++)
        {
            Assert.Equal(first.SpeedsMs[i], second.SpeedsMs[i]);
        }
    }

    [Fact]
    public void CoarserStep_ChangesLapTimeByLessThanHalfAPercent()
    {
        const double fineStep = 2d;
        var car = new CarPhysics(15d, 0.0022d, 86d, 8d, 34d);
        var fine = Stadium(200, ArcSamples(70d, fineStep), 70d);
        if (fine.Length % 2 != 0)
        {
            throw new InvalidOperationException("The convergence fixture must have an even sample count.");
        }

        var coarse = new double[fine.Length / 2];
        for (var i = 0; i < coarse.Length; i++)
        {
            coarse[i] = fine[i * 2];
        }

        var fineLap = SpeedTraceModel.Compute(fine, fineStep, car).LapSeconds;
        var coarseLap = SpeedTraceModel.Compute(coarse, fineStep * 2d, car).LapSeconds;
        var relative = Math.Abs(fineLap - coarseLap) / fineLap;

        Assert.True(relative < 0.005d, $"fine {fineLap:F4} s, coarse {coarseLap:F4} s, relative {relative:P3}");
    }

    [Fact]
    public void Circle_SectorTimesAreEqualAndSumToTheLap()
    {
        var car = new CarPhysics(12d, 0d, 80d, 6d, 20d);
        var trace = SpeedTraceModel.Compute(Circle(90d, 6d), 6d, car);

        Assert.Equal(3, trace.SectorSeconds.Count);
        var sum = trace.SectorSeconds.Sum();
        Assert.Equal(trace.LapSeconds, sum, 9);
        Assert.Equal(trace.SectorSeconds[0], trace.SectorSeconds[1], 6);
        Assert.Equal(trace.SectorSeconds[0], trace.SectorSeconds[2], 6);
        Assert.True(trace.LapSeconds > 0d);
    }

    [Fact]
    public void Stadium_ReportsTwoCornersAtTheAnalyticMinimum()
    {
        const double step = 4d;
        const double radius = 45d;
        var car = new CarPhysics(14d, 0.001d, 90d, 8d, 35d);
        var arc = ArcSamples(radius, step);
        var straight = 100;
        var trace = SpeedTraceModel.Compute(Stadium(straight, arc, radius), step, car);

        Assert.Equal(2, trace.Corners.Count);
        var expectedSpeed = AnalyticCornerSpeed(radius, car);
        foreach (var corner in trace.Corners)
        {
            Assert.Equal(radius, corner.RadiusM, 6);
            Assert.Equal(expectedSpeed, corner.MinimumSpeedMs, 4);
            Assert.True(corner.MinimumSpeedMs < car.TopSpeedMs);
        }

        // Start mid-corner so one minimum plateau crosses the timing line.
        var curvature = Stadium(straight, arc, radius);
        var shift = straight + (arc / 2);
        var rotated = new double[curvature.Length];
        for (var i = 0; i < curvature.Length; i++)
        {
            rotated[i] = curvature[(i + shift) % curvature.Length];
        }

        var wrapped = SpeedTraceModel.Compute(rotated, step, car);
        Assert.Equal(2, wrapped.Corners.Count);
        Assert.All(wrapped.Corners, corner => Assert.Equal(radius, corner.RadiusM, 6));
        Assert.Contains(wrapped.Corners, corner => corner.DistanceM < arc * step);
    }

    [Fact]
    public void ReferenceRatings_MatchTheEraBaseline_WhenTheCapDoesNotBind()
    {
        const int season = 1990;
        var era = EraCarPhysics.For(season);
        var car = CarPhysics.From(new CarPerformance(50, 50, 50, 50, 50), season, new EraPerformanceLimits(100));

        Assert.Equal(era.MechanicalGripMs2, car.MechanicalGripMs2, 9);
        Assert.Equal(era.DownforcePerM, car.DownforcePerM, 12);
        Assert.Equal(era.TopSpeedMs, car.TopSpeedMs, 9);
        Assert.Equal(era.AccelerationMs2, car.AccelerationMs2, 9);
        Assert.Equal(era.BrakingMs2, car.BrakingMs2, 9);
    }

    [Fact]
    public void PowerRating_ScalesAccelerationLinearly_AndTopSpeedByTheCubeRoot()
    {
        var era = EraCarPhysics.For(2004);
        var strong = CarPhysics.From(new CarPerformance(100, 50, 50, 50, 50), 2004, new EraPerformanceLimits(100));
        var power = 1d + SpeedTraceConstants.PowerSensitivity;

        Assert.Equal(era.AccelerationMs2 * power, strong.AccelerationMs2, 9);
        Assert.Equal(era.TopSpeedMs * Math.Cbrt(power), strong.TopSpeedMs, 9);
        Assert.Equal(era.MechanicalGripMs2, strong.MechanicalGripMs2, 9);
        Assert.Equal(era.BrakingMs2, strong.BrakingMs2, 9);
    }

    [Fact]
    public void GripAndBrakingRatings_ScaleThoseAxesOnly()
    {
        var era = EraCarPhysics.For(2010);
        var car = CarPhysics.From(new CarPerformance(50, 50, 100, 0, 50), 2010, new EraPerformanceLimits(100));

        Assert.Equal(era.MechanicalGripMs2 * (1d + SpeedTraceConstants.MechanicalGripSensitivity), car.MechanicalGripMs2, 9);
        Assert.Equal(era.BrakingMs2 * (1d - SpeedTraceConstants.BrakingSensitivity), car.BrakingMs2, 9);
        Assert.Equal(era.TopSpeedMs, car.TopSpeedMs, 9);
        Assert.Equal(era.DownforcePerM, car.DownforcePerM, 12);
    }

    [Fact]
    public void DownforceAboveTheCap_MatchesTheCap()
    {
        const int season = 2000;
        var cap = new EraPerformanceLimits(60);
        var atCap = CarPhysics.From(new CarPerformance(50, 60, 50, 50, 50), season, cap);
        var above = CarPhysics.From(new CarPerformance(50, 100, 50, 50, 50), season, cap);
        var unrestricted = CarPhysics.From(
            new CarPerformance(50, 100, 50, 50, 50),
            season,
            new EraPerformanceLimits(100));

        Assert.Equal(atCap.DownforcePerM, above.DownforcePerM, 12);
        Assert.True(unrestricted.DownforcePerM > above.DownforcePerM);
        Assert.True(above.DownforcePerM > EraCarPhysics.For(season).DownforcePerM);
    }

    [Fact]
    public void EraBaseline_HasNoDownforceInTheFifties_AndAGroundEffectPeak()
    {
        Assert.Equal(0d, EraCarPhysics.For(1950).DownforcePerM);
        Assert.Equal(0d, EraCarPhysics.For(1955).DownforcePerM);
        Assert.Equal(0d, EraCarPhysics.For(1960).DownforcePerM);

        var wings = EraCarPhysics.For(1970);
        var groundEffect = EraCarPhysics.For(1980);
        var rulesLimited = EraCarPhysics.For(1990);
        var hybrid = EraCarPhysics.For(2020);

        Assert.True(wings.DownforcePerM > 0d);
        Assert.True(groundEffect.DownforcePerM > wings.DownforcePerM);
        Assert.True(rulesLimited.DownforcePerM < groundEffect.DownforcePerM);
        Assert.True(hybrid.DownforcePerM > rulesLimited.DownforcePerM);
        Assert.True(hybrid.TopSpeedMs > EraCarPhysics.For(1950).TopSpeedMs);
        Assert.True(hybrid.BrakingMs2 > EraCarPhysics.For(1950).BrakingMs2);
        Assert.True(EraCarPhysics.For(2000).MechanicalGripMs2 < EraCarPhysics.For(1990).MechanicalGripMs2);
        Assert.True(EraCarPhysics.For(2010).MechanicalGripMs2 > EraCarPhysics.For(2000).MechanicalGripMs2);
    }

    [Fact]
    public void EraAverageCar_OnTheStadium_HasASaneLapTimeCurve()
    {
        // Two 650 m straights and two 80 m semicircles, sampled every 5 m.
        // Printed for the PR: lap time of the era-average car. ESTIMATES, not a calibration target.
        const double step = 5d;
        const double radius = 80d;
        var curvature = Stadium(straightSamples: 130, arcSamples: ArcSamples(radius, step), radius);
        var lengthM = curvature.Length * step;

        int[] seasons = [1955, 1976, 1988, 2004, 2020];
        var laps = new double[seasons.Length];
        output.WriteLine($"Stadium length {lengthM.ToString("F1", CultureInfo.InvariantCulture)} m, radius {radius} m, step {step} m.");
        output.WriteLine("season  lap_s   top_kmh  avg_kmh");
        for (var i = 0; i < seasons.Length; i++)
        {
            var trace = SpeedTraceModel.Compute(curvature, step, EraCarPhysics.For(seasons[i]));
            laps[i] = trace.LapSeconds;
            var topKmh = trace.TopSpeedMs * 3.6d;
            var avgKmh = lengthM / trace.LapSeconds * 3.6d;
            output.WriteLine(
                $"{seasons[i]}  {trace.LapSeconds.ToString("F2", CultureInfo.InvariantCulture)}  {topKmh.ToString("F1", CultureInfo.InvariantCulture)}  {avgKmh.ToString("F1", CultureInfo.InvariantCulture)}");
        }

        Assert.True(laps[0] > laps[1], "1955 should be slower than 1976");
        Assert.True(laps[1] > laps[2], "1976 should be slower than 1988");
        Assert.True(laps[2] > laps[3], "1988 should be slower than 2004");
        Assert.InRange(laps[4] / laps[3], 0.90d, 1.12d);
        Assert.True(laps[0] > laps[3] * 1.15d, "1955 should be clearly slower than 2004");
        Assert.All(laps, lap => Assert.InRange(lap, 20d, 120d));
    }

    private static double LapTimeGain(double[] curvature, double step, CarPhysics slower, CarPhysics faster)
    {
        var slowLap = SpeedTraceModel.Compute(curvature, step, slower).LapSeconds;
        var fastLap = SpeedTraceModel.Compute(curvature, step, faster).LapSeconds;
        return (slowLap - fastLap) / slowLap;
    }

    private static double AnalyticCornerSpeed(double radius, CarPhysics car)
    {
        var denominator = 1d - (car.DownforcePerM * radius);
        var speed = Math.Sqrt(car.MechanicalGripMs2 * radius / denominator);
        return Math.Min(speed, car.TopSpeedMs);
    }

    /// <summary>Same integration the model uses, so the seam check does not call back into it.</summary>
    private static double SpeedAfterAcceleration(double speed, double distance, CarPhysics car)
    {
        var vmax = car.TopSpeedMs;
        var remaining = 1d - ((speed * speed) / (vmax * vmax));
        var decay = Math.Exp(-2d * car.AccelerationMs2 * distance / (vmax * vmax));
        return Math.Sqrt(vmax * vmax * (1d - (remaining * decay)));
    }

    private static double ZoneLength(BrakingZone zone, int sampleCount, double step)
    {
        var length = zone.EndDistanceM - zone.StartDistanceM;
        if (length <= 0d)
        {
            length += sampleCount * step;
        }

        return length;
    }

    private static int ArcSamples(double radius, double step) =>
        Math.Max(1, (int)Math.Round(Math.PI * radius / step));

    private static double[] Circle(double radius, double step, double sign = 1d)
    {
        var n = Math.Max(8, (int)Math.Round(2d * Math.PI * radius / step));
        var curvature = new double[n];
        var kappa = sign / radius;
        for (var i = 0; i < n; i++)
        {
            curvature[i] = kappa;
        }

        return curvature;
    }

    /// <summary>
    /// Straight, semicircle, straight, semicircle. Curvature is zero on the straights and ±1/radius on the arcs.
    /// </summary>
    private static double[] Stadium(int straightSamples, int arcSamples, double radius)
    {
        var curvature = new double[2 * straightSamples + 2 * arcSamples];
        var kappa = 1d / radius;
        var index = 0;
        index = Fill(curvature, index, straightSamples, 0d);
        index = Fill(curvature, index, arcSamples, kappa);
        index = Fill(curvature, index, straightSamples, 0d);
        Fill(curvature, index, arcSamples, -kappa);
        return curvature;
    }

    private static int Fill(double[] curvature, int index, int count, double value)
    {
        for (var i = 0; i < count; i++)
        {
            curvature[index++] = value;
        }

        return index;
    }
}
