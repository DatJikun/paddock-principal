using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Contracts;

/// <summary>What a person does with an offer.</summary>
public enum ResponseKind
{
    /// <summary>The offer is acceptable.</summary>
    Accept = 0,

    /// <summary>The person asks for other terms.</summary>
    Counter = 1,

    /// <summary>The person turns the offer down for good.</summary>
    Refuse = 2,
}

/// <summary>
/// The person's answer to one offer, with everything the decision trace needs.
/// <see cref="Reasons"/> are never empty for a counter or a refusal. <see cref="Counter"/> is set for a counter.
/// </summary>
public sealed record PersonResponse(
    ResponseKind Kind,
    OfferTerms? Counter,
    IReadOnlyList<string> Reasons,
    OfferEvaluation Evaluation,
    double Floor,
    double Threshold);

/// <summary>An offer that is acceptable to the person, with the utility the person found in it.</summary>
public sealed record AcceptableOffer(Negotiation Negotiation, double Utility);

/// <summary>
/// The subject-independent rules of a negotiation: how patient a person is, how a person answers an offer, and how a person
/// picks among rival offers. Everything is a pure function (INV-005): the tie-break draws come in as a function so the caller
/// decides which named stream they come from (INV-004). The terms of a subject enter only through
/// <see cref="CounterpartyEvaluator"/>; a supplier subject (T43) would bring its own evaluator and reuse the rest.
/// </summary>
public static class NegotiationCore
{
    /// <summary>
    /// Rounds a person is willing to spend (ESTIMATE 3 to 5): professionalism lengthens patience, temperament shortens it.
    /// </summary>
    public static int MaxRounds(PersonalityTraits traits)
    {
        var score = (traits.Professionalism - traits.Temperament) / 9.5;
        var rounds = NegotiationEstimates.BaseRounds + (int)Math.Round(score, MidpointRounding.AwayFromZero);
        return Math.Clamp(rounds, NegotiationEstimates.MinRounds, NegotiationEstimates.MaxRounds);
    }

    /// <summary>
    /// True when the latest offer of the negotiation was a nudge: no meaningful change from the counter or offer before it.
    /// </summary>
    public static bool LastOfferWasNudge(Negotiation negotiation)
    {
        ArgumentNullException.ThrowIfNull(negotiation);
        var history = negotiation.History;
        var last = -1;
        for (var i = history.Count - 1; i >= 0; i--)
        {
            if (history[i].Kind == RoundKind.Offer)
            {
                last = i;
                break;
            }
        }

        if (last <= 0)
        {
            return false;
        }

        OfferTerms? reference = null;
        for (var i = last - 1; i >= 0; i--)
        {
            if (history[i].Kind is RoundKind.Offer or RoundKind.Counter)
            {
                reference = history[i].Terms;
                break;
            }
        }

        return reference is not null && !history[last].Terms!.IsMeaningfulChangeFrom(reference);
    }

    /// <summary>
    /// The person answers the current offer of <paramref name="negotiation"/>. Out of interest, the person refuses at once.
    /// Otherwise the offer is accepted when its utility reaches the threshold (the floor, a margin, and extra for lost interest),
    /// countered when some salary would get it there, and refused when none would.
    /// A nudge is named among the reasons, so the person explains why interest fell.
    /// </summary>
    public static PersonResponse Respond(Negotiation negotiation, EvaluationContext context, double floor)
    {
        ArgumentNullException.ThrowIfNull(negotiation);
        ArgumentNullException.ThrowIfNull(context);
        var offer = negotiation.CurrentOffer ?? throw new InvalidOperationException("There is no offer to answer.");
        var evaluation = CounterpartyEvaluator.Evaluate(offer, context);
        var threshold = CounterpartyEvaluator.Threshold(floor, negotiation.Interest);
        var nudged = LastOfferWasNudge(negotiation);

        if (negotiation.Interest <= 0)
        {
            return new PersonResponse(
                ResponseKind.Refuse,
                null,
                [NegotiationReasons.NoRealChange, NegotiationReasons.PatienceLost],
                evaluation,
                floor,
                threshold);
        }

        if (evaluation.Utility >= threshold)
        {
            return new PersonResponse(ResponseKind.Accept, null, [], evaluation, floor, threshold);
        }

        var reasons = new List<string>(CounterpartyEvaluator.Diagnose(evaluation, context));
        if (nudged)
        {
            reasons.Insert(0, NegotiationReasons.NoRealChange);
        }

        var counter = CounterpartyEvaluator.TryCounter(offer, context, threshold);
        if (counter is not null)
        {
            return new PersonResponse(ResponseKind.Counter, counter, reasons, evaluation, floor, threshold);
        }

        if (negotiation.RoundsLeft == 0)
        {
            reasons.Add(NegotiationReasons.PatienceLost);
        }

        return new PersonResponse(ResponseKind.Refuse, null, reasons, evaluation, floor, threshold);
    }

