namespace Paddock.Simulation.Ai;

/// <summary>
/// Every guessed number of the AI team principals (T44). All of them are ESTIMATES: none is a measured fact, a calibrated
/// value or a statement about a real person, and each can be edited here without hunting through call sites. DESIGN section 8 gives
/// the shape of the utility (<c>U = w1*prestige + w2*car + w3*pay + w4*status - w5*risk</c>) but no weights; the weights of the
/// four archetypes live in <see cref="ArchetypeProfile"/> and are ESTIMATES too. The names say what a number does, not what it
/// should be.
/// </summary>
public static class AiEstimates
{
    // --- Decision level (competence tier), from the believed attributes of the principal ---

    /// <summary>ESTIMATE: number of competence tiers (open question 2 of issue #109: four tiers by default).</summary>
    public const int LevelCount = 4;

    /// <summary>ESTIMATE: the believed mean attribute (1 to 20 scale) a principal needs for tier 2, 3 and 4. Below the first is tier 1.</summary>
    public static readonly IReadOnlyList<double> LevelFloors = [8.0, 12.0, 16.0];

    /// <summary>ESTIMATE: the attribute value assumed for a principal whose attribute the team has no belief about.</summary>
    public const double UnknownAttribute = 10.0;

    /// <summary>ESTIMATE: standard deviation of the noise on an option's utility, by tier (index 0 is tier 1). A weak principal misjudges more.</summary>
    public static readonly IReadOnlyList<double> NoiseSdByLevel = [0.30, 0.18, 0.10, 0.04];

    /// <summary>ESTIMATE: how many candidates a principal of each tier looks at for one vacancy.</summary>
    public static readonly IReadOnlyList<int> ShortlistByLevel = [2, 3, 5, 8];

    /// <summary>ESTIMATE: the lowest tier that plans beyond the season (sacrificing a season, long contracts for young talent).</summary>
    public const int PlanningLevel = 2;

    /// <summary>ESTIMATE: tier-1 principals sign for at most this many seasons.</summary>
    public const int ShortContractYears = 2;

    // --- Stars, the scale a person is judged on (0 to 5, as in PP-040) ---

    /// <summary>ESTIMATE: the stars of a person the team has no belief about (the midfield benchmark).</summary>
    public const double UnknownStars = 2.5;

    /// <summary>ESTIMATE: the believed range of stars of a person the team knows nothing about. It is wide, so a risk-averse principal discounts it.</summary>
    public const double UnknownStarsLow = 1.0;

    public const double UnknownStarsHigh = 4.0;

    /// <summary>ESTIMATE: the stars at which a person is neither a gain nor a loss. Quality is scored against it.</summary>
    public const double ReferenceStars = 2.5;

    /// <summary>ESTIMATE: age range in which a driver is at his best. Outside it the age term turns negative.</summary>
    public const int PrimeAgeFrom = 23;

    public const int PrimeAgeTo = 33;

    /// <summary>ESTIMATE: utility lost per year of age above <see cref="PrimeAgeTo"/>, and per year below <see cref="PrimeAgeFrom"/> (weighted by risk aversion).</summary>
    public const double AgePenaltyPerYearOld = 0.04;

    public const double AgePenaltyPerYearYoung = 0.02;

    /// <summary>ESTIMATE: a driver this young or younger counts as a young talent whose potential matters.</summary>
    public const int YoungTalentAge = 26;

    // --- Market ---

    /// <summary>ESTIMATE: scale of the quality term (utility per star above the reference, before the archetype weight).</summary>
    public const double QualityScale = 0.4;

    /// <summary>ESTIMATE: scale of the price term (utility lost when the ask equals the whole payroll budget, before the archetype weight).</summary>
    public const double PriceScale = 1.5;

    /// <summary>ESTIMATE: the utility of an incumbent's continuity (settled partnership), before the archetype weight.</summary>
    public const double ContinuityBonus = 0.30;

    /// <summary>ESTIMATE: what a search costs, subtracted from the best alternative when weighing a renewal against letting the contract run out.</summary>
    public const double SearchFriction = 0.15;

    /// <summary>ESTIMATE: the utility of having nobody for a vacant seat, falling with the days it has stood empty.</summary>
    public const double WaitUtility = -0.05;

    public const double WaitUtilityPerWeek = -0.04;

    /// <summary>ESTIMATE: the utility of walking away, or of letting a contract run out, when the market offers nobody else.</summary>
    public const double NoAlternativeUtility = -0.5;

    /// <summary>ESTIMATE: after this many days a vacancy cannot be left open: the principal signs the best affordable candidate.</summary>
    public const int MaxVacancyDays = 45;

    /// <summary>ESTIMATE: the share of a person's market ask the team offers first, by negotiation tier (index 0 is tier 1). A good negotiator opens lower.</summary>
    public static readonly IReadOnlyList<double> OpeningShareByNegotiation = [1.05, 1.00, 0.95, 0.90];

