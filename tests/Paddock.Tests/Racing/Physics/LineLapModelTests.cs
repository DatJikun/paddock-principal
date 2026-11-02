using System.Globalization;
using Paddock.Domain.World.Tracks;
using Paddock.Simulation.Racing.Pace;
using Paddock.Simulation.Racing.Physics;
using Xunit.Abstractions;

namespace Paddock.Tests.Racing.Physics;

/// <summary>
/// One car's speed profile on the racing line (#353). Lap-time bands are ESTIMATES (see
/// <see cref="LinePhysicsConstants"/>): wide enough to catch a broken model, not a calibration.
/// </summary>
public class LineLapModelTests(ITestOutputHelper output)
{
    /// <summary>
    /// The 2010 era car with race-start fuel on Monza 1972, pinned on Linux; CI checks it on Windows too (TECH §6.5a).
    /// Re-pin only for an intended change of the model, the line or the geometry; never for a difference between OSes.
    /// </summary>
    private const string MonzaTraceHash = "6A6AF0426B1D9765FC13EF6639E4BDB0C1ED6E0D7749AADA6E773C0158395805";

    private static readonly CarPhysics Era2010 = EraCarPhysics.For(2010);

    [Fact]
    public void TheLapTraceIsBitIdenticalToThePinnedHash()
    {
        var trace = LineLapModel.Run(LinePhysicsTestKit.Monza, Era2010, LinePhysicsConstants.RaceStartFuelKg2010);
        Assert.Equal(MonzaTraceHash, LinePhysicsTestKit.Hash(LinePhysicsTestKit.Trace(trace)));
    }

    [Fact]
    public void TheSameInputsGiveTheSameLap()
    {
        var a = LineLapModel.Run(LinePhysicsTestKit.Monaco, Era2010, 50d, 0.95d);
        var b = LineLapModel.Run(LinePhysicsTestKit.Monaco, Era2010, 50d, 0.95d);

        Assert.Equal(LinePhysicsTestKit.Hash(LinePhysicsTestKit.Trace(a)), LinePhysicsTestKit.Hash(LinePhysicsTestKit.Trace(b)));
    }

    [Fact]
    public void AtTheReferenceMassTheCarHasTheSpeedTraceGripAndTopSpeed()
    {
        var car = LineCar.From(Era2010, fuelKg: 0d);
        const double v = 60d;

        var lateral = car.TyreMu * (LinePhysicsConstants.GravityMs2 + (car.DownforceNPerMs2 * v * v / car.MassKg));
        Assert.Equal(Era2010.MechanicalGripMs2 + (Era2010.DownforcePerM * v * v), lateral, 9);
        Assert.Equal(Era2010.TopSpeedMs, Math.Cbrt(car.PowerW / car.DragNPerMs2), 9);
        Assert.Equal(LinePhysicsConstants.EnginePowerW2010, car.PowerW, 6);
        Assert.Equal(LinePhysicsConstants.CarMassKg2010, car.MassKg);
    }

    [Fact]
    public void OnACircleTheCarHoldsOneSpeedOnTheEdgeOfTheFrictionCircle()
    {
        var line = RacingLine.Build(LinePhysicsTestKit.Circle(120d), widthM: 12d);
        var car = LineCar.From(Era2010, fuelKg: 80d);
        var trace = LineLapModel.Run(line, car);

        // The spline circle's curvature ripples by a few parts in 10⁵, and the speed with it.
        var v = trace.SpeedsMs[0];
        Assert.All(trace.SpeedsMs, speed => Assert.Equal(v, speed, 2));

        // Lateral load and the force that holds speed against drag use up the whole circle.
        var kappa = Math.Abs(line.CurvaturePerM[0]);
        var lateral = v * v * kappa;
        var drag = car.DragNPerMs2 * v * v / car.MassKg;
        var grip = car.TyreMu * (LinePhysicsConstants.GravityMs2 + (car.DownforceNPerMs2 * v * v / car.MassKg));
        Assert.Equal(1d, ((lateral * lateral) + (drag * drag)) / (grip * grip), 9);
    }

