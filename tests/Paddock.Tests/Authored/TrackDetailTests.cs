using System.Text.Json.Nodes;
using Paddock.Data.Authored;
using Paddock.Domain.World.Tracks;

namespace Paddock.Tests.Authored;

/// <summary>
/// Track file v2 (#348, PP-073, TECH §6.5a): the hand-made fixture with every optional field resolves as authored,
/// the committed layouts resolve to era defaults and keep their length, and each validator rule has a failing input
/// whose message names the file, the field and the reason. The fixture is invented, not a real circuit.
/// </summary>
public class TrackDetailTests
{
    private const string FixtureLayout = "fixture_2010";
    private const string FixtureFile = FixtureLayout + ".json";

    private static readonly Lazy<string> FixtureJson = new(() => File.ReadAllText(
        Path.Combine(RepoPaths.Root(), "tests", "Paddock.Tests", "Fixtures", "tracks", FixtureFile)));

    private static readonly Lazy<AuthoredData> RepoData = new(
        () => AuthoredDataLoader.Load(Path.Combine(RepoPaths.Root(), "data")));

    /// <summary>The sample circuits plus the fixture layout (raced in <paramref name="years"/>, length close to the fixture's raw length).</summary>
    private static string CircuitsWithFixture(string years = "2010")
    {
        const string Anchor = """
                  "layout_id": "test_1950",
            """;
        var fixtureLayout = """
                  "layout_id": "fixture_2010",
                  "years_used_in_f1": [YEARS],
                  "length_km": 5.883,
                  "corners": null,
                  "character": ["street"],
                  "profile_guess": { "straights": 0.25, "high_speed": 0.25, "low_speed": 0.25, "braking": 0.25 },
                  "confidence": "high",
                  "source": "https://example.com/fixture",
                  "notes": "fixture"
                },
                {
            """;
        Assert.Contains(Anchor, AuthoredSamples.Circuits, StringComparison.Ordinal);
        return AuthoredSamples.Circuits.Replace(
            Anchor,
            fixtureLayout.Replace("YEARS", years, StringComparison.Ordinal) + Anchor,
            StringComparison.Ordinal);
    }

    private static AuthoredData LoadWith(string json, string years = "2010")
    {
        using var temp = new TempAuthoredData(
            circuits: CircuitsWithFixture(years),
            geometries: new Dictionary<string, string>(StringComparer.Ordinal) { [FixtureFile] = json });
        return AuthoredDataLoader.Load(temp.Root);
    }

    private static ResolvedTrackGeometry ResolveFixture(string? json = null) =>
        TrackGeometryCatalog.Create(LoadWith(json ?? FixtureJson.Value)).Resolve(FixtureLayout);

    private static List<AuthoredDataError> GeometryErrors(string json) =>
        AuthoredDataValidator.Validate(LoadWith(json))
            .Where(e => e.Code.StartsWith("geometry-", StringComparison.Ordinal))
            .ToList();

    private static string Mutate(Action<JsonObject> change)
    {
        var node = JsonNode.Parse(FixtureJson.Value)!.AsObject();
        change(node);
        return node.ToJsonString();
    }

    [Fact]
    public void Fixture_PassesEveryGeometryRule()
    {
        Assert.Empty(GeometryErrors(FixtureJson.Value));
    }

    [Fact]
    public void Fixture_WidthResolvesAsAuthored_InterpolatedAroundTheLap()
    {
        var detail = ResolveFixture().Detail;

        Assert.True(detail.HasAuthoredWidth);
        Assert.Equal(15.0, detail.WidthAt(0.0), precision: 9);
        Assert.Equal(12.0, detail.WidthAt(0.25), precision: 9);
        Assert.Equal(13.5, detail.WidthAt(0.125), precision: 9);
        Assert.Equal(11.5, detail.WidthAt(0.4), precision: 9);

        // Between the last point (0.8, 14 m) and the first point across the line (1.0, 15 m).
        Assert.Equal(14.5, detail.WidthAt(0.9), precision: 9);
        Assert.Equal(detail.WidthAt(0.9), detail.WidthAt(1.9), precision: 12);
    }

