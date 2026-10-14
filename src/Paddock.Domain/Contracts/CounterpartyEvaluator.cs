using Paddock.Domain.People;
using Paddock.Domain.World;

namespace Paddock.Domain.Contracts;

/// <summary>Translation keys of the reasons a person states (PP-021). The Application layer lists the same keys for the i18n check.</summary>
public static class NegotiationReasons
{
    public const string SalaryTooLow = "negotiation.reason.salary_too_low";

    public const string Status = "negotiation.reason.status";

    public const string TeamTooWeak = "negotiation.reason.team_too_weak";

    public const string Risk = "negotiation.reason.risk";

    public const string BetterProspects = "negotiation.reason.better_prospects";

    public const string NoRealChange = "negotiation.reason.no_real_change";

    public const string PatienceLost = "negotiation.reason.patience_lost";

    public const string BetterOffer = "negotiation.reason.better_offer";

    public const string DeadlinePassed = "negotiation.reason.deadline_passed";

    public const string SignedElsewhere = "negotiation.reason.signed_elsewhere";

    public static IReadOnlyList<string> All { get; } =
    [
        SalaryTooLow,
        Status,
        TeamTooWeak,
        Risk,
        BetterProspects,
        NoRealChange,
        PatienceLost,
        BetterOffer,
        DeadlinePassed,
        SignedElsewhere,
    ];
}

/// <summary>Names of the terms of a person's utility.</summary>
public static class UtilityTerms
{
    public const string Prestige = "prestige";

    public const string Car = "car";

    public const string Salary = "salary";

    public const string Status = "status";

    public const string Risk = "risk";

    public const string Loyalty = "loyalty";
}

/// <summary>One term of a person's utility: its weight, the score it was given, and what it contributed.</summary>
public sealed record UtilityFactor(string Name, double Weight, double Score, double Contribution);

/// <summary>The utility a person finds in an offer, with the terms behind it.</summary>
public sealed record OfferEvaluation(double Utility, IReadOnlyList<UtilityFactor> Factors)
{
    public double Contribution(string name) => Factors.First(factor => factor.Name == name).Contribution;

    public double Score(string name) => Factors.First(factor => factor.Name == name).Score;

    public double Weight(string name) => Factors.First(factor => factor.Name == name).Weight;
}

/// <summary>
/// Everything a person weighs besides the offer. <see cref="Traits"/> are the person's own hidden personality;
/// <see cref="ReferenceSalary"/> comes from what the offering team believes about the person (bands), not from truth.
/// <see cref="FirstSeatTaken"/> is true when another driver already holds the first-driver seat of the organization (#325): a person
/// then cannot ask for that seat in a counter.
/// </summary>
public sealed record EvaluationContext(
    PersonalityTraits Traits,
    int Age,
    OrganizationAppeal Appeal,
    long ReferenceSalary,
    NegotiationSubject Subject,
    bool IsCurrentEmployer,
    bool FirstSeatTaken = false);

/// <summary>
/// The person's side of a negotiation: DESIGN section 8, <c>U = w1*prestige + w2*car + w3*salary + w4*status - w5*risk</c>,
/// with weights from the personality. A pure function of its inputs: no state, no RNG (INV-005). It reads the person's own
/// traits and nothing about the person's true attributes, so two worlds that differ only in a hidden attribute give the same
/// answer (INV-003). All numbers are ESTIMATES (<see cref="NegotiationEstimates"/>).
/// </summary>
public static class CounterpartyEvaluator
{
    public static OfferEvaluation Evaluate(OfferTerms offer, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(context);
        var weights = WeightsFor(context.Traits);
        var appeal = context.Appeal;

        var package = offer.Salary + ExpectedBonus(offer, appeal.ExpectedCar);
        var salaryScore = Math.Clamp(
            (package / (double)Math.Max(1L, context.ReferenceSalary)) - 1.0,
            NegotiationEstimates.SalaryScoreMin,
            NegotiationEstimates.SalaryScoreMax);
        var statusScore = StatusScore(offer, context.Subject);
        var riskScore = PerceivedRisk(offer, appeal.Risk);

        var factors = new List<UtilityFactor>(6)
        {
            new(UtilityTerms.Prestige, weights.Prestige, appeal.Prestige, weights.Prestige * appeal.Prestige),
            new(UtilityTerms.Car, weights.Car, appeal.ExpectedCar, weights.Car * appeal.ExpectedCar),
            new(UtilityTerms.Salary, weights.Salary, salaryScore, weights.Salary * salaryScore),
            new(UtilityTerms.Status, weights.Status, statusScore, weights.Status * statusScore),
            new(UtilityTerms.Risk, weights.Risk, riskScore, -weights.Risk * riskScore),
        };
        if (context.IsCurrentEmployer)
        {
            var loyalty = (context.Traits.Loyalty - 10) / 10.0 * NegotiationEstimates.LoyaltyBonusScale;
            factors.Add(new UtilityFactor(UtilityTerms.Loyalty, 1.0, loyalty, loyalty));
        }

        return new OfferEvaluation(factors.Sum(factor => factor.Contribution), factors);
    }

