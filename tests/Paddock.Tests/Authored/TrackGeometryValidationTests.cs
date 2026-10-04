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

    [Fact]
    public void TwoFilesForTheSameLayout_ReportDuplicate()
    {
        var geometries = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["a.json"] = ValidTrackJson("test_1950"),
            ["b.json"] = ValidTrackJson("test_1950"),
        };

        using var fixture = new TempAuthoredData(geometries: geometries);
        var errors = AuthoredDataValidator.Validate(AuthoredDataLoader.Load(fixture.Root));

        var error = Assert.Single(errors, e => e.Code == AuthoredDataValidator.GeometryDuplicate);
        Assert.Contains("test_1950", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CornerOnExistingPoint_IsAccepted_AndOptional()
    {
        var withCorners = ValidTrackJson().Replace(
            "\"notes\": \"Valid test fixture\"",
            "\"notes\": \"Valid test fixture\", \"corners\": [{ \"point\": 4, \"name\": \"East bend\" }, { \"point\": 10, \"name\": \"West bend\" }]",
            StringComparison.Ordinal);
        Assert.Contains("East bend", withCorners, StringComparison.Ordinal);

        using var fixture = new TempAuthoredData(geometries: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["test_1950.json"] = withCorners,
        });
        var data = AuthoredDataLoader.Load(fixture.Root);

        Assert.Equal(2, data.TrackGeometries[0].Corners!.Count);
        Assert.DoesNotContain(AuthoredDataValidator.Validate(data), e => e.Code.StartsWith("geometry-", StringComparison.Ordinal));
    }

    [Fact]
    public void BadCorners_AreReported()
    {
        var json = ValidTrackJson().Replace(
            "\"notes\": \"Valid test fixture\"",
            "\"notes\": \"x\", \"corners\": [{ \"point\": 99, \"name\": \"Nowhere\" }, { \"point\": 4, \"name\": \"\" }, { \"point\": 4, \"name\": \"Same\" }, { \"point\": 5, \"name\": \"Same\" }]",
            StringComparison.Ordinal);

        using var fixture = new TempAuthoredData(geometries: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["test_1950.json"] = json,
        });
        var errors = AuthoredDataValidator.Validate(AuthoredDataLoader.Load(fixture.Root))
            .Where(e => e.Code == AuthoredDataValidator.GeometryCorner)
            .Select(e => e.Message)
            .ToArray();

        Assert.Contains(errors, m => m.Contains("outside 0..11", StringComparison.Ordinal));
        Assert.Contains(errors, m => m.Contains("empty name", StringComparison.Ordinal));
        Assert.Contains(errors, m => m.Contains("two corners on control point 4", StringComparison.Ordinal));
        Assert.Contains(errors, m => m.Contains("two corners named 'Same'", StringComparison.Ordinal));
    }

    [Fact]
    public void ControlPointsThatDoubleBack_ReportSharpBend()
    {
        // A control point sits 100 m back along the straight it just ran down: the spline has to reverse
        // within a few metres, a cusp and not a corner.
        var json = """
            {
              "layout_id": "test_1950",
              "control_points": [
                [0.0, 0.0],
                [800.0, 0.0],
                [1600.0, 0.0],
                [1500.0, 3.0],
                [1700.0, 400.0],
                [1500.0, 1000.0],
                [900.0, 1100.0],
                [0.0, 1100.0],
                [-200.0, 700.0],
                [-200.0, 300.0]
              ],
              "source": "fixture",
              "notes": ""
            }
            """;

        using var fixture = new TempAuthoredData(geometries: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["test_1950.json"] = json,
        });
        var errors = AuthoredDataValidator.Validate(AuthoredDataLoader.Load(fixture.Root));

        Assert.Contains(errors, e => e.Code == AuthoredDataValidator.GeometrySharpBend);
    }

    [Fact]
    public void SmoothCircuit_DoesNotReportSharpBend()
    {
        using var fixture = new TempAuthoredData(geometries: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["test_1950.json"] = ValidTrackJson(),
        });

        Assert.DoesNotContain(
            AuthoredDataValidator.Validate(AuthoredDataLoader.Load(fixture.Root)),
            e => e.Code == AuthoredDataValidator.GeometrySharpBend);
    }
}
