using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Paddock.Domain.World.Tracks;

namespace Paddock.Tests.World;

/// <summary>
/// Keeps <c>ui/prototype/tests/fixtures/track-geometry-reference.json</c> equal to what <see cref="TrackGeometry"/>
/// produces today. The UI test (<c>ui/prototype/tests/track-spline.test.mjs</c>) compares the renderer-side
/// <c>TrackSpline</c> to this file, so the two splines cannot drift apart unnoticed: change the maths here and this
/// test fails until the fixture is regenerated (<c>PADDOCK_UPDATE_FIXTURES=1 dotnet test --filter TrackSplineReference</c>),
/// which in turn makes the UI test show whether the renderer still matches.
/// </summary>
public class TrackSplineReferenceFixtureTests
{
    private const int SamplesPerCase = 120;

    /// <summary>Synthetic shapes with the spacing real layouts have: long chords next to tight corners.</summary>
    private static readonly (string Name, (double X, double Y)[] Points)[] Cases =
    [
        (
            "uneven_stadium",
            [
                (0, 0), (500, 0), (1200, 0), (1800, 0), (2000, 150), (2000, 450),
                (1800, 600), (1200, 600), (500, 600), (0, 600), (-200, 450), (-200, 150),
            ]),
        (
            "hairpin_and_chicane",
            [
                (0, 0), (700, 0), (1400, 0), (1440, 10), (1470, 40), (1480, 80), (1470, 120), (1440, 150),
                (1400, 160), (1000, 160), (960, 170), (930, 200), (900, 235), (860, 250), (820, 235), (790, 200),
                (760, 170), (720, 160), (300, 160), (0, 160), (-40, 150), (-60, 120), (-60, 40), (-40, 10),
            ]),
        (
            "sweeper_and_kink",
            [
                (0, 0), (900, 80), (1700, 400), (2100, 1000), (2000, 1700), (1500, 2100), (900, 2250), (700, 2300),
                (620, 2380), (640, 2480), (560, 2560), (300, 2500), (-200, 2000), (-500, 1200), (-400, 500),
            ]),
    ];

    private static string FixturePath() => Path.Combine(
        RepoPaths.Root(), "ui", "prototype", "tests", "fixtures", "track-geometry-reference.json");

    [Fact]
    public void Fixture_MatchesTrackGeometry()
    {
        var expected = BuildFixture();
        var path = FixturePath();

        if (Environment.GetEnvironmentVariable("PADDOCK_UPDATE_FIXTURES") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Compact(expected.ToJsonString(new JsonSerializerOptions { WriteIndented = true })) + "\n");
        }

        Assert.True(File.Exists(path), $"Missing {path}. Generate it with PADDOCK_UPDATE_FIXTURES=1.");
        var actual = JsonNode.Parse(File.ReadAllText(path))!.AsObject();

        var expectedCases = expected["cases"]!.AsArray();
        var actualCases = actual["cases"]!.AsArray();
        Assert.Equal(expectedCases.Count, actualCases.Count);

        for (var c = 0; c < expectedCases.Count; c++)
        {
            var e = expectedCases[c]!.AsObject();
            var a = actualCases[c]!.AsObject();
            Assert.Equal((string)e["name"]!, (string)a["name"]!);
            Assert.Equal((double)e["length_m"]!, (double)a["length_m"]!, precision: 6);

            var ePoints = e["points"]!.AsArray();
            var aPoints = a["points"]!.AsArray();
            Assert.Equal(ePoints.Count, aPoints.Count);
            for (var i = 0; i < ePoints.Count; i++)
            {
                for (var k = 0; k < 3; k++)
                {
                    Assert.True(
                        Math.Abs((double)ePoints[i]![k]! - (double)aPoints[i]![k]!) < 1e-6,
                        $"{(string)e["name"]!} sample {i} component {k} drifted; regenerate the fixture if the change is intended.");
                }
            }
        }
    }

    /// <summary>Puts every array of plain numbers on one line so the fixture stays readable in a diff.</summary>
    private static string Compact(string indented) => Regex.Replace(
        indented,
        @"\[\s*(-?[\d.eE+\-]+(?:,\s*-?[\d.eE+\-]+)*)\s*\]",
        m => "[" + Regex.Replace(m.Groups[1].Value, @"\s+", " ") + "]");

    private static JsonObject BuildFixture()
    {
        var cases = new JsonArray();
        foreach (var (name, points) in Cases)
        {
            var geometry = TrackGeometry.Build(points, referenceLengthM: null, stepM: 2.0);
            var count = geometry.Samples.Count;
            var samples = new JsonArray();
            for (var j = 0; j < SamplesPerCase; j++)
            {
                var index = (int)((long)j * count / SamplesPerCase);
                var (x, y) = geometry.Samples[index];
                samples.Add(new JsonArray((double)index / count, x, y));
            }

            var controlPoints = new JsonArray();
            foreach (var (x, y) in points)
            {
                controlPoints.Add(new JsonArray(x, y));
            }

            cases.Add(new JsonObject
            {
                ["name"] = name,
                ["control_points"] = controlPoints,
                ["length_m"] = geometry.LengthM,
                ["points"] = samples,
            });
        }

        return new JsonObject
        {
            ["comment"] = "Generated by tests/Paddock.Tests/World/TrackSplineReferenceFixtureTests.cs from TrackGeometry.Build. "
                + "points are [lap fraction, x, y] in metres. Do not edit by hand.",
            ["samples_per_case"] = SamplesPerCase,
            ["cases"] = cases,
        };
    }
}
