using Paddock.Domain.Time;

namespace Paddock.Domain.Contracts;

/// <summary>
/// Every guessed number of the contract and negotiation system. All of them are ESTIMATES: nothing here is a measured
/// fact or a calibrated value, and each can be edited here without hunting through call sites (like
/// <c>GenerationEstimates</c>). The names say what a number does, not what it should be.
/// </summary>
public static class NegotiationEstimates
{
    // --- Offer utility, DESIGN section 8: U = w1*prestige + w2*car + w3*salary + w4*status - w5*risk ---

    /// <summary>ESTIMATE: base weight of team prestige in a person's utility.</summary>
    public const double WeightPrestige = 0.25;

    /// <summary>ESTIMATE: base weight of the expected car.</summary>
    public const double WeightCar = 0.25;

    /// <summary>ESTIMATE: base weight of salary against the era benchmark.</summary>
    public const double WeightSalary = 0.30;

    /// <summary>ESTIMATE: base weight of seat status.</summary>
    public const double WeightStatus = 0.15;

    /// <summary>ESTIMATE: base weight of team risk (subtracted).</summary>
    public const double WeightRisk = 0.20;

    /// <summary>ESTIMATE: the salary score is (package / reference - 1), kept inside this range.</summary>
    public const double SalaryScoreMin = -1.0;

    /// <summary>ESTIMATE: upper end of the salary score. Paying double the reference is the most that adds utility.</summary>
    public const double SalaryScoreMax = 1.0;

    /// <summary>ESTIMATE: status score of a number one seat (the scale runs from 0 to 1).</summary>
    public const double StatusScoreNumberOne = 1.0;

    /// <summary>ESTIMATE: status score of an equal seat.</summary>
    public const double StatusScoreEqual = 0.6;

    /// <summary>ESTIMATE: status score of a number two seat.</summary>
    public const double StatusScoreNumberTwo = 0.25;

    /// <summary>ESTIMATE: status score of a reserve seat.</summary>
    public const double StatusScoreReserve = 0.0;

    /// <summary>ESTIMATE: key staff have no seat status, so this neutral score stands in for it.</summary>
    public const double StatusScoreStaff = 0.6;

    // --- What a bonus is worth to the person (expected value, from the expected car) ---

    /// <summary>ESTIMATE: championship points a season is expected to bring at the strongest car.</summary>
    public const double ReferencePointsPerSeason = 100.0;

    /// <summary>ESTIMATE: wins a season is expected to bring at the strongest car (scales with the car squared).</summary>
    public const double ReferenceWinsPerSeason = 5.0;

    /// <summary>ESTIMATE: chance of a title at the strongest car (scales with the car cubed).</summary>
    public const double TitleChanceAtTopCar = 0.4;

    // --- Clauses and length change how risky a deal feels ---

    /// <summary>ESTIMATE: factor on perceived risk when the person holds an exit clause.</summary>
    public const double ExitClauseRiskFactor = 0.7;

    /// <summary>ESTIMATE: factor on perceived risk when the person holds the option.</summary>
    public const double PersonOptionRiskFactor = 0.9;

    /// <summary>ESTIMATE: factor on perceived risk when the team holds the option.</summary>
    public const double TeamOptionRiskFactor = 1.1;

    /// <summary>ESTIMATE: risk grows by this share for every contract year after the first.</summary>
    public const double RiskPerExtraYear = 0.1;

    /// <summary>ESTIMATE: utility a loyalty of 20 adds for the current employer (scales linearly from loyalty 10).</summary>
    public const double LoyaltyBonusScale = 0.05;

    /// <summary>ESTIMATE: perceived risk up to this level is not worth a complaint; above it the person names risk as a reason.</summary>
    public const double RiskTolerance = 0.6;

    // --- Alternatives ---

    /// <summary>ESTIMATE: utility below which a person would rather wait on the free market than sign.</summary>
    public const double ReservationUtility = 0.15;

    /// <summary>ESTIMATE: utility of retirement for a person aged 30 or younger.</summary>
    public const double RetirementBase = -0.05;

    /// <summary>ESTIMATE: retirement gains this much utility for each year of age over 30.</summary>
    public const double RetirementPerYear = 0.02;

    /// <summary>ESTIMATE: years over 30 that still raise the utility of retirement.</summary>
    public const int RetirementYearsCap = 15;

    // --- Acceptance and patience ---

    /// <summary>ESTIMATE: an offer must beat the best alternative by at least this much to be accepted.</summary>
    public const double AcceptanceMargin = 0.02;

    /// <summary>ESTIMATE: extra margin, multiplied by the share of interest already lost.</summary>
    public const double InterestMargin = 0.20;

    /// <summary>ESTIMATE: interest at the start of a negotiation, in thousandths.</summary>
    public const int InterestStart = 1000;

    /// <summary>ESTIMATE: interest lost when an offer is not a meaningful improvement, in thousandths.</summary>
    public const int NudgePenalty = 250;

    /// <summary>ESTIMATE: smallest salary rise that counts as a real change, as a percentage of the previous salary.</summary>
    public const int MinMeaningfulImprovementPercent = 2;

    /// <summary>ESTIMATE: fewest rounds any person will give.</summary>
    public const int MinRounds = 3;

    /// <summary>ESTIMATE: most rounds any person will give.</summary>
    public const int MaxRounds = 5;

    /// <summary>ESTIMATE: rounds of an average person; professionalism adds and temperament takes away.</summary>
    public const int BaseRounds = 4;

    // --- Time ---

    /// <summary>ESTIMATE: days a negotiation stays open when the manager names no deadline.</summary>
    public const int DefaultDeadlineDays = 30;

