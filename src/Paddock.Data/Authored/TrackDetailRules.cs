using System.Globalization;
using Paddock.Domain.World.Tracks;

namespace Paddock.Data.Authored;

/// <summary>Which v2 section of a layout file a problem belongs to.</summary>
public enum TrackDetailSection
{
    Width,
    Edges,
    PitLane,
    Elevation,
}

/// <summary>One broken rule in the v2 part of a layout file: the section, the JSON field path and a readable reason.</summary>
public sealed record TrackDetailProblem(TrackDetailSection Section, string Field, string Reason);

/// <summary>
/// The rules for the optional v2 fields of a layout file (PP-073, TECH §6.5a). One rule set for both readers:
/// <c>validate-authored</c> turns each problem into an error that names the file, the field and the reason, and
/// <see cref="TrackGeometryCatalog"/> drops a section with any problem and uses the era defaults for it instead, so a
/// broken file never breaks a race and never gets read differently by the two.
/// </summary>
public static class TrackDetailRules
{
    /// <summary>How far the first and last pit lane points may lie from the centre line at entry and exit (metres, ESTIMATE).</summary>
    public const double PitLaneJoinToleranceM = 30.0;

    /// <summary>Lowest accepted pit lane speed limit (km/h, ESTIMATE).</summary>
    public const double MinPitLimitKph = 30.0;

    /// <summary>Highest accepted pit lane speed limit (km/h, ESTIMATE).</summary>
    public const double MaxPitLimitKph = 150.0;

    /// <summary>Widest accepted kerb (metres, ESTIMATE).</summary>
    public const double MaxKerbM = 5.0;

    /// <summary>Widest accepted run-off strip (metres, ESTIMATE).</summary>
    public const double MaxRunoffM = 500.0;

    /// <summary>Shortest accepted gap between consecutive pit lane points (metres, same as the centre line).</summary>
    public const double MinPitPointGapM = 1.0;

    /// <summary>
    /// Checks the v2 fields of <paramref name="file"/>.
    /// </summary>
    /// <param name="file">The layout file.</param>
    /// <param name="racedEras">Every era row the layout is raced in; an authored width must fit each band.</param>
    /// <param name="centreLine">Centre line in the same frame as the pit lane points after <paramref name="pointScale"/>, or null to skip the join check.</param>
    /// <param name="pointScale">Factor applied to pit lane coordinates and lane distances before the join check.</param>
    public static List<TrackDetailProblem> Check(
        TrackGeometryFile file,
        IReadOnlyList<TrackEraDefault> racedEras,
        TrackGeometry? centreLine,
        double pointScale)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(racedEras);
        var problems = new List<TrackDetailProblem>();
        if (file.WidthM is not null)
        {
            CheckWidth(file.WidthM, racedEras, problems);
        }

        if (file.Edges is not null)
        {
            CheckEdges(file.Edges, problems);
        }

        if (file.PitLane is not null)
        {
            CheckPitLane(file.PitLane, centreLine, pointScale, problems);
        }

        if (file.ElevationM is not null)
        {
            CheckProfile(TrackDetailSection.Elevation, "elevation_m", file.ElevationM.Select(p => p.S).ToList(), problems);
        }