    /// <summary>ESTIMATE: the most above his market ask a team pays, as a share, before the archetype's own stretch.</summary>
    public const double BaseCeilingShare = 1.10;

    /// <summary>ESTIMATE: a person's answer in a counter that is within this share of the ceiling is worth a revised offer, not a walk-away.</summary>
    public const double ReviseWithinShare = 1.25;

    /// <summary>ESTIMATE: share of the gap between the team's offer and the person's counter the team closes in a revised offer.</summary>
    public const double ReviseGapShare = 0.6;

    /// <summary>ESTIMATE: contract length in seasons for a young talent a planner wants to keep, and for an ordinary signing.</summary>
    public const int LongContractYears = 3;

    public const int OrdinaryContractYears = 2;

    /// <summary>ESTIMATE: the renewal talk starts when this many days are left on the contract (about half a year).</summary>
    public const int RenewalLeadDays = 180;

    /// <summary>ESTIMATE: a person who refused the team, or whose talk it ended, is not approached again for this many days.</summary>
    public const int RetryBlockDays = 180;

    /// <summary>ESTIMATE: key staff matter this much relative to a driver in the same utility (staff pay is a fifth of a driver's).</summary>
    public const double StaffQualityWeight = 0.8;

    // --- Money ---

    /// <summary>ESTIMATE: the overdraft a team may run, as a share of its annual budget. Matches the "whole season to recover" rule of PP-050 only in spirit: finance sets the real insolvency rule.</summary>
    public const double OverdraftAllowanceShare = 0.25;

    /// <summary>ESTIMATE: a new commitment must keep cash plus the allowance above this share of what the rest of the season already owes.</summary>
    public const double CommitmentSafetyShare = 1.0;

    // --- Review cadence ---

    /// <summary>ESTIMATE: days between scheduled reviews when nothing else wakes the principal.</summary>
    public const int ScheduledReviewDays = 30;

    /// <summary>ESTIMATE: days until the next look when a review filed a command (to read the answer).</summary>
    public const int AfterActionDays = 1;

    /// <summary>ESTIMATE: days until the next look when a vacancy could not be filled (no candidate, no money, no free negotiation slot).</summary>
    public const int VacancyRetryDays = 5;

    // --- Development (T42) ---

    /// <summary>ESTIMATE: constructors' positions up to this one count as a fight for the title or the podium.</summary>
    public const int ContentionPosition = 3;

    /// <summary>ESTIMATE: weight of a share of the next car when the rules change a lot (utility per unit of share, before the archetype weight).</summary>
    public const double NextYearValue = 0.9;

    /// <summary>ESTIMATE: share of the development account a rule change is expected to wipe, per unit of changed dimensions (matches the order of the T42 estimate).</summary>
    public const double AccountRuleLoss = 2.5;

    /// <summary>ESTIMATE: the tiny preference for leaving a plan as it is, before the archetype's stability weight (avoids churn).</summary>
    public const double PlanInertia = 0.10;

    // --- Supply (T43) ---

    /// <summary>ESTIMATE: scale of the price term of a supply deal (utility lost when the yearly price equals the whole budget).</summary>
    public const double SupplyPriceScale = 4.0;

    /// <summary>ESTIMATE: scale of the performance term of a supplier (utility per position of its customers' average standing against mid-table).</summary>
    public const double SupplyFormScale = 0.6;

    /// <summary>ESTIMATE: the least the principal wants a supply deal to be worth, so a deal that costs more than it brings is declined.</summary>
    public const double SupplyAcceptUtility = -0.05;

    /// <summary>ESTIMATE: seasons asked for in a supply deal, by archetype pattern (see <see cref="ArchetypeProfile.SupplySeasons"/>).</summary>
    public const int MaxSupplySeasons = 3;

    // --- Sponsors (T38) ---

    /// <summary>ESTIMATE: scale of the income term of a sponsor (utility per annual budget share the sponsor brings).</summary>
    public const double SponsorIncomeScale = 6.0;

    /// <summary>ESTIMATE: days the principal weighs waiting for better terms against the risk of a rival taking the sponsor.</summary>
    public const int SponsorWaitHorizonDays = 14;

    /// <summary>ESTIMATE: daily chance the principal assumes that a waiting sponsor is signed by a rival (the world's own number is hidden).</summary>
    public const double AssumedRivalDailyChance = 0.01;

    /// <summary>ESTIMATE: daily gain in terms, in thousandths of the full price, the principal expects from waiting (the world's number is on the talk view as a band of the cap).</summary>
    public const int AssumedWaitingGainMilliPerDay = 6;

    /// <summary>ESTIMATE: a renewal offer from a sponsor is accepted when it is at least this share of the indicative price.</summary>
    public const double SponsorOfferAcceptShare = 0.80;

    // --- Archetype assignment from tier, age and recent results ---

    /// <summary>ESTIMATE: a principal this old or older leans to the safe archetypes (Builder or Survivor), a young one to the bold ones.</summary>
    public const int VeteranAge = 55;

    public const int YoungPrincipalAge = 40;
}
