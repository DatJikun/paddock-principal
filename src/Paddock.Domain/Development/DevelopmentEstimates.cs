namespace Paddock.Domain.Development;

/// <summary>
/// Uncalibrated numbers of car development (T42). Every value is an ESTIMATE until the race engine is calibrated; none of
/// them is a fact about any real team. Math uses only +, -, *, / and <see cref="Math.Sqrt(double)"/>, which are exactly
/// rounded, so a result is the same on every operating system.
/// </summary>
public static class DevelopmentEstimates
{
    /// <summary>ESTIMATE: split of a plan nobody has set (current car, account, next concept). Sums to 100. The account is retired from play (PP-066): nobody sets it any more.</summary>
    public const int DefaultCurrentPercent = 80;

    public const int DefaultAccountPercent = 0;

    public const int DefaultNextYearPercent = 20;

    /// <summary>Area priorities run from 0 (do not bother) to this value. The default is the middle.</summary>
    public const int MaxPriority = 10;

    public const int DefaultPriority = 5;

    /// <summary>ESTIMATE: days an upgrade or a research project takes at the era's reference headcount. Concept design uses the era anchors.</summary>
    public const int UpgradeBaseDays = 42;

    public const int ResearchBaseDays = 56;

    /// <summary>ESTIMATE: concept design days at the reference headcount. About three months in 1950, four in 1955, and about fifteen in 2025.</summary>
    public const int ConceptDesignDays1950 = 90;

    public const int ConceptDesignDays1955 = 120;

    public const int ConceptDesignDays1990 = 285;

    public const int ConceptDesignDays2025 = 450;

    /// <summary>ESTIMATE: the 1955 concept-design anchor. Kept so older notes can name one number; duration now follows the anchors.</summary>
    public const int ConceptBaseDays = ConceptDesignDays1955;

    /// <summary>ESTIMATE: the shortest and longest a project can get compared with its base duration, whatever the headcount.</summary>
    public const double DurationFloor = 0.4;

    public const double DurationCeiling = 2.5;

    /// <summary>ESTIMATE: share of a typical team's annual budget that full-effort development uses. Lowered with v2 (PP-066), where parts and the next concept run side by side, with <see cref="GainPerFunding"/> raised to match so a part gains what it did.</summary>
    public const double AnnualBudgetShare = 0.06;

    /// <summary>ESTIMATE: share of the remaining headroom an Upgrade closes per unit of funding at quality 1.</summary>
    public const double GainPerFunding = 5.0;

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

    /// <summary>
    /// ESTIMATE: projects in 1950 close this share of the later gain. It rises to <see cref="SettledGainScale"/> by
    /// <see cref="EraScaleSettledYear"/> and stays there.
    /// </summary>
    public const double EarlyGainScale = 0.7;

    public const double SettledGainScale = 1.0;

    /// <summary>ESTIMATE: a 1950 concept is this many times the settled concept risk. It falls to 1 by <see cref="EraScaleSettledYear"/>.</summary>
    public const double EarlyConceptRiskScale = 1.5;

    public const double SettledConceptRiskScale = 1.0;

    /// <summary>ESTIMATE: year by which the early-era gain and concept-risk scales have reached their settled values.</summary>
    public const int EraScaleSettledYear = 1990;

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

    /// <summary>
    /// ESTIMATE: days a committed concept takes to build in 1950, at the era's reference headcount. It grows with the era's car
    /// complexity (<see cref="DevelopmentMath.ConceptProductionDays"/>) and shrinks with a bigger team, but a bigger team does not
    /// make the car better (DESIGN §5.3).
    /// </summary>
    public const int ConceptProductionBaseDays = 30;

    /// <summary>ESTIMATE: production costs this share of what the concept cost to develop, posted in full when it is committed.</summary>
    public const double ConceptProductionCostShare = 0.5;

    /// <summary>ESTIMATE: days the "commit now or keep developing?" inbox decision waits before the default (keep developing) applies.</summary>
    public const int ConceptDecisionDays = 14;

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

    /// <summary>ESTIMATE: concept design days at the era's reference headcount, linear between anchors.</summary>
    public static readonly IReadOnlyList<(int Year, int Days)> ConceptDesignAnchors =
    [
        (1950, ConceptDesignDays1950),
        (1955, ConceptDesignDays1955),
        (EraScaleSettledYear, ConceptDesignDays1990),
        (2025, ConceptDesignDays2025),
    ];

