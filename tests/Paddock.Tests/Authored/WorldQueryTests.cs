using Paddock.Domain.World;

namespace Paddock.Tests.Authored;

public class WorldQueryTests
{
    [Fact]
    public void RuleSet_For_ThrowsWhenTwoPeriodsCoverTheSeason()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => RuleSet.For(
            1976,
            ["refuelling"],
            [
                new RulePeriod("refuelling", "allowed", 1950, null),
                new RulePeriod("refuelling", "banned", 1970, 1980),
            ]));

        Assert.Contains("refuelling", ex.Message, StringComparison.Ordinal);
        Assert.Contains("1976", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LayoutFor_ThrowsWhenTheRoundIsNotAssigned()
    {
        var layout = new TrackLayout(
            "nurburgring_1951",
            "nurburgring",
            22.835,
            new Dictionary<string, double> { ["straights"] = 1d },
            ["long"]);

        var ex = Assert.Throws<InvalidOperationException>(() => RaceCalendar.LayoutFor(
            1976,
            99,
            [layout],
            [new RaceAssignment(1976, 10, layout.Id)]));

        Assert.Contains("99", ex.Message, StringComparison.Ordinal);
    }
}
