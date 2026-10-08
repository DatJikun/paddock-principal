using Paddock.Domain.World.Tracks;

namespace Paddock.Simulation.Racing.Pace;

/// <summary>
/// How the time of one lap spreads along the track (#286): slow in corners, fast on the straights. It comes from the
/// steady-state speed trace of the era-average car on the layout's curvature (<see cref="SpeedTraceModel"/>, PP-049), so it
/// is only a shape: the lap engine still decides how long each lap takes, and the shape decides where the car is inside it.
/// Display only (the frames of <see cref="Paddock.Simulation.Racing.LapFrameInterpolator"/>), never results. A pure function of the geometry and the
/// season: no state and no RNG (INV-002, INV-004).
/// </summary>
public sealed class LapSpeedShape
{
    private readonly double[] _time;
    private readonly double[] _ratio;

    private LapSpeedShape(double[] time, double[] ratio)
    {
        _time = time;
        _ratio = ratio;
    }

    /// <summary>Samples along the lap.</summary>
    public int Count => _ratio.Length;

    /// <summary>
    /// The shape of <paramref name="geometry"/> for the average car of <paramref name="season"/>. Null when the trace cannot be
    /// built (a degenerate geometry); the frames then move at an even speed through the lap, as before.
    /// </summary>
    public static LapSpeedShape? For(TrackGeometry geometry, int season)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var curvature = geometry.Curvature;
        if (curvature.Count < 3 || !(geometry.LengthM > 0))
        {
            return null;
        }

        SpeedTrace trace;
        try
        {
            trace = SpeedTraceModel.Compute(curvature, geometry.LengthM / curvature.Count, EraCarPhysics.For(season));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return null;
        }

        return FromSpeeds(trace.SpeedsMs);
    }

    /// <summary>The shape of a closed loop sampled at even distances with these speeds. Null when a speed is not positive.</summary>
    public static LapSpeedShape? FromSpeeds(IReadOnlyList<double> speeds)
    {
        ArgumentNullException.ThrowIfNull(speeds);
        var n = speeds.Count;
        if (n < 3 || speeds.Any(v => !(v > 0) || !double.IsFinite(v)))
        {
            return null;
        }

        // Time over each step at the mean of its two ends; the last step closes the loop.
        var time = new double[n + 1];
        for (var i = 0; i < n; i++)
        {
            var next = speeds[(i + 1) % n];
            time[i + 1] = time[i] + (2d / (speeds[i] + next));
        }

        var total = time[n];
        for (var i = 1; i <= n; i++)
        {
            time[i] /= total;
        }

        time[n] = 1d;

        // The mean speed of the lap is n steps over the total time; each sample's speed relative to it.
        var mean = n / total;
        var ratio = new double[n];
        for (var i = 0; i < n; i++)
        {
            ratio[i] = speeds[i] / mean;
        }

        return new LapSpeedShape(time, ratio);
    }

    /// <summary>The share of the lap's distance covered after <paramref name="timeShare"/> of its time (both 0..1).</summary>
    public double DistanceShareAt(double timeShare)
    {
        var t = Math.Clamp(timeShare, 0d, 1d);
        var lo = 0;
        var hi = _time.Length - 1;
        while (hi - lo > 1)
        {
            var mid = (lo + hi) / 2;
            if (_time[mid] <= t)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        var span = _time[hi] - _time[lo];
        var u = span > 0 ? (t - _time[lo]) / span : 0d;
        return (lo + u) / Count;
    }

    /// <summary>The speed at <paramref name="distanceShare"/> of the lap, as a multiple of the lap's mean speed.</summary>
    public double SpeedRatioAt(double distanceShare)
    {
        var x = Math.Clamp(distanceShare, 0d, 1d) * Count;
        var i = Math.Min(Count - 1, (int)Math.Floor(x));
        var u = x - i;
        return (_ratio[i] * (1 - u)) + (_ratio[(i + 1) % Count] * u);
    }
}
