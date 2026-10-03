using Paddock.Domain.People;

namespace Paddock.Domain.Pool;

/// <summary>
/// ESTIMATE rules for the talent pool, development in the pool and scouting (T40). None of these is a measured fact.
/// Every number sits here so it can be edited without hunting through call sites, and the pool code reads only this type.
/// Calibration of the lead years (ROADMAP open question 4) stays with the people schedule, not here.
/// </summary>
public static class PoolEstimates
{
    // Pool size and fillers (DESIGN 2.1).

    /// <summary>ESTIMATE: the pool is topped up with fictional fillers to this many drivers on the pool-entry day of each season.</summary>
    public const int TargetSize = 16;

    /// <summary>ESTIMATE: lower edge of the size the pool is expected to stay in (checked over 30 simulated years).</summary>
    public const int ExpectedMinSize = 8;

    /// <summary>ESTIMATE: upper edge of the size the pool is expected to stay in.</summary>
    public const int ExpectedMaxSize = 30;

    /// <summary>ESTIMATE: a member without a contract lapses after this many seasons in the pool.</summary>
    public const int MaxSeasonsInPool = 6;

    /// <summary>ESTIMATE: a member older than this (calendar-year age) lapses, once he has spent a season in the pool.</summary>
    public const int MaxAge = 28;

    /// <summary>ESTIMATE: the age rule needs at least this many seasons in the pool, so a late entrant is not dropped at once.</summary>
    public const int MinSeasonsBeforeAgeLapse = 1;

    /// <summary>ESTIMATE: a filler enters at this age or older (calendar-year age; the pool starts at about 17, DESIGN 2.1).</summary>
    public const int FillerAgeMin = 17;

    /// <summary>ESTIMATE: a filler enters younger than this age.</summary>
    public const int FillerAgeMaxExclusive = 21;

    /// <summary>
    /// ESTIMATE: quality mix of fillers, weights in thousandths. Mostly filler quality, a few solid drivers and rarely a future
    /// star, so the pool is not a list of future champions (DESIGN 2.1). There is no contender band: nobody enters as one.
    /// </summary>
    public static readonly (QualityBand Band, int Weight)[] FillerQualityWeights =
    [
        (QualityBand.Filler, 740),
        (QualityBand.Solid, 200),
        (QualityBand.FutureStar, 60),
    ];

    /// <summary>ESTIMATE: nationalities offered to fillers, each with weight 1.</summary>
    public static readonly string[] FillerNationalities = ["GBR", "ITA", "DEU", "FRA", "BRA", "USA"];

    // Development in the pool.

    /// <summary>ESTIMATE: share of the gap to the hidden potential that a member closes in one season, in percent, before luck.</summary>
    public const int DevelopmentRatePercent = 30;

    /// <summary>ESTIMATE: luck of one season, as a share of the rate, in percent. The lowest value.</summary>
    public const int LuckMinPercent = 50;

    /// <summary>ESTIMATE: luck of one season, in percent. The highest value.</summary>
    public const int LuckMaxPercent = 150;

    /// <summary>ESTIMATE: speed of a funded season of the cheap, slow programme, in percent of the unfunded rate.</summary>
    public const int CheapSlowSpeedPercent = 125;

    /// <summary>ESTIMATE: speed of a funded season of the expensive, fast programme, in percent of the unfunded rate.</summary>
    public const int ExpensiveFastSpeedPercent = 200;

    /// <summary>ESTIMATE: nominal cost of the cheap, slow programme. The economy is a later phase, so the unit is "not modelled".</summary>
    public const long CheapSlowCost = 50_000;

    /// <summary>ESTIMATE: nominal cost of the expensive, fast programme.</summary>
    public const long ExpensiveFastCost = 250_000;

    // Scouting (PP-013, DESIGN 6.2 and 10).

    /// <summary>ESTIMATE: half the width of a band at the start of observation, in attribute points (the band is about twice this).</summary>
    public const double StartHalfWidth = 5.0;

    /// <summary>ESTIMATE: half the width a band approaches with a lot of observation. Observation alone never makes a value exact.</summary>
    public const double MinHalfWidth = 0.5;

    /// <summary>ESTIMATE: observation points at which the band is halfway between its start and its minimum width.</summary>
    public const double HalfWidthTauPoints = 6.0;

    /// <summary>ESTIMATE: potential is harder to judge than a current attribute, so its band is this many times wider.</summary>
    public const double PotentialWidthFactor = 1.5;

    /// <summary>ESTIMATE: the least a band spans, in attribute points (high minus low).</summary>
    public const int MinBandSpan = 1;

    /// <summary>ESTIMATE: noise of a perfect scout (judgement 20) as a share of the half width. Below 1, so he never misses the truth.</summary>
    public const double BestScoutNoiseShare = 0.9;

    /// <summary>ESTIMATE: noise of the worst scout (judgement 1) as a share of the half width. Above 1, so he can miss the truth.</summary>
    public const double WorstScoutNoiseShare = 1.5;

    /// <summary>ESTIMATE: observation points per month (thousandths) on one person the organization follows closely.</summary>
    public const long PersonFocusMilliPerMonth = 1000;

    /// <summary>ESTIMATE: observation points per month (thousandths) on each member when the organization watches the whole pool.</summary>
    public const long PoolFocusMilliPerMonth = 250;

    /// <summary>ESTIMATE: observation points (thousandths) for each race the organization shared with the person.</summary>
    public const long SharedRaceMilli = 1500;

    /// <summary>ESTIMATE: contact network (1-20) scales observation: this many percent, plus <see cref="NetworkPercentPerPoint"/> per point.</summary>
    public const long NetworkBasePercent = 50;

    /// <summary>ESTIMATE: percent added to observation for each point of contact network.</summary>
    public const long NetworkPercentPerPoint = 5;

    /// <summary>ESTIMATE: talent judgement of an organization with no scout of its own (the team principal's eye).</summary>
    public const int DefaultScoutJudgement = 6;

    /// <summary>ESTIMATE: contact network of an organization with no scout of its own.</summary>
    public const int DefaultScoutNetwork = 6;

    /// <summary>Day of the month on which scouting observes. Time passes in months.</summary>
    public const int ScoutingDayOfMonth = 1;

    public static int SpeedPercent(JuniorProgramme programme) => programme switch
    {
        JuniorProgramme.CheapSlow => CheapSlowSpeedPercent,
        JuniorProgramme.ExpensiveFast => ExpensiveFastSpeedPercent,
        _ => throw new ArgumentOutOfRangeException(nameof(programme), programme, "Unknown junior programme."),
    };

    public static long CostOf(JuniorProgramme programme) => programme switch
    {
        JuniorProgramme.CheapSlow => CheapSlowCost,
        JuniorProgramme.ExpensiveFast => ExpensiveFastCost,
        _ => throw new ArgumentOutOfRangeException(nameof(programme), programme, "Unknown junior programme."),
    };
}
