using Paddock.Domain.Racing;

namespace Paddock.Tests.Racing;

/// <summary>#331: the place of a driver by plain totals. Points first, then wins, then podiums; full equality shares a place.</summary>
public sealed class SeasonPlacesTests
{
    [Fact]
    public void TiesBreakByWinsThenPodiumsAndEqualRowsShareAPlace()
    {
        var places = SeasonPlaces.Of(
        [
            new SeasonTotal(1960, "a", 10m, 0, 2),
            new SeasonTotal(1960, "b", 10m, 1, 1),
            new SeasonTotal(1960, "c", 10m, 1, 1),
            new SeasonTotal(1960, "d", 3m, 0, 0),
            new SeasonTotal(1961, "a", 1m, 0, 0),
        ]);

        Assert.Equal(1, places[(1960, "b")]);
        Assert.Equal(1, places[(1960, "c")]);
        Assert.Equal(3, places[(1960, "a")]);
        Assert.Equal(4, places[(1960, "d")]);
        Assert.Equal(1, places[(1961, "a")]);
    }
}
