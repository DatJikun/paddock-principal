using Paddock.Domain.World.Tracks;

namespace Paddock.Tests.World;

public class TrackGeometryTests
{
    [Fact]
    public void Circle_24Points_LengthWithinOnePercent_CurvatureWithinFivePercent()
    {
        const double radius = 100.0;
        const int pointCount = 24;
        var points = new List<(double X, double Y)>(pointCount);

        // Counter-clockwise circle (left turns)
        for (var i = 0; i < pointCount; i++)
        {
            var angle = 2.0 * Math.PI * i / pointCount;
            points.Add((radius * Math.Cos(angle), radius * Math.Sin(angle)));
        }

        var geom = TrackGeometry.Build(points, referenceLengthM: null, stepM: 2.0);

        var expectedLength = 2.0 * Math.PI * radius;
        var lengthError = Math.Abs(geom.LengthM - expectedLength) / expectedLength;
        Assert.True(lengthError < 0.01, $"Length error {lengthError:P3} was not within 1%. Length: {geom.LengthM}, expected: {expectedLength}");

        const double expectedCurvature = 1.0 / radius; // 0.01 m^-1
        Assert.NotEmpty(geom.Curvature);
        foreach (var kappa in geom.Curvature)
        {
            // Within 5% of 0.01 everywhere (positive for left turns)
            var curvatureError = Math.Abs(kappa - expectedCurvature) / expectedCurvature;
            Assert.True(
                curvatureError < 0.05,
                $"Curvature {kappa:F6} deviates by {curvatureError:P3} from {expectedCurvature:F6} (exceeds 5% tolerance)");
        }
    }

    [Fact]
    public void Stadium_StraightsAndBends_CurvatureMatches()
    {
        const double straightLength = 500.0;
        const double bendRadius = 100.0;
        var points = new List<(double X, double Y)>();

        // Bottom straight (west to east, Y = 0): X from 0 to 500
        for (var x = 0.0; x <= straightLength; x += 100.0)
        {
            points.Add((x, 0.0));
        }

        // East bend (turn left from -pi/2 to +pi/2 around center (500, 100))
        const int bendSteps = 8;
        for (var i = 1; i < bendSteps; i++)
        {
            var angle = -Math.PI / 2.0 + Math.PI * i / bendSteps;
            points.Add((straightLength + bendRadius * Math.Cos(angle), bendRadius + bendRadius * Math.Sin(angle)));
        }

        // Top straight (east to west, Y = 200): X from 500 down to 0
        for (var x = straightLength; x >= 0.0; x -= 100.0)
        {
            points.Add((x, 2.0 * bendRadius));
        }

        // West bend (turn left from +pi/2 to +3pi/2 around center (0, 100))
        for (var i = 1; i < bendSteps; i++)
        {
            var angle = Math.PI / 2.0 + Math.PI * i / bendSteps;
            points.Add((bendRadius * Math.Cos(angle), bendRadius + bendRadius * Math.Sin(angle)));
        }

        var geom = TrackGeometry.Build(points, referenceLengthM: null, stepM: 2.0);

        // On the straights (middle of bottom or top straight, well away from transitions)
        // bottom straight: Y near 0, X in [100, 400]
        var straightSamplesCount = 0;
        var bendSamplesCount = 0;

        for (var i = 0; i < geom.Samples.Count; i++)
        {
            var (x, y) = geom.Samples[i];
            var kappa = geom.Curvature[i];

            // Deep on the straights
            if ((Math.Abs(y) < 1.0 || Math.Abs(y - 200.0) < 1.0) && x >= 150.0 && x <= 350.0)
            {
                straightSamplesCount++;
                Assert.True(Math.Abs(kappa) < 0.0005, $"Curvature on straight at ({x:F1}, {y:F1}) was {kappa:F6}, expected ≈ 0");
            }

            // Deep in the bends (apex of east or west turn)
            if (x > straightLength + 70.0 || x < -70.0)
            {
                bendSamplesCount++;
                // Bend radius = 100 -> expected curvature ≈ 0.01 (positive for left turn)
                Assert.InRange(kappa, 0.008, 0.012);
            }
        }

        Assert.True(straightSamplesCount > 10, "Expected straight samples to be checked");
        Assert.True(bendSamplesCount > 10, "Expected bend samples to be checked");
    }

