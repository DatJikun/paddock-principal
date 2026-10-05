using Paddock.Domain.Infrastructure;

namespace Paddock.Tests.Infrastructure;

/// <summary>Pure formulas of the factory, rented tests and race logistics (#231, PP-064). Numbers are ESTIMATES.</summary>
public class InfrastructureMathTests
{
    [Fact]
    public void TheFrontierGrowsSoAnUnchangedFactoryAges()
    {
        var quality = 100_000;
        Assert.True(InfrastructureMath.Relative(quality, 1950, building: false) > InfrastructureMath.Relative(quality, 1960, building: false));
        Assert.Equal(InfrastructureEstimates.FrontierMilli1950, InfrastructureMath.FrontierMilli(1950));
        Assert.True(InfrastructureMath.FrontierMilli(1951) > InfrastructureMath.FrontierMilli(1950));
    }

    [Fact]
    public void ALaterUpgradeCostsMoreAndGainsLess()
    {
        const long typical = 10_000_000;
        var cheap = InfrastructureMath.UpgradeCostCents(0, 1955, typical);
        var dear = InfrastructureMath.UpgradeCostCents(90_000, 1955, typical);
        Assert.True(dear > cheap);
        Assert.True(InfrastructureMath.UpgradeGainMilli(0, 1955) > InfrastructureMath.UpgradeGainMilli(90_000, 1955));
        Assert.True(InfrastructureMath.UpgradeDays(90_000, 1955) > InfrastructureMath.UpgradeDays(0, 1955));
    }

    [Fact]
    public void BuildingHalvesTheWorkingQuality()
    {
        Assert.Equal(50_000, InfrastructureMath.WorkingMilli(100_000, building: true));
        Assert.Equal(100_000, InfrastructureMath.WorkingMilli(100_000, building: false));
    }

    [Fact]
    public void AFactoryAtTheFrontierExecutesBetterAndFasterThanOneAtZero()
    {
        var weak = InfrastructureMath.ExecutionQualityScale(0, 0, 0);
        var strong = InfrastructureMath.ExecutionQualityScale(1, 0, 0);
        Assert.True(strong > weak);
        Assert.True(InfrastructureMath.DurationScale(1) < InfrastructureMath.DurationScale(0));
    }

    [Fact]
    public void BannedTestingAllowsNoRentalsAndANominatedYearAllowsOne()
    {
        Assert.Equal(0, InfrastructureMath.TestsAllowed("in_season_banned"));
        Assert.Equal(InfrastructureEstimates.TestsNominated, InfrastructureMath.TestsAllowed("one_nominated_test"));
        Assert.Equal(InfrastructureEstimates.TestsUnrestricted, InfrastructureMath.TestsAllowed("unrestricted"));
        Assert.True(InfrastructureMath.TestRentalCents(10_000_000) > 0);
    }

    [Fact]
    public void ArgentinaIsAShipAndCostsMoreDaysThanAEuropeanLorry()
    {
        const long typical = 10_000_000;
        var monza = LogisticsMath.Quote("ITA", "ITA", typical);
        var spa = LogisticsMath.Quote("ITA", "BEL", typical);
        var argentina = LogisticsMath.Quote("ITA", LogisticsMath.Argentina, typical);
        Assert.Equal(LogisticsMode.Lorry, monza.Mode);
        Assert.Equal(LogisticsMode.Lorry, spa.Mode);
        Assert.Equal(LogisticsMode.Ship, argentina.Mode);
        Assert.True(argentina.Days > spa.Days);
        Assert.True(argentina.CostCents > spa.CostCents);
        Assert.True(spa.CostCents > monza.CostCents);
        Assert.Equal(InfrastructureEstimates.LogisticsShipDays, argentina.Days);
    }
}
