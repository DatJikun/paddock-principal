using Paddock.Data.Authored;
using Paddock.Domain.Racing;
using Paddock.Simulation.Racing;
using Paddock.Simulation.Racing.Pace;
using Paddock.Tests.Racing.Points;

namespace Paddock.Tests.Racing.Pace;

/// <summary>Cars brake for the corners on the map (#286): the lap's time spread by the era car's speed trace. Display only.</summary>
public class LapSpeedShapeTests
{
    [Fact]
    public void AnEvenSpeedKeepsTimeAndDistanceInStep()
    {
        var shape = LapSpeedShape.FromSpeeds([50, 50, 50, 50])!;

        Assert.Equal(0.25, shape.DistanceShareAt(0.25), 9);
        Assert.Equal(1.0, shape.SpeedRatioAt(0.6), 9);
    }

    [Fact]
    public void ASlowStretchTakesMoreOfTheLapsTime()
    {
        // Fast for the first half of the loop, slow for the second.
        var shape = LapSpeedShape.FromSpeeds([80, 80, 80, 80, 20, 20, 20, 20])!;

        Assert.True(shape.DistanceShareAt(0.25) > 0.4, "the fast half is covered in less than half the time");
        Assert.True(shape.SpeedRatioAt(0.2) > 1.2);
        Assert.True(shape.SpeedRatioAt(0.7) < 0.6);
        Assert.Equal(0d, shape.DistanceShareAt(0), 9);
        Assert.Equal(1d, shape.DistanceShareAt(1), 9);
    }

    [Fact]
    public void TheEraCarIsSlowInTheBendsOfARealShape()
    {
        var geometry = TrackGeometryCatalog.Create(PointsTestKit.Real).Resolve("aintree_1955").Geometry;
        var shape = LapSpeedShape.For(geometry, 1955)!;
        var ratios = Enumerable.Range(0, 100).Select(i => shape.SpeedRatioAt(i / 100d)).ToArray();

        Assert.True(ratios.Max() > 1.2, "faster than average on the straights");
        Assert.True(ratios.Min() < 0.8, "slower than average in the bends");
        for (var i = 1; i <= 100; i++)
        {
            Assert.True(shape.DistanceShareAt(i / 100d) >= shape.DistanceShareAt((i - 1) / 100d));
        }
    }

    [Fact]
    public void ShapedFramesStillCrossTheLineWhenTheTapeSays()
    {
        const double lapM = 1_000;
        var tape = RaceTape.From(
        [
            new RaceStarted(0, 0, 0, 2, ["a"]),
            new LapCompleted(1, 1, 10_000, "a", 10_000, 1, 0),
            new LapCompleted(2, 2, 20_000, "a", 10_000, 1, 0),
            new Finished(3, 2, 20_000, "a", 1, 2, 20_000),
            new RaceEnded(4, 2, 20_000),
        ]);
        var shape = LapSpeedShape.FromSpeeds([80, 80, 80, 80, 20, 20, 20, 20]);

        var even = LapFrameInterpolator.Attach(tape, lapM, 1);
        var shaped = LapFrameInterpolator.Attach(tape, lapM, 1, shape);

        double At(RaceTape t, long ms) => t.Frames.Single(f => f.CarId == "a" && f.RaceTimeMs == ms).DistanceM;
        Assert.Equal(At(even, 10_000), At(shaped, 10_000), 6);
        Assert.Equal(At(even, 20_000), At(shaped, 20_000), 6);
        Assert.True(At(shaped, 13_000) > At(even, 13_000), "ahead of an even pace on the fast stretch");
        var fast = shaped.Frames.Single(f => f.CarId == "a" && f.RaceTimeMs == 12_000).SpeedMps;
        var slow = shaped.Frames.Single(f => f.CarId == "a" && f.RaceTimeMs == 18_000).SpeedMps;
        Assert.True(fast > 2 * slow, "the speed follows the shape");
    }
}
