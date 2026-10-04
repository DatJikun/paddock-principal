namespace Paddock.Simulation.Ai;

/// <summary>
/// Translation keys of the AI principals (PP-021). A decision trace names its factors and its player-visible reason with these keys, so the
/// "why" view of a player shows text in the language of the game, and nothing here is prose. The texts are in <c>strings/pl.json</c> and
/// <c>strings/en.json</c> under the same keys (a test checks every constant of this class against both files). Option ids are machine ids
/// built from the <c>Option*</c> constants and an optional <c>/subject</c> suffix; their texts are the <c>ai.option.*</c> keys.
/// </summary>
public static class AiTextKeys
{
    // ---- Factors of a utility ----

    public const string FactorQuality = "ai.factor.quality";
    public const string FactorPotential = "ai.factor.potential";
    public const string FactorAge = "ai.factor.age";
    public const string FactorPrice = "ai.factor.price";
    public const string FactorContinuity = "ai.factor.continuity";
    public const string FactorStyle = "ai.factor.style";
    public const string FactorNoise = "ai.factor.noise";
    public const string FactorHistory = "ai.factor.history";
    public const string FactorBestAlternative = "ai.factor.bestAlternative";
    public const string FactorSearchCost = "ai.factor.searchCost";
    public const string FactorWaitCost = "ai.factor.waitCost";
    public const string FactorAffordability = "ai.factor.affordability";
    public const string FactorOverpay = "ai.factor.overpay";
    public const string FactorRisk = "ai.factor.risk";
    public const string FactorResultsNow = "ai.factor.resultsNow";
    public const string FactorNextYear = "ai.factor.nextYear";
    public const string FactorAccount = "ai.factor.account";
    public const string FactorSacrifice = "ai.factor.sacrifice";
    public const string FactorInertia = "ai.factor.inertia";
    public const string FactorDisruption = "ai.factor.disruption";
    public const string FactorSupplierForm = "ai.factor.supplierForm";
    public const string FactorSupplyLag = "ai.factor.supplyLag";
    public const string FactorSupplyPrice = "ai.factor.supplyPrice";
    public const string FactorSupplyNeed = "ai.factor.supplyNeed";
    public const string FactorNoDeal = "ai.factor.noDeal";
    public const string FactorIncome = "ai.factor.income";
    public const string FactorWaitingGain = "ai.factor.waitingGain";
    public const string FactorRivalRisk = "ai.factor.rivalRisk";
    public const string FactorScoutingValue = "ai.factor.scoutingValue";
    public const string FactorScoutingCost = "ai.factor.scoutingCost";

    // ---- Options ----

    public const string OptionRenew = "renew";
    public const string OptionLetExpire = "let_expire";
    public const string OptionCandidate = "candidate";
    public const string OptionWait = "wait";
    public const string OptionOffer = "offer";
    public const string OptionAccept = "accept";
    public const string OptionRevise = "revise";
    public const string OptionWalk = "walk";
    public const string OptionDecline = "decline";
    public const string OptionNoDeal = "no_deal";
    public const string OptionSignNow = "sign_now";
    public const string OptionWaitTerms = "wait_terms";
    public const string OptionScoutPool = "scout_pool";
    public const string OptionNoScouting = "no_scouting";

    // ---- Reasons a player may read ----

    public const string ReasonRenewed = "ai.reason.renewed";
    public const string ReasonLetExpire = "ai.reason.letExpire";
    public const string ReasonSigned = "ai.reason.signed";
    public const string ReasonWaited = "ai.reason.waited";
    public const string ReasonOffer = "ai.reason.offer";
    public const string ReasonAccepted = "ai.reason.accepted";
    public const string ReasonRevised = "ai.reason.revised";
    public const string ReasonWalked = "ai.reason.walked";
    public const string ReasonDeclined = "ai.reason.declined";
    public const string ReasonSplit = "ai.reason.split";
    public const string ReasonSacrifice = "ai.reason.sacrifice";
    public const string ReasonTiming = "ai.reason.timing";
    public const string ReasonSupplier = "ai.reason.supplier";
    public const string ReasonNoSupplier = "ai.reason.noSupplier";
    public const string ReasonSponsorChosen = "ai.reason.sponsorChosen";
    public const string ReasonSponsorSigned = "ai.reason.sponsorSigned";
    public const string ReasonSponsorWaits = "ai.reason.sponsorWaits";
    public const string ReasonScouting = "ai.reason.scouting";

    /// <summary>
    /// The translation key of the text of an option id. A fixed id (<c>renew</c>) maps to <c>ai.option.renew</c>; <c>split/balanced</c> to
    /// <c>ai.option.split.balanced</c>. An id that ends in a subject (<c>candidate/&lt;person&gt;</c>, <c>supplier/...</c>, <c>sponsor/...</c>) maps to the
    /// key of its first part, and the reader shows the subject beside the text.
    /// </summary>
    public static string OptionKey(string optionId)
    {
        ArgumentNullException.ThrowIfNull(optionId);
        var slash = optionId.IndexOf('/', StringComparison.Ordinal);
        if (slash < 0)
        {
            return "ai.option." + optionId;
        }

        var head = optionId[..slash];
        return head is OptionCandidate or "supplier" or "sponsor"
            ? "ai.option." + head
            : "ai.option." + head + "." + optionId[(slash + 1)..];
    }
}