    [Fact]
    public void Fixture_EdgesResolveAsAuthored_WithEraDefaultsOutsideTheSpans()
    {
        var detail = ResolveFixture().Detail;

        // Span 0.94 -> 0.05 on the right wraps across the start line.
        Assert.Equal(new TrackEdge(1.5, RunoffSurface.Asphalt, 30.0, Barrier: true), detail.EdgeAt(0.97, TrackSide.Right));
        Assert.Equal(new TrackEdge(1.5, RunoffSurface.Asphalt, 30.0, Barrier: true), detail.EdgeAt(0.02, TrackSide.Right));
        Assert.Equal(new TrackEdge(2.0, RunoffSurface.Gravel, 90.0, Barrier: true), detail.EdgeAt(0.30, TrackSide.Right));
        Assert.Equal(new TrackEdge(1.0, RunoffSurface.Grass, 20.0, Barrier: true), detail.EdgeAt(0.30, TrackSide.Left));
        Assert.Equal(new TrackEdge(1.2, RunoffSurface.Grass, 40.0, Barrier: false), detail.EdgeAt(0.55, TrackSide.Right));

        // A span ends before its "to" and the left side at the start line is not described.
        Assert.Equal(TrackEraDefaults.For(2010).Edge, detail.EdgeAt(0.36, TrackSide.Right));
        Assert.Equal(TrackEraDefaults.For(2010).Edge, detail.EdgeAt(0.02, TrackSide.Left));
    }

    [Fact]
    public void Fixture_PitLaneResolvesAsAuthored_AndTheTimeAtTheLimitComesFromItsLength()
    {
        var resolved = ResolveFixture();
        var pit = resolved.Detail.PitLane;

        Assert.NotNull(pit);
        Assert.Equal(0.94, pit.EntryLapFraction);
        Assert.Equal(0.179, pit.ExitLapFraction);
        Assert.Equal(80.0, pit.LimitKph);
        Assert.Equal(12, pit.BoxesM.Count);

        // The lane is scaled with the centre line, so it stays attached to it.
        var scale = resolved.Geometry.ScaleFactor;
        Assert.InRange(scale, 0.99, 1.01);
        Assert.Equal(-350.0 * scale, pit.Points[0].X, precision: 9);
        Assert.Equal(1150.0 * scale, pit.LimitLengthM, precision: 9);
        Assert.Equal(500.0 * scale, pit.BoxesM[0], precision: 9);

        var expectedLength = Math.Sqrt(100.0 * 100.0 + 17.0 * 17.0) + 1150.0 + Math.Sqrt(150.0 * 150.0 + 17.0 * 17.0);
        Assert.Equal(expectedLength * scale, pit.LengthM, precision: 6);

        // 1150 m at 80 km/h = 51.75 s before scaling; the time is length over speed, nothing authored by hand.
        Assert.Equal(pit.LimitLengthM / (80.0 / 3.6), pit.TimeAtLimitSeconds, precision: 12);
        Assert.Equal(51.75 * scale, pit.TimeAtLimitSeconds, precision: 9);
    }

    [Fact]
    public void Fixture_ElevationResolvesAsAuthored()
    {
        var detail = ResolveFixture().Detail;

        Assert.Equal(15.0, detail.ElevationAt(0.15)!.Value, precision: 9);
        Assert.Equal(3, detail.Elevation.Count);
    }

    [Fact]
    public void Fixture_ResolvesTheSameTwice()
    {
        var a = ResolveFixture().Detail;
        var b = ResolveFixture().Detail;

        for (var i = 0; i < 1000; i++)
        {
            var s = i / 1000.0;
            Assert.Equal(a.WidthAt(s), b.WidthAt(s));
            Assert.Equal(a.EdgeAt(s, TrackSide.Left), b.EdgeAt(s, TrackSide.Left));
        }
    }

    [Fact]
    public void BrokenSection_FallsBackToTheEraDefault_OtherSectionsStillApply()
    {
        var json = Mutate(o => o["width_m"]![1]!["w"] = 40.0);

        var detail = ResolveFixture(json).Detail;

        Assert.False(detail.HasAuthoredWidth);
        Assert.Equal(TrackEraDefaults.For(2010).WidthM, detail.WidthAt(0.3));
        Assert.NotNull(detail.PitLane);
        Assert.NotEmpty(detail.Edges);
    }

