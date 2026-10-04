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

/// <summary>A layout's geometry together with its provenance.</summary>
public sealed record ResolvedTrackGeometry(
    string LayoutId,
    TrackGeometry Geometry,
    TrackGeometrySource Source,
    IReadOnlyList<TrackCorner> Corners);

/// <summary>
/// Resolves <c>layout_id</c> to <see cref="TrackGeometry"/>, scaled to the layout's <c>length_km</c> from
/// <c>circuits.json</c>. A layout without an authored file (or with one that cannot be built) never throws: it gets
/// the neutral fallback shape at the right length and <see cref="TrackGeometrySource.Fallback"/>, so a missing
/// geometry costs the map its shape but never breaks a race. <c>validate-authored</c> is where missing or broken
/// files are caught; the UI applies the same rule (<c>ui/prototype/js/track-shape.js</c>).
/// An unknown layout id is a programming error and throws <see cref="ArgumentException"/>.
/// </summary>
public sealed class TrackGeometryCatalog
{
    private readonly Dictionary<string, double> _lengthKmByLayout;
    private readonly Dictionary<string, TrackGeometryFile> _filesByLayout;
    private readonly ConcurrentDictionary<string, ResolvedTrackGeometry> _cache = new(StringComparer.Ordinal);

    private TrackGeometryCatalog(
        Dictionary<string, double> lengthKmByLayout,
        Dictionary<string, TrackGeometryFile> filesByLayout)
    {
        _lengthKmByLayout = lengthKmByLayout;
        _filesByLayout = filesByLayout;
    }

    public static TrackGeometryCatalog Create(AuthoredData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var lengths = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var circuit in data.Circuits.Circuits)
        {
            foreach (var layout in circuit.Layouts)
            {
                lengths.TryAdd(layout.LayoutId, layout.LengthKm);
            }
        }

        var files = new Dictionary<string, TrackGeometryFile>(StringComparer.Ordinal);
        foreach (var file in data.TrackGeometries)
        {
            files.TryAdd(file.LayoutId, file);
        }

        return new TrackGeometryCatalog(lengths, files);
    }

    /// <summary>True when the layout has an authored geometry file.</summary>
    public bool HasAuthored(string layoutId) => _filesByLayout.ContainsKey(layoutId);

    public ResolvedTrackGeometry Resolve(string layoutId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutId);
        if (!_lengthKmByLayout.ContainsKey(layoutId))
        {
            throw new ArgumentException($"Unknown layout '{layoutId}'.", nameof(layoutId));
        }

        return _cache.GetOrAdd(layoutId, Build);
    }

    private ResolvedTrackGeometry Build(string layoutId)
    {
        var lengthM = _lengthKmByLayout[layoutId] * 1000.0;
        if (_filesByLayout.TryGetValue(layoutId, out var file) && TryBuildAuthored(file, lengthM, out var resolved))
        {
            return resolved;
        }

        return new ResolvedTrackGeometry(layoutId, TrackGeometry.Fallback(lengthM), TrackGeometrySource.Fallback, []);
    }

    private static bool TryBuildAuthored(TrackGeometryFile file, double lengthM, out ResolvedTrackGeometry resolved)
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

        resolved = new ResolvedTrackGeometry(file.LayoutId, geometry, TrackGeometrySource.Authored, PlaceCorners(file, points, geometry));
        return true;
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
