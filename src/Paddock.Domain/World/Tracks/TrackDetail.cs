namespace Paddock.Domain.World.Tracks;

/// <summary>Which side of the centre line, looking in the direction of travel.</summary>
public enum TrackSide
{
    Left,
    Right,
}

/// <summary>Surface of the run-off strip between the kerb and the barrier.</summary>
public enum RunoffSurface
{
    Asphalt,
    Grass,
    Gravel,
}

/// <summary>
/// The layers outside the track edge on one side at one point of the lap: kerb, run-off strip and whether a barrier
/// closes it (PP-073). The first engine v2 only reads the run-off cost; barrier hits and beaching come later.
/// </summary>
/// <param name="KerbM">Kerb width in metres (0 = no kerb).</param>
/// <param name="Runoff">Run-off surface.</param>
/// <param name="RunoffM">Run-off width in metres from the kerb to the barrier (or to open ground).</param>
/// <param name="Barrier">True when a barrier closes the run-off.</param>
public sealed record TrackEdge(double KerbM, RunoffSurface Runoff, double RunoffM, bool Barrier);

/// <summary>One era's defaults for a layout that has no authored width or edges, and the width band the validator accepts.</summary>
/// <param name="FromYear">First season the row applies to (rows are looked up by the layout's first F1 season).</param>
/// <param name="WidthM">Default track width in metres.</param>
/// <param name="MinWidthM">Narrowest authored width the validator accepts.</param>
/// <param name="MaxWidthM">Widest authored width the validator accepts.</param>
/// <param name="Edge">Default kerb, run-off and barrier on both sides.</param>
public sealed record TrackEraDefault(int FromYear, double WidthM, double MinWidthM, double MaxWidthM, TrackEdge Edge);

/// <summary>
/// Era defaults for track width and edges (PP-073). Every number here is an ESTIMATE from general knowledge of how
/// circuits looked in each period (1950s public roads with straw bales and no kerbs, Armco and grass in the 1970s,
/// gravel traps from the 1980s, asphalt run-off in the 2000s), to be calibrated when the 2010 tracks are described.
/// This is the only table of these numbers in the code; the editor does not copy it.
/// </summary>
public static class TrackEraDefaults
{
    /// <summary>Rows ordered by <see cref="TrackEraDefault.FromYear"/>; the last row whose year is not after the layout's first season wins.</summary>
    public static IReadOnlyList<TrackEraDefault> Table { get; } =
    [
        new(1950, WidthM: 10.0, MinWidthM: 5.0, MaxWidthM: 25.0, new TrackEdge(KerbM: 0.0, RunoffSurface.Grass, RunoffM: 5.0, Barrier: true)),
        new(1970, WidthM: 11.0, MinWidthM: 7.0, MaxWidthM: 25.0, new TrackEdge(KerbM: 1.0, RunoffSurface.Grass, RunoffM: 15.0, Barrier: true)),
        new(1980, WidthM: 12.0, MinWidthM: 7.0, MaxWidthM: 25.0, new TrackEdge(KerbM: 1.2, RunoffSurface.Gravel, RunoffM: 40.0, Barrier: true)),
        new(1995, WidthM: 13.0, MinWidthM: 7.0, MaxWidthM: 25.0, new TrackEdge(KerbM: 1.5, RunoffSurface.Gravel, RunoffM: 60.0, Barrier: true)),
        new(2005, WidthM: 14.0, MinWidthM: 7.0, MaxWidthM: 25.0, new TrackEdge(KerbM: 1.5, RunoffSurface.Asphalt, RunoffM: 50.0, Barrier: true)),
    ];

    /// <summary>The row for a layout first raced in <paramref name="firstSeason"/>; seasons before 1950 use the 1950 row.</summary>
    public static TrackEraDefault For(int firstSeason)
    {
        var row = Table[0];
        foreach (var candidate in Table)
        {
            if (candidate.FromYear <= firstSeason)
            {
                row = candidate;
            }
        }

        return row;
    }
}

