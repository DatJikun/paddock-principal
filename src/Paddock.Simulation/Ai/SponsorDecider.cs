using System.Globalization;

namespace Paddock.Simulation.Ai;

/// <summary>A sponsor that could fill a slot and the price the team's own commercial view puts on it (an indicative ESTIMATE of the world, not a quote).</summary>
public sealed record SponsorCandidate(string SponsorId, long IndicativeCents);

/// <summary>A sponsor slot with nothing in it: no deal, no talk.</summary>
public sealed record SponsorSlotCase(int Slot, IReadOnlyList<SponsorCandidate> Candidates);

/// <summary>One open sponsor talk, as the team reads it: the amount the sponsor would sign for today and the most waiting can reach.</summary>
public sealed record SponsorTalkCase(string TalkId, string SponsorId, long CurrentCents, long CappedCents);

/// <summary>A renewal the sponsor offers, with what the team's other candidates for the slot are worth.</summary>
public sealed record SponsorOfferCase(string OfferId, string SponsorId, long AnnualCents, long ReferenceCents);

/// <summary>What the sponsor decider reads.</summary>
public sealed record SponsorInput(
    DateOnly Today,
    long AnnualBudgetCents,
    IReadOnlyList<SponsorSlotCase> Slots,
    IReadOnlyList<SponsorTalkCase> Talks,
    IReadOnlyList<SponsorOfferCase> Offers);

/// <summary>What a sponsor review asks the host to do.</summary>
public abstract record SponsorAction
{
    private SponsorAction()
    {
    }

    public sealed record BeginTalks(string SponsorId, int Slot) : SponsorAction;

    public sealed record SignAtCurrentTerms(string TalkId) : SponsorAction;

    public sealed record RespondToOffer(string OfferId, bool Accept) : SponsorAction;
}

/// <summary>
/// Sponsor talks (T38): which sponsor to approach for an empty slot, when to stop waiting for better terms and sign, and whether to
/// take a renewal offer. The waiting game weighs the better terms the principal expects against the chance that a rival takes the
/// sponsor first; the real chance is hidden, so the principal uses its own ESTIMATE (<see cref="AiEstimates.AssumedRivalDailyChance"/>).
/// A fearful archetype signs sooner. Money counts for every archetype and most for the one that must get through on the budget.
/// </summary>
public static class SponsorDecider
{
    public const string BeginKind = "sponsor.begin";

    public const string WaitKind = "sponsor.wait";

    public const string OfferKind = "sponsor.offer";

    public static IReadOnlyList<SponsorAction> Review(DecisionContext context, SponsorInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(input);
        var actions = new List<SponsorAction>();
        foreach (var offer in input.Offers.OrderBy(item => item.OfferId, StringComparer.Ordinal))
        {
            Offer(context, input, offer, actions);
        }

        foreach (var talk in input.Talks.OrderBy(item => item.TalkId, StringComparer.Ordinal))
        {
            Wait(context, input, talk, actions);
        }

        foreach (var slot in input.Slots.OrderBy(item => item.Slot))
        {
            Begin(context, input, slot, actions);
        }

        return actions;
    }

    private static double Value(DecisionContext context, SponsorInput input, double cents)
    {
        var weight = 0.5 + (context.Profile.Thrift / 2.0);
        return weight * AiEstimates.SponsorIncomeScale * cents / Math.Max(1L, input.AnnualBudgetCents);
    }

