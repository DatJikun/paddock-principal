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

        Assert.True(errors.Count == 0, string.Join('\n', errors.Select(error => error.Code + ": " + error.Message)));
        Assert.Equal(50, data.Catalog.Count);
        Assert.Equal(266, data.Timeline.Count);
        Assert.Equal(23, data.OtherSeriesIdeas.Count);
        Assert.Equal(78, data.Circuits.Circuits.Count);
        Assert.Equal(156, data.Layouts.Count);
        Assert.Equal(1172, data.RaceLayoutMap.Count);
        Assert.Equal(40, data.Technologies.Count);
        Assert.Equal(1287, data.Engines.Entries.Count);
        Assert.Equal(213, data.ConstructorIds.Count);
        Assert.Equal(10, data.Lineage.Lineages.Count);
        Assert.Equal(51, data.Founders.Organizations.Count);
        Assert.Equal(182, data.Staff.Count);
        Assert.Equal(18, data.EraCatalog.Count);
        Assert.Equal(125, data.EraTimeline.Count);
        Assert.Equal(77, data.CpiYears.Count);
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
    public void EraSet_For_1975_StillAllowsTobaccoSponsorship_And_2010_EndsDirectLogos()
    {
        var data = LoadRepo();
        var in1975 = data.EraSetFor(1975);
        var in2010 = data.EraSetFor(2010);

        Assert.Equal(1975, in1975.Season);
        Assert.Equal(data.EraDimensionIds, in1975.Values.Keys);
        // 1973–2006 is the national-restrictions mosaic: tobacco sponsorship is still allowed, with local bans.
        Assert.Equal("national_restrictions_mosaic", in1975.Value("tobacco_advertising"));
        // From 2008 direct cigarette logos are gone. 2010 sits in that band.
        Assert.Equal("direct_logos_ended_surrogates_remain", in2010.Value("tobacco_advertising"));
        Assert.Equal(
            in1975.Value("tobacco_advertising"),
            EraSet.For(1975, data.EraDimensionIds, data.EraPeriods).Value("tobacco_advertising"));
    }

    [Fact]
    public void CostCap_IsAbsentIn1990_AndPresentIn2022()
    {
        var data = LoadRepo();

        // The team cost cap is the regulations dimension budget_cap_usd_million.
        Assert.Equal("none", data.RuleSetFor(1990).Value("budget_cap_usd_million"));
        Assert.Equal("base_140", data.RuleSetFor(2022).Value("budget_cap_usd_million"));
        // The era revenue model names the Concorde agreement that introduced the cap.
        Assert.Equal("concorde_2_fopa_expansion", data.EraSetFor(1990).Value("revenue_model"));
        Assert.Equal("concorde_8_cost_cap_equitable", data.EraSetFor(2022).Value("revenue_model"));
    }

    [Fact]
    public void Cpi_ToUsd2025_RoundTripsTheBaseYear_UsingTheFileIndexes()
    {
        var book = LoadRepo().CpiBook;
        const decimal nominal = 1_000_000m;

        Assert.Equal(24.067m, book.Index(1950));
        Assert.Equal(321.943m, book.Index(CpiBook.BaseYear));
        Assert.Equal(331.655m, book.Index(2026));
        Assert.Equal(nominal, book.ToUsd2025(nominal, CpiBook.BaseYear));
        Assert.Equal(nominal * 321.943m / 24.067m, book.ToUsd2025(nominal, 1950));
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

    [Fact]
    public void RealFiles_LanciaD50HandoverEvent_ExistsAndIsValid()
    {
        // The team_events.json file is not yet loaded by AuthoredDataLoader, but this test
        // verifies the D50 handover event is properly formed as historical proposal data.
        var dataRoot = Path.Combine(RepoPaths.Root(), "data");
        var teamEventsPath = Path.Combine(dataRoot, "authored", "events", "team_events.json");
        var json = File.ReadAllText(teamEventsPath);

        // Verify the event exists in the file
        Assert.Contains("lancia_d50_handed_to_ferrari_1955", json);
        Assert.Contains("\"kind\": \"team_sold\"", json);
        Assert.Contains("\"counterparty\": \"ferrari\"", json);

        // Verify it mentions the key details: cars handover and payment terms
        Assert.Contains("D50 cars", json);
        Assert.Contains("50 million lire", json);
        Assert.Contains("5 years", json);
    }

    private static AuthoredData LoadRepo() =>
        AuthoredDataLoader.Load(Path.Combine(RepoPaths.Root(), "data"));
}