    /// <summary>Utility of retiring at <paramref name="age"/>: a little better each year over 30.</summary>
    public static double RetirementUtility(int age)
    {
        var years = Math.Clamp(age - 30, 0, NegotiationEstimates.RetirementYearsCap);
        return NegotiationEstimates.RetirementBase + (NegotiationEstimates.RetirementPerYear * years);
    }

    /// <summary>
    /// The best alternative to signing, other than another offer: waiting on the market, retiring, or staying on the
    /// current contract (<paramref name="currentContractUtility"/>, when the person has one elsewhere).
    /// </summary>
    public static double Floor(int age, double? currentContractUtility)
    {
        var floor = Math.Max(NegotiationEstimates.ReservationUtility, RetirementUtility(age));
        return currentContractUtility is double current ? Math.Max(floor, current) : floor;
    }

    /// <summary>The utility an offer must reach: the floor, the acceptance margin, and extra for lost interest.</summary>
    public static double Threshold(double floor, int interest)
    {
        var lost = 1.0 - (Math.Clamp(interest, 0, NegotiationEstimates.InterestStart) / (double)NegotiationEstimates.InterestStart);
        return floor + NegotiationEstimates.AcceptanceMargin + (NegotiationEstimates.InterestMargin * lost);
    }

    /// <summary>
    /// The reasons a person gives for finding an offer short: the terms that fall furthest below what the person
    /// expects, at most two, as translation keys. When nothing stands out the person simply has better prospects. Never empty.
    /// </summary>
    public static IReadOnlyList<string> Diagnose(OfferEvaluation evaluation, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(context);
        var salary = evaluation.Weight(UtilityTerms.Salary) * Math.Max(0.0, -evaluation.Score(UtilityTerms.Salary));
        var status = context.Subject.Kind == NegotiationSubjectKind.DriverSeat
            ? evaluation.Weight(UtilityTerms.Status) * Math.Max(0.0, NegotiationEstimates.StatusScoreEqual - evaluation.Score(UtilityTerms.Status))
            : 0.0;
        var team = (evaluation.Weight(UtilityTerms.Prestige) * Math.Max(0.0, 0.5 - context.Appeal.Prestige))
            + (evaluation.Weight(UtilityTerms.Car) * Math.Max(0.0, 0.5 - context.Appeal.ExpectedCar));
        var risk = evaluation.Weight(UtilityTerms.Risk) * Math.Max(0.0, evaluation.Score(UtilityTerms.Risk) - NegotiationEstimates.RiskTolerance);

        var found = new List<(string Key, double Deficit, int Order)>
        {
            (NegotiationReasons.SalaryTooLow, salary, 0),
            (NegotiationReasons.Status, status, 1),
            (NegotiationReasons.TeamTooWeak, team, 2),
            (NegotiationReasons.Risk, risk, 3),
        };
        var reasons = found
            .Where(item => item.Deficit > 1e-9)
            .OrderByDescending(item => item.Deficit)
            .ThenBy(item => item.Order)
            .Take(2)
            .Select(item => item.Key)
            .ToList();
        if (reasons.Count == 0)
        {
            reasons.Add(NegotiationReasons.BetterProspects);
        }

        return reasons;
    }

    /// <summary>
    /// The terms the person would sign instead: the cheapest change that reaches <paramref name="threshold"/>, trying salary
    /// alone, then a better seat status with salary, then number one with salary (not while another driver holds the first seat, #325).
    /// Null when no salary the person would ask for (up to twice the reference) is enough.
    /// </summary>
    public static OfferTerms? TryCounter(OfferTerms offer, EvaluationContext context, double threshold)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(context);
        foreach (var candidate in CounterBases(offer, context.Subject, context.FirstSeatTaken))
        {
            var evaluation = Evaluate(candidate, context);
            if (evaluation.Utility >= threshold)
            {
                return candidate;
            }

            var weight = evaluation.Weight(UtilityTerms.Salary);
            var others = evaluation.Utility - evaluation.Contribution(UtilityTerms.Salary);
            var needed = ((threshold - others) / weight) + 1e-9;
            if (needed > NegotiationEstimates.SalaryScoreMax)
            {
                continue;
            }

            var package = context.ReferenceSalary * (1.0 + needed);
            var bonus = ExpectedBonus(candidate, context.Appeal.ExpectedCar);
            var salary = (long)Math.Ceiling(package - bonus);
            salary = Math.Max(salary, candidate.Salary + 1);
            var asked = new OfferTerms(
                salary,
                candidate.PointsBonus,
                candidate.WinBonus,
                candidate.TitleBonus,
                candidate.Years,
                candidate.Seat,
                candidate.Option,
                candidate.Exit);
            if (Evaluate(asked, context).Utility >= threshold)
            {
                return asked;
            }
        }

