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

    [Fact]
    public void EnginesFor_ReturnsEverySupplyForThatTeamSeason()
    {
        IReadOnlyList<EngineSupply> supplies =
        [
            new("mclaren", 1988, "Honda", "Honda RA168E", "partner"),
            new("mclaren", 1987, "Porsche", "TAG TTE PO1", "partner"),
            new("williams", 1988, "Judd", "Judd CV", "customer"),
            new("mclaren", 1988, "Honda", "Honda RA168E spare", "works"),
        ];

        var found = EngineBook.EnginesFor("mclaren", 1988, supplies);

        Assert.Equal(2, found.Count);
        Assert.All(found, supply => Assert.Equal("Honda", supply.Supplier));
        Assert.Empty(EngineBook.EnginesFor("ferrari", 1988, supplies));
    }

    [Fact]
    public void LineageOf_ReturnsTheIdOrNone()
    {
        IReadOnlyList<LineageSpan> spans =
        [
            new("tyrrell-mercedes", "tyrrell", 1970, 1998),
            new("tyrrell-mercedes", "mercedes", 2010, null),
        ];

        Assert.Null(TeamLineage.LineageOf("tyrrell", 1969, spans));
        Assert.Equal("tyrrell-mercedes", TeamLineage.LineageOf("tyrrell", 1975, spans));
        Assert.Equal("tyrrell-mercedes", TeamLineage.LineageOf("mercedes", 2015, spans));
    }

    [Fact]
    public void LineageOf_ThrowsWhenTwoLineagesCoverTheSeason()
    {
        IReadOnlyList<LineageSpan> spans =
        [
            new("alpha", "cooper", 1950, 1960),
            new("beta", "cooper", 1955, 1965),
        ];

        var ex = Assert.Throws<InvalidOperationException>(() => TeamLineage.LineageOf("cooper", 1958, spans));

        Assert.Contains("cooper", ex.Message, StringComparison.Ordinal);
        Assert.Contains("1958", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StaffAt_ReturnsEachStintCoveringTheSeason()
    {
        IReadOnlyList<StaffAssignment> assignments =
        [
            new("gordon_murray", "mclaren", "technical_director", 1987, 1991),
            new("ron_dennis", "mclaren", "owner", 1981, 2009),
            new("ron_dennis", "mclaren", "team_principal", 1981, 2009),
            new("gordon_murray", "brabham", "chief_designer", 1969, 1986),
        ];

        var found = StaffBook.StaffAt("mclaren", 1988, assignments);

        Assert.Equal(
            [
                new StaffAssignment("gordon_murray", "mclaren", "technical_director", 1987, 1991),
                new StaffAssignment("ron_dennis", "mclaren", "owner", 1981, 2009),
                new StaffAssignment("ron_dennis", "mclaren", "team_principal", 1981, 2009),
            ],
            found);
    }
}
