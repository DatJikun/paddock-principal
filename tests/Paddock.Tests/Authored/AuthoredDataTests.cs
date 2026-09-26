using Paddock.Data.Authored;
using Paddock.Domain.World;

namespace Paddock.Tests.Authored;

public class AuthoredDataTests
{
    [Fact]
    public void RealFiles_ValidateWithNoErrors()
    {
        var data = LoadRepo();
        var errors = AuthoredDataValidator.Validate(data);

        Assert.True(errors.Count == 0, string.Join('\n', errors.Select(error => error.Message)));
        Assert.Equal(41, data.Catalog.Count);
        Assert.Equal(232, data.Timeline.Count);
        Assert.Equal(23, data.OtherSeriesIdeas.Count);
        Assert.Equal(78, data.Circuits.Circuits.Count);
        Assert.Equal(156, data.Layouts.Count);
        Assert.Equal(1172, data.RaceLayoutMap.Count);
    }

    [Fact]
    public void RuleSet_For_1976_AllowsRefuelling_BansDrs_AndPaysNineForAWin()
    {
        var data = LoadRepo();
        var rules = RuleSet.For(1976, data.DimensionIds, data.Periods);

        Assert.Equal(1976, rules.Season);
        Assert.Equal(data.DimensionIds, rules.Values.Keys);
        Assert.Equal("allowed", rules.Value("refuelling"));
        Assert.Equal("banned", rules.Value("drs"));
        Assert.Equal("top6_9_6_4_3_2_1", rules.Value("points_scale"));
        Assert.Equal(rules.Value("refuelling"), data.RuleSetFor(1976).Value("refuelling"));
    }

    [Fact]
    public void RuleSet_For_2012_BansRefuelling_AndAllowsDrs()
    {
        var data = LoadRepo();
        var rules = RuleSet.For(2012, data.DimensionIds, data.Periods);

        Assert.Equal("banned", rules.Value("refuelling"));
        Assert.Equal("allowed", rules.Value("drs"));
    }

    [Fact]
    public void LayoutFor_1976_Round10_IsTheNordschleife()
    {
        // circuits.json calls layout nurburgring_1951 the Nordschleife (22.835 km).
        var data = LoadRepo();
        var layout = RaceCalendar.LayoutFor(1976, 10, data.Layouts, data.RaceAssignments);

        Assert.Equal("nurburgring_1951", layout.Id);
        Assert.Equal("nurburgring", layout.CircuitId);
        Assert.Equal(22.835, layout.LengthKm, precision: 3);
        Assert.Contains("long", layout.CharacterTags);
        Assert.Equal(layout.Id, data.LayoutFor(1976, 10).Id);
    }

    private static AuthoredData LoadRepo() =>
        AuthoredDataLoader.Load(Path.Combine(RepoPaths.Root(), "data"));
}