        return null;
    }

    private static IEnumerable<OfferTerms> CounterBases(OfferTerms offer, NegotiationSubject subject, bool firstSeatTaken)
    {
        yield return offer;
        if (subject.Kind != NegotiationSubjectKind.DriverSeat || offer.Seat is not SeatStatus seat)
        {
            yield break;
        }

        if (seat is SeatStatus.NumberTwo or SeatStatus.Reserve)
        {
            yield return WithSeat(offer, SeatStatus.Equal);
        }

        if (seat != SeatStatus.NumberOne && !firstSeatTaken)
        {
            yield return WithSeat(offer, SeatStatus.NumberOne);
        }
    }

    private static OfferTerms WithSeat(OfferTerms offer, SeatStatus seat) =>
        new(offer.Salary, offer.PointsBonus, offer.WinBonus, offer.TitleBonus, offer.Years, seat, offer.Option, offer.Exit);

    private static double ExpectedBonus(OfferTerms offer, double car) =>
        (offer.PointsBonus * NegotiationEstimates.ReferencePointsPerSeason * car)
        + (offer.WinBonus * NegotiationEstimates.ReferenceWinsPerSeason * car * car)
        + (offer.TitleBonus * NegotiationEstimates.TitleChanceAtTopCar * car * car * car);

    private static double StatusScore(OfferTerms offer, NegotiationSubject subject)
    {
        if (subject.Kind != NegotiationSubjectKind.DriverSeat)
        {
            return NegotiationEstimates.StatusScoreStaff;
        }

        return offer.Seat switch
        {
            SeatStatus.NumberOne => NegotiationEstimates.StatusScoreNumberOne,
            SeatStatus.Equal => NegotiationEstimates.StatusScoreEqual,
            SeatStatus.NumberTwo => NegotiationEstimates.StatusScoreNumberTwo,
            SeatStatus.Reserve => NegotiationEstimates.StatusScoreReserve,
            _ => throw new ArgumentException("A driver seat needs a seat status.", nameof(offer)),
        };
    }

    private static double PerceivedRisk(OfferTerms offer, double risk)
    {
        var factor = 1.0 + (NegotiationEstimates.RiskPerExtraYear * (offer.Years - 1));
        if (offer.Exit is not null)
        {
            factor *= NegotiationEstimates.ExitClauseRiskFactor;
        }

        if (offer.Option is OfferOption option)
        {
            factor *= option.Holder == OptionHolder.Person
                ? NegotiationEstimates.PersonOptionRiskFactor
                : NegotiationEstimates.TeamOptionRiskFactor;
        }

        return risk * factor;
    }

    private static (double Prestige, double Car, double Salary, double Status, double Risk) WeightsFor(PersonalityTraits traits)
    {
        var prestige = NegotiationEstimates.WeightPrestige;
        var car = NegotiationEstimates.WeightCar;
        var salary = NegotiationEstimates.WeightSalary;
        var status = NegotiationEstimates.WeightStatus;
        var risk = NegotiationEstimates.WeightRisk;
        switch (traits.Primary)
        {
            case PrimaryPersonality.SeeksSecurity:
                risk *= 1.6;
                break;
            case PrimaryPersonality.Mercenary:
                salary *= 1.6;
                prestige *= 0.7;
                break;
            case PrimaryPersonality.Loyal:
                risk *= 0.6;
                break;
            case PrimaryPersonality.Prestige:
                prestige *= 1.6;
                salary *= 0.8;
                break;
            case PrimaryPersonality.ShortTerm:
                salary *= 1.3;
                risk *= 0.6;
                break;
            case PrimaryPersonality.Ambitious:
                car *= 1.4;
                status *= 1.3;
                break;
            case PrimaryPersonality.Mentor:
                status *= 0.6;
                break;
            case PrimaryPersonality.TeamPlayer:
                status *= 0.7;
                prestige *= 0.9;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(traits), traits.Primary, "Unknown personality.");
        }

        var ambition = 1.0 + ((traits.Ambition - 10) / 20.0);
        car *= ambition;
        status *= ambition;
        var ego = traits.Ego - 10;
        status *= 1.0 + (ego / 20.0);
        prestige *= 1.0 + (ego / 40.0);
        return (prestige, car, salary, status, risk);
    }
}
