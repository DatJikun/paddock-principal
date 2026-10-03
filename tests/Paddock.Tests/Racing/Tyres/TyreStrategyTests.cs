using Paddock.Simulation.Racing.Tyres;

namespace Paddock.Tests.Racing.Tyres;

/// <summary>Calculators only; all numbers behind them are ESTIMATE.</summary>
public class TyreStrategyTests
{
    private static TyreCompound Compound(string id) =>
        TyreCompoundCatalog.Default.AvailableCompounds(2020).Single(c => c.Id == id);

    [Fact]
    public void StintLength_IsLapsUntilTheCliff()
    {
        var c3 = Compound("C3");
        var stint = TyreStrategy.StintLength(c3, TyreConditions.Reference);
        Assert.Equal(30, stint);

        // The last lap of the stint is below the cliff, the next one is past it.
        var lastLossStep = TyreWear.LossAtAge(c3, stint - 1, TyreConditions.Reference) - TyreWear.LossAtAge(c3, stint - 2, TyreConditions.Reference);
        var overCliffStep = TyreWear.LossAtAge(c3, stint + 1, TyreConditions.Reference) - TyreWear.LossAtAge(c3, stint, TyreConditions.Reference);
        Assert.True(overCliffStep > 5 * lastLossStep);
    }

    [Fact]
    public void StintLength_ShrinksWithAbrasiveTracksAndHeavyCars_AndGrowsWithHarderCompounds()
    {
        var c3 = Compound("C3");
        var typical = TyreStrategy.StintLength(c3, TyreConditions.Reference);
        Assert.True(TyreStrategy.StintLength(c3, new TyreConditions(0.9, 0.5, 1.0)) < typical);
        Assert.True(TyreStrategy.StintLength(c3, new TyreConditions(0.5, 0.5, 1.3)) < typical);
        Assert.True(TyreStrategy.StintLength(c3, new TyreConditions(0.5, 0.9, 1.0)) > typical);
        Assert.True(TyreStrategy.StintLength(Compound("C1"), TyreConditions.Reference) > typical);
        Assert.True(TyreStrategy.StintLength(Compound("C5"), TyreConditions.Reference) < typical);
    }

    [Fact]
    public void StintLength_WithALossBudget_EndsEarlier_ButNotBeforeOneLap()
    {
        var c3 = Compound("C3");
        var limited = TyreStrategy.StintLength(c3, TyreConditions.Reference, maxWearLossSeconds: 0.5);
        Assert.InRange(limited, 1, TyreStrategy.StintLength(c3, TyreConditions.Reference) - 1);
        Assert.Equal(1, TyreStrategy.StintLength(c3, TyreConditions.Reference, maxWearLossSeconds: 0));
        Assert.Equal(
            TyreStrategy.StintLength(c3, TyreConditions.Reference),
            TyreStrategy.StintLength(c3, TyreConditions.Reference, maxWearLossSeconds: 1000));
        Assert.Throws<ArgumentOutOfRangeException>(() => TyreStrategy.StintLength(c3, TyreConditions.Reference, -1));
    }

    [Fact]
    public void PitWindow_IsOrdered_AndOpensLaterWhenAStopCostsMore()
    {
        var c3 = Compound("C3");
        var previous = 0;
        foreach (var pitLoss in new[] { 0.0, 5, 10, 20, 40, 80 })
        {
            var window = TyreStrategy.PitWindow(c3, TyreConditions.Reference, pitLoss);
            Assert.InRange(window.EarliestLap, 1, window.LatestLap);
            Assert.Equal(TyreStrategy.StintLength(c3, TyreConditions.Reference), window.LatestLap);
            Assert.True(window.EarliestLap >= previous);
            previous = window.EarliestLap;
        }

        var normal = TyreStrategy.PitWindow(c3, TyreConditions.Reference, 20);
        Assert.True(normal.EarliestLap < normal.LatestLap);
        Assert.Equal(c3.CliffLap, TyreStrategy.PitWindow(c3, TyreConditions.Reference, 1000).LatestLap);
    }

    [Fact]
    public void Undercut_GainsTimeAgainstAWornSet_AndNothingAgainstAnEqualOne()
    {
        var c3 = Compound("C3");
        var worn = TyreStrategy.UndercutGain(c3, 24, c3, TyreConditions.Reference);
        Assert.True(worn > 0, $"gain was {worn}");

        var equal = TyreStrategy.UndercutGain(c3, 0, c3, TyreConditions.Reference);
        Assert.Equal(0, equal, 12);

        // The older the rival's set, the more an undercut gains.
        Assert.True(
            TyreStrategy.UndercutGain(c3, 28, c3, TyreConditions.Reference)
            > TyreStrategy.UndercutGain(c3, 18, c3, TyreConditions.Reference));
    }

    [Fact]
    public void Undercut_PastTheCliff_GainsMuch_AndMoreLapsMeanMoreGain()
    {
        var c3 = Compound("C3");
        var pastCliff = TyreStrategy.UndercutGain(c3, 35, c3, TyreConditions.Reference);
        Assert.True(pastCliff > 2, $"gain was {pastCliff}");
        Assert.True(
            TyreStrategy.UndercutGain(c3, 24, c3, TyreConditions.Reference, rivalExtraLaps: 3)
            > TyreStrategy.UndercutGain(c3, 24, c3, TyreConditions.Reference, rivalExtraLaps: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => TyreStrategy.UndercutGain(c3, 10, c3, TyreConditions.Reference, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TyreStrategy.UndercutGain(c3, -1, c3, TyreConditions.Reference));
    }

    [Fact]
    public void Calculators_AreDeterministic()
    {
        var c4 = Compound("C4");
        var conditions = new TyreConditions(0.7, 0.4, 1.1);
        Assert.Equal(TyreStrategy.PitWindow(c4, conditions, 22), TyreStrategy.PitWindow(c4, conditions, 22));
        Assert.Equal(
            TyreStrategy.UndercutGain(c4, 15, Compound("C3"), conditions),
            TyreStrategy.UndercutGain(c4, 15, Compound("C3"), conditions));
    }
}