    [Fact]
    public void WhenAeroOutgrowsTheCornersTheCarNeverLifts()
    {
        // A 6 km stadium has ~460 m bends: the 2010 car's downforce grows faster with speed than the bend asks for.
        var line = RacingLine.Build(TrackGeometry.Fallback(6000d));
        var trace = LineLapModel.Run(line, Era2010, fuelKg: 0d);

        Assert.All(trace.SpeedsMs, speed => Assert.Equal(Era2010.TopSpeedMs, speed, 9));
        Assert.Equal(1d, trace.FullThrottleShare, 12);
    }

    [Fact]
    public void TheCarNeverExceedsItsTopSpeedAndBrakesForCorners()
    {
        var trace = LineLapModel.Run(LinePhysicsTestKit.Monza, Era2010, 0d);

        Assert.All(trace.SpeedsMs, speed => Assert.True(speed <= Era2010.TopSpeedMs + 1e-9));
        Assert.True(trace.TopSpeedMs > Era2010.TopSpeedMs - 1d, "Monza's straights are long enough to reach top speed");
        Assert.True(trace.MinimumSpeedMs < 0.6d * trace.TopSpeedMs, "the chicanes need braking");
        Assert.InRange(trace.FullThrottleShare, 0.5d, 0.99d);
    }

    // ---- Monotonic sanity (#353 acceptance) --------------------------------------------------------------

    public static TheoryData<string> Tracks => ["monza", "monaco"];

    [Theory]
    [MemberData(nameof(Tracks))]
    public void MorePowerGripDownforceOrBrakesNeverMakeALapSlower(string track)
    {
        var line = Line(track);
        var limits = new EraPerformanceLimits(DownforceCap: 100d);
        double Lap(CarPerformance perf) => LineLapModel.Run(line, CarPhysics.From(perf, 2010, limits), 60d).LapSeconds;

        var basis = new CarPerformance(Power: 50, Downforce: 50, MechanicalGrip: 50, Braking: 50, Reliability: 50);
        foreach (var step in new[] { 0d, 20d, 40d, 60d, 80d })
        {
            var at = Lap(basis with { Power = step, Downforce = step, MechanicalGrip = step, Braking = step });
            var up = step + 20d;
            Assert.True(Lap(basis with { Power = up, Downforce = step, MechanicalGrip = step, Braking = step }) <= at, $"power {step}→{up}");
            Assert.True(Lap(basis with { Power = step, Downforce = up, MechanicalGrip = step, Braking = step }) <= at, $"downforce {step}→{up}");
            Assert.True(Lap(basis with { Power = step, Downforce = step, MechanicalGrip = up, Braking = step }) <= at, $"grip {step}→{up}");
            Assert.True(Lap(basis with { Power = step, Downforce = step, MechanicalGrip = step, Braking = up }) <= at, $"brakes {step}→{up}");
        }
    }

    [Theory]
    [MemberData(nameof(Tracks))]
    public void MoreFuelNeverMakesALapFaster(string track)
    {
        var line = Line(track);
        var previous = 0d;
        foreach (var fuel in new[] { 0d, 10d, 40d, 80d, 120d, 160d, 240d })
        {
            var lap = LineLapModel.Run(line, Era2010, fuel).LapSeconds;
            Assert.True(lap >= previous, $"{fuel} kg of fuel");
            previous = lap;
        }

        Assert.True(previous > LineLapModel.Run(line, Era2010, 0d).LapSeconds, "fuel costs time");
    }

    [Theory]
    [MemberData(nameof(Tracks))]
    public void WornTyresNeverMakeALapFaster(string track)
    {
        var line = Line(track);
        var previous = 0d;
        foreach (var grip in new[] { 1.05d, 1d, 0.97d, 0.93d, 0.85d })
        {
            var lap = LineLapModel.Run(line, Era2010, 60d, grip).LapSeconds;
            Assert.True(lap > previous, $"tyre grip {grip}");
            previous = lap;
        }
    }

    // ---- Plausible bands (ESTIMATE) and the PP-049 comparison --------------------------------------------

