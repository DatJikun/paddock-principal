namespace Paddock.Domain.Finance;

/// <summary>
/// Every guessed number in finance v0. None of these is a measured 1955 fact (PP-050: benchmark estimates are accepted).
/// The ledger draws <b>no</b> random stream (INV-004): nothing here is sampled.
/// </summary>
public static class FinanceEstimates
{
    /// <summary>ESTIMATE: popularity index, in thousandths, at the start of a career. 1000 is 1.0.</summary>
    public const int BaselinePopularityMilli = 1000;

    /// <summary>ESTIMATE: popularity cannot fall below this (thousandths). 700 is 0.70.</summary>
    public const int MinPopularityMilli = 700;

    /// <summary>ESTIMATE: popularity cannot rise above this (thousandths). 1400 is 1.40.</summary>
    public const int MaxPopularityMilli = 1400;

    /// <summary>ESTIMATE: added to the index for a close title fight (many winners, tiny points gap). See <see cref="PopularityModel"/>.</summary>
    public const double CloseFightGain = 0.08;

    /// <summary>ESTIMATE: subtracted from the index for a dominant season (one winner, a large points gap).</summary>
    public const double DominanceLoss = 0.08;

    /// <summary>
    /// ESTIMATE (PP-057): added for a close drivers' title, independent of how many teams won.
    /// Sized so a dead heat between one team's drivers (2016) still finishes below a close fight among several teams,
    /// and above a season where one driver also runs away with it.
    /// </summary>
    public const double DriverFightGain = 0.10;

    /// <summary>
    /// ESTIMATE: share of the typical team's annual benchmark paid as start money across the season
    /// (<c>promoter_individual_deals</c>), then split per race and per entry.
    /// </summary>
    public const double StartMoneyShare = 0.30;

    /// <summary>ESTIMATE: share of the typical benchmark paid as prize money across the season, then split per race by position.</summary>
    public const double PrizeMoneyShare = 0.20;

    /// <summary>ESTIMATE: share of the typical benchmark spent on travelling to and running a season of races, split per race.</summary>
    public const double RaceRunningShare = 0.15;

    /// <summary>
    /// ESTIMATE: share of the typical benchmark in the fallback <see cref="RevenueModels.PoolByPosition"/> pool.
    /// Used only when the era's <c>revenue_model</c> is not <c>promoter_individual_deals</c>. Not calibrated.
    /// </summary>
    public const double FallbackPoolShare = 0.50;

    /// <summary>ESTIMATE: classified positions 1 through this many earn prize weight. Others earn none.</summary>
    public const int PrizePositions = 6;

    /// <summary>ESTIMATE: cash strictly below this (cents) starts the insolvency grace. Zero means any overdraft.</summary>
    public const long InsolvencyThresholdCents = 0;

    /// <summary>
    /// ESTIMATE bands for <see cref="PreviousSeasonStandingTier"/>: previous constructors' championship position 1 is
    /// <see cref="TeamTier.Top"/>, positions 2 through this value are <see cref="TeamTier.Typical"/>, the rest are
    /// <see cref="TeamTier.Low"/>. A team with no previous standing is <see cref="TeamTier.Typical"/>.
    /// </summary>
    public const int TypicalStandingThrough = 3;

    /// <summary>ESTIMATE: forecast prize money assumes a finish in this position when the races ahead are unknown.</summary>
    public const int ForecastFinishPosition = 3;

    /// <summary>Salaries are posted on this day of each month. ESTIMATE schedule (not a historical pay day).</summary>
    public const int SalaryDayOfMonth = 1;

    public const string BudgetLow = "team_budget_low_nominal_usd";

    public const string BudgetTypical = "team_budget_typical_nominal_usd";

    public const string BudgetTop = "team_budget_top_nominal_usd";

    public const string RevenueModelDimension = "revenue_model";

    public const string PromoterModel = "promoter_individual_deals";

    /// <summary>Name of the fallback implementation. Not an authored <c>revenue_model</c> value.</summary>
    public const string PoolByPositionModel = "pool_by_position";
}