/// <summary>A point of a profile along the lap: <paramref name="LapFraction"/> in [0, 1) and a value in metres.</summary>
public readonly record struct TrackProfilePoint(double LapFraction, double Value);

/// <summary>An edge description for one side over a lap range. <c>From &gt; To</c> wraps across the start line.</summary>
public sealed record TrackEdgeSpan(double From, double To, TrackSide Side, TrackEdge Edge)
{
    /// <summary>True when <paramref name="lapFraction"/> lies in [From, To), wrapping across the start line when From &gt; To.</summary>
    public bool Contains(double lapFraction) =>
        From <= To ? lapFraction >= From && lapFraction < To : lapFraction >= From || lapFraction < To;
}

/// <summary>
/// The physical pit lane: an open polyline in metres (same frame and scale as the resolved centre line), where it
/// leaves and rejoins the lap, the speed-limited part and the garage positions.
/// </summary>
public sealed class TrackPitLane
{
    public TrackPitLane(
        IReadOnlyList<(double X, double Y)> points,
        double entryLapFraction,
        double exitLapFraction,
        double limitFromM,
        double limitToM,
        double limitKph,
        IReadOnlyList<double> boxesM)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(boxesM);
        if (points.Count < 2)
        {
            throw new ArgumentException("A pit lane needs at least 2 points.", nameof(points));
        }

        if (limitKph <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(limitKph), "Speed limit must be positive.");
        }

        Points = points;
        EntryLapFraction = entryLapFraction;
        ExitLapFraction = exitLapFraction;
        LimitFromM = limitFromM;
        LimitToM = limitToM;
        LimitKph = limitKph;
        BoxesM = boxesM;
        LengthM = PolylineLength(points);
    }

    /// <summary>Pit lane polyline from the entry end to the exit end.</summary>
    public IReadOnlyList<(double X, double Y)> Points { get; }

    /// <summary>Where the lane leaves the lap, as a fraction of lap length.</summary>
    public double EntryLapFraction { get; }

    /// <summary>Where the lane rejoins the lap, as a fraction of lap length.</summary>
    public double ExitLapFraction { get; }

    /// <summary>Start of the speed limit in metres along the lane from its entry end.</summary>
    public double LimitFromM { get; }

    /// <summary>End of the speed limit in metres along the lane from its entry end.</summary>
    public double LimitToM { get; }

    /// <summary>Speed limit in km/h.</summary>
    public double LimitKph { get; }

    /// <summary>Garage positions in metres along the lane, in order from the entry; slot 0 is nearest the entry.</summary>
    public IReadOnlyList<double> BoxesM { get; }

    /// <summary>Length of the whole lane polyline in metres.</summary>
    public double LengthM { get; }

    /// <summary>Length of the speed-limited part in metres.</summary>
    public double LimitLengthM => LimitToM - LimitFromM;

    /// <summary>Seconds to drive the speed-limited part at exactly the limit (no stop, no acceleration phases).</summary>
    public double TimeAtLimitSeconds => LimitLengthM / (LimitKph / 3.6);

    private static double PolylineLength(IReadOnlyList<(double X, double Y)> points)
    {
        var length = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            var dx = points[i].X - points[i - 1].X;
            var dy = points[i].Y - points[i - 1].Y;
            length += Math.Sqrt(dx * dx + dy * dy);
        }

        return length;
    }
}

/// <summary>
/// Track v2 details on top of the centre line (PP-073, TECH §6.5a): width, edge layers, pit lane and elevation.
/// Anything the file does not describe resolves to <see cref="EraDefault"/>. Read-only, no RNG, no world state;
/// lookups use only comparisons and + - * /, so they are identical on every platform.
/// </summary>
public sealed class TrackDetail
{
    private readonly TrackProfilePoint[] _width;
    private readonly TrackEdgeSpan[] _edges;
    private readonly TrackProfilePoint[] _elevation;

