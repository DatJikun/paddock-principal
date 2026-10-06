using System.Globalization;
using Paddock.Domain.Infrastructure;
using Paddock.Domain.World;
using Paddock.Domain.World.Tracks;

namespace Paddock.Data.Authored;

/// <summary>
/// Checks authored regulations, tracks, technologies, teams, staff, and eras.
/// Every violation is returned; the first failure does not stop the rest.
/// The covered window is 1950–2026. A timeline or lineage <c>to</c> of null stays open through 2026.
/// Number dimensions have no catalog value list (the files use both kilograms and the token "unknown"), so only
/// enum and bool dimensions are checked against that list.
/// Era and CPI sources must be absolute https URLs. <c>cpi_us.json</c> lists every year from 1950 through
/// <see cref="CpiBook.BaseYear"/> exactly once. <see cref="LastSeason"/> is allowed only when <c>partial</c> is true.
/// </summary>
public static partial class AuthoredDataValidator
{
    public const int FirstSeason = 1950;
    public const int LastSeason = 2026;
    public const double ProfileWeightTolerance = 0.01;

    public const string UnknownDimension = "unknown-dimension";
    public const string EnumValue = "enum-value";
    public const string Gap = "gap";
    public const string Overlap = "overlap";
    public const string InvertedPeriod = "inverted-period";
    public const string ProfileWeights = "profile-weights";
    public const string UnknownLayout = "unknown-layout";
    public const string EraUnknownDimension = "era-unknown-dimension";
    public const string EraEnumValue = "era-enum-value";
    public const string EraGap = "era-gap";
    public const string EraOverlap = "era-overlap";
    public const string EraInvertedPeriod = "era-inverted-period";
    public const string EraSourceUrl = "era-source-url";
    public const string EraCpiYear = "era-cpi-year";
    public const string EraCpiPartial = "era-cpi-partial";
    public const string GeometryUnknownLayout = "geometry-unknown-layout";
    public const string GeometryPointCount = "geometry-point-count";
    public const string GeometryPointsTooClose = "geometry-points-too-close";
    public const string GeometryLengthMismatch = "geometry-length-mismatch";
    public const string GeometrySelfIntersection = "geometry-self-intersection";
    public const string GeometryDuplicate = "geometry-duplicate";
    public const string GeometryCorner = "geometry-corner";
    public const string GeometrySharpBend = "geometry-sharp-bend";

    /// <summary>Largest turn allowed between two consecutive 2 m path segments (about 4.6 m radius), in degrees. A cusp or loop exceeds it.</summary>
    private const double MaxBendPer2MDegrees = 25.0;

    public static IReadOnlyList<AuthoredDataError> Validate(AuthoredData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var errors = new List<AuthoredDataError>();
        var catalogById = IndexCatalog(data.Catalog);

        foreach (var period in data.Timeline)
        {
            if (!catalogById.ContainsKey(period.Dimension))
            {
                errors.Add(new AuthoredDataError(
                    UnknownDimension,
                    $"timeline period for '{period.Dimension}' ({Span(period.From, period.To)}) uses a dimension that is not in the catalog"));
            }
        }

        foreach (var period in data.Timeline)
        {
            if (!catalogById.TryGetValue(period.Dimension, out var dimension))
            {
                continue;
            }

            if (dimension.Type is not ("enum" or "bool"))
            {
                continue;
            }

            if (ContainsOrdinal(dimension.Values, period.Value))
            {
                continue;
            }

            errors.Add(new AuthoredDataError(
                EnumValue,
                $"dimension '{period.Dimension}' value '{period.Value}' in {Span(period.From, period.To)} is not in the catalog value list"));
        }

        var periodsByDimension = GroupPeriods(data.Timeline);
        foreach (var dimension in data.Catalog)
        {
            periodsByDimension.TryGetValue(dimension.Id, out var periods);
            AppendCoverage(errors, dimension.Id, periods ?? [], InvertedPeriod, Gap, Overlap, "dimension");
        }

        foreach (var circuit in data.Circuits.Circuits)
        {
            foreach (var layout in circuit.Layouts)
            {
                var sum = layout.ProfileGuess.Straights
                    + layout.ProfileGuess.HighSpeed
                    + layout.ProfileGuess.LowSpeed
                    + layout.ProfileGuess.Braking;
                if (Math.Abs(sum - 1d) > ProfileWeightTolerance)
                {
                    errors.Add(new AuthoredDataError(
                        ProfileWeights,
                        $"layout '{layout.LayoutId}' profile weights sum to {sum.ToString("0.000", CultureInfo.InvariantCulture)}, expected 1 +/- {ProfileWeightTolerance.ToString("0.00", CultureInfo.InvariantCulture)}"));
                }
            }
        }

        var layoutIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var circuit in data.Circuits.Circuits)
        {
            foreach (var layout in circuit.Layouts)
            {
                layoutIds.Add(layout.LayoutId);
            }
        }

