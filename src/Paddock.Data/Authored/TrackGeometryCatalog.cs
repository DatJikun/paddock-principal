using System.Collections.Concurrent;
using Paddock.Domain.World.Tracks;

namespace Paddock.Data.Authored;

/// <summary>Where a resolved geometry came from.</summary>
public enum TrackGeometrySource
{
    /// <summary>Built from the layout's file in <c>data/authored/tracks/geometry</c>.</summary>
    Authored,

    /// <summary>No usable file: the neutral stadium of <see cref="TrackGeometry.Fallback"/> at the layout's length.</summary>
    Fallback,
}

/// <summary>A named corner placed on the lap, as a fraction of lap length from the start line.</summary>
public sealed record TrackCorner(string Name, double LapFraction);

/// <summary>
/// A layout's geometry together with its provenance and the v2 details (width, edges, pit lane, elevation; PP-073).
/// <see cref="Detail"/> always exists: whatever the file does not describe resolves to the era defaults.
/// </summary>
public sealed record ResolvedTrackGeometry(
    string LayoutId,
    TrackGeometry Geometry,
    TrackGeometrySource Source,
    IReadOnlyList<TrackCorner> Corners,
    TrackDetail Detail);

/// <summary>
/// Resolves <c>layout_id</c> to <see cref="TrackGeometry"/>, scaled to the layout's <c>length_km</c> from
/// <c>circuits.json</c>. A layout without an authored file (or with one that cannot be built) never throws: it gets
/// the neutral fallback shape at the right length and <see cref="TrackGeometrySource.Fallback"/>, so a missing
/// geometry costs the map its shape but never breaks a race. <c>validate-authored</c> is where missing or broken
/// files are caught; the UI applies the same rule (<c>ui/prototype/js/track-shape.js</c>).
/// An unknown layout id is a programming error and throws <see cref="ArgumentException"/>.
/// </summary>
public sealed class TrackGeometryCatalog : ITrackGeometrySource
{
    private readonly Dictionary<string, double> _lengthKmByLayout;
    private readonly Dictionary<string, CircuitLayout> _layouts;
    private readonly Dictionary<string, TrackGeometryFile> _filesByLayout;
    private readonly ConcurrentDictionary<string, ResolvedTrackGeometry> _cache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string LayoutId, int EraYear), ResolvedTrackGeometry> _seasonCache = new();

    private TrackGeometryCatalog(
        Dictionary<string, double> lengthKmByLayout,
        Dictionary<string, CircuitLayout> layouts,
        Dictionary<string, TrackGeometryFile> filesByLayout)
    {
        _lengthKmByLayout = lengthKmByLayout;
        _layouts = layouts;
        _filesByLayout = filesByLayout;
    }

    public static TrackGeometryCatalog Create(AuthoredData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var lengths = new Dictionary<string, double>(StringComparer.Ordinal);
        var layouts = new Dictionary<string, CircuitLayout>(StringComparer.Ordinal);
        foreach (var circuit in data.Circuits.Circuits)
        {
            foreach (var layout in circuit.Layouts)
            {
                lengths.TryAdd(layout.LayoutId, layout.LengthKm);
                layouts.TryAdd(layout.LayoutId, layout);
            }
        }

        var files = new Dictionary<string, TrackGeometryFile>(StringComparer.Ordinal);
        foreach (var file in data.TrackGeometries)
        {
            files.TryAdd(file.LayoutId, file);
        }

        return new TrackGeometryCatalog(lengths, layouts, files);
    }

    /// <summary>
    /// The season a season-free <see cref="Resolve(string)"/> takes its era defaults from: the layout's LAST F1 season,
    /// because the map and the editor should show the layout as it looked most recently (a 1972 layout still raced in
    /// 2010 is drawn with 2010 run-off). The 1950 row's year when the list is empty. Races use
    /// <see cref="Resolve(string, int)"/> with the season actually raced (PP-073).
    /// </summary>
    public static int LastSeason(CircuitLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return layout.YearsUsedInF1.Count == 0 ? TrackEraDefaults.Table[0].FromYear : layout.YearsUsedInF1.Max();
    }

    /// <summary>
    /// Every era row a layout is raced in. An authored width must fit the band of each of them, because the same
    /// file serves every season (validator and catalog use the same list, so they agree on what is broken).
    /// </summary>
    public static IReadOnlyList<TrackEraDefault> ErasOf(CircuitLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return layout.YearsUsedInF1.Count == 0
            ? [TrackEraDefaults.Table[0]]
            : layout.YearsUsedInF1.Select(TrackEraDefaults.For).Distinct().OrderBy(e => e.FromYear).ToList();
    }

    /// <summary>True when the layout has an authored geometry file.</summary>
    public bool HasAuthored(string layoutId) => _filesByLayout.ContainsKey(layoutId);

    /// <summary>The geometry <see cref="Resolve(string)"/> gives (the fallback shape when there is no file); null for an unknown layout.</summary>
    public TrackGeometry? GeometryOf(string layoutId) =>
        !string.IsNullOrWhiteSpace(layoutId) && _lengthKmByLayout.ContainsKey(layoutId) ? Resolve(layoutId).Geometry : null;

    /// <summary>
    /// The layout with era defaults of its last F1 season (<see cref="LastSeason"/>): for the map and the editor,
    /// which show a layout without a season. A race resolves with <see cref="Resolve(string, int)"/>.
    /// </summary>
    public ResolvedTrackGeometry Resolve(string layoutId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutId);
        if (!_lengthKmByLayout.ContainsKey(layoutId))
        {
            throw new ArgumentException($"Unknown layout '{layoutId}'.", nameof(layoutId));
        }

        return _cache.GetOrAdd(layoutId, Build);
    }

    /// <summary>
    /// The layout as raced in <paramref name="season"/>: same centre line, corners and authored fields as
    /// <see cref="Resolve(string)"/>, but whatever the file does not describe takes the defaults of that season's era
    /// (PP-073: a 1972 layout raced in 2010 gets 2010 width and run-off).
    /// </summary>
    public ResolvedTrackGeometry Resolve(string layoutId, int season)
    {
        var baseline = Resolve(layoutId);
        var era = TrackEraDefaults.For(season);
        if (ReferenceEquals(era, baseline.Detail.EraDefault))
        {
            return baseline;
        }

        return _seasonCache.GetOrAdd((layoutId, era.FromYear), key => baseline with
        {
            Detail = baseline.Source == TrackGeometrySource.Authored
                ? BuildDetail(_filesByLayout[key.LayoutId], era, ErasOf(_layouts[key.LayoutId]), baseline.Geometry)
                : TrackDetail.Defaults(era),
        });
    }

    private ResolvedTrackGeometry Build(string layoutId)
    {
        var lengthM = _lengthKmByLayout[layoutId] * 1000.0;
        var layout = _layouts[layoutId];
        var era = TrackEraDefaults.For(LastSeason(layout));
        if (_filesByLayout.TryGetValue(layoutId, out var file) && TryBuildAuthored(file, lengthM, era, ErasOf(layout), out var resolved))
        {
            return resolved;
        }

        return new ResolvedTrackGeometry(
            layoutId,
            TrackGeometry.Fallback(lengthM),
            TrackGeometrySource.Fallback,
            [],
            TrackDetail.Defaults(era));
    }

    private static bool TryBuildAuthored(
        TrackGeometryFile file,
        double lengthM,
        TrackEraDefault era,
        IReadOnlyList<TrackEraDefault> racedEras,
        out ResolvedTrackGeometry resolved)
    {
        resolved = null!;
        if (file.ControlPoints.Count < 3 || file.ControlPoints.Any(p => p is null || p.Length != 2))
        {
            return false;
        }

        var points = file.ControlPoints.Select(p => (X: p[0], Y: p[1])).ToArray();
        TrackGeometry geometry;
        try
        {
            geometry = TrackGeometry.Build(points, lengthM);
        }
        catch (ArgumentException)
        {
            return false;
        }

        resolved = new ResolvedTrackGeometry(
            file.LayoutId,
            geometry,
            TrackGeometrySource.Authored,
            PlaceCorners(file, points, geometry),
            BuildDetail(file, era, racedEras, geometry));
        return true;
    }

    /// <summary>
    /// Resolves the v2 fields. A section that breaks any rule of <see cref="TrackDetailRules"/> is dropped as a whole
    /// and falls back to the era defaults (the validator reports it); the others still apply. Pit lane coordinates and
    /// lane distances are scaled with the centre line so the lane stays attached to it. Which sections are broken
    /// does not depend on the season: width is checked against every era the layout is raced in.
    /// </summary>
    private static TrackDetail BuildDetail(
        TrackGeometryFile file,
        TrackEraDefault era,
        IReadOnlyList<TrackEraDefault> racedEras,
        TrackGeometry geometry)
    {
        var scale = geometry.ScaleFactor;
        var broken = TrackDetailRules.Check(file, racedEras, geometry, scale).Select(p => p.Section).ToHashSet();

        var width = file.WidthM is null || broken.Contains(TrackDetailSection.Width)
            ? []
            : file.WidthM.Select(p => new TrackProfilePoint(p.S, p.W)).ToList();

        var edges = new List<TrackEdgeSpan>();
        if (file.Edges is not null && !broken.Contains(TrackDetailSection.Edges))
        {
            foreach (var e in file.Edges)
            {
                TrackDetailRules.TryParseSide(e.Side, out var side);
                TrackDetailRules.TryParseRunoff(e.Runoff, out var runoff);
                edges.Add(new TrackEdgeSpan(e.From, e.To, side, new TrackEdge(e.KerbM, runoff, e.RunoffM, e.Barrier)));
            }
        }

        TrackPitLane? pitLane = null;
        if (file.PitLane is { } pit && !broken.Contains(TrackDetailSection.PitLane))
        {
            pitLane = new TrackPitLane(
                pit.Points.Select(p => (p[0] * scale, p[1] * scale)).ToList(),
                pit.EntryS,
                pit.ExitS,
                pit.LimitFrom * scale,
                pit.LimitTo * scale,
                pit.LimitKph,
                pit.Boxes.Select(b => b * scale).ToList());
        }

        var elevation = file.ElevationM is null || broken.Contains(TrackDetailSection.Elevation)
            ? []
            : file.ElevationM.Select(p => new TrackProfilePoint(p.S, p.H)).ToList();

        return new TrackDetail(era, width, edges, pitLane, elevation);
    }

    /// <summary>
    /// A corner's lap fraction is the fraction of the resampled sample nearest to its (scaled) control point.
    /// Corners that point outside the file are skipped; the validator reports them.
    /// </summary>
    private static List<TrackCorner> PlaceCorners(
        TrackGeometryFile file,
        IReadOnlyList<(double X, double Y)> points,
        TrackGeometry geometry)
    {
        var corners = new List<TrackCorner>();
        if (file.Corners is null)
        {
            return corners;
        }

        var samples = geometry.Samples;
        foreach (var corner in file.Corners)
        {
            if (corner.Point < 0 || corner.Point >= points.Count || string.IsNullOrWhiteSpace(corner.Name))
            {
                continue;
            }

            var x = points[corner.Point].X * geometry.ScaleFactor;
            var y = points[corner.Point].Y * geometry.ScaleFactor;
            var best = 0;
            var bestD = double.PositiveInfinity;
            for (var i = 0; i < samples.Count; i++)
            {
                var d = (samples[i].X - x) * (samples[i].X - x) + (samples[i].Y - y) * (samples[i].Y - y);
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }

            corners.Add(new TrackCorner(corner.Name, (double)best / samples.Count));
        }

        corners.Sort((a, b) => a.LapFraction.CompareTo(b.LapFraction));
        return corners;
    }
}
