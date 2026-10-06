using Paddock.Domain.Development;

namespace Paddock.Domain.Infrastructure;

/// <summary>
/// Pure formulas of facilities: frontier, relative quality, upgrade cost and gain, upkeep, rented tests, and how they feed
/// development capacity. No clock and no RNG. A race result is never read from a facility level (PP-058).
/// </summary>
public static class InfrastructureMath
{
    /// <summary>ESTIMATE: the year's state of the art in milli-units. Grows every season so an unchanged facility ages (PP-026).</summary>
    public static int FrontierMilli(int year)
    {
        var years = Math.Max(0, year - 1950);
        return InfrastructureEstimates.FrontierMilli1950
            + (years * InfrastructureEstimates.FrontierGrowthMilliPerYear);
    }

    /// <summary>Standing quality while idle, or degraded standing while a rebuild is in progress.</summary>
    public static int WorkingMilli(int qualityMilli, bool building)
    {
        var standing = Math.Clamp(qualityMilli, 0, InfrastructureEstimates.MaxQualityMilli);
        if (!building)
        {
            return standing;
        }

        return (int)Math.Round(standing * InfrastructureEstimates.BuildingWorkShare, MidpointRounding.AwayFromZero);
    }

    /// <summary>Working quality divided by this year's frontier, 0 to a little above 1 when a team is ahead of the year.</summary>
    public static double Relative(int qualityMilli, int year, bool building)
    {
        var frontier = FrontierMilli(year);
        if (frontier <= 0)
        {
            return 0d;
        }

        return DevelopmentEstimates.Quantize(WorkingMilli(qualityMilli, building) / (double)frontier);
    }

    /// <summary>Absolute milli-units one upgrade adds. Shrinks as relative quality rises (PP-058 diminishing returns).</summary>
    public static int UpgradeGainMilli(int qualityMilli, int year)
    {
        var relative = Math.Clamp(Relative(qualityMilli, year, building: false), 0d, 1d);
        var room = Math.Max(0.05d, 1d - relative);
        var gain = InfrastructureEstimates.MaxUpgradeGainMilli * Math.Pow(room, InfrastructureEstimates.ReturnCurve);
        var milli = (int)Math.Round(gain, MidpointRounding.AwayFromZero);
        return Math.Clamp(Math.Max(InfrastructureEstimates.MinUpgradeGainMilli, milli), 1, InfrastructureEstimates.MaxQualityMilli);
    }

    /// <summary>Cents of typical-budget share. Rises as relative quality rises (PP-058 rising cost).</summary>
    public static long UpgradeCostCents(int qualityMilli, int year, long typicalBudgetCents)
    {
        if (typicalBudgetCents <= 0)
        {
            return 0L;
        }

        var relative = Math.Max(0d, Relative(qualityMilli, year, building: false));
        var share = InfrastructureEstimates.BaseUpgradeCostShare * Math.Pow(1d + relative, InfrastructureEstimates.CostCurve);
        var cents = (long)Math.Round(typicalBudgetCents * share, MidpointRounding.AwayFromZero);
        return Math.Max(1L, cents);
    }

    public static int UpgradeDays(int qualityMilli, int year)
    {
        var relative = Math.Clamp(Relative(qualityMilli, year, building: false), 0d, 1d);
        var days = InfrastructureEstimates.BaseUpgradeDays
            + (int)Math.Round(InfrastructureEstimates.ExtraUpgradeDays * relative, MidpointRounding.AwayFromZero);
        return Math.Max(1, days);
    }

    public static long UpkeepCents(int qualityMilli, int year, long typicalBudgetCents, bool building)
    {
        if (typicalBudgetCents <= 0)
        {
            return 0L;
        }

        var relative = Math.Max(0d, Relative(qualityMilli, year, building));
        var cents = (long)Math.Round(
            typicalBudgetCents * InfrastructureEstimates.UpkeepShareAtFull * relative,
            MidpointRounding.AwayFromZero);
        return Math.Max(0L, cents);
    }

    public static int AfterUpgradeMilli(int qualityMilli, int year)
    {
        var next = qualityMilli + UpgradeGainMilli(qualityMilli, year);
        return Math.Clamp(next, 0, InfrastructureEstimates.MaxQualityMilli);
    }

    /// <summary>Cents of one private test-track rental. Zero when the typical budget is missing.</summary>
    public static long TestRentalCents(long typicalBudgetCents)
    {
        if (typicalBudgetCents <= 0)
        {
            return 0L;
        }

        return Math.Max(1L, (long)Math.Round(typicalBudgetCents * InfrastructureEstimates.TestRentalShare, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// How many private tests the era's <c>in_season_testing</c> rule allows this year. Zero means the command is refused.
    /// </summary>
    public static int TestsAllowed(string? inSeasonTesting) => inSeasonTesting switch
    {
        "in_season_banned" => 0,
        "one_nominated_test" => InfrastructureEstimates.TestsNominated,
        "limited_days" => InfrastructureEstimates.TestsLimitedDays,
        "annual_mileage_cap" => InfrastructureEstimates.TestsMileageCap,
        _ => InfrastructureEstimates.TestsUnrestricted,
    };

    /// <summary>Multiplier on <see cref="EngineeringCapacity.Quality"/> from factory and later aero tools.</summary>
    public static double ExecutionQualityScale(double factoryRelative, double tunnelRelative, double cfdRelative)
    {
        var scale = InfrastructureEstimates.ExecutionFloor
            + (InfrastructureEstimates.ExecutionFactorySpan * Clamp01(factoryRelative))
            + (InfrastructureEstimates.ExecutionTunnelSpan * Clamp01(tunnelRelative))
            + (InfrastructureEstimates.ExecutionCfdSpan * Clamp01(cfdRelative));
        return DevelopmentEstimates.Quantize(scale);
    }

    /// <summary>Multiplier on project duration. Below 1 the factory makes parts faster; it never raises quality.</summary>
    public static double DurationScale(double factoryRelative)
    {
        var unit = Clamp01(factoryRelative);
        var scale = InfrastructureEstimates.DurationSlow
            + ((InfrastructureEstimates.DurationFast - InfrastructureEstimates.DurationSlow) * unit);
        return DevelopmentEstimates.Quantize(scale);
    }

    /// <summary>Multiplier on daily understanding growth from the factory and, later, a simulator. Track rental is a command, not this.</summary>
    public static double UnderstandingScale(double factoryRelative, double simulatorRelative)
    {
        var scale = InfrastructureEstimates.UnderstandingFactoryFloor
            + (InfrastructureEstimates.UnderstandingFactorySpan * Clamp01(factoryRelative))
            + (InfrastructureEstimates.UnderstandingSimulatorSpan * Clamp01(simulatorRelative));
        return DevelopmentEstimates.Quantize(scale);
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0d, 1d);
}
