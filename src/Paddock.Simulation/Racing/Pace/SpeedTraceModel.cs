using System.Globalization;

namespace Paddock.Simulation.Racing.Pace;

/// <summary>
/// Steady-state speed along a closed loop from curvature and <see cref="CarPhysics"/> (PP-049).
/// A pure function: no state, no clock, no RNG (INV-002, INV-004). It does not know about
/// <c>TrackGeometry</c> — the caller passes a curvature sample per step.
/// </summary>
/// <remarks>
/// Corner limit: <c>v² = a0·r / (1 − c·r)</c> with <c>r = 1/|κ|</c>. When <c>c·r ≥ 1</c>, aero alone
/// holds the corner and the limit is top speed. The limit is also clamped to top speed.
/// Forward pass: <c>a(v) = accel · (1 − (v / vmax)²)</c>, integrated exactly over the step.
/// Backward pass: constant <see cref="CarPhysics.BrakingMs2"/>.
/// The two passes repeat around the loop until the trace stops changing, because the end of the lap
/// is the start of the lap.
/// </remarks>
public static class SpeedTraceModel
{
    private const double ConvergenceToleranceMs = 1e-8;
    private const int MaxIterations = 64;
    private const double StraightCurvature = 1e-12;
    private const double SpeedPlateauMs = 1e-6;
    private const double BrakingMatchMs = 1e-5;

    public static SpeedTrace Compute(IReadOnlyList<double> curvaturePerM, double stepM, CarPhysics car)
    {
        ArgumentNullException.ThrowIfNull(curvaturePerM);
        Validate(curvaturePerM, stepM, car);

        var n = curvaturePerM.Count;
        var limits = new double[n];
        var speeds = new double[n];
        for (var i = 0; i < n; i++)
        {
            limits[i] = CornerLimit(curvaturePerM[i], car);
            speeds[i] = limits[i];
        }

        var converged = false;
        var maxDelta = 0d;
        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            maxDelta = 0d;

            // Forward: nothing may go faster than acceleration from the previous sample allows.
            for (var i = 0; i < n; i++)
            {
                var prev = i == 0 ? n - 1 : i - 1;
                var reach = SpeedAfterAcceleration(speeds[prev], stepM, car);
                var updated = Math.Min(speeds[i], Math.Min(limits[i], reach));
                maxDelta = Math.Max(maxDelta, speeds[i] - updated);
                speeds[i] = updated;
            }

            // Backward: nothing may go faster than the braking distance to the next sample allows.
            for (var i = n - 1; i >= 0; i--)
            {
                var next = i == n - 1 ? 0 : i + 1;
                var reach = MaxSpeedBeforeBraking(speeds[next], stepM, car);
                var updated = Math.Min(speeds[i], reach);
                maxDelta = Math.Max(maxDelta, speeds[i] - updated);
                speeds[i] = updated;
            }

            if (maxDelta <= ConvergenceToleranceMs)
            {
                converged = true;
                break;
            }
        }

        if (!converged)
        {
            throw new InvalidOperationException(
                $"Speed trace did not converge in {MaxIterations} passes (last change {maxDelta.ToString("R", CultureInfo.InvariantCulture)} m/s).");
        }

