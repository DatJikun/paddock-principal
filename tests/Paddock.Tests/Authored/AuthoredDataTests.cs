using Paddock.Data.Authored;
using Paddock.Domain.World;

namespace Paddock.Tests.Authored;

public class AuthoredDataTests
{
    [Fact(Skip = "Expected failure until the authored files are corrected. Offending entries: founders organization 'williams' (founded 1977, constructor entry williams 1975-1976); lineage 'jordan-aston-martin' (mf1 2006 overlaps spyker_mf1 2006). Do not loosen the rules. See issue #21.")]
    public void RealFiles_ValidateWithNoErrors()
    {
        var errors = AuthoredDataValidator.Validate(LoadRepo());
        Assert.True(errors.Count == 0, string.Join('\n', errors.Select(error => error.Code + ": " + error.Message)));
    }

    [Fact]
    public void RealFiles_OnlyKnownDataFailures_AreReported()
    {
        var data = LoadRepo();
        var errors = AuthoredDataValidator.Validate(data);

        Assert.Equal(
            [
                new AuthoredDataError(
                    AuthoredDataValidator.LineageOverlap,
                    "lineage 'jordan-aston-martin' entries 'mf1' (2006) and 'spyker_mf1' (2006) overlap"),
                new AuthoredDataError(
                    AuthoredDataValidator.FoundedAfterEntry,
                    "organization 'williams' founded 1977 is after constructor 'williams' entry 1975-1976"),
            ],
            errors);
        Assert.Equal(41, data.Catalog.Count);
        Assert.Equal(232, data.Timeline.Count);
        Assert.Equal(23, data.OtherSeriesIdeas.Count);
        Assert.Equal(78, data.Circuits.Circuits.Count);
        Assert.Equal(156, data.Layouts.Count);
        Assert.Equal(1172, data.RaceLayoutMap.Count);
        Assert.Equal(40, data.Technologies.Count);
        Assert.Equal(1287, data.Engines.Entries.Count);
        Assert.Equal(213, data.ConstructorIds.Count);
        Assert.Equal(10, data.Lineage.Lineages.Count);
        Assert.Equal(50, data.Founders.Organizations.Count);
        Assert.Equal(182, data.Staff.Count);
    }

    [Fact]
    public void RealFiles_EnginesFor_Mclaren1988_ContainsHonda()
    {
        var data = LoadRepo();
        var supplies = data.EnginesFor("mclaren", 1988);

        Assert.Contains(supplies, supply => supply.Supplier == "Honda");
        Assert.Equal(supplies, EngineBook.EnginesFor("mclaren", 1988, data.EngineSupplies));
    }

    [Fact]
    public void RealFiles_LineageOf_Tyrrell1975_EqualsMercedes2015()
    {
        var data = LoadRepo();
        var tyrrell = data.LineageOf("tyrrell", 1975);
        var mercedes = data.LineageOf("mercedes", 2015);

        Assert.Equal(tyrrell, mercedes);
        Assert.Equal("tyrrell-mercedes", tyrrell);
        Assert.Equal(tyrrell, TeamLineage.LineageOf("tyrrell", 1975, data.LineageSpans));
    }

    [Fact]
    public void RealFiles_StaffAt_Mclaren1988_IncludesGordonMurrayAndRonDennis()
    {
        var data = LoadRepo();
        var staff = data.StaffAt("mclaren", 1988);

        Assert.Contains(staff, post => post.PersonId == "gordon_murray" && post.Role == "technical_director");
        Assert.Contains(staff, post => post.PersonId == "ron_dennis");
        Assert.Equal(staff, StaffBook.StaffAt("mclaren", 1988, data.StaffAssignments));
    }

    [Fact]
    public void RealFiles_GroundEffect_WasFirstUsedIn1977_AndPrerequisitesResolve()
    {
        var data = LoadRepo();
        var technology = Assert.Single(data.Technologies, item => item.Id == "ground_effect");

        Assert.Equal(1977, technology.FirstUsed.Season);
        Assert.NotEmpty(technology.Prerequisites);
        foreach (var prerequisite in technology.Prerequisites)
        {
            Assert.Contains(data.Technologies, item => item.Id == prerequisite);
        }
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
