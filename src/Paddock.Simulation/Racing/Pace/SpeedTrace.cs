namespace Paddock.Simulation.Racing.Pace;

/// <summary>
/// One braking zone on a closed lap: the car is on the braking limit from
/// <see cref="StartDistanceM"/> to <see cref="EndDistanceM"/>.
/// Distances are measured from sample 0, in metres, along the loop. A zone that crosses the start/finish
/// has <see cref="EndDistanceM"/> less than <see cref="StartDistanceM"/>.
/// </summary>
/// <param name="StartDistanceM">Distance of the first sample of the zone.</param>
/// <param name="EndDistanceM">Distance of the first sample after the zone (the exit).</param>
/// <param name="EntrySpeedMs">Speed at <see cref="StartDistanceM"/>, m/s.</param>
/// <param name="ExitSpeedMs">Speed at <see cref="EndDistanceM"/>, m/s.</param>
public readonly record struct BrakingZone(
    double StartDistanceM,
    double EndDistanceM,
    double EntrySpeedMs,
    double ExitSpeedMs);

/// <summary>
/// A local minimum of the speed trace that sits below the car's top speed: one corner.
/// A flat bottom (constant-radius arc) is a single corner at the middle of the plateau.
/// </summary>
/// <param name="DistanceM">Distance of the minimum along the lap, metres from sample 0.</param>
/// <param name="MinimumSpeedMs">Speed at that minimum, m/s.</param>
/// <param name="RadiusM">Corner radius 1/|κ| at the minimum. Positive infinity on a straight.</param>
public readonly record struct TraceCorner(double DistanceM, double MinimumSpeedMs, double RadiusM);

/// <summary>
/// The steady speed profile of one closed lap. Pure output of <see cref="SpeedTraceModel"/>:
/// no RNG, no clock. Sample <c>i</c> sits at distance <c>i · step</c>.
/// </summary>
public sealed class SpeedTrace
{
    public SpeedTrace(
        IReadOnlyList<double> speedsMs,
        double lapSeconds,
        double topSpeedMs,
        IReadOnlyList<BrakingZone> brakingZones,
        IReadOnlyList<TraceCorner> corners,
        IReadOnlyList<double> sectorSeconds)
    {
        ArgumentNullException.ThrowIfNull(speedsMs);
        ArgumentNullException.ThrowIfNull(brakingZones);
        ArgumentNullException.ThrowIfNull(corners);
        ArgumentNullException.ThrowIfNull(sectorSeconds);

        SpeedsMs = speedsMs.ToArray();
        LapSeconds = lapSeconds;
        TopSpeedMs = topSpeedMs;
        BrakingZones = brakingZones.ToArray();
        Corners = corners.ToArray();
        SectorSeconds = sectorSeconds.ToArray();
    }

    /// <summary>Speed at each curvature sample, m/s. Same length as the curvature array.</summary>
    public IReadOnlyList<double> SpeedsMs { get; }

    /// <summary>Time to complete the closed loop, seconds.</summary>
    public double LapSeconds { get; }

    /// <summary>Highest speed reached on the lap, m/s. This is the trace peak, not the car's capability.</summary>
    public double TopSpeedMs { get; }

    /// <summary>Braking zones in lap order. Empty when the car never brakes.</summary>
    public IReadOnlyList<BrakingZone> BrakingZones { get; }

    /// <summary>Local speed minima below the car's top speed, in lap order.</summary>
    public IReadOnlyList<TraceCorner> Corners { get; }

    /// <summary>
    /// Time spent in each of three equal-distance sectors, in lap order.
    /// The three values sum to <see cref="LapSeconds"/> (floating point aside).
    /// </summary>
    public IReadOnlyList<double> SectorSeconds { get; }
}
