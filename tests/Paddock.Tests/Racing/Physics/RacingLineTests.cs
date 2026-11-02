using Paddock.Domain.World.Tracks;
using Paddock.Simulation.Racing.Physics;

namespace Paddock.Tests.Racing.Physics;

/// <summary>The minimum-curvature racing line and its tables (#353, TECH §6.5a).</summary>
public class RacingLineTests
{
    /// <summary>
    /// Pinned on Linux; CI checks it on Windows too. It moves when the line, <c>TrackGeometry</c> or
    /// <c>monza_1972.json</c> changes on purpose (re-pin and say why in the PR). If it differs only on one OS, a
    /// platform-dependent function slipped into the build (TECH §6.5a): fix that, do not re-pin.
    /// </summary>
    private const string MonzaLineHash = "45C6B1E96F8E34CDD8CDACC15FD135FB946B83C72E67C02A2A845D957831D136";

    [Fact]
    public void TheLineTablesAreBitIdenticalToThePinnedHash() =>
        Assert.Equal(MonzaLineHash, LinePhysicsTestKit.Hash(LinePhysicsTestKit.Tables(LinePhysicsTestKit.Monza)));

    [Fact]
    public void OnACircleTheLineRunsAroundTheOutsideEdge()
    {
        const double radius = 150d;
        var line = RacingLine.Build(LinePhysicsTestKit.Circle(radius), widthM: 12d);

        // Counter-clockwise, so left is inside: the widest radius is the full offset to the right.
        Assert.All(line.OffsetsM, offset => Assert.Equal(-line.MaxOffsetM, offset, 6));
        Assert.Equal(2d * Math.PI * (radius + line.MaxOffsetM), line.LengthM, 0);
        Assert.All(line.CurvaturePerM, kappa => Assert.Equal(1d / (radius + line.MaxOffsetM), kappa, 5));
    }

    [Fact]
    public void TheLineStaysInsideTheWidthAndUsesAllOfIt()
    {
        foreach (var line in new[] { LinePhysicsTestKit.Monza, LinePhysicsTestKit.Monaco })
        {
            var bound = (line.WidthM / 2d) - (LinePhysicsConstants.CarWidthM2010 / 2d) - LinePhysicsConstants.EdgeMarginM;
            Assert.Equal(bound, line.MaxOffsetM, 12);
            Assert.All(line.OffsetsM, offset => Assert.InRange(offset, -bound, bound));
            Assert.Contains(line.OffsetsM, offset => offset > bound * 0.99);
            Assert.Contains(line.OffsetsM, offset => offset < -bound * 0.99);
            Assert.All(line.Headings, h => Assert.Equal(1d, Math.Sqrt((h.X * h.X) + (h.Y * h.Y)), 12));
            Assert.Equal(line.SegmentLengthsM.Sum(), line.LengthM, 6);
        }
    }

    [Fact]
    public void TheLineHasLessCurvatureThanTheCentreline()
    {
        foreach (var (layout, line) in new[] { ("monza_1972", LinePhysicsTestKit.Monza), ("monaco_1950", LinePhysicsTestKit.Monaco) })
        {
            var geometry = LinePhysicsTestKit.Geometry(layout);
            Assert.True(SumOfSquares(line.CurvaturePerM) < 0.8d * SumOfSquares(geometry.Curvature), layout);
            Assert.True(MaxAbs(line.CurvaturePerM) < MaxAbs(geometry.Curvature), layout);
        }
    }

    [Fact]
    public void WithNoRoomBesideTheCarTheLineIsTheCentreline()
    {
        var geometry = LinePhysicsTestKit.Geometry("monaco_1950");
        var line = RacingLine.Build(geometry, widthM: LinePhysicsConstants.CarWidthM2010);

        Assert.Equal(0d, line.MaxOffsetM);
        Assert.All(line.OffsetsM, offset => Assert.Equal(0d, offset));
        for (var i = 0; i < line.Count; i++)
        {
            Assert.Equal(geometry.Samples[i], line.Points[i]);
        }
    }

    [Fact]
    public void AWiderTrackGivesALineWithLessCurvature()
    {
        var geometry = LinePhysicsTestKit.Geometry("monaco_1950");
        var narrow = RacingLine.Build(geometry, widthM: 8d);
        var wide = RacingLine.Build(geometry, widthM: 15d);

        Assert.True(SumOfSquares(wide.CurvaturePerM) < SumOfSquares(narrow.CurvaturePerM));
    }

    [Fact]
    public void BadInputIsRejected()
    {
        var geometry = TrackGeometry.Fallback(3000);
        Assert.Throws<ArgumentNullException>(() => RacingLine.Build(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => RacingLine.Build(geometry, 0d));
        Assert.Throws<ArgumentOutOfRangeException>(() => RacingLine.Build(geometry, double.NaN));
    }

    private static double SumOfSquares(IReadOnlyList<double> values) => values.Sum(v => v * v);

    private static double MaxAbs(IReadOnlyList<double> values) => values.Max(Math.Abs);
}