    /// <summary>ESTIMATE: multiplier on the share a project closes, linear between anchors.</summary>
    public static readonly IReadOnlyList<(int Year, double Scale)> GainScaleAnchors =
    [
        (1950, EarlyGainScale),
        (EraScaleSettledYear, SettledGainScale),
        (2025, SettledGainScale),
    ];

    /// <summary>ESTIMATE: multiplier on <see cref="ConceptRiskMultiple"/>, linear between anchors.</summary>
    public static readonly IReadOnlyList<(int Year, double Scale)> ConceptRiskScaleAnchors =
    [
        (1950, EarlyConceptRiskScale),
        (EraScaleSettledYear, SettledConceptRiskScale),
        (2025, SettledConceptRiskScale),
    ];

    // ---- Car development v2 (PP-066). All ESTIMATES until the race engine is calibrated. ----

    /// <summary>ESTIMATE: share of its own ceiling a pure evolution concept starts at, and the same for a pure revolution (PP-066: about 85% for evolution).</summary>
    public const double EvolutionStartFraction = 0.85;

    public const double RevolutionStartFraction = 0.66;

    /// <summary>ESTIMATE: mean shift, in rating points, of a new concept's ceiling against the ceiling of the concept it replaces. Evolution is narrow, revolution is high.</summary>
    public const double EvolutionCeilingShift = 1.5;

    public const double RevolutionCeilingShift = 8;

    /// <summary>ESTIMATE: spread (a sum of twelve uniforms is used, so this is a standard deviation) of the new ceiling.</summary>
    public const double EvolutionCeilingSd = 2;

    public const double RevolutionCeilingSd = 6;

    /// <summary>ESTIMATE: points a team with the best design staff adds to, or the worst takes from, the mean shift: staff quality 0..1 moves it by this span around 0.5.</summary>
    public const double QualityShiftSpan = 5;

    /// <summary>ESTIMATE: a revolution fails this much more often than an evolution (1 + this at full revolution).</summary>
    public const double RevolutionRiskExtra = 0.5;

    /// <summary>ESTIMATE: how much the lead engineer's innovation widens a new concept's ceiling spread: factor 1 + this at innovation 20.</summary>
    public const double InnovationSpread = 0.6;

    /// <summary>ESTIMATE: chance a finished project is a breakthrough: the base at innovation 1, plus the span times the squared innovation unit.</summary>
    public const double BreakthroughBase = 0.02;

    public const double BreakthroughSpan = 0.10;

    /// <summary>ESTIMATE: a breakthrough upgrade closes this multiple of the share it would have closed (still capped by the headroom).</summary>
    public const double BreakthroughShareMultiple = 2.2;

    /// <summary>ESTIMATE: a breakthrough in an upgrade lifts the concept's ceiling by this much (the idea had more in it), and in a new concept by the second value.</summary>
    public const double BreakthroughUpgradeLift = 2;

    public const double BreakthroughConceptLift = 6;

    /// <summary>ESTIMATE: innovation widens the execution noise of a project: the span grows by this share at innovation 20 (the mean stays 1).</summary>
    public const double InnovationNoise = 1;

    /// <summary>ESTIMATE: rating points of the ceiling a concept loses each new season it stays in the car (rivals' knowledge and the rules move on).</summary>
    public const double ConceptAgingPerSeason = 1.2;

    /// <summary>ESTIMATE: facts about the concept's character the player sees as numbers. Aero direction presets run from -1 (straights) to 1 (corners).</summary>
    public const int AeroPresetMilli = 500;

    /// <summary>ESTIMATE: default character of the next concept (evolution, balanced aero).</summary>
    public const int DefaultNextPhilosophyMilli = -1000;

    public const int DefaultNextAeroMilli = 0;

    /// <summary>ESTIMATE: the most understanding notes kept per team for the Auto screen.</summary>
    public const int MaxNotes = 8;

    /// <summary>ESTIMATE: widest and narrowest extra half-width of a public (rival) band compared with the team's own.</summary>
    public const double RivalBandExtra = 4;

    /// <summary>ESTIMATE: how many rivals the grid comparison shows.</summary>
    public const int TopRivals = 3;

    public static double Quantize(double value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);

    public static int Milli(double value) => (int)Math.Round(value * 1000d, MidpointRounding.AwayFromZero);
}
