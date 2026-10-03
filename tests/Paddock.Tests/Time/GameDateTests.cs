using Paddock.Domain.Time;

namespace Paddock.Tests.Time;

public class GameDateTests
{
    [Fact]
    public void LeapYearsFollowTheGregorianRule()
    {
        Assert.Equal(new GameDate(1952, 2, 29), new GameDate(1952, 2, 28).AddDays(1));
        Assert.Equal(new GameDate(1952, 3, 1), new GameDate(1952, 2, 29).AddDays(1));
        Assert.Equal(61, new GameDate(1952, 3, 1).DayOfYear);
        Assert.Equal(366, new GameDate(1952, 12, 31).DayOfYear);

        Assert.Equal(new GameDate(1951, 3, 1), new GameDate(1951, 2, 28).AddDays(1));
        Assert.Equal(60, new GameDate(1951, 3, 1).DayOfYear);
        Assert.Equal(365, new GameDate(1951, 12, 31).DayOfYear);

        Assert.Equal(new GameDate(1900, 3, 1), new GameDate(1900, 2, 28).AddDays(1));
        Assert.Equal(new GameDate(2000, 2, 29), new GameDate(2000, 2, 28).AddDays(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameDate(1951, 2, 29));
    }

    [Fact]
    public void SeasonBoundariesAreTheCalendarYear()
    {
        var start = GameDate.SeasonStart(1952);
        var end = GameDate.SeasonEnd(1951);

        Assert.Equal(new GameDate(1952, 1, 1), start);
        Assert.True(start.IsSeasonStart);
        Assert.Equal(1, start.DayOfYear);
        Assert.Equal(1952, start.Season);
        Assert.False(start.IsSeasonEnd);

        Assert.Equal(new GameDate(1951, 12, 31), end);
        Assert.True(end.IsSeasonEnd);
        Assert.False(end.IsSeasonStart);
        Assert.Equal(start, end.AddDays(1));
        Assert.False(start.AddDays(-1).IsSeasonStart);
        Assert.Equal(new GameDate(1951, 12, 31), start.AddDays(-1));
        Assert.Equal(0, start.AddDays(0).DaysUntil(start));
        Assert.Equal(1, end.DaysUntil(start));
    }

    [Fact]
    public void DatesOrderByYearThenMonthThenDay()
    {
        Assert.True(new GameDate(1950, 12, 31) < new GameDate(1951, 1, 1));
        Assert.True(new GameDate(1951, 1, 2) > new GameDate(1951, 1, 1));
        Assert.True(new GameDate(1951, 3, 1) >= new GameDate(1951, 3, 1));
        Assert.True(GameDate.SeasonStart(1950) < GameDate.SeasonEnd(1950));
        Assert.True(GameDate.SeasonEnd(1950) <= GameDate.SeasonStart(1951));
    }
}