    [Fact]
    public void EraDefaults_FollowTheSeasonRaced_AuthoredFieldsDoNot()
    {
        // One layout raced in 1972 and in 2010, once without authored width (defaults) and once with the full fixture.
        var withoutWidth = Mutate(o => o.Remove("width_m"));
        var bare = TrackGeometryCatalog.Create(LoadWith(withoutWidth, years: "1972, 2010"));
        var full = TrackGeometryCatalog.Create(LoadWith(FixtureJson.Value, years: "1972, 2010"));

        var bare1972 = bare.Resolve(FixtureLayout, 1972).Detail;
        var bare2010 = bare.Resolve(FixtureLayout, 2010).Detail;

        // Defaults differ by the season raced: 1970 row in 1972, 2005 row in 2010.
        Assert.Equal(TrackEraDefaults.For(1972).WidthM, bare1972.WidthAt(0.3));
        Assert.Equal(TrackEraDefaults.For(2010).WidthM, bare2010.WidthAt(0.3));
        Assert.NotEqual(bare1972.WidthAt(0.3), bare2010.WidthAt(0.3));
        Assert.Equal(TrackEraDefaults.For(1972).Edge, bare1972.EdgeAt(0.1, TrackSide.Left));
        Assert.Equal(TrackEraDefaults.For(2010).Edge, bare2010.EdgeAt(0.1, TrackSide.Left));
        Assert.NotEqual(bare1972.EdgeAt(0.1, TrackSide.Left), bare2010.EdgeAt(0.1, TrackSide.Left));

        // Authored fields are the same in both seasons.
        var full1972 = full.Resolve(FixtureLayout, 1972);
        var full2010 = full.Resolve(FixtureLayout, 2010);
        for (var i = 0; i < 200; i++)
        {
            var s = i / 200.0;
            Assert.Equal(full1972.Detail.WidthAt(s), full2010.Detail.WidthAt(s));
            Assert.Equal(full1972.Detail.ElevationAt(s), full2010.Detail.ElevationAt(s));
        }

        Assert.Equal(full1972.Detail.EdgeAt(0.3, TrackSide.Right), full2010.Detail.EdgeAt(0.3, TrackSide.Right));
        Assert.Equal(full1972.Detail.PitLane!.TimeAtLimitSeconds, full2010.Detail.PitLane!.TimeAtLimitSeconds);
        Assert.Same(full1972.Geometry, full2010.Geometry);
        Assert.Equal(full1972.Corners, full2010.Corners);

        // Without a season (map, editor): the layout's last season.
        Assert.Equal(TrackEraDefaults.For(2010).WidthM, bare.Resolve(FixtureLayout).Detail.WidthAt(0.3));
        Assert.Same(bare.Resolve(FixtureLayout, 1972), bare.Resolve(FixtureLayout, 1973));
    }

    [Fact]
    public void AuthoredWidth_MustFitTheBandOfEveryEraTheLayoutIsRacedIn()
    {
        // 6 m fits the 1950 band (5-25 m) but not the 1970 band (7-25 m); the layout is raced in both eras.
        var json = Mutate(o => o["width_m"]![1]!["w"] = 6.0);

        var error = Assert.Single(
            AuthoredDataValidator.Validate(LoadWith(json, years: "1955, 1972")),
            e => e.Code.StartsWith("geometry-", StringComparison.Ordinal));
        Assert.Equal($"tracks/geometry/{FixtureFile}: width_m[1].w: 6 m is outside the 1970 era band 7-25 m", error.Message);

        var detail = TrackGeometryCatalog.Create(LoadWith(json, years: "1955, 1972")).Resolve(FixtureLayout, 1955).Detail;
        Assert.False(detail.HasAuthoredWidth);
    }

    [Fact]
    public void CommittedLayouts_ResolveToEraDefaults_AtTheirCircuitLength()
    {
        var data = RepoData.Value;
        var catalog = TrackGeometryCatalog.Create(data);
        var layouts = data.Circuits.Circuits.SelectMany(c => c.Layouts).ToDictionary(l => l.LayoutId, StringComparer.Ordinal);

        Assert.Equal(26, data.TrackGeometries.Count);
        foreach (var file in data.TrackGeometries)
        {
            var layout = layouts[file.LayoutId];
            var resolved = catalog.Resolve(file.LayoutId);
            var era = TrackEraDefaults.For(layout.YearsUsedInF1.Max());

            Assert.Equal(TrackGeometrySource.Authored, resolved.Source);
            Assert.Equal(layout.LengthKm * 1000.0, resolved.Geometry.LengthM, precision: 6);
            Assert.Same(era, resolved.Detail.EraDefault);
            Assert.False(resolved.Detail.HasAuthoredWidth);
            Assert.Equal(era.WidthM, resolved.Detail.WidthAt(0.5));
            Assert.Equal(era.Edge, resolved.Detail.EdgeAt(0.5, TrackSide.Left));
            Assert.Null(resolved.Detail.PitLane);
            Assert.Null(resolved.Detail.ElevationAt(0.5));
            foreach (var season in layout.YearsUsedInF1)
            {
                var raced = catalog.Resolve(file.LayoutId, season);
                Assert.Same(resolved.Geometry, raced.Geometry);
                Assert.Equal(TrackEraDefaults.For(season).WidthM, raced.Detail.WidthAt(0.5));
            }
        }
    }