    [Fact]
    public void ReferenceLengthM_Scaling_HitsExactLength()
    {
        var points = new (double X, double Y)[]
        {
            (0.0, 0.0),
            (100.0, 0.0),
            (200.0, 0.0),
            (250.0, 50.0),
            (250.0, 150.0),
            (200.0, 200.0),
            (100.0, 200.0),
            (0.0, 200.0),
            (-50.0, 150.0),
            (-50.0, 50.0),
        };

        const double targetLength = 5800.0;
        var geom = TrackGeometry.Build(points, referenceLengthM: targetLength, stepM: 2.0);

        Assert.Equal(targetLength, geom.LengthM, precision: 6);
        Assert.True(geom.ScaleFactor > 0.0);
        Assert.NotEqual(1.0, geom.ScaleFactor);
    }

    [Fact]
    public void LeftVsRightTurns_GiveOppositeCurvatureSigns()
    {
        const double radius = 100.0;
        const int n = 16;
        var pointsLeft = new List<(double X, double Y)>();
        var pointsRight = new List<(double X, double Y)>();

        // Left turn = CCW
        for (var i = 0; i < n; i++)
        {
            var angle = 2.0 * Math.PI * i / n;
            pointsLeft.Add((radius * Math.Cos(angle), radius * Math.Sin(angle)));
        }

        // Right turn = CW
        for (var i = 0; i < n; i++)
        {
            var angle = -2.0 * Math.PI * i / n;
            pointsRight.Add((radius * Math.Cos(angle), radius * Math.Sin(angle)));
        }

        var geomLeft = TrackGeometry.Build(pointsLeft, stepM: 2.0);
        var geomRight = TrackGeometry.Build(pointsRight, stepM: 2.0);

        Assert.All(geomLeft.Curvature, kappa => Assert.True(kappa > 0.005, "Left turn should have positive curvature"));
        Assert.All(geomRight.Curvature, kappa => Assert.True(kappa < -0.005, "Right turn should have negative curvature"));
    }

    [Fact]
    public void Determinism_TwoBuildsAreBitIdentical()
    {
        var points = new (double X, double Y)[]
        {
            (100.0, 0.0),
            (500.0, 0.0),
            (700.0, 200.0),
            (700.0, 600.0),
            (400.0, 800.0),
            (0.0, 800.0),
            (-200.0, 500.0),
            (-100.0, 200.0),
        };

        var build1 = TrackGeometry.Build(points, referenceLengthM: 3500.0, stepM: 2.0);
        var build2 = TrackGeometry.Build(points, referenceLengthM: 3500.0, stepM: 2.0);

        Assert.Equal(build1.LengthM, build2.LengthM);
        Assert.Equal(build1.ScaleFactor, build2.ScaleFactor);
        Assert.Equal(build1.Samples.Count, build2.Samples.Count);
        Assert.Equal(build1.Curvature.Count, build2.Curvature.Count);

        for (var i = 0; i < build1.Samples.Count; i++)
        {
            Assert.Equal(build1.Samples[i].X, build2.Samples[i].X);
            Assert.Equal(build1.Samples[i].Y, build2.Samples[i].Y);
            Assert.Equal(build1.Curvature[i], build2.Curvature[i]);
        }

        var svg1 = build1.SvgPath(500.0);
        var svg2 = build2.SvgPath(500.0);
        Assert.Equal(svg1, svg2);
    }

    [Fact]
    public void SvgPath_FitsInsideViewBox_AndFollowsStandardFormat()
    {
        var points = new (double X, double Y)[]
        {
            (0.0, 0.0),
            (300.0, 0.0),
            (400.0, 100.0),
            (400.0, 300.0),
            (300.0, 400.0),
            (0.0, 400.0),
            (-100.0, 300.0),
            (-100.0, 100.0),
        };

        var geom = TrackGeometry.Build(points, stepM: 2.0);
        const double viewBox = 600.0;
        var path = geom.SvgPath(viewBox);

        Assert.StartsWith("M ", path, StringComparison.Ordinal);
        Assert.EndsWith(" Z", path, StringComparison.Ordinal);
        Assert.Contains(" L ", path, StringComparison.Ordinal);
    }
}