    /// <param name="eraDefault">Defaults for whatever the file leaves out.</param>
    /// <param name="width">Authored width points, sorted by lap fraction; empty means the era width everywhere.</param>
    /// <param name="edges">Authored edge spans; outside them both sides use the era edge.</param>
    /// <param name="pitLane">Physical pit lane, or null when the file has none.</param>
    /// <param name="elevation">Authored elevation points, sorted by lap fraction; empty when not described.</param>
    public TrackDetail(
        TrackEraDefault eraDefault,
        IReadOnlyList<TrackProfilePoint> width,
        IReadOnlyList<TrackEdgeSpan> edges,
        TrackPitLane? pitLane,
        IReadOnlyList<TrackProfilePoint> elevation)
    {
        ArgumentNullException.ThrowIfNull(eraDefault);
        ArgumentNullException.ThrowIfNull(width);
        ArgumentNullException.ThrowIfNull(edges);
        ArgumentNullException.ThrowIfNull(elevation);
        EraDefault = eraDefault;
        _width = [.. width];
        _edges = [.. edges];
        _elevation = [.. elevation];
        PitLane = pitLane;
    }

    /// <summary>Only era defaults: no authored width, edges, pit lane or elevation.</summary>
    public static TrackDetail Defaults(TrackEraDefault eraDefault) => new(eraDefault, [], [], null, []);

    public TrackEraDefault EraDefault { get; }

    /// <summary>True when the file describes the width (otherwise <see cref="WidthAt"/> is the era width).</summary>
    public bool HasAuthoredWidth => _width.Length > 0;

    public IReadOnlyList<TrackProfilePoint> Width => _width;

    public IReadOnlyList<TrackEdgeSpan> Edges => _edges;

    public TrackPitLane? PitLane { get; }

    /// <summary>Authored elevation; the simulation does not read it yet (PP-073).</summary>
    public IReadOnlyList<TrackProfilePoint> Elevation => _elevation;

    /// <summary>Track width in metres at a lap fraction, linearly interpolated around the closed lap.</summary>
    public double WidthAt(double lapFraction) =>
        _width.Length == 0 ? EraDefault.WidthM : Interpolate(_width, Wrap(lapFraction));

    /// <summary>Elevation in metres at a lap fraction, or null when the file has no elevation.</summary>
    public double? ElevationAt(double lapFraction) =>
        _elevation.Length == 0 ? null : Interpolate(_elevation, Wrap(lapFraction));

    /// <summary>Kerb, run-off and barrier on one side at a lap fraction; the era edge outside authored spans.</summary>
    public TrackEdge EdgeAt(double lapFraction, TrackSide side)
    {
        var s = Wrap(lapFraction);
        foreach (var span in _edges)
        {
            if (span.Side == side && span.Contains(s))
            {
                return span.Edge;
            }
        }

        return EraDefault.Edge;
    }

    private static double Wrap(double lapFraction)
    {
        var s = lapFraction - Math.Floor(lapFraction);
        return s >= 1.0 ? 0.0 : s;
    }

    /// <summary>Linear interpolation over points sorted by lap fraction, wrapping from the last point to the first.</summary>
    private static double Interpolate(TrackProfilePoint[] points, double s)
    {
        if (points.Length == 1)
        {
            return points[0].Value;
        }

        for (var i = 0; i < points.Length - 1; i++)
        {
            var a = points[i];
            var b = points[i + 1];
            if (s >= a.LapFraction && s < b.LapFraction)
            {
                return a.Value + (b.Value - a.Value) * (s - a.LapFraction) / (b.LapFraction - a.LapFraction);
            }
        }

        // Wrap segment: from the last point across the start line to the first point.
        var last = points[^1];
        var first = points[0];
        var span = first.LapFraction + 1.0 - last.LapFraction;
        var along = s >= last.LapFraction ? s - last.LapFraction : s + 1.0 - last.LapFraction;
        return last.Value + (first.Value - last.Value) * along / span;
    }
}