    /// <summary>
    /// Picks the offer a person signs among acceptable ones: the best utility; a tie (within
    /// <see cref="NegotiationEstimates.TieEpsilon"/>) goes to the organization the person trusts more, and an equal trust to the
    /// higher draw. <paramref name="draw"/> must come from the Market stream, keyed by the negotiation id, so that an
    /// unrelated negotiation never shifts it. Returns the winner and the number of offers it was chosen among.
    /// </summary>
    public static AcceptableOffer ChooseWinner(
        IReadOnlyList<AcceptableOffer> candidates,
        PersonId person,
        ITrustSource trust,
        Func<string, double> draw)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(trust);
        ArgumentNullException.ThrowIfNull(draw);
        if (candidates.Count == 0)
        {
            throw new ArgumentException("There is no offer to choose among.", nameof(candidates));
        }

        var best = candidates.Max(candidate => candidate.Utility);
        var tied = candidates
            .Where(candidate => best - candidate.Utility <= NegotiationEstimates.TieEpsilon)
            .OrderBy(candidate => candidate.Negotiation.Number)
            .ToList();
        if (tied.Count == 1)
        {
            return tied[0];
        }

        var trusts = tied.Select(candidate => trust.Trust(person, candidate.Negotiation.Proposer)).ToList();
        var topTrust = trusts.Max();
        var trusted = new List<AcceptableOffer>();
        for (var i = 0; i < tied.Count; i++)
        {
            if (topTrust - trusts[i] <= 1e-9)
            {
                trusted.Add(tied[i]);
            }
        }

        if (trusted.Count == 1)
        {
            return trusted[0];
        }

        AcceptableOffer? winner = null;
        var winnerDraw = double.NegativeInfinity;
        foreach (var candidate in trusted)
        {
            var value = draw(candidate.Negotiation.Id);
            if (value > winnerDraw)
            {
                winner = candidate;
                winnerDraw = value;
            }
        }

        return winner!;
    }

    /// <summary>The day by which a person decides among the acceptable offers: the earliest deadline among them.</summary>
    public static GameDate DecisionDay(IReadOnlyList<AcceptableOffer> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count == 0)
        {
            throw new ArgumentException("There is no offer.", nameof(candidates));
        }

        return candidates.Min(candidate => candidate.Negotiation.Deadline);
    }
}

/// <summary>
/// What the team can know when it makes an offer. Used by AI-side callers (T44 will use it) and by
/// anything that wants a sensible starting offer: it reads only <see cref="PersonKnowledgeView"/> (bands), never truth (INV-003).
/// </summary>
public static class ReferenceOffer
{
    /// <summary>
    /// The stars (0 to 5) the team believes the person has: the mean of the middle of the known bands of the attributes the
    /// subject uses. A person the team has no belief about gets <see cref="NegotiationEstimates.UnknownStars"/>.
    /// </summary>
    public static double Stars(NegotiationSubject subject, PersonKnowledgeView? knowledge)
    {
        if (knowledge is not PersonKnowledgeView view)
        {
            return NegotiationEstimates.UnknownStars;
        }

        IReadOnlyList<string> keys = subject.Kind == NegotiationSubjectKind.DriverSeat
            ? GenerationEstimates.DriverAttributeKeys
            : StaffCatalogue.AttributeKeys(subject.StaffRole);
        var total = 0.0;
        var count = 0;
        foreach (var attribute in view.Attributes)
        {
            if (keys.Contains(attribute.Key))
            {
                total += (attribute.Band.Low + attribute.Band.High) / 2.0;
                count++;
            }
        }

        return count == 0 ? NegotiationEstimates.UnknownStars : NegotiationEstimates.StarsFromMean(total / count);
    }

    /// <summary>The era benchmark pay at the stars the team believes the person has.</summary>
    public static long ReferenceSalary(IPayBenchmark pay, int season, NegotiationSubject subject, PersonKnowledgeView? knowledge)
    {
        ArgumentNullException.ThrowIfNull(pay);
        return pay.Reference(season, subject, Stars(subject, knowledge));
    }

    /// <summary>A starting offer at the reference salary, with no bonuses or clauses. Same bands in, same offer out.</summary>
    public static OfferTerms Suggest(
        NegotiationSubject subject,
        PersonKnowledgeView? knowledge,
        int season,
        IPayBenchmark pay,
        SeatStatus seat = SeatStatus.Equal,
        int years = 2)
    {
        var salary = ReferenceSalary(pay, season, subject, knowledge);
        return new OfferTerms(
            salary,
            0,
            0,
            0,
            years,
            subject.Kind == NegotiationSubjectKind.DriverSeat ? seat : null,
            null,
            null);
    }
}