        var sectors = SectorTimes(speeds, stepM);
        var lapSeconds = sectors[0] + sectors[1] + sectors[2];
        return new SpeedTrace(
            speeds,
            lapSeconds,
            Max(speeds),
            BrakingZones(speeds, stepM, car),
            Corners(speeds, curvaturePerM, stepM, car),
            sectors);
    }

    /// <summary><c>v² = a0·r / (1 − c·r)</c>, or top speed when the corner is straight or aero-saturated.</summary>
    private static double CornerLimit(double curvaturePerM, CarPhysics car)
    {
        var kappa = Math.Abs(curvaturePerM);
        if (kappa <= StraightCurvature)
        {
            return car.TopSpeedMs;
        }

        var radius = 1d / kappa;
        var downforceRadius = car.DownforcePerM * radius;
        if (downforceRadius >= 1d)
        {
            return car.TopSpeedMs;
        }

        var speedSquared = car.MechanicalGripMs2 * radius / (1d - downforceRadius);
        return Math.Min(Math.Sqrt(speedSquared), car.TopSpeedMs);
    }

    /// <summary>
    /// Exact step of <c>a(v) = A·(1 − (v/vmax)²)</c>.
    /// <c>v1² = vmax² · (1 − (1 − v0²/vmax²) · exp(−2·A·ds / vmax²))</c>.
    /// </summary>
    private static double SpeedAfterAcceleration(double speed, double distance, CarPhysics car)
    {
        var vmax = car.TopSpeedMs;
        if (speed >= vmax)
        {
            return vmax;
        }

        var fractionOfVmaxSquared = (speed * speed) / (vmax * vmax);
        var remaining = 1d - fractionOfVmaxSquared;
        if (remaining <= 0d)
        {
            return vmax;
        }

        var decay = Math.Exp(-2d * car.AccelerationMs2 * distance / (vmax * vmax));
        var speedSquared = vmax * vmax * (1d - (remaining * decay));
        if (speedSquared >= vmax * vmax)
        {
            return vmax;
        }

        return Math.Sqrt(Math.Max(0d, speedSquared));
    }

    private static double MaxSpeedBeforeBraking(double nextSpeed, double distance, CarPhysics car) =>
        Math.Sqrt((nextSpeed * nextSpeed) + (2d * car.BrakingMs2 * distance));

    private static double[] SectorTimes(IReadOnlyList<double> speeds, double stepM)
    {
        var n = speeds.Count;
        var lap = n * stepM;
        var third = lap / 3d;
        var sectors = new double[3];

        for (var i = 0; i < n; i++)
        {
            var v0 = speeds[i];
            var v1 = speeds[(i + 1) % n];
            var segmentTime = 2d * stepM / (v0 + v1);
            var segStart = i * stepM;
            var cursor = segStart;
            var segEnd = segStart + stepM;

            while (cursor < segEnd - 1e-9)
            {
                var sector = Math.Min(2, (int)(cursor / third));
                var boundary = (sector + 1) * third;
                if (cursor >= boundary - 1e-9 && sector < 2)
                {
                    sector++;
                    boundary = (sector + 1) * third;
                }

                var take = Math.Min(segEnd, boundary) - cursor;
                sectors[sector] += segmentTime * (take / stepM);
                cursor += take;
            }
        }

        return sectors;
    }

    private static List<BrakingZone> BrakingZones(double[] speeds, double stepM, CarPhysics car)
    {
        var n = speeds.Length;
        var braking = new bool[n];
        for (var i = 0; i < n; i++)
        {
            var next = (i + 1) % n;
            var onTheLimit = MaxSpeedBeforeBraking(speeds[next], stepM, car);
            braking[i] = speeds[i] > speeds[next] && Math.Abs(speeds[i] - onTheLimit) <= BrakingMatchMs;
        }

        var zones = new List<BrakingZone>();
        for (var i = 0; i < n; i++)
        {
            if (!braking[i])
            {
                continue;
            }

            var prev = (i + n - 1) % n;
            if (braking[prev])
            {
                continue;
            }

            var count = 0;
            while (count < n && braking[(i + count) % n])
            {
                count++;
            }

            var exit = (i + count) % n;
            zones.Add(new BrakingZone(i * stepM, exit * stepM, speeds[i], speeds[exit]));
        }

        return zones;
    }

    private static List<TraceCorner> Corners(
        double[] speeds,
        IReadOnlyList<double> curvaturePerM,
        double stepM,
        CarPhysics car)
    {
        var n = speeds.Length;
        var groups = new List<(int Start, int End)>();
        var groupStart = 0;
        for (var i = 1; i < n; i++)
        {
            if (Math.Abs(speeds[i] - speeds[groupStart]) > SpeedPlateauMs)
            {
                groups.Add((groupStart, i - 1));
                groupStart = i;
            }
        }

        groups.Add((groupStart, n - 1));

        var wrapped = false;
        if (groups.Count > 1 && Math.Abs(speeds[groups[0].Start] - speeds[groups[^1].Start]) <= SpeedPlateauMs)
        {
            wrapped = true;
        }

        var corners = new List<TraceCorner>();
        var first = wrapped ? 1 : 0;
        var last = groups.Count;
        for (var g = first; g < last; g++)
        {
            var start = groups[g].Start;
            var end = groups[g].End;
            var wraps = false;
            if (wrapped && g == groups.Count - 1)
            {
                start = groups[^1].Start;
                end = groups[0].End;
                wraps = true;
            }

            AddCornerIfMinimum(corners, speeds, curvaturePerM, stepM, car, start, end, wraps);
        }

        corners.Sort(static (a, b) => a.DistanceM.CompareTo(b.DistanceM));
        return corners;
    }

    private static void AddCornerIfMinimum(
        List<TraceCorner> corners,
        double[] speeds,
        IReadOnlyList<double> curvaturePerM,
        double stepM,
        CarPhysics car,
        int start,
        int end,
        bool wraps)
    {
        var n = speeds.Length;
        var count = wraps ? (n - start) + (end + 1) : (end - start + 1);
        if (count >= n)
        {
            return;
        }

        var speed = speeds[start];
        if (speed >= car.TopSpeedMs - SpeedPlateauMs)
        {
            return;
        }

        var leftIndex = (start + n - 1) % n;
        var rightIndex = (end + 1) % n;
        if (speeds[leftIndex] <= speed + SpeedPlateauMs || speeds[rightIndex] <= speed + SpeedPlateauMs)
        {
            return;
        }

        var midOffset = (count - 1) / 2d;
        var mid = start + midOffset;
        if (wraps)
        {
            mid %= n;
        }

        var sample = (int)Math.Floor(mid) % n;

        var kappa = Math.Abs(curvaturePerM[sample]);
        var radius = kappa <= StraightCurvature ? double.PositiveInfinity : 1d / kappa;
        corners.Add(new TraceCorner(mid * stepM, speed, radius));
    }

    private static double Max(double[] speeds)
    {
        var max = speeds[0];
        for (var i = 1; i < speeds.Length; i++)
        {
            if (speeds[i] > max)
            {
                max = speeds[i];
            }
        }

        return max;
    }

    private static void Validate(IReadOnlyList<double> curvaturePerM, double stepM, CarPhysics car)
    {
        if (curvaturePerM.Count == 0)
        {
            throw new ArgumentException("Curvature must contain at least one sample.", nameof(curvaturePerM));
        }

        for (var i = 0; i < curvaturePerM.Count; i++)
        {
            if (!double.IsFinite(curvaturePerM[i]))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(curvaturePerM),
                    curvaturePerM[i],
                    $"Expected a finite curvature at index {i.ToString(CultureInfo.InvariantCulture)}.");
            }
        }

        Positive(stepM, nameof(stepM));
        Positive(car.MechanicalGripMs2, nameof(car.MechanicalGripMs2));
        NotNegative(car.DownforcePerM, nameof(car.DownforcePerM));
        Positive(car.TopSpeedMs, nameof(car.TopSpeedMs));
        Positive(car.AccelerationMs2, nameof(car.AccelerationMs2));
        Positive(car.BrakingMs2, nameof(car.BrakingMs2));
    }

    private static void Positive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0d)
        {
            throw new ArgumentOutOfRangeException(name, value, "Expected a finite value > 0.");
        }
    }

    private static void NotNegative(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0d)
        {
            throw new ArgumentOutOfRangeException(name, value, "Expected a finite value >= 0.");
        }
    }
}
