using System.Globalization;
using Paddock.Data.Authored;
using Paddock.Domain.World.Tracks;

namespace Paddock.Tests.Authored;

/// <summary>
/// Quality gates on the geometry files that are actually committed in <c>data/authored/tracks/geometry</c>
/// (the other test classes use synthetic fixtures). Adding a file puts it under all of these automatically.
/// </summary>
public class TrackGeometryAuthoredDataTests
{
    /// <summary>The MVP starts in 1955 (PP-050): every layout raced that season must be drawable.</summary>
    private const int StartSeason = 1955;

    private static readonly Lazy<AuthoredData> Data = new(
        () => AuthoredDataLoader.Load(Path.Combine(RepoPaths.Root(), "data")));

    private static IReadOnlyDictionary<string, double> LengthKmByLayout() => Data.Value.Circuits.Circuits
        .SelectMany(c => c.Layouts)
        .ToDictionary(l => l.LayoutId, l => l.LengthKm, StringComparer.Ordinal);

    [Fact]
    public void CommittedGeometry_PassesEveryGeometryRuleOfTheValidator()
    {
        var errors = AuthoredDataValidator.Validate(Data.Value)
            .Where(e => e.Code.StartsWith("geometry-", StringComparison.Ordinal))
            .Select(e => $"{e.Code}: {e.Message}")
            .ToArray();

        Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors));
        Assert.NotEmpty(Data.Value.TrackGeometries);
    }

    [Fact]
    public void EveryFile_IsNamedAfterItsLayout_AndEveryLayoutExists()
    {
        var dir = Path.Combine(RepoPaths.Root(), "data", "authored", "tracks", "geometry");
        var files = Directory.GetFiles(dir, "*.json");
        var lengths = LengthKmByLayout();

        Assert.Equal(files.Length, Data.Value.TrackGeometries.Count);
        foreach (var geometry in Data.Value.TrackGeometries)
        {
            Assert.True(File.Exists(Path.Combine(dir, geometry.LayoutId + ".json")), $"{geometry.LayoutId}.json: file name must be <layout_id>.json");
            Assert.True(lengths.ContainsKey(geometry.LayoutId), $"{geometry.LayoutId} is not a layout in circuits.json");
        }
    }

    [Fact]
    public void EveryLayoutRacedInTheStartSeason_HasAuthoredGeometry()
    {
        var catalog = TrackGeometryCatalog.Create(Data.Value);
        var missing = Data.Value.RaceLayoutMap
            .Where(e => e.Season == StartSeason)
            .Select(e => e.LayoutId)
            .Distinct(StringComparer.Ordinal)
            .Where(id => !catalog.HasAuthored(id))
            .ToArray();

        Assert.Empty(missing);
        Assert.Equal(7, Data.Value.RaceLayoutMap.Where(e => e.Season == StartSeason).Select(e => e.LayoutId).Distinct().Count());
    }

    [Fact]
    public void EveryLayoutRacedFrom1950To1960_HasAuthoredGeometry()
    {
        var catalog = TrackGeometryCatalog.Create(Data.Value);
        var missing = Data.Value.RaceLayoutMap
            .Where(e => e.Season is >= 1950 and <= 1960)
            .Select(e => e.LayoutId)
            .Distinct(StringComparer.Ordinal)
            .Where(id => !catalog.HasAuthored(id))
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void CommittedGeometry_RawLengthIsWithinTwoPercentOfTheCircuitLength()
    {
        // The validator allows 15% so a rough sketch can be committed; shapes meant to ship should land much closer,
        // because TrackGeometry rescales to length_km and a big correction would distort every corner radius.
        var lengths = LengthKmByLayout();
        foreach (var file in Data.Value.TrackGeometries)
        {
            var raw = TrackGeometry.Build(file.ControlPoints.Select(p => (p[0], p[1])).ToArray(), referenceLengthM: null);
            var reference = lengths[file.LayoutId] * 1000.0;
            var deviation = Math.Abs(raw.LengthM - reference) / reference;
            Assert.True(
                deviation < 0.02,
                $"{file.LayoutId}: raw length {raw.LengthM.ToString("0", CultureInfo.InvariantCulture)} m vs {reference.ToString("0", CultureInfo.InvariantCulture)} m ({deviation:P1})");
        }
    }

    [Fact]
    public void CommittedGeometry_HasNoNeedlesslyClosePoints()
    {
        foreach (var file in Data.Value.TrackGeometries)
        {
            for (var i = 0; i < file.ControlPoints.Count; i++)
            {
                var a = file.ControlPoints[i];
                var b = file.ControlPoints[(i + 1) % file.ControlPoints.Count];
                var gap = Math.Sqrt(((a[0] - b[0]) * (a[0] - b[0])) + ((a[1] - b[1]) * (a[1] - b[1])));
                Assert.True(gap >= 5.0, $"{file.LayoutId}: points {i} and {(i + 1) % file.ControlPoints.Count} are {gap:F1} m apart (the validator only demands 1 m; 5 m keeps the spline calm)");
            }
        }
    }

    [Fact]
    public void CommittedGeometry_SaysHonestlyWhereItCameFrom()
    {
        foreach (var file in Data.Value.TrackGeometries)
        {
            Assert.False(string.IsNullOrWhiteSpace(file.Source), $"{file.LayoutId}: source must say where the shape comes from");
            Assert.DoesNotContain("placeholder", file.Source, StringComparison.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrWhiteSpace(file.Notes), $"{file.LayoutId}: notes must say what the shape is");
        }
    }

    [Fact]
    public void Catalog_ResolvesEveryCommittedFile_WithNamedCornersInLapOrder()
    {
        var catalog = TrackGeometryCatalog.Create(Data.Value);
        var lengths = LengthKmByLayout();

        foreach (var file in Data.Value.TrackGeometries)
        {
            var resolved = catalog.Resolve(file.LayoutId);
            Assert.Equal(TrackGeometrySource.Authored, resolved.Source);
            Assert.Equal(lengths[file.LayoutId] * 1000.0, resolved.Geometry.LengthM, precision: 6);
            Assert.Equal(file.Corners?.Count ?? 0, resolved.Corners.Count);

            for (var i = 1; i < resolved.Corners.Count; i++)
            {
                Assert.True(resolved.Corners[i].LapFraction > resolved.Corners[i - 1].LapFraction, $"{file.LayoutId}: corners out of order");
            }
        }
    }

    [Fact]
    public void LayoutWithoutAGeometryFile_StillResolves_ToTheFallback()
    {
        // The documented rule (TECH §6.5): no file means a neutral stadium of the right length, never an exception.
        var catalog = TrackGeometryCatalog.Create(Data.Value);
        var withoutFile = LengthKmByLayout().Keys.First(id => !catalog.HasAuthored(id));

        var resolved = catalog.Resolve(withoutFile);

        Assert.Equal(TrackGeometrySource.Fallback, resolved.Source);
        Assert.Equal(LengthKmByLayout()[withoutFile] * 1000.0, resolved.Geometry.LengthM, precision: 6);
    }
}