        foreach (var entry in data.RaceLayoutMap)
        {
            if (layoutIds.Contains(entry.LayoutId))
            {
                continue;
            }

            errors.Add(new AuthoredDataError(
                UnknownLayout,
                $"race {entry.Season.ToString(CultureInfo.InvariantCulture)} round {entry.Round.ToString(CultureInfo.InvariantCulture)} points at missing layout '{entry.LayoutId}'"));
        }

        AppendTechnologiesTeamsAndStaff(errors, data);
        AppendEraErrors(errors, data);
        AppendTrackGeometryErrors(errors, data);
        AppendFacilities(errors, data);
        return errors;
    }

    public const string FacilityKind = "facility-kind";

    public const string FacilityQuality = "facility-quality";

    private static void AppendFacilities(List<AuthoredDataError> errors, AuthoredData data)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var spec in data.Facilities.Kinds)
        {
            var id = FacilityKindIds.Of(spec.Kind);
            if (!seen.Add(id))
            {
                errors.Add(new AuthoredDataError(FacilityKind, $"facility kind '{id}' appears twice"));
            }

            if (spec.UnlockYear < FirstSeason)
            {
                errors.Add(new AuthoredDataError(
                    FacilityKind,
                    $"facility kind '{id}' unlocks in {spec.UnlockYear.ToString(CultureInfo.InvariantCulture)}, before {FirstSeason.ToString(CultureInfo.InvariantCulture)}"));
            }
        }

        foreach (var (kind, milli) in data.Facilities.DefaultQualityMilli)
        {
            if (milli is < 0 or > InfrastructureEstimates.MaxQualityMilli)
            {
                errors.Add(new AuthoredDataError(
                    FacilityQuality,
                    $"default quality of '{FacilityKindIds.Of(kind)}' is {milli.ToString(CultureInfo.InvariantCulture)}, out of 0..{InfrastructureEstimates.MaxQualityMilli.ToString(CultureInfo.InvariantCulture)}"));
            }
        }

        foreach (var (team, qualities) in data.Facilities.TeamQualityMilli)
        {
            foreach (var (kind, milli) in qualities)
            {
                if (milli is < 0 or > InfrastructureEstimates.MaxQualityMilli)
                {
                    errors.Add(new AuthoredDataError(
                        FacilityQuality,
                        $"team '{team}' quality of '{FacilityKindIds.Of(kind)}' is {milli.ToString(CultureInfo.InvariantCulture)}, out of range"));
                }
            }
        }
    }

    private static Dictionary<string, CatalogDimension> IndexCatalog(IReadOnlyList<CatalogDimension> catalog)
    {
        var catalogById = new Dictionary<string, CatalogDimension>(catalog.Count, StringComparer.Ordinal);
        foreach (var dimension in catalog)
        {
            catalogById.TryAdd(dimension.Id, dimension);
        }

        return catalogById;
    }

    private static Dictionary<string, List<TimelinePeriod>> GroupPeriods(IReadOnlyList<TimelinePeriod> timeline)
    {
        var periodsByDimension = new Dictionary<string, List<TimelinePeriod>>(StringComparer.Ordinal);
        foreach (var period in timeline)
        {
            if (!periodsByDimension.TryGetValue(period.Dimension, out var periods))
            {
                periods = [];
                periodsByDimension.Add(period.Dimension, periods);
            }

            periods.Add(period);
        }

        return periodsByDimension;
    }

    private static void AppendEraErrors(List<AuthoredDataError> errors, AuthoredData data)
    {
        var catalogById = IndexCatalog(data.EraCatalog);

        foreach (var period in data.EraTimeline)
        {
            if (!catalogById.ContainsKey(period.Dimension))
            {
                errors.Add(new AuthoredDataError(
                    EraUnknownDimension,
                    $"era timeline period for '{period.Dimension}' ({Span(period.From, period.To)}) uses a dimension that is not in the era catalog"));
            }
        }

        foreach (var period in data.EraTimeline)
        {
            if (!catalogById.TryGetValue(period.Dimension, out var dimension))
            {
                continue;
            }

            if (dimension.Type is not ("enum" or "bool"))
            {
                continue;
            }

            if (ContainsOrdinal(dimension.Values, period.Value))
            {
                continue;
            }

            errors.Add(new AuthoredDataError(
                EraEnumValue,
                $"era dimension '{period.Dimension}' value '{period.Value}' in {Span(period.From, period.To)} is not in the era catalog value list"));
        }

        var periodsByDimension = GroupPeriods(data.EraTimeline);
        foreach (var dimension in data.EraCatalog)
        {
            periodsByDimension.TryGetValue(dimension.Id, out var periods);
            AppendCoverage(errors, dimension.Id, periods ?? [], EraInvertedPeriod, EraGap, EraOverlap, "era dimension");
        }

        foreach (var period in data.EraTimeline)
        {
            if (IsAbsoluteHttps(period.Source))
            {
                continue;
            }

            errors.Add(new AuthoredDataError(
                EraSourceUrl,
                $"era timeline period for '{period.Dimension}' ({Span(period.From, period.To)}) source '{period.Source}' is not an absolute https URL"));
        }

        foreach (var row in data.CpiYears)
        {
            if (IsAbsoluteHttps(row.Source))
            {
                continue;
            }

            errors.Add(new AuthoredDataError(
                EraSourceUrl,
                $"cpi year {row.Year.ToString(CultureInfo.InvariantCulture)} source '{row.Source}' is not an absolute https URL"));
        }

        AppendCpiYears(errors, data.CpiYears);
    }

    private static void AppendCpiYears(List<AuthoredDataError> errors, IReadOnlyList<CpiYear> years)
    {
        var counts = new Dictionary<int, int>();
        foreach (var row in years)
        {
            counts.TryGetValue(row.Year, out var count);
            counts[row.Year] = count + 1;
        }

        AppendMissingCpiYears(errors, counts);

        var duplicateYears = new List<int>();
        var outsideYears = new List<int>();
        foreach (var (year, count) in counts)
        {
            if (year >= FirstSeason && year <= CpiBook.BaseYear)
            {
                if (count > 1)
                {
                    duplicateYears.Add(year);
                }

                continue;
            }

            if (year == LastSeason && year > CpiBook.BaseYear)
            {
                if (count > 1)
                {
                    duplicateYears.Add(year);
                }

                continue;
            }

            outsideYears.Add(year);
        }

        duplicateYears.Sort();
        outsideYears.Sort();

        foreach (var year in duplicateYears)
        {
            errors.Add(new AuthoredDataError(
                EraCpiYear,
                $"cpi_us.json lists {year.ToString(CultureInfo.InvariantCulture)} more than once"));
        }

        foreach (var year in outsideYears)
        {
            errors.Add(new AuthoredDataError(
                EraCpiYear,
                $"cpi_us.json lists {year.ToString(CultureInfo.InvariantCulture)}, outside {FirstSeason.ToString(CultureInfo.InvariantCulture)}-{LastSeason.ToString(CultureInfo.InvariantCulture)}"));
        }

        foreach (var row in years)
        {
            if (row.Year != LastSeason || row.Year <= CpiBook.BaseYear || row.Partial)
            {
                continue;
            }

            errors.Add(new AuthoredDataError(
                EraCpiPartial,
                $"cpi_us.json year {row.Year.ToString(CultureInfo.InvariantCulture)} must have partial set to true"));
        }
    }

    private static void AppendMissingCpiYears(List<AuthoredDataError> errors, Dictionary<int, int> counts)
    {
        var rangeStart = -1;
        for (var year = FirstSeason; year <= CpiBook.BaseYear + 1; year++)
        {
            var missing = year <= CpiBook.BaseYear && !counts.ContainsKey(year);
            if (missing)
            {
                if (rangeStart < 0)
                {
                    rangeStart = year;
                }

                continue;
            }

            if (rangeStart < 0)
            {
                continue;
            }

            var from = rangeStart;
            var to = year - 1;
            var span = from == to
                ? from.ToString(CultureInfo.InvariantCulture)
                : $"{from.ToString(CultureInfo.InvariantCulture)}-{to.ToString(CultureInfo.InvariantCulture)}";
            errors.Add(new AuthoredDataError(EraCpiYear, $"cpi_us.json does not list {span}"));
            rangeStart = -1;
        }
    }

    private static void AppendCoverage(
        List<AuthoredDataError> errors,
        string dimensionId,
        List<TimelinePeriod> periods,
        string invertedCode,
        string gapCode,
        string overlapCode,
        string subject)
    {
        var counts = new int[LastSeason - FirstSeason + 1];
        foreach (var period in periods)
        {
            if (period.To is int toYear && period.From > toYear)
            {
                errors.Add(new AuthoredDataError(
                    invertedCode,
                    $"{subject} '{dimensionId}' period {period.From.ToString(CultureInfo.InvariantCulture)}-{toYear.ToString(CultureInfo.InvariantCulture)} ends before it starts"));
                continue;
            }

            var end = period.To ?? LastSeason;
            var start = Math.Max(FirstSeason, period.From);
            var stop = Math.Min(LastSeason, end);
            if (start > stop)
            {
                continue;
            }

            for (var year = start; year <= stop; year++)
            {
                counts[year - FirstSeason]++;
            }
        }

        AppendRanges(errors, dimensionId, counts, missing: true, gapCode, subject);
        AppendRanges(errors, dimensionId, counts, missing: false, overlapCode, subject);
    }

    private static void AppendRanges(
        List<AuthoredDataError> errors,
        string dimensionId,
        int[] counts,
        bool missing,
        string code,
        string subject)
    {
        var rangeStart = -1;
        for (var index = 0; index <= counts.Length; index++)
        {
            var inRange = index < counts.Length && (missing ? counts[index] == 0 : counts[index] > 1);
            if (inRange)
            {
                if (rangeStart < 0)
                {
                    rangeStart = index;
                }

                continue;
            }

            if (rangeStart < 0)
            {
                continue;
            }

            var from = FirstSeason + rangeStart;
            var to = FirstSeason + index - 1;
            var span = from == to
                ? from.ToString(CultureInfo.InvariantCulture)
                : $"{from.ToString(CultureInfo.InvariantCulture)}-{to.ToString(CultureInfo.InvariantCulture)}";
            errors.Add(missing
                ? new AuthoredDataError(code, $"{subject} '{dimensionId}' does not cover {span}")
                : new AuthoredDataError(code, $"{subject} '{dimensionId}' overlaps in {span}"));
            rangeStart = -1;
        }
    }

    private static bool ContainsOrdinal(IReadOnlyList<string>? values, string candidate)
    {
        if (values is null)
        {
            return false;
        }

        foreach (var value in values)
        {
            if (string.Equals(value, candidate, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string Span(int from, int? to)
    {
        if (to is null)
        {
            return $"{from.ToString(CultureInfo.InvariantCulture)}-open";
        }

        if (from == to.Value)
        {
            return from.ToString(CultureInfo.InvariantCulture);
        }

        return $"{from.ToString(CultureInfo.InvariantCulture)}-{to.Value.ToString(CultureInfo.InvariantCulture)}";
    }

    private static void AppendTrackGeometryErrors(List<AuthoredDataError> errors, AuthoredData data)
    {
        var layoutById = new Dictionary<string, CircuitLayout>(StringComparer.Ordinal);
        foreach (var circuit in data.Circuits.Circuits)
        {
            foreach (var layout in circuit.Layouts)
            {
                layoutById.TryAdd(layout.LayoutId, layout);
            }
        }

        var seenGeometry = new HashSet<string>(StringComparer.Ordinal);
        foreach (var geom in data.TrackGeometries)
        {
            if (!seenGeometry.Add(geom.LayoutId))
            {
                errors.Add(new AuthoredDataError(
                    GeometryDuplicate,
                    $"track geometry for layout '{geom.LayoutId}' is defined more than once"));
            }

            AppendTrackCornerErrors(errors, geom);

            var hasLayout = layoutById.TryGetValue(geom.LayoutId, out var layout);
            if (!hasLayout)
            {
                errors.Add(new AuthoredDataError(
                    GeometryUnknownLayout,
                    $"track geometry references unknown layout '{geom.LayoutId}'"));
            }

            if (geom.ControlPoints.Count < 8)
            {
                errors.Add(new AuthoredDataError(
                    GeometryPointCount,
                    $"track geometry '{geom.LayoutId}' has {geom.ControlPoints.Count.ToString(CultureInfo.InvariantCulture)} control points, minimum is 8"));
            }

            var points = new List<(double X, double Y)>(geom.ControlPoints.Count);
            var invalidPointFormat = false;
            for (var i = 0; i < geom.ControlPoints.Count; i++)
            {
                var pt = geom.ControlPoints[i];
                if (pt is null || pt.Length != 2)
                {
                    errors.Add(new AuthoredDataError(
                        GeometryPointCount,
                        $"track geometry '{geom.LayoutId}' point {i.ToString(CultureInfo.InvariantCulture)} must have exactly 2 coordinates"));
                    invalidPointFormat = true;
                    break;
                }

                points.Add((pt[0], pt[1]));
            }

            if (invalidPointFormat)
            {
                continue;
            }

            for (var i = 0; i < points.Count; i++)
            {
                var next = (i + 1) % points.Count;
                var dx = points[next].X - points[i].X;
                var dy = points[next].Y - points[i].Y;
                var dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < 1.0)
                {
                    errors.Add(new AuthoredDataError(
                        GeometryPointsTooClose,
                        $"track geometry '{geom.LayoutId}' consecutive points {i.ToString(CultureInfo.InvariantCulture)} and {next.ToString(CultureInfo.InvariantCulture)} are {dist.ToString("0.00", CultureInfo.InvariantCulture)} m apart, minimum is 1.0 m"));
                }
            }

            if (points.Count < 3)
            {
                continue;
            }

            TrackGeometry rawGeometry;
            try
            {
                rawGeometry = TrackGeometry.Build(points, referenceLengthM: null, stepM: 2.0);
            }
            catch (Exception ex)
            {
                errors.Add(new AuthoredDataError(
                    GeometryPointCount,
                    $"track geometry '{geom.LayoutId}' failed to build: {ex.Message}"));
                continue;
            }

            if (hasLayout && layout is not null)
            {
                var refLengthM = layout.LengthKm * 1000.0;
                var diff = Math.Abs(rawGeometry.LengthM - refLengthM);
                if (diff > 0.15 * refLengthM)
                {
                    errors.Add(new AuthoredDataError(
                        GeometryLengthMismatch,
                        $"track geometry '{geom.LayoutId}' raw length {rawGeometry.LengthM.ToString("0.0", CultureInfo.InvariantCulture)} m is not within +/-15% of circuits.json length {refLengthM.ToString("0.0", CultureInfo.InvariantCulture)} m"));
                }
            }

            if (HasSelfIntersections(rawGeometry.Samples))
            {
                errors.Add(new AuthoredDataError(
                    GeometrySelfIntersection,
                    $"track geometry '{geom.LayoutId}' resampled path intersects itself"));
            }

            var kinkAt = FindSharpBend(rawGeometry.Samples);
            if (kinkAt >= 0)
            {
                errors.Add(new AuthoredDataError(
                    GeometrySharpBend,
                    $"track geometry '{geom.LayoutId}' bends more than {MaxBendPer2MDegrees.ToString("0", CultureInfo.InvariantCulture)} degrees within 2 m near sample {kinkAt.ToString(CultureInfo.InvariantCulture)} (a kink or a cusp, check control points that double back)"));
            }
        }
    }

    private static void AppendTrackCornerErrors(List<AuthoredDataError> errors, TrackGeometryFile geom)
    {
        if (geom.Corners is null)
        {
            return;
        }

        var usedPoints = new HashSet<int>();
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < geom.Corners.Count; i++)
        {
            var corner = geom.Corners[i];
            if (corner.Point < 0 || corner.Point >= geom.ControlPoints.Count)
            {
                errors.Add(new AuthoredDataError(
                    GeometryCorner,
                    $"track geometry '{geom.LayoutId}' corner {i.ToString(CultureInfo.InvariantCulture)} points at control point {corner.Point.ToString(CultureInfo.InvariantCulture)}, outside 0..{(geom.ControlPoints.Count - 1).ToString(CultureInfo.InvariantCulture)}"));
            }
            else if (!usedPoints.Add(corner.Point))
            {
                errors.Add(new AuthoredDataError(
                    GeometryCorner,
                    $"track geometry '{geom.LayoutId}' has two corners on control point {corner.Point.ToString(CultureInfo.InvariantCulture)}"));
            }

            if (string.IsNullOrWhiteSpace(corner.Name))
            {
                errors.Add(new AuthoredDataError(
                    GeometryCorner,
                    $"track geometry '{geom.LayoutId}' corner {i.ToString(CultureInfo.InvariantCulture)} has an empty name"));
            }
            else if (!usedNames.Add(corner.Name))
            {
                errors.Add(new AuthoredDataError(
                    GeometryCorner,
                    $"track geometry '{geom.LayoutId}' has two corners named '{corner.Name}'"));
            }
        }
    }

    /// <summary>Index of the first sample where the path turns sharper than the allowed bend per segment, or -1.</summary>
    private static int FindSharpBend(IReadOnlyList<(double X, double Y)> samples)
    {
        var count = samples.Count;
        var minCos = Math.Cos(MaxBendPer2MDegrees * Math.PI / 180.0);
        for (var i = 0; i < count; i++)
        {
            var a = samples[i];
            var b = samples[(i + 1) % count];
            var c = samples[(i + 2) % count];
            var ux = b.X - a.X;
            var uy = b.Y - a.Y;
            var vx = c.X - b.X;
            var vy = c.Y - b.Y;
            var lengths = Math.Sqrt(ux * ux + uy * uy) * Math.Sqrt(vx * vx + vy * vy);
            if (lengths < 1e-9)
            {
                continue;
            }

            if ((ux * vx + uy * vy) / lengths < minCos)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool HasSelfIntersections(IReadOnlyList<(double X, double Y)> samples)
    {
        var count = samples.Count;
        if (count < 4)
        {
            return false;
        }

        for (var i = 0; i < count; i++)
        {
            var a = samples[i];
            var b = samples[(i + 1) % count];
            var minAx = Math.Min(a.X, b.X);
            var maxAx = Math.Max(a.X, b.X);
            var minAy = Math.Min(a.Y, b.Y);
            var maxAy = Math.Max(a.Y, b.Y);

            for (var j = i + 1; j < count; j++)
            {
                if (j == i + 1 || (i == 0 && j == count - 1))
                {
                    continue;
                }

                var c = samples[j];
                var d = samples[(j + 1) % count];

                if (maxAx < Math.Min(c.X, d.X) || minAx > Math.Max(c.X, d.X) ||
                    maxAy < Math.Min(c.Y, d.Y) || minAy > Math.Max(c.Y, d.Y))
                {
                    continue;
                }

                if (SegmentsIntersectProperly(a, b, c, d))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SegmentsIntersectProperly(
        (double X, double Y) a,
        (double X, double Y) b,
        (double X, double Y) c,
        (double X, double Y) d)
    {
        static double Cross((double X, double Y) p, (double X, double Y) q, (double X, double Y) r) =>
            (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);

        var cp1 = Cross(a, b, c);
        var cp2 = Cross(a, b, d);
        var cp3 = Cross(c, d, a);
        var cp4 = Cross(c, d, b);

        const double eps = 1e-9;
        return ((cp1 > eps && cp2 < -eps) || (cp1 < -eps && cp2 > eps)) &&
               ((cp3 > eps && cp4 < -eps) || (cp3 < -eps && cp4 > eps));
    }
}