    /// <summary>
    /// ESTIMATE: days a person holds to a counter or an agreement once given. The deadline is pushed out to at least this far, so
    /// the manager, who reads the answer the morning after it was given, always has time to act on it.
    /// </summary>
    public const int ResponseHoldDays = 7;

    /// <summary>ESTIMATE: shortest deadline a manager may name.</summary>
    public const int MinDeadlineDays = 7;

    /// <summary>ESTIMATE: longest deadline a manager may name.</summary>
    public const int MaxDeadlineDays = 90;

    /// <summary>ESTIMATE: fewest days a person needs to answer an offer.</summary>
    public const int ResponseDelayMinDays = 2;

    /// <summary>ESTIMATE: extra days of response delay, drawn from the Market stream (0 up to this value, inclusive).</summary>
    public const int ResponseDelayJitterDays = 3;

    /// <summary>ESTIMATE: a person under contract may be approached only in the last this-many days of it.</summary>
    public const int NegotiationWindowDays = 365;

    /// <summary>ESTIMATE: the renewal prompt goes out when this many days (about six months) are left.</summary>
    public const int RenewalPromptDays = 183;

    /// <summary>
    /// ESTIMATE: days a renewal prompt waits for the manager. When they pass, the default (extend on current terms if the person
    /// agrees and the budget allows, otherwise let it run out) applies and the manager is told. The prompt never holds the clock.
    /// </summary>
    public const int RenewalDecisionDays = 30;

    /// <summary>ESTIMATE: key staff get a renewal prompt of their own; every other staff role is one grouped item.</summary>
    public static bool IsKeyStaff(Paddock.Domain.People.StaffRole role) =>
        role is Paddock.Domain.People.StaffRole.TechnicalDirector or Paddock.Domain.People.StaffRole.ChiefDesigner;

    /// <summary>ESTIMATE: an option has to be exercised this many days before the contract ends.</summary>
    public const int OptionNoticeDays = 120;

    // --- Terms ---

    /// <summary>ESTIMATE: longest contract, in seasons.</summary>
    public const int MaxYears = 5;

    /// <summary>ESTIMATE: stars assumed for a person the team knows nothing about (the midfield benchmark).</summary>
    public const double UnknownStars = 2.5;

    /// <summary>ESTIMATE: share of the driver pay benchmark that key staff earn.</summary>
    public const double StaffPayShare = 0.2;

    /// <summary>ESTIMATE: stars at which a driver earns the midfield benchmark. At the top (5 stars) the top benchmark.</summary>
    public const double MidfieldStars = 2.5;

    /// <summary>ESTIMATE: pay of a zero-star person as a share of the midfield benchmark.</summary>
    public const double MinPayShare = 0.2;

    // --- Parallel work, ending a contract ---

    /// <summary>ESTIMATE: negotiations a team can run at once without a skilled principal.</summary>
    public const int ParallelBase = 3;

    /// <summary>ESTIMATE: every this-many points of the principal's negotiation attribute allow one more negotiation.</summary>
    public const int ParallelPerNegotiationPoints = 5;

    /// <summary>ESTIMATE: two utilities closer than this are a tie.</summary>
    public const double TieEpsilon = 0.001;

    /// <summary>ESTIMATE: share of the remaining salary an employer owes when it ends a contract early.</summary>
    public const double TerminationShare = 0.5;

    /// <summary>ESTIMATE: competence tier written into the trace of a person's decision.</summary>
    public const int PersonDeciderLevel = 1;

    /// <summary>The stars (0 to 5, PP-040) for a mean attribute value on the 1-20 scale.</summary>
    public static double StarsFromMean(double meanAttribute) =>
        Math.Clamp(meanAttribute / 4.0, 0.0, 5.0);

    /// <summary>The end of a contract that starts on <paramref name="start"/> and runs <paramref name="years"/> seasons.</summary>
    public static GameDate EndFor(GameDate start, int years) => GameDate.SeasonEnd(start.Year + years - 1);

    // --- A raise asked during a contract (PP-057). All ESTIMATES. ---

    /// <summary>ESTIMATE: chance a fully loyal and happy driver still asks, once per season.</summary>
    public const double RaiseAskFloor = 0.02;

    /// <summary>ESTIMATE: extra chance when the driver is both disloyal and unhappy. Loyal or happy drivers sit near the floor.</summary>
    public const double RaiseAskSpan = 0.85;

    /// <summary>ESTIMATE: the smallest raise, as a share of the current salary, asked by a driver already at his reachable stars.</summary>
    public const double PeakRaiseShare = 0.02;

    /// <summary>ESTIMATE: share of the demanded gap a team offers when it meets the driver part of the way.</summary>
    public const double PartialRaiseShare = 0.5;

    /// <summary>ESTIMATE: an AI team accepts a demand no larger than this share of the current salary.</summary>
    public const double AiRaiseAcceptShare = 0.15;

    /// <summary>ESTIMATE: trust points (0–100) lost when a demand is refused.</summary>
    public const int RaiseTrustHit = 10;

    /// <summary>ESTIMATE: points added to the chance of leaving after a refusal. Read as a penalty on the utility of staying.</summary>
    public const int RaiseLeaveHit = 15;

    /// <summary>ESTIMATE: trust a pair starts at, 0–100, until a demand is answered.</summary>
    public const int RaiseTrustStart = 50;

    /// <summary>ESTIMATE: days a raise decision waits before the default (refuse) applies.</summary>
    public const int RaiseDecisionDays = 14;

    /// <summary>ESTIMATE: happiness used when the team has no recent results to judge morale by.</summary>
    public const double NeutralMorale = 0.5;

    /// <summary>ESTIMATE: how much one point of leave-bias (0–100) subtracts from the utility of staying.</summary>
    public const double RaiseLeaveUtility = 0.01;
}