    /// <summary>
    /// ESTIMATE bands for the average 2010 car with no fuel on the hand-drawn layouts. Real 2010 poles were 1:21.96 at
    /// Monza (5.793 km, with Roggia) and 1:13.83 at Monaco (3.340 km); our layouts are the 1972 Monza (no Roggia,
    /// gentle hand-drawn chicanes) and the 1950 Monaco (3.145 km), on a 12 m default width, so both run faster. The
    /// bands catch a broken model, not a calibration.
    /// </summary>
    [Theory]
    [InlineData("monza", 65d, 90d)]
    [InlineData("monaco", 50d, 80d)]
    public void A2010LapLandsInAPlausibleBand(string track, double fastest, double slowest)
    {
        var trace = LineLapModel.Run(Line(track), Era2010, 0d);
        output.WriteLine($"{track}: {trace.LapSeconds.ToString("F3", CultureInfo.InvariantCulture)} s");
        Assert.InRange(trace.LapSeconds, fastest, slowest);
    }

    [Fact]
    public void MonzaRewardsTopSpeedMoreThanDownforceAndMonacoTheOtherWayRound()
    {
        // PP-049: on Monza +10 km/h of top speed is worth far more than +10 % downforce. Here "+10 km/h" is the same
        // engine with less drag, which is what raising CarPhysics.TopSpeedMs alone means.
        double Gain(RacingLine line, CarPhysics better) =>
            LineLapModel.Run(line, Era2010, 0d).LapSeconds - LineLapModel.Run(line, better, 0d).LapSeconds;

        var faster = Era2010 with { TopSpeedMs = Era2010.TopSpeedMs + (10d / 3.6d) };
        var grippier = Era2010 with { DownforcePerM = Era2010.DownforcePerM * 1.1d };

        var monzaTop = Gain(LinePhysicsTestKit.Monza, faster);
        var monzaAero = Gain(LinePhysicsTestKit.Monza, grippier);
        var monacoTop = Gain(LinePhysicsTestKit.Monaco, faster);
        var monacoAero = Gain(LinePhysicsTestKit.Monaco, grippier);
        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Monza: +10 km/h {monzaTop:F3} s, +10% downforce {monzaAero:F3} s; Monaco: +10 km/h {monacoTop:F3} s, +10% downforce {monacoAero:F3} s"));

        Assert.True(monzaTop > 0d && monzaAero > 0d && monacoTop > 0d && monacoAero > 0d);
        Assert.True(monzaTop > 2d * monzaAero, "Monza is a top-speed track");
        Assert.True(monacoAero > monacoTop, "Monaco is a downforce track");
    }

    [Fact]
    public void ReportForThePullRequest()
    {
        foreach (var (name, line, layout) in new[]
        {
            ("monza_1972", LinePhysicsTestKit.Monza, LinePhysicsTestKit.Resolve("monza_1972")),
            ("monaco_1950", LinePhysicsTestKit.Monaco, LinePhysicsTestKit.Resolve("monaco_1950")),
        })
        {
            var empty = LineLapModel.Run(line, Era2010, 0d);
            var full = LineLapModel.Run(line, Era2010, LinePhysicsConstants.RaceStartFuelKg2010);
            var centre = LineLapModel.Run(RacingLine.Build(layout.Geometry, LinePhysicsConstants.CarWidthM2010), Era2010, 0d);
            output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{name}: centreline {layout.Geometry.LengthM:F0} m, line {line.LengthM:F0} m, {line.KnotCount} knots; lap {empty.LapSeconds:F3} s empty, {full.LapSeconds:F3} s with {LinePhysicsConstants.RaceStartFuelKg2010} kg, {centre.LapSeconds:F3} s on the centreline; top {empty.TopSpeedMs * 3.6:F1} km/h, min {empty.MinimumSpeedMs * 3.6:F1} km/h, full throttle {empty.FullThrottleShare:P1}"));
            foreach (var corner in layout.Corners)
            {
                var n = line.Count;
                var centreIndex = (int)(corner.LapFraction * n);
                var window = n / 40;
                var min = double.MaxValue;
                for (var k = -window; k <= window; k++)
                {
                    min = Math.Min(min, empty.SpeedsMs[(((centreIndex + k) % n) + n) % n]);
                }

                output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {corner.Name}: {min * 3.6:F0} km/h"));
            }
        }
    }

    private static RacingLine Line(string track) => track switch
    {
        "monza" => LinePhysicsTestKit.Monza,
        "monaco" => LinePhysicsTestKit.Monaco,
        _ => throw new ArgumentOutOfRangeException(nameof(track), track, null),
    };
}
