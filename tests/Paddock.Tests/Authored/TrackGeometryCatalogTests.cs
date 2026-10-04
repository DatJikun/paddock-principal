using Paddock.Data.Authored;

namespace Paddock.Tests.Authored;

public class TrackGeometryCatalogTests
{
    private const string GeometryWithCorners = """
        {
          "layout_id": "test_1950",
          "control_points": [
            [0.0, 0.0],
            [500.0, 0.0],
            [1200.0, 0.0],
            [1800.0, 0.0],
            [2000.0, 150.0],
            [2000.0, 450.0],
            [1800.0, 600.0],
            [1200.0, 600.0],
            [500.0, 600.0],
            [0.0, 600.0],
            [-200.0, 450.0],
            [-200.0, 150.0]
          ],
          "source": "fixture",
          "notes": "",
          "corners": [
            { "point": 5, "name": "East bend" },
            { "point": 10, "name": "West bend" }
          ]
        }
        """;

    private static TrackGeometryCatalog CatalogWith(params (string File, string Json)[] files)
    {
        var geometries = files.ToDictionary(f => f.File, f => f.Json, StringComparer.Ordinal);
        using var fixture = new TempAuthoredData(geometries: geometries);
        return TrackGeometryCatalog.Create(AuthoredDataLoader.Load(fixture.Root));
    }

    [Fact]
    public void LayoutWithFile_ResolvesAuthoredGeometryScaledToCircuitLength()
    {
        var catalog = CatalogWith(("test_1950.json", GeometryWithCorners));

        var resolved = catalog.Resolve("test_1950");

        Assert.True(catalog.HasAuthored("test_1950"));
        Assert.Equal(TrackGeometrySource.Authored, resolved.Source);
        Assert.Equal(5_000.0, resolved.Geometry.LengthM, precision: 6);
    }

    [Fact]
    public void Corners_AreNamedAndPlacedInLapOrder()
    {
        var resolved = CatalogWith(("test_1950.json", GeometryWithCorners)).Resolve("test_1950");

        Assert.Equal(["East bend", "West bend"], resolved.Corners.Select(c => c.Name));
        Assert.All(resolved.Corners, c => Assert.InRange(c.LapFraction, 0.0, 1.0));
        Assert.True(resolved.Corners[0].LapFraction < resolved.Corners[1].LapFraction);

        // The east bend (point 5) is about 40% round the lap, the west bend (point 10) about 90%.
        Assert.InRange(resolved.Corners[0].LapFraction, 0.35, 0.50);
        Assert.InRange(resolved.Corners[1].LapFraction, 0.85, 0.99);
    }

    [Fact]
    public void LayoutWithoutFile_FallsBackToNeutralShapeAtCircuitLength()
    {
        var catalog = CatalogWith();

        var resolved = catalog.Resolve("test_1950");

        Assert.False(catalog.HasAuthored("test_1950"));
        Assert.Equal(TrackGeometrySource.Fallback, resolved.Source);
        Assert.Equal(5_000.0, resolved.Geometry.LengthM, precision: 6);
        Assert.Empty(resolved.Corners);
    }

    [Fact]
    public void UnknownLayout_IsAProgrammingError()
    {
        var catalog = CatalogWith();

        Assert.Throws<ArgumentException>(() => catalog.Resolve("no_such_layout_1900"));
    }

    [Fact]
    public void ResolvingTwice_ReturnsTheSameInstance()
    {
        var catalog = CatalogWith(("test_1950.json", GeometryWithCorners));

        Assert.Same(catalog.Resolve("test_1950"), catalog.Resolve("test_1950"));
    }
}
