using Paddock.Domain.World;
using Paddock.Simulation.Racing.Pits;

namespace Paddock.Tests.Racing.Pits;

/// <summary>Reads the real regulation catalog; the numbers the data does not have are ESTIMATE.</summary>
public class PitRulesTests
{
    [Fact]
    public void EverySeasonOfTheData_HasPitRules()
    {
        for (var season = 1950; season <= 2026; season++)
        {
            var rules = PitTestKit.Rules(season);
            Assert.Equal(season, rules.Season);
            Assert.True(rules.StopsAllowed, season.ToString());
            Assert.True(rules.StandardCrewSize >= 4, season.ToString());
        }
    }

    [Fact]
    public void FiftiesAreOnTheOldRules_SharedCarsChangeDriver_NoLaneLimit_NoMixRule()
    {
        var rules = PitTestKit.Rules(1953);

        Assert.True(rules.TyreChangeAllowed);
        Assert.True(rules.RefuellingAllowed);
        Assert.True(rules.DriverChangeAllowed);
        Assert.Equal(1, rules.RequiredDistinctDryCompounds);
        Assert.Null(rules.PitLaneSpeedLimitKph);
        Assert.Equal(0, rules.MinimumStopSeconds);
        Assert.Equal(0, rules.MinimumStops(dryRace: true));
        Assert.Equal(4, rules.StandardCrewSize);
    }

    [Fact]
    public void DriverChanges_FollowTheSharedDrivePointsRule()
    {
        for (var season = 1950; season <= 1957; season++)
        {
            Assert.True(PitTestKit.Rules(season).DriverChangeAllowed, season.ToString());
        }

        for (var season = 1958; season <= 2026; season++)
        {
            Assert.False(PitTestKit.Rules(season).DriverChangeAllowed, season.ToString());
        }
    }

    [Fact]
    public void Refuelling_FollowsTheCatalog()
    {
        foreach (var (season, allowed) in new[] { (1950, true), (1983, true), (1984, false), (1993, false), (1994, true), (2009, true), (2010, false), (2026, false) })
        {
            Assert.Equal(allowed, PitTestKit.Rules(season).RefuellingAllowed);
        }
    }

    [Fact]
    public void TyreChanges_AreForbiddenOnlyIn2005_AndTheMixIsMandatoryFrom2007()
    {
        Assert.False(PitTestKit.Rules(2005).TyreChangeAllowed);
        Assert.True(PitTestKit.Rules(2004).TyreChangeAllowed);
        Assert.True(PitTestKit.Rules(2006).TyreChangeAllowed);

        Assert.Equal(1, PitTestKit.Rules(2006).RequiredDistinctDryCompounds);
        foreach (var season in new[] { 2007, 2012, 2015, 2016, 2024 })
        {
            Assert.Equal(2, PitTestKit.Rules(season).RequiredDistinctDryCompounds);
        }
    }

    [Fact]
    public void MandatoryStops_NeedAMixRuleAndTyreChanges_AndAWetRaceWaivesThem()
    {
        Assert.Equal(1, PitTestKit.Rules(2012).MinimumStops(dryRace: true));
        Assert.Equal(0, PitTestKit.Rules(2012).MinimumStops(dryRace: false));
        Assert.Equal(0, PitTestKit.Rules(2005).MinimumStops(dryRace: true));
        Assert.Equal(0, PitTestKit.Rules(1990).MinimumStops(dryRace: true));
    }

    [Fact]
    public void MixSatisfied_NeedsTwoDifferentDryCompounds_UnlessWet()
    {
        var rules = PitTestKit.Rules(2012);
        bool IsWet(string id) => id == "wet";

        Assert.False(rules.MixSatisfied(["C3", "C3"], IsWet, wetRace: false));
        Assert.True(rules.MixSatisfied(["C3", "C4"], IsWet, wetRace: false));
        Assert.False(rules.MixSatisfied(["C3", "wet"], IsWet, wetRace: false));
        Assert.True(rules.MixSatisfied(["C3"], IsWet, wetRace: true));
        Assert.True(PitTestKit.Rules(1990).MixSatisfied(["C3"], IsWet, wetRace: false));
        Assert.True(PitTestKit.Rules(2005).MixSatisfied(["C3"], IsWet, wetRace: false));
    }

    [Fact]
    public void PitLaneSpeedLimit_StartsIn1993()
    {
        Assert.Null(PitTestKit.Rules(1992).PitLaneSpeedLimitKph);
        Assert.Equal(PitConstants.PitLaneLimitKph, PitTestKit.Rules(1993).PitLaneSpeedLimitKph);
        Assert.Equal(PitConstants.PitLaneLimitKph, PitTestKit.Rules(2020).PitLaneSpeedLimitKph);
    }

    [Fact]
    public void LaneLoss_IsLaneTimeAtTheLimitLessTheTimeOnTrack_PlusEntryAndExit()
    {
        var rules = PitTestKit.Rules(2000);
        var expected = (400 / (80 / 3.6)) - (400 / (160 / 3.6)) + PitConstants.EntryExitSeconds;

        Assert.Equal(expected, rules.PitLaneTimeLossSeconds(400, 160), 9);

        // A longer lane costs more, a lower limit costs more, a faster track costs more.
        Assert.True(rules.PitLaneTimeLossSeconds(600, 160) > rules.PitLaneTimeLossSeconds(400, 160));
        Assert.True(rules.PitLaneTimeLossSeconds(400, 220) > rules.PitLaneTimeLossSeconds(400, 160));
        Assert.True(rules.PitLaneTimeLossSeconds() > PitTestKit.Rules(1970).PitLaneTimeLossSeconds());
    }

    [Fact]
    public void LaneLoss_RejectsNonsense()
    {
        var rules = PitTestKit.Rules(2000);
        Assert.Throws<ArgumentOutOfRangeException>(() => rules.PitLaneTimeLossSeconds(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => rules.PitLaneTimeLossSeconds(400, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => rules.PitLaneTimeLossSeconds(double.NaN));
    }

    [Fact]
    public void MinimumStopTime_AndCrewSize_FollowTheEra()
    {
        Assert.Equal(0, PitTestKit.Rules(2009).MinimumStopSeconds);
        Assert.Equal(PitConstants.MinimumStopSeconds, PitTestKit.Rules(2010).MinimumStopSeconds);

        var sizes = Enumerable.Range(1950, 77).Select(s => PitTestKit.Rules(s).StandardCrewSize).ToArray();
        Assert.Equal(sizes.Order(), sizes);
        Assert.True(sizes[^1] > sizes[0]);
    }

    [Fact]
    public void MissingDimensions_GetCautiousDefaults_AndUnknownValuesThrow()
    {
        var bare = RuleSet.For(1990, ["other"], [new RulePeriod("other", "x", 1950, null)]);
        var rules = PitRules.For(bare);

        Assert.True(rules.TyreChangeAllowed);
        Assert.False(rules.RefuellingAllowed);
        Assert.False(rules.DriverChangeAllowed);
        Assert.Equal(1, rules.RequiredDistinctDryCompounds);

        var odd = RuleSet.For(1990, ["refuelling"], [new RulePeriod("refuelling", "sometimes", 1950, null)]);
        Assert.Throws<InvalidOperationException>(() => PitRules.For(odd));
    }
}
