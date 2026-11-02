namespace Paddock.Simulation.Racing.Physics;

/// <summary>
/// One car's lap along a <see cref="RacingLine"/>: the output of <see cref="LineLapModel"/>. Pure data, no RNG.
/// Speed <c>i</c> belongs to station <c>i</c> of the line.
/// </summary>
public sealed class LineLapTrace
{
    private readonly double[] _speeds;

    public LineLapTrace(IReadOnlyList<double> speedsMs, double lapSeconds, double topSpeedMs, double minimumSpeedMs, double fullThrottleShare)
    {
        ArgumentNullException.ThrowIfNull(speedsMs);
        _speeds = speedsMs.ToArray();
        LapSeconds = lapSeconds;
        TopSpeedMs = topSpeedMs;
        MinimumSpeedMs = minimumSpeedMs;
        FullThrottleShare = fullThrottleShare;
    }

    /// <summary>Speed at each station of the line, m/s.</summary>
    public IReadOnlyList<double> SpeedsMs => _speeds;

    /// <summary>Time for the closed lap, seconds.</summary>
    public double LapSeconds { get; }

    /// <summary>Highest speed on the lap, m/s.</summary>
    public double TopSpeedMs { get; }

    /// <summary>Lowest speed on the lap, m/s.</summary>
    public double MinimumSpeedMs { get; }

    /// <summary>
    /// Share of the line's length (0..1) where the car accelerates as hard as power and grip allow or holds its top
    /// speed: not braking, not held back by a corner.
    /// </summary>
    public double FullThrottleShare { get; }
}
