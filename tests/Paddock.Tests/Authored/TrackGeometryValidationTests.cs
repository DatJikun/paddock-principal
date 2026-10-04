using Paddock.Data.Authored;

namespace Paddock.Tests.Authored;

public class TrackGeometryValidationTests
{
    private static string ValidTrackJson(string layoutId = "test_1950") => $$"""
        {
          "layout_id": "{{layoutId}}",
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
          "source": "placeholder, not a real track",
          "notes": "Valid test fixture"
        }
        """;

    [Fact]
    public void ValidGeometry_PassesValidationWithoutErrors()
    {
        var geometries = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["test_1950.json"] = ValidTrackJson("test_1950"),
        };

        using var fixture = new TempAuthoredData(geometries: geometries);
        var data = AuthoredDataLoader.Load(fixture.Root);
        var errors = AuthoredDataValidator.Validate(data);

        Assert.DoesNotContain(errors, e => e.Code.StartsWith("geometry-", StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownLayout_ReportsError()
    {
        var geometries = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["unknown.json"] = ValidTrackJson("unknown_layout_9999"),
        };

        using var fixture = new TempAuthoredData(geometries: geometries);
        var data = AuthoredDataLoader.Load(fixture.Root);
        var errors = AuthoredDataValidator.Validate(data);

        var error = Assert.Single(errors, e => e.Code == AuthoredDataValidator.GeometryUnknownLayout);
        Assert.Contains("unknown_layout_9999", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PointCount_LessThan8_ReportsError()
    {
        var json = """
            {
              "layout_id": "test_1950",
              "control_points": [
                [0.0, 0.0],
                [500.0, 0.0],
                [1000.0, 0.0],
                [1000.0, 500.0],
                [500.0, 500.0],
                [0.0, 500.0],
                [-200.0, 250.0]
              ],
              "source": "fixture",
              "notes": ""
            }
            """;

        var geometries = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["test_1950.json"] = json,
        };

        using var fixture = new TempAuthoredData(geometries: geometries);
        var data = AuthoredDataLoader.Load(fixture.Root);
        var errors = AuthoredDataValidator.Validate(data);

        var error = Assert.Single(errors, e => e.Code == AuthoredDataValidator.GeometryPointCount);
        Assert.Contains("7 control points, minimum is 8", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConsecutivePointsCloserThan1m_ReportsError()
    {
        var json = """
            {
              "layout_id": "test_1950",
              "control_points": [
                [0.0, 0.0],
                [0.5, 0.0],
                [1000.0, 0.0],
                [1500.0, 200.0],
                [1500.0, 600.0],
                [1000.0, 800.0],
                [0.0, 800.0],
                [-500.0, 400.0]
              ],
              "source": "fixture",
              "notes": ""
            }
            """;

        var geometries = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["test_1950.json"] = json,
        };

        using var fixture = new TempAuthoredData(geometries: geometries);
        var data = AuthoredDataLoader.Load(fixture.Root);
        var errors = AuthoredDataValidator.Validate(data);

        var error = Assert.Single(errors, e => e.Code == AuthoredDataValidator.GeometryPointsTooClose);
        Assert.Contains("consecutive points 0 and 1 are 0.50 m apart", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LastAndFirstPointsCloserThan1m_ReportsError()
    {
        var json = """
            {
              "layout_id": "test_1950",
              "control_points": [
                [0.0, 0.0],
                [500.0, 0.0],
                [1000.0, 0.0],
                [1500.0, 200.0],
                [1500.0, 600.0],
                [1000.0, 800.0],
                [0.0, 800.0],
                [0.2, 0.3]
              ],
              "source": "fixture",
              "notes": ""
            }
            """;

        var geometries = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["test_1950.json"] = json,
        };

        using var fixture = new TempAuthoredData(geometries: geometries);
        var data = AuthoredDataLoader.Load(fixture.Root);
        var errors = AuthoredDataValidator.Validate(data);

        var error = Assert.Single(errors, e => e.Code == AuthoredDataValidator.GeometryPointsTooClose);
        Assert.Contains("consecutive points 7 and 0", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RawLengthMismatch_Exceeding15Percent_ReportsError()
    {
        // test_1950 length is 5.0 km (5000 m).
        // A circle with R = 200 has perimeter ~1256 m, far outside 5000 m +/- 15% (4250 - 5750 m).
        var json = """
            {
              "layout_id": "test_1950",
              "control_points": [
                [200.0, 0.0],
                [141.4, 141.4],
                [0.0, 200.0],
                [-141.4, 141.4],
                [-200.0, 0.0],
                [-141.4, -141.4],
                [0.0, -200.0],
                [141.4, -141.4]
              ],
              "source": "fixture",
              "notes": ""
            }
            """;

        var geometries = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["test_1950.json"] = json,
        };

        using var fixture = new TempAuthoredData(geometries: geometries);
        var data = AuthoredDataLoader.Load(fixture.Root);
        var errors = AuthoredDataValidator.Validate(data);

        var error = Assert.Single(errors, e => e.Code == AuthoredDataValidator.GeometryLengthMismatch);
        Assert.Contains("raw length", error.Message, StringComparison.Ordinal);
        Assert.Contains("is not within +/-15%", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SelfIntersectingSpline_Figure8_ReportsError()
    {
        // Figure-8 pattern that clearly crosses in the middle
        var json = """
            {
              "layout_id": "test_1950",
              "control_points": [
                [0.0, 0.0],
                [600.0, 600.0],
                [1200.0, 600.0],
                [1200.0, 0.0],
                [600.0, 0.0],
                [0.0, 600.0],
                [-600.0, 600.0],
                [-600.0, 0.0]
              ],
              "source": "fixture",
              "notes": ""
            }
            """;

        var geometries = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["test_1950.json"] = json,
        };

        using var fixture = new TempAuthoredData(geometries: geometries);
        var data = AuthoredDataLoader.Load(fixture.Root);
        var errors = AuthoredDataValidator.Validate(data);

        var error = Assert.Single(errors, e => e.Code == AuthoredDataValidator.GeometrySelfIntersection);
        Assert.Contains("intersects itself", error.Message, StringComparison.Ordinal);
    }
}
