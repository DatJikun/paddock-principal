using Paddock.Domain.Random;

namespace Paddock.Tests.Random;

public class RngStreamsTests
{
    [Fact]
    public void CanonicalStreamNamesMatchTheSpec()
    {
        Assert.Equal(
            [
                "Weather",
                "LapNoise",
                "Incidents",
                "Failures",
                "PitStops",
                "Market",
                "AiDecisions",
                "People",
                "History",
                "LifeEvents",
            ],
            RngStreamName.All);
        Assert.Equal(RngStreamName.All.Count, RngStreamName.All.Distinct().Count());
    }

    [Fact]
    public void SameInputsProduceTheSameSequence()
    {
        var left = RngStreams.Derive(42, RngStreamName.Weather, 1950);
        var right = RngStreams.Derive(42, RngStreamName.Weather, 1950);

        Assert.Equal(Read(left, 16), Read(right, 16));
    }

    [Fact]
    public void DifferentStreamNamesSeasonsRoundsAndSeedsDiverge()
    {
        var baseline = RngStreams.Derive(42, RngStreamName.Weather, 1950).NextULong();

        Assert.NotEqual(baseline, RngStreams.Derive(42, RngStreamName.Market, 1950).NextULong());
        Assert.NotEqual(baseline, RngStreams.Derive(42, RngStreamName.Weather, 1951).NextULong());
        Assert.NotEqual(baseline, RngStreams.Derive(43, RngStreamName.Weather, 1950).NextULong());
        Assert.NotEqual(baseline, RngStreams.Derive(42, RngStreamName.Weather, 1950, round: 0).NextULong());
        Assert.NotEqual(
            RngStreams.Derive(42, RngStreamName.Weather, 1950, round: 0).NextULong(),
            RngStreams.Derive(42, RngStreamName.Weather, 1950, round: 1).NextULong());
    }

    [Fact]
    public void EachCanonicalStreamIsDistinctForTheSameSeason()
    {
        var firstDraws = RngStreamName.All
            .Select(name => RngStreams.Derive(1, name, 1950).NextULong())
            .ToList();

        Assert.Equal(firstDraws.Count, firstDraws.Distinct().Count());
    }

    [Fact]
    public void DrawingFromOneStreamDoesNotAffectAnother()
    {
        var untouched = RngStreams.Derive(7, RngStreamName.Incidents, 1960, round: 3);
        var observed = RngStreams.Derive(7, RngStreamName.Incidents, 1960, round: 3);
        var other = RngStreams.Derive(7, RngStreamName.LapNoise, 1960, round: 3);

        for (var i = 0; i < 100; i++)
        {
            _ = other.NextULong();
        }

        Assert.Equal(Read(untouched, 8), Read(observed, 8));
    }

    [Fact]
    public void ALaterSeasonDoesNotDependOnDrawsFromThePreviousSeason()
    {
        var later = RngStreams.Derive(9, RngStreamName.People, 1951);
        var earlier = RngStreams.Derive(9, RngStreamName.People, 1950);
        for (var i = 0; i < 500; i++)
        {
            _ = earlier.NextDouble();
        }

        var freshLater = RngStreams.Derive(9, RngStreamName.People, 1951);
        Assert.Equal(Read(freshLater, 4), Read(later, 4));
    }

    [Fact]
    public void EmptyOrNullStreamNameIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => RngStreams.Derive(1, null!, 1950));
        Assert.Throws<ArgumentException>(() => RngStreams.Derive(1, "", 1950));
    }

    private static ulong[] Read(Xoshiro256StarStar rng, int count)
    {
        var values = new ulong[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = rng.NextULong();
        }

        return values;
    }
}
