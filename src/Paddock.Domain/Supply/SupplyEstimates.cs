namespace Paddock.Domain.Supply;

/// <summary>
/// Uncalibrated numbers of the supply system (T43). There is no source for 1955 prices or engine progress (research R13, R14 not
/// done): every value here is an ESTIMATE for the reviewer to calibrate. Nothing here is a fact about history.
/// </summary>
public static class SupplyEstimates
{
    /// <summary>ESTIMATE: extra thousandths a supplier asks for exclusivity.</summary>
    public const int ExclusiveMarkupMilli = 150;

    /// <summary>ESTIMATE: discount per season beyond the first, in thousandths.</summary>
    public const int YearsDiscountMilli = 20;

    /// <summary>ESTIMATE: the most a long deal can discount, in thousandths.</summary>
    public const int YearsDiscountCapMilli = 80;

    /// <summary>ESTIMATE: customers one supplier serves with one item at a time.</summary>
    public const int MaxCustomersPerSupplier = 5;

    /// <summary>ESTIMATE: engine rating points a partner supply adds to reliability (factory support).</summary>
    public const double PartnerReliabilitySupport = 3;

    /// <summary>ESTIMATE: how much an engine rating above or below 50 moves the car's power level.</summary>
    public const double EnginePowerWeight = 0.4;

    /// <summary>ESTIMATE: how much an engine rating above or below 50 moves the car's reliability level.</summary>
    public const double EngineReliabilityWeight = 0.3;

    /// <summary>ESTIMATE: yearly gain of a supplier's engine, in rating points.</summary>
    public const double ProgressPerSeason = 1.5;

    /// <summary>ESTIMATE: widest spread of a supplier's power around its base, in rating points.</summary>
    public const double PowerSpread = 10;

    /// <summary>ESTIMATE: widest spread of a supplier's reliability around its base, in rating points.</summary>
    public const double ReliabilitySpread = 8;

    /// <summary>ESTIMATE: widest spread of a supplier's efficiency around the neutral 50.</summary>
    public const double EfficiencySpread = 12;

    /// <summary>ESTIMATE: widest grip balance of a tyre supplier (the T30 profile range is 1).</summary>
    public const double TyreGripSpread = 0.5;

    /// <summary>ESTIMATE: partner-tuned bonus of a tyre supplier.</summary>
    public const double TyrePartnerBonus = 0.4;

    /// <summary>ESTIMATE: days before an unanswered or countered negotiation lapses. Same as the T39 default.</summary>
    public const int DeadlineDays = 30;

    /// <summary>ESTIMATE: the furthest ahead of the current season a deal may start.</summary>
    public const int MaxStartSeasonsAhead = 1;

    /// <summary>ESTIMATE: seasons behind the supplier's newest version a customer receives, by kind. Works gets the new version first.</summary>
    public static int LagSeasons(SupplyKind kind) => kind switch
    {
        SupplyKind.Works => 0,
        SupplyKind.Partner => 1,
        SupplyKind.Customer => 1,
        SupplyKind.LastYearEngine => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown supply kind."),
    };

    /// <summary>ESTIMATE: price of a kind in thousandths of the reference price. Works costs nothing: the team builds it.</summary>
    public static int PriceMilli(SupplyKind kind) => kind switch
    {
        SupplyKind.Works => 0,
        SupplyKind.Partner => 1200,
        SupplyKind.Customer => 1000,
        SupplyKind.LastYearEngine => 500,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown supply kind."),
    };

    /// <summary>ESTIMATE: annual reference price in thousandths of a typical team's era budget.</summary>
    public static int ReferenceBudgetMilli(SupplyItem item) => item switch
    {
        SupplyItem.Engine => 100,
        SupplyItem.Tyres => 40,
        SupplyItem.Fuel => 15,
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, "Unknown supply item."),
    };

    /// <summary>ESTIMATE: seasons a deal runs when it comes from the authored start data, by kind.</summary>
    public static int InitialSeasons(SupplyKind kind) => kind switch
    {
        SupplyKind.Works => 5,
        SupplyKind.LastYearEngine => 1,
        _ => 2,
    };
}