    [Fact]
    public void FallbackLayout_HasEraDefaultsAndNoPitLane()
    {
        using var temp = new TempAuthoredData();
        var resolved = TrackGeometryCatalog.Create(AuthoredDataLoader.Load(temp.Root)).Resolve("test_1950");

        Assert.Equal(TrackGeometrySource.Fallback, resolved.Source);
        Assert.Equal(TrackEraDefaults.For(1950).WidthM, resolved.Detail.WidthAt(0.0));
        Assert.Null(resolved.Detail.PitLane);
    }

    [Theory]
    [InlineData(1949, 1950)]
    [InlineData(1950, 1950)]
    [InlineData(1969, 1950)]
    [InlineData(1970, 1970)]
    [InlineData(1994, 1980)]
    [InlineData(2010, 2005)]
    [InlineData(2026, 2005)]
    public void EraDefaults_PickTheLastRowNotAfterTheFirstSeason(int season, int expectedRow)
    {
        Assert.Equal(expectedRow, TrackEraDefaults.For(season).FromYear);
    }

    /// <summary>Each case breaks one rule of the fixture and expects one error naming the file, the field and the reason.</summary>
    [Theory]
    [InlineData("width-empty", AuthoredDataValidator.GeometryWidth, "width_m", "is empty")]
    [InlineData("width-s-range", AuthoredDataValidator.GeometryWidth, "width_m[1].s", "not a lap fraction")]
    [InlineData("width-s-order", AuthoredDataValidator.GeometryWidth, "width_m[2].s", "does not come after")]
    [InlineData("width-band", AuthoredDataValidator.GeometryWidth, "width_m[1].w", "outside the 2005 era band")]
    [InlineData("edge-from", AuthoredDataValidator.GeometryEdge, "edges[1].from", "not a lap fraction")]
    [InlineData("edge-to", AuthoredDataValidator.GeometryEdge, "edges[1].to", "not a lap fraction")]
    [InlineData("edge-empty", AuthoredDataValidator.GeometryEdge, "edges[1]", "the span is empty")]
    [InlineData("edge-side", AuthoredDataValidator.GeometryEdge, "edges[1].side", "is not left or right")]
    [InlineData("edge-runoff", AuthoredDataValidator.GeometryEdge, "edges[1].runoff", "is not asphalt, grass or gravel")]
    [InlineData("edge-kerb", AuthoredDataValidator.GeometryEdge, "edges[1].kerb_m", "outside 0-5 m")]
    [InlineData("edge-runoff-m", AuthoredDataValidator.GeometryEdge, "edges[1].runoff_m", "outside 0-500 m")]
    [InlineData("edge-overlap", AuthoredDataValidator.GeometryEdge, "edges[3]", "overlaps edges[1] on the right side")]
    [InlineData("edge-overlap-wrap", AuthoredDataValidator.GeometryEdge, "edges[4]", "overlaps edges[0] on the right side")]
    [InlineData("pit-point-shape", AuthoredDataValidator.GeometryPitLane, "pit_lane.points[2]", "exactly 2 coordinates")]
    [InlineData("pit-point-count", AuthoredDataValidator.GeometryPitLane, "pit_lane.points", "minimum is 2")]
    [InlineData("pit-point-gap", AuthoredDataValidator.GeometryPitLane, "pit_lane.points[3]", "from the previous point, minimum is 1 m")]
    [InlineData("pit-crossing", AuthoredDataValidator.GeometryPitLane, "pit_lane.points", "crosses itself")]
    [InlineData("pit-entry-range", AuthoredDataValidator.GeometryPitLane, "pit_lane.entry_s", "not a lap fraction")]
    [InlineData("pit-exit-equal", AuthoredDataValidator.GeometryPitLane, "pit_lane.exit_s", "equals entry_s")]
    [InlineData("pit-entry-far", AuthoredDataValidator.GeometryPitLane, "pit_lane.points[0]", "from the centre line at entry_s 0.5")]
    [InlineData("pit-limit-from", AuthoredDataValidator.GeometryPitLane, "pit_lane.limit_from", "before the start of the lane")]
    [InlineData("pit-limit-to", AuthoredDataValidator.GeometryPitLane, "pit_lane.limit_to", "past the end of the lane")]
    [InlineData("pit-limit-order", AuthoredDataValidator.GeometryPitLane, "pit_lane.limit_to", "not after limit_from")]
    [InlineData("pit-kph", AuthoredDataValidator.GeometryPitLane, "pit_lane.limit_kph", "outside 30-150 km/h")]
    [InlineData("pit-boxes-empty", AuthoredDataValidator.GeometryPitLane, "pit_lane.boxes", "needs at least one garage")]
    [InlineData("pit-box-outside", AuthoredDataValidator.GeometryPitLane, "pit_lane.boxes[0]", "outside the speed limit section")]
    [InlineData("pit-box-order", AuthoredDataValidator.GeometryPitLane, "pit_lane.boxes[1]", "does not come after the previous garage")]
    [InlineData("elevation-order", AuthoredDataValidator.GeometryElevation, "elevation_m[1].s", "does not come after")]
    public void EachRule_ReportsTheFileTheFieldAndTheReason(string breakage, string code, string field, string reason)
    {
        var json = Mutate(o => Break(o, breakage));

        var errors = GeometryErrors(json);

        var error = Assert.Single(errors);
        Assert.Equal(code, error.Code);
        Assert.StartsWith($"tracks/geometry/{FixtureFile}: {field}: ", error.Message, StringComparison.Ordinal);
        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
    }

