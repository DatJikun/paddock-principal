namespace Paddock.Domain.Infrastructure;

/// <summary>
/// Uncalibrated numbers of team facilities, test rentals and race logistics (PP-026, PP-064). Every value is an ESTIMATE
/// until calibration; none is a fact about any real factory. Math uses only +, -, *, / and
/// <see cref="Math.Pow(double, double)"/>, which are exactly rounded on every OS.
/// </summary>
public static class InfrastructureEstimates
{
    /// <summary>ESTIMATE: absolute quality of the 1950 state of the art, in milli-units (100000 = 100.000 points).</summary>
    public const int FrontierMilli1950 = 100_000;

    /// <summary>ESTIMATE: how many milli-units the year's frontier adds each season. An unchanged facility ages relative to it.</summary>
    public const int FrontierGrowthMilliPerYear = 1_500;

    /// <summary>Quality milli never exceeds this (a hard cap on stored absolute quality).</summary>
    public const int MaxQualityMilli = 400_000;

    /// <summary>ESTIMATE: while a facility is being rebuilt it works at this share of its standing quality (DESIGN: degraded while building).</summary>
    public const double BuildingWorkShare = 0.5;

    /// <summary>ESTIMATE: largest quality step one upgrade can add at relative quality 0, in milli-units.</summary>
    public const int MaxUpgradeGainMilli = 12_000;

    /// <summary>ESTIMATE: smallest quality step still worth posting, even near the frontier.</summary>
    public const int MinUpgradeGainMilli = 400;

    /// <summary>ESTIMATE: exponent of diminishing returns (PP-058). Higher means the next step shrinks faster as relative quality rises.</summary>
    public const double ReturnCurve = 1.6;

    /// <summary>ESTIMATE: share of a typical team's annual budget the first (relative 0) upgrade costs.</summary>
    public const double BaseUpgradeCostShare = 0.04;

    /// <summary>ESTIMATE: exponent of rising cost (PP-058). Higher means the next step costs more as relative quality rises.</summary>
    public const double CostCurve = 2.2;

    /// <summary>ESTIMATE: days the first upgrade takes. Extra days scale with relative quality.</summary>
    public const int BaseUpgradeDays = 40;

    /// <summary>ESTIMATE: extra days at relative quality 1.</summary>
    public const int ExtraUpgradeDays = 80;

    /// <summary>ESTIMATE: yearly upkeep as a share of typical budget, at relative quality 1, per owned facility.</summary>
    public const double UpkeepShareAtFull = 0.008;

    /// <summary>ESTIMATE: execution-quality multiplier of a team whose factory is at relative 0.</summary>
    public const double ExecutionFloor = 0.72;

    /// <summary>ESTIMATE: extra execution quality a factory at relative 1 adds above the floor (people and budget still set the rest).</summary>
    public const double ExecutionFactorySpan = 0.28;

    /// <summary>ESTIMATE: extra execution from an unlocked wind tunnel at relative 1.</summary>
    public const double ExecutionTunnelSpan = 0.08;

    /// <summary>ESTIMATE: extra execution from unlocked CFD at relative 1.</summary>
    public const double ExecutionCfdSpan = 0.06;

    /// <summary>ESTIMATE: project duration scale of a factory at relative 0 (slower parts).</summary>
    public const double DurationSlow = 1.18;

    /// <summary>ESTIMATE: project duration scale of a factory at relative 1 (faster parts).</summary>
    public const double DurationFast = 0.78;

    /// <summary>ESTIMATE: daily understanding scale of a factory at relative 0 (engineers at the plant, not a rented track).</summary>
    public const double UnderstandingFactoryFloor = 0.88;

    /// <summary>ESTIMATE: extra daily understanding a factory at relative 1 adds above the floor.</summary>
    public const double UnderstandingFactorySpan = 0.12;

    /// <summary>ESTIMATE: extra understanding an unlocked driver simulator at relative 1 adds.</summary>
    public const double UnderstandingSimulatorSpan = 0.10;

    /// <summary>ESTIMATE: share of typical budget one private test-track rental costs.</summary>
    public const double TestRentalShare = 0.006;

    /// <summary>ESTIMATE: understanding points one booked test adds, through the same cap as daily growth.</summary>
    public const double UnderstandingPerTest = 2.4;

    /// <summary>ESTIMATE: days between booking a private test and the test itself. Until then the booking can be cancelled for free.</summary>
    public const int TestLeadDays = 7;

    /// <summary>ESTIMATE: private tests allowed when in-season testing is unrestricted.</summary>
    public const int TestsUnrestricted = 12;

    /// <summary>ESTIMATE: private tests allowed under an annual mileage cap.</summary>
    public const int TestsMileageCap = 8;

    /// <summary>ESTIMATE: private tests allowed under a limited-days rule.</summary>
    public const int TestsLimitedDays = 4;

    /// <summary>ESTIMATE: private tests allowed when the rules name one nominated test.</summary>
    public const int TestsNominated = 1;

    /// <summary>
    /// ESTIMATE (#268): share of typical budget a same-country lorry trip costs, on top of the uniform race-running share. Was 0.0004, which
    /// made the first round of 1955 cost the team 210 dollars. A trip should be a line you notice next to the cost of the weekend itself
    /// (<see cref="Paddock.Domain.Finance.FinanceEstimates.RaceRunningShare"/> spread over the rounds of a season, about 2 percent of the
    /// budget a round in 1955): a home hop is a small part of it, a lorry across Europe about a third to a half, and a ship to another
    /// continent more than the whole weekend.
    /// </summary>
    public const double LogisticsHomeShare = 0.003;

    /// <summary>ESTIMATE (#268): share of typical budget a same-continent lorry trip costs. Was 0.0012.</summary>
    public const double LogisticsLorryShare = 0.008;

    /// <summary>ESTIMATE (#268): share of typical budget an overseas ship trip costs (Argentina in the 1950s). Was 0.0035.</summary>
    public const double LogisticsShipShare = 0.025;

    /// <summary>ESTIMATE: days a same-country lorry hop takes.</summary>
    public const int LogisticsHomeDays = 1;

    /// <summary>ESTIMATE: days a European (same-continent) lorry trip takes.</summary>
    public const int LogisticsLorryDays = 3;

    /// <summary>
    /// ESTIMATE: days a transatlantic ship to a round such as Argentina takes (load + passage).
    /// A 16 kt liner Genoa–Buenos Aires is about 16 steaming days; Blue Star's 1950s River Plate cargo
    /// turnaround was seven weeks including ports. 21 days is the one-way freight stand-in, not a sailing schedule.
    /// </summary>
    public const int LogisticsShipDays = 21;
}
