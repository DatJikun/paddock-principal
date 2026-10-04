namespace Paddock.Domain.Development;

/// <summary>
/// Uncalibrated numbers of car development (T42). Every value is an ESTIMATE until the race engine is calibrated; none of
/// them is a fact about any real team. Math uses only +, -, *, / and <see cref="Math.Sqrt(double)"/>, which are exactly
/// rounded, so a result is the same on every operating system.
/// </summary>
public static class DevelopmentEstimates
{
    /// <summary>ESTIMATE: split of a plan nobody has set (current car, account, next year's car). Sums to 100.</summary>
    public const int DefaultCurrentPercent = 60;

    public const int DefaultAccountPercent = 20;

    public const int DefaultNextYearPercent = 20;

    /// <summary>Area priorities run from 0 (do not bother) to this value. The default is the middle.</summary>
    public const int MaxPriority = 10;

    public const int DefaultPriority = 5;

    /// <summary>ESTIMATE: days a project takes at the era's reference headcount.</summary>
    public const int UpgradeBaseDays = 42;

    public const int ResearchBaseDays = 56;

    public const int ConceptBaseDays = 120;

    /// <summary>ESTIMATE: the shortest and longest a project can get compared with its base duration, whatever the headcount.</summary>
    public const double DurationFloor = 0.4;

    public const double DurationCeiling = 2.5;

    /// <summary>ESTIMATE: share of a typical team's annual budget that full-effort development uses.</summary>
    public const double AnnualBudgetShare = 0.15;

    /// <summary>ESTIMATE: share of the remaining headroom an Upgrade closes per unit of funding at quality 1.</summary>
    public const double GainPerFunding = 2.0;

    /// <summary>ESTIMATE: a Concept project closes this multiple of an Upgrade's share for the same funding, over all areas.</summary>
    public const double ConceptGainMultiple = 2.5;

    /// <summary>ESTIMATE: no project closes more than this share of the headroom in one go. Keeps gain below the ceiling.</summary>
    public const double MaxShare = 0.6;

    /// <summary>ESTIMATE: execution noise multiplies the expected share by 0.75 + 0.5 * u, so it averages 1.</summary>
    public const double NoiseLow = 0.75;

    public const double NoiseSpan = 0.5;

    /// <summary>ESTIMATE: chance an Upgrade or Research fails before staff skill is counted; a Concept is riskier.</summary>
    public const double BaseRisk = 0.12;

    public const double ConceptRiskMultiple = 2;

    public const double MinRisk = 0.02;

    /// <summary>ESTIMATE: share of the account's stock added to an Upgrade's share, at a full account.</summary>
    public const double AccountBoost = 0.5;

    /// <summary>ESTIMATE: fraction of the account an Upgrade uses up when it draws on it.</summary>
    public const double AccountUse = 0.2;

    /// <summary>ESTIMATE: daily fraction of the account lost because rivals move on. About 17% a year.</summary>
    public const double AccountDailyDecay = 0.0005;

    /// <summary>ESTIMATE: account lost per unit of the share of technical regulation dimensions that changed.</summary>
    public const double RuleChangeLossScale = 2.5;

    /// <summary>ESTIMATE: understanding points an Upgrade costs per unit of share it closes (a new part is not yet understood).</summary>
    public const double UpgradeUnderstandingHit = 60;

    /// <summary>ESTIMATE: understanding points per day from engineers working on the car, before the era cap.</summary>
    public const double UnderstandingPerDay = 0.1;

    public const double UnderstandingPerActiveProject = 0.05;

    /// <summary>ESTIMATE: understanding points a finished race adds, plus a bit per hundred kilometres.</summary>
    public const double UnderstandingPerRace = 1.5;

    public const double UnderstandingPerHundredKm = 0.4;

    /// <summary>ESTIMATE: one project slot per this many engineers, plus one, up to <see cref="MaxSlots"/>.</summary>
    public const int HeadcountPerSlot = 40;

    public const int MaxSlots = 4;

    /// <summary>ESTIMATE: share of the era's average headcount a team with no key engineers still has.</summary>
    public const double StaffFactorFloor = 0.6;

    /// <summary>ESTIMATE: quality of a team whose key engineers are at the bottom of the scale.</summary>
    public const double QualityFloor = 0.15;

    /// <summary>ESTIMATE: quality is lowered to at most this share when the cash is gone (it never drops below).</summary>
    public const double BudgetFactorFloor = 0.8;

    /// <summary>ESTIMATE: pay the project's cost to the ledger in whole weeks.</summary>
    public const int PostEveryDays = 7;

    /// <summary>ESTIMATE: engineers' utility weights.</summary>
    public const double WeightDeficit = 4;

    public const double WeightHeadroom = 2;

    public const double WeightPriority = 1;

    public const double WeightSkill = 2;

    public const double WeightExperience = 0.5;

    public const double WeightAdaptation = 0.5;

    public const double WeightInnovation = 1;

    /// <summary>ESTIMATE: rating points of headroom that count as "a lot" when engineers weigh an area.</summary>
    public const double HeadroomScale = 30;

    /// <summary>ESTIMATE: an area with less headroom than this is not worth a project.</summary>
    public const double MinWorthHeadroom = 0.5;

    /// <summary>ESTIMATE: a full account closes the research option.</summary>
    public const int AccountFullMilli = 95_000;

    /// <summary>ESTIMATE: two top options closer than this are a tie, broken by the <c>AiDecisions</c> stream.</summary>
    public const double TieEpsilon = 0.02;

    /// <summary>ESTIMATE: years in the team until an engineer has fully adapted.</summary>
    public const int AdaptationYears = 4;

    /// <summary>ESTIMATE: experience grows from age 25 and reaches 1 after this many years.</summary>
    public const int ExperienceAgeSpan = 25;

    /// <summary>ESTIMATE: the "we are close" reply is sent for a project at least this far along.</summary>
    public const double CloseProgress = 0.5;

    /// <summary>ESTIMATE: days an inbox reply about a split change waits before the default (keep the plan) applies.</summary>
    public const int ReplyDays = 14;

    /// <summary>
    /// ESTIMATE: average team headcount by era, linear between anchors. Placeholder for the departments of DESIGN §6.2
    /// (headcount per department), which the owner has not accepted yet.
    /// </summary>
    public static readonly IReadOnlyList<(int Year, int Headcount)> HeadcountAnchors =
    [
        (1950, 25), (1970, 100), (1990, 300), (2010, 600), (2025, 1000),
    ];

    public static double Quantize(double value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);

    public static int Milli(double value) => (int)Math.Round(value * 1000d, MidpointRounding.AwayFromZero);
}