    private static void Begin(DecisionContext context, SponsorInput input, SponsorSlotCase slot, List<SponsorAction> actions)
    {
        if (slot.Candidates.Count == 0)
        {
            return;
        }

        var options = slot.Candidates
            .OrderBy(item => item.SponsorId, StringComparer.Ordinal)
            .Select(candidate => new OptionDraft(
                "sponsor/" + candidate.SponsorId,
                [new FactorDraft(AiTextKeys.FactorIncome, Value(context, input, candidate.IndicativeCents))]))
            .ToArray();
        var note = new TraceNote(
            BeginKind,
            "slot-" + slot.Slot.ToString(CultureInfo.InvariantCulture),
            "slot-" + slot.Slot.ToString(CultureInfo.InvariantCulture),
            "Picks a sponsor for the empty slot " + slot.Slot.ToString(CultureInfo.InvariantCulture) + ".",
            null,
            IsKeyDecision: false);
        var chosen = UtilityChooser.Choose(context, DecisionFacet.Sponsors, note, options, _ => AiTextKeys.ReasonSponsorChosen);
        actions.Add(new SponsorAction.BeginTalks(chosen.Id["sponsor/".Length..], slot.Slot));
    }

    private static void Wait(DecisionContext context, SponsorInput input, SponsorTalkCase talk, List<SponsorAction> actions)
    {
        var horizon = AiEstimates.SponsorWaitHorizonDays;
        var perDay = talk.CappedCents * AiEstimates.AssumedWaitingGainMilliPerDay / 1000.0 / 1.05;
        var expected = Math.Min(talk.CappedCents, talk.CurrentCents + (perDay * horizon));
        var survive = Math.Max(0.0, 1.0 - (AiEstimates.AssumedRivalDailyChance * horizon));
        var sign = new OptionDraft(AiTextKeys.OptionSignNow, [new FactorDraft(AiTextKeys.FactorIncome, Value(context, input, talk.CurrentCents))]);
        var wait = new OptionDraft(
            AiTextKeys.OptionWaitTerms,
            [
                new FactorDraft(AiTextKeys.FactorWaitingGain, Value(context, input, expected) * survive),
                new FactorDraft(AiTextKeys.FactorRivalRisk, -context.Profile.RiskAversion * 0.1),
            ]);
        var note = new TraceNote(
            WaitKind,
            talk.TalkId,
            talk.TalkId,
            "Weighs signing at " + talk.CurrentCents.ToString(CultureInfo.InvariantCulture) + " cents against waiting for up to " + talk.CappedCents.ToString(CultureInfo.InvariantCulture) + ".",
            null,
            IsKeyDecision: false);
        var chosen = UtilityChooser.Choose(context, DecisionFacet.Sponsors, note, [sign, wait], id => id == AiTextKeys.OptionSignNow ? AiTextKeys.ReasonSponsorSigned : AiTextKeys.ReasonSponsorWaits);
        if (chosen.Id == AiTextKeys.OptionSignNow)
        {
            actions.Add(new SponsorAction.SignAtCurrentTerms(talk.TalkId));
        }
    }

    private static void Offer(DecisionContext context, SponsorInput input, SponsorOfferCase offer, List<SponsorAction> actions)
    {
        var accept = new OptionDraft(AiTextKeys.OptionAccept, [new FactorDraft(AiTextKeys.FactorIncome, Value(context, input, offer.AnnualCents))]);
        var floor = offer.ReferenceCents * AiEstimates.SponsorOfferAcceptShare;
        var decline = new OptionDraft(
            AiTextKeys.OptionDecline,
            [
                new FactorDraft(AiTextKeys.FactorIncome, Value(context, input, Math.Max(floor, 0.0) * 0.9)),
                new FactorDraft(AiTextKeys.FactorRivalRisk, -(0.1 + (context.Profile.RiskAversion * 0.2))),
            ]);
        var note = new TraceNote(
            OfferKind,
            offer.OfferId,
            offer.OfferId,
            "Weighs the sponsor's renewal offer of " + offer.AnnualCents.ToString(CultureInfo.InvariantCulture) + " cents.",
            null,
            IsKeyDecision: false);
        var chosen = UtilityChooser.Choose(context, DecisionFacet.Sponsors, note, [accept, decline], id => id == AiTextKeys.OptionAccept ? AiTextKeys.ReasonAccepted : AiTextKeys.ReasonDeclined);
        actions.Add(new SponsorAction.RespondToOffer(offer.OfferId, chosen.Id == AiTextKeys.OptionAccept));
    }
}