    private static void Break(JsonObject o, string breakage)
    {
        var width = o["width_m"]!.AsArray();
        var edges = o["edges"]!.AsArray();
        var pit = o["pit_lane"]!.AsObject();
        var points = pit["points"]!.AsArray();
        switch (breakage)
        {
            case "width-empty": o["width_m"] = new JsonArray(); break;
            case "width-s-range": width[1]!["s"] = 1.0; width.RemoveAt(3); width.RemoveAt(2); break;
            case "width-s-order": width[2]!["s"] = 0.25; break;
            case "width-band": width[1]!["w"] = 4.0; break;
            case "edge-from": edges[1]!["from"] = -0.1; break;
            case "edge-to": edges[1]!["to"] = 1.0; break;
            case "edge-empty": edges[1]!["to"] = 0.24; break;
            case "edge-side": edges[1]!["side"] = "outside"; break;
            case "edge-runoff": edges[1]!["runoff"] = "sand"; break;
            case "edge-kerb": edges[1]!["kerb_m"] = 6.0; break;
            case "edge-runoff-m": edges[1]!["runoff_m"] = 600.0; break;
            case "edge-overlap": edges[3]!["from"] = 0.30; break;
            case "edge-overlap-wrap": edges[4]!["to"] = 0.95; break;
            case "pit-point-shape": points[2] = new JsonArray(-100.0, 25.0, 3.0); break;
            case "pit-point-count":
                pit["points"] = new JsonArray(new JsonArray(-350.0, 8.0));
                pit["limit_from"] = 0.0;
                pit["limit_to"] = 0.0;
                pit["boxes"] = new JsonArray(0.0);
                break;
            case "pit-point-gap": points[3] = new JsonArray(-99.5, 25.0); break;
            case "pit-crossing": points[4] = new JsonArray(300.0, -40.0); points[5] = new JsonArray(-200.0, 60.0); break;
            case "pit-entry-range": pit["entry_s"] = 1.2; break;
            case "pit-exit-equal": pit["exit_s"] = 0.94; break;
            case "pit-entry-far": pit["entry_s"] = 0.5; break;
            case "pit-limit-from": pit["limit_from"] = -5.0; break;
            case "pit-limit-to": pit["limit_to"] = 5000.0; break;
            case "pit-limit-order": pit["limit_to"] = 90.0; break;
            case "pit-kph": pit["limit_kph"] = 200.0; break;
            case "pit-boxes-empty": pit["boxes"] = new JsonArray(); break;
            case "pit-box-outside": pit["boxes"]![0] = 50.0; break;
            case "pit-box-order": pit["boxes"]![1] = 500.0; break;
            case "elevation-order": o["elevation_m"]![1]!["s"] = 0.0; break;
            default: throw new ArgumentOutOfRangeException(nameof(breakage), breakage, "unknown breakage");
        }
    }
}