        return problems;
    }

    /// <summary>Parses a side name; false for anything but <c>left</c> or <c>right</c>.</summary>
    public static bool TryParseSide(string? value, out TrackSide side)
    {
        switch (value)
        {
            case "left":
                side = TrackSide.Left;
                return true;
            case "right":
                side = TrackSide.Right;
                return true;
            default:
                side = default;
                return false;
        }
    }

    /// <summary>Parses a run-off surface; false for anything but <c>asphalt</c>, <c>grass</c> or <c>gravel</c>.</summary>
    public static bool TryParseRunoff(string? value, out RunoffSurface surface)
    {
        switch (value)
        {
            case "asphalt":
                surface = RunoffSurface.Asphalt;
                return true;
            case "grass":
                surface = RunoffSurface.Grass;
                return true;
            case "gravel":
                surface = RunoffSurface.Gravel;
                return true;
            default:
                surface = default;
                return false;
        }
    }

    private static void CheckWidth(IReadOnlyList<TrackWidthPointFile> width, IReadOnlyList<TrackEraDefault> racedEras, List<TrackDetailProblem> problems)
    {
        CheckProfile(TrackDetailSection.Width, "width_m", width.Select(p => p.S).ToList(), problems);
        for (var i = 0; i < width.Count; i++)
        {
            var w = width[i].W;
            var era = racedEras.FirstOrDefault(e => w < e.MinWidthM || w > e.MaxWidthM);
            if (era is not null)
            {
                problems.Add(new(
                    TrackDetailSection.Width,
                    $"width_m[{I(i)}].w",
                    $"{N(w)} m is outside the {I(era.FromYear)} era band {N(era.MinWidthM)}-{N(era.MaxWidthM)} m"));
            }
        }
    }

    private static void CheckProfile(TrackDetailSection section, string field, IReadOnlyList<double> fractions, List<TrackDetailProblem> problems)
    {
        if (fractions.Count == 0)
        {
            problems.Add(new(section, field, "is empty; leave the field out to use the era default"));
            return;
        }

        for (var i = 0; i < fractions.Count; i++)
        {
            var s = fractions[i];
            if (s < 0.0 || s >= 1.0)
            {
                problems.Add(new(section, $"{field}[{I(i)}].s", $"{N(s)} is not a lap fraction in [0, 1)"));
            }
            else if (i > 0 && s <= fractions[i - 1])
            {
                problems.Add(new(section, $"{field}[{I(i)}].s", $"{N(s)} does not come after the previous point's {N(fractions[i - 1])} (points go forward along the lap)"));
            }
        }
    }

    private static void CheckEdges(IReadOnlyList<TrackEdgeFile> edges, List<TrackDetailProblem> problems)
    {
        var intervals = new List<(int Index, TrackSide Side, double From, double To)>();
        for (var i = 0; i < edges.Count; i++)
        {
            var e = edges[i];
            var at = $"edges[{I(i)}]";
            var rangeOk = true;
            if (e.From < 0.0 || e.From >= 1.0)
            {
                problems.Add(new(TrackDetailSection.Edges, at + ".from", $"{N(e.From)} is not a lap fraction in [0, 1)"));
                rangeOk = false;
            }

            if (e.To < 0.0 || e.To >= 1.0)
            {
                problems.Add(new(TrackDetailSection.Edges, at + ".to", $"{N(e.To)} is not a lap fraction in [0, 1)"));
                rangeOk = false;
            }

            if (rangeOk && e.From == e.To)
            {
                problems.Add(new(TrackDetailSection.Edges, at, $"from and to are both {N(e.From)}, the span is empty"));
                rangeOk = false;
            }

            var sideOk = TryParseSide(e.Side, out var side);
            if (!sideOk)
            {
                problems.Add(new(TrackDetailSection.Edges, at + ".side", $"'{e.Side}' is not left or right"));
            }

            if (!TryParseRunoff(e.Runoff, out _))
            {
                problems.Add(new(TrackDetailSection.Edges, at + ".runoff", $"'{e.Runoff}' is not asphalt, grass or gravel"));
            }

            if (e.KerbM < 0.0 || e.KerbM > MaxKerbM)
            {
                problems.Add(new(TrackDetailSection.Edges, at + ".kerb_m", $"{N(e.KerbM)} m is outside 0-{N(MaxKerbM)} m"));
            }

            if (e.RunoffM < 0.0 || e.RunoffM > MaxRunoffM)
            {
                problems.Add(new(TrackDetailSection.Edges, at + ".runoff_m", $"{N(e.RunoffM)} m is outside 0-{N(MaxRunoffM)} m"));
            }

            if (rangeOk && sideOk)
            {
                if (e.From < e.To)
                {
                    intervals.Add((i, side, e.From, e.To));
                }
                else
                {
                    intervals.Add((i, side, e.From, 1.0));
                    intervals.Add((i, side, 0.0, e.To));
                }
            }
        }

        var reported = new HashSet<(int, int)>();
        for (var a = 0; a < intervals.Count; a++)
        {
            for (var b = a + 1; b < intervals.Count; b++)
            {
                var x = intervals[a];
                var y = intervals[b];
                if (x.Index == y.Index || x.Side != y.Side || !(x.From < y.To && y.From < x.To))
                {
                    continue;
                }

                var pair = (Math.Min(x.Index, y.Index), Math.Max(x.Index, y.Index));
                if (reported.Add(pair))
                {
                    problems.Add(new(
                        TrackDetailSection.Edges,
                        $"edges[{I(pair.Item2)}]",
                        $"overlaps edges[{I(pair.Item1)}] on the {(x.Side == TrackSide.Left ? "left" : "right")} side"));
                }
            }
        }
    }

    private static void CheckPitLane(TrackPitLaneFile pit, TrackGeometry? centreLine, double pointScale, List<TrackDetailProblem> problems)
    {
        const TrackDetailSection Pit = TrackDetailSection.PitLane;
        var points = new List<(double X, double Y)>(pit.Points.Count);
        for (var i = 0; i < pit.Points.Count; i++)
        {
            var p = pit.Points[i];
            if (p is null || p.Length != 2)
            {
                problems.Add(new(Pit, $"pit_lane.points[{I(i)}]", "must have exactly 2 coordinates [x, y]"));
                return;
            }

            points.Add((p[0], p[1]));
        }

        if (points.Count < 2)
        {
            problems.Add(new(Pit, "pit_lane.points", $"has {I(points.Count)} points, minimum is 2"));
            return;
        }

        var length = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            var d = Distance(points[i - 1], points[i]);
            length += d;
            if (d < MinPitPointGapM)
            {
                problems.Add(new(Pit, $"pit_lane.points[{I(i)}]", $"is {N(d)} m from the previous point, minimum is {N(MinPitPointGapM)} m"));
            }
        }

        var crossing = FindOpenPolylineCrossing(points);
        if (crossing is { } c)
        {
            problems.Add(new(Pit, "pit_lane.points", $"the lane crosses itself between segments {I(c.A)} and {I(c.B)}"));
        }

        var entryOk = CheckLapFraction(pit.EntryS, "pit_lane.entry_s", problems);
        var exitOk = CheckLapFraction(pit.ExitS, "pit_lane.exit_s", problems);
        if (entryOk && exitOk && pit.EntryS == pit.ExitS)
        {
            problems.Add(new(Pit, "pit_lane.exit_s", $"equals entry_s ({N(pit.EntryS)}); the lane must leave and rejoin at different points"));
            exitOk = false;
        }

        if (centreLine is not null)
        {
            if (entryOk)
            {
                CheckJoin(centreLine, pit.EntryS, Scale(points[0], pointScale), "pit_lane.points[0]", "entry_s", problems);
            }

            if (exitOk)
            {
                CheckJoin(centreLine, pit.ExitS, Scale(points[^1], pointScale), $"pit_lane.points[{I(points.Count - 1)}]", "exit_s", problems);
            }
        }

        if (pit.LimitFrom < 0.0)
        {
            problems.Add(new(Pit, "pit_lane.limit_from", $"{N(pit.LimitFrom)} m is before the start of the lane"));
        }

        if (pit.LimitTo > length)
        {
            problems.Add(new(Pit, "pit_lane.limit_to", $"{N(pit.LimitTo)} m is past the end of the lane ({N(length)} m)"));
        }

        var limitOk = pit.LimitTo > pit.LimitFrom;
        if (!limitOk)
        {
            problems.Add(new(Pit, "pit_lane.limit_to", $"{N(pit.LimitTo)} m is not after limit_from ({N(pit.LimitFrom)} m)"));
        }

        if (pit.LimitKph < MinPitLimitKph || pit.LimitKph > MaxPitLimitKph)
        {
            problems.Add(new(Pit, "pit_lane.limit_kph", $"{N(pit.LimitKph)} km/h is outside {N(MinPitLimitKph)}-{N(MaxPitLimitKph)} km/h"));
        }

        if (pit.Boxes.Count == 0)
        {
            problems.Add(new(Pit, "pit_lane.boxes", "is empty; a pit lane needs at least one garage"));
        }

        // Garages are checked against the limit section only when that section itself is valid.
        for (var i = 0; limitOk && i < pit.Boxes.Count; i++)
        {
            var b = pit.Boxes[i];
            if (b < pit.LimitFrom || b > pit.LimitTo)
            {
                problems.Add(new(Pit, $"pit_lane.boxes[{I(i)}]", $"{N(b)} m is outside the speed limit section {N(pit.LimitFrom)}-{N(pit.LimitTo)} m"));
            }
            else if (i > 0 && b <= pit.Boxes[i - 1])
            {
                problems.Add(new(Pit, $"pit_lane.boxes[{I(i)}]", $"{N(b)} m does not come after the previous garage at {N(pit.Boxes[i - 1])} m"));
            }
        }
    }

    private static bool CheckLapFraction(double s, string field, List<TrackDetailProblem> problems)
    {
        if (s >= 0.0 && s < 1.0)
        {
            return true;
        }

        problems.Add(new(TrackDetailSection.PitLane, field, $"{N(s)} is not a lap fraction in [0, 1)"));
        return false;
    }

    private static void CheckJoin(
        TrackGeometry centreLine,
        double lapFraction,
        (double X, double Y) lanePoint,
        string field,
        string joinName,
        List<TrackDetailProblem> problems)
    {
        var onLine = PointAt(centreLine, lapFraction);
        var d = Distance(onLine, lanePoint);
        if (d > PitLaneJoinToleranceM)
        {
            problems.Add(new(
                TrackDetailSection.PitLane,
                field,
                $"is {N(d)} m from the centre line at {joinName} {N(lapFraction)}, maximum is {N(PitLaneJoinToleranceM)} m"));
        }
    }

    /// <summary>The resampled centre line point nearest to a lap fraction.</summary>
    internal static (double X, double Y) PointAt(TrackGeometry geometry, double lapFraction)
    {
        var count = geometry.Samples.Count;
        var index = (int)Math.Round(lapFraction * count) % count;
        return geometry.Samples[index];
    }

    private static (int A, int B)? FindOpenPolylineCrossing(IReadOnlyList<(double X, double Y)> p)
    {
        for (var i = 0; i < p.Count - 1; i++)
        {
            for (var j = i + 2; j < p.Count - 1; j++)
            {
                if (SegmentsCross(p[i], p[i + 1], p[j], p[j + 1]))
                {
                    return (i, j);
                }
            }
        }

        return null;
    }

    private static bool SegmentsCross((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) d)
    {
        var d1 = Cross(c, d, a);
        var d2 = Cross(c, d, b);
        var d3 = Cross(a, b, c);
        var d4 = Cross(a, b, d);
        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    private static double Cross((double X, double Y) o, (double X, double Y) a, (double X, double Y) b) =>
        (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

    private static (double X, double Y) Scale((double X, double Y) p, double factor) => (p.X * factor, p.Y * factor);

    private static double Distance((double X, double Y) a, (double X, double Y) b) =>
        Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
