using Paddock.Application.Localization;
using Paddock.Domain.Contracts;

namespace Paddock.Application.Contracts;

/// <summary>
/// Player-facing keys of contracts and negotiations. Copy lives in <c>strings/pl.json</c> and <c>strings/en.json</c>.
/// Every const is marked <c>[TranslationKey]</c> so <c>i18n-check</c> requires it in both files. The reason keys repeat
/// <see cref="NegotiationReasons"/> (which lives in Domain, where the attribute is not available); a test keeps the two lists equal.
/// </summary>
public static class ContractKeys
{
    // --- Command refusals ---

    [TranslationKey]
    public const string UnknownNegotiation = "negotiation.error.unknownNegotiation";

    [TranslationKey]
    public const string NotController = "negotiation.error.notController";

    [TranslationKey]
    public const string UnknownPerson = "negotiation.error.unknownPerson";

    [TranslationKey]
    public const string NotThatRole = "negotiation.error.notThatRole";

    [TranslationKey]
    public const string PersonRetired = "negotiation.error.personRetired";

    [TranslationKey]
    public const string OwnEmployee = "negotiation.error.ownEmployee";

    [TranslationKey]
    public const string PersonUnderContract = "negotiation.error.personUnderContract";

    [TranslationKey]
    public const string AlreadyNegotiating = "negotiation.error.alreadyNegotiating";

    [TranslationKey]
    public const string TooManyOpen = "negotiation.error.tooManyOpen";

    [TranslationKey]
    public const string BadDeadline = "negotiation.error.badDeadline";

    [TranslationKey]
    public const string DeadlinePassed = "negotiation.error.deadlinePassed";

    [TranslationKey]
    public const string WrongStatus = "negotiation.error.wrongStatus";

    [TranslationKey]
    public const string NoRoundsLeft = "negotiation.error.noRoundsLeft";

    [TranslationKey]
    public const string TermsDontFit = "negotiation.error.termsDontFit";

    [TranslationKey]
    public const string CannotAfford = "negotiation.error.cannotAfford";

    [TranslationKey]
    public const string PersonTaken = "negotiation.error.personTaken";

    [TranslationKey]
    public const string UnknownContract = "contract.error.unknownContract";

    [TranslationKey]
    public const string NotEmployer = "contract.error.notEmployer";

    [TranslationKey]
    public const string ContractNotActive = "contract.error.contractNotActive";

    [TranslationKey]
    public const string OptionUnavailable = "contract.error.optionUnavailable";

    [TranslationKey]
    public const string OfferRequired = "contract.error.offerRequired";

    [TranslationKey]
    public const string TooEarlyToRenew = "contract.error.tooEarlyToRenew";

    [TranslationKey]
    public const string CompensationTooLow = "contract.error.compensationTooLow";

    [TranslationKey]
    public const string CannotPay = "contract.error.cannotPay";

    // --- Negotiation status ---

    [TranslationKey]
    public const string StatusOpen = "negotiation.status.open";

    [TranslationKey]
    public const string StatusAwaitingResponse = "negotiation.status.awaitingResponse";

    [TranslationKey]
    public const string StatusCountered = "negotiation.status.countered";

    [TranslationKey]
    public const string StatusPersonAgreed = "negotiation.status.personAgreed";

    [TranslationKey]
    public const string StatusConsidering = "negotiation.status.considering";

    [TranslationKey]
    public const string StatusAgreed = "negotiation.status.agreed";

    [TranslationKey]
    public const string StatusRefused = "negotiation.status.refused";

    [TranslationKey]
    public const string StatusWalkedAway = "negotiation.status.walkedAway";

    [TranslationKey]
    public const string StatusLost = "negotiation.status.lost";

    [TranslationKey]
    public const string StatusLapsed = "negotiation.status.lapsed";

    // --- How interested the person still is (a band, never the number) ---

    [TranslationKey]
    public const string InterestHigh = "negotiation.interest.high";

    [TranslationKey]
    public const string InterestMedium = "negotiation.interest.medium";

    [TranslationKey]
    public const string InterestLow = "negotiation.interest.low";

    // --- Reasons a person states ---

    [TranslationKey]
    public const string ReasonSalaryTooLow = "negotiation.reason.salary_too_low";

    [TranslationKey]
    public const string ReasonStatus = "negotiation.reason.status";

    [TranslationKey]
    public const string ReasonTeamTooWeak = "negotiation.reason.team_too_weak";

    [TranslationKey]
    public const string ReasonRisk = "negotiation.reason.risk";

    [TranslationKey]
    public const string ReasonBetterProspects = "negotiation.reason.better_prospects";

    [TranslationKey]
    public const string ReasonNoRealChange = "negotiation.reason.no_real_change";

    [TranslationKey]
    public const string ReasonPatienceLost = "negotiation.reason.patience_lost";

    [TranslationKey]
    public const string ReasonBetterOffer = "negotiation.reason.better_offer";

    [TranslationKey]
    public const string ReasonDeadlinePassed = "negotiation.reason.deadline_passed";

    [TranslationKey]
    public const string ReasonSignedElsewhere = "negotiation.reason.signed_elsewhere";

    // --- Inbox items ---

    [TranslationKey]
    public const string InboxCounterSubject = "negotiation.inbox.counter.subject";

    [TranslationKey]
    public const string InboxAgreedSubject = "negotiation.inbox.agreed.subject";

    [TranslationKey]
    public const string InboxRefusedSubject = "negotiation.inbox.refused.subject";

    [TranslationKey]
    public const string InboxLostSubject = "negotiation.inbox.lost.subject";

    [TranslationKey]
    public const string InboxLapsedSubject = "negotiation.inbox.lapsed.subject";

    [TranslationKey]
    public const string InboxAcceptLabel = "negotiation.inbox.accept.label";

    [TranslationKey]
    public const string InboxAcceptConsequence = "negotiation.inbox.accept.consequence";

    [TranslationKey]
    public const string InboxSignLabel = "negotiation.inbox.sign.label";

    [TranslationKey]
    public const string InboxSignConsequence = "negotiation.inbox.sign.consequence";

    [TranslationKey]
    public const string InboxWalkLabel = "negotiation.inbox.walk.label";

    [TranslationKey]
    public const string InboxWalkConsequence = "negotiation.inbox.walk.consequence";

    [TranslationKey]
    public const string InboxReviseLabel = "negotiation.inbox.revise.label";

    [TranslationKey]
    public const string InboxReviseConsequence = "negotiation.inbox.revise.consequence";

    [TranslationKey]
    public const string RenewalSubject = "contract.inbox.renewal.subject";

    [TranslationKey]
    public const string RenewalRenewLabel = "contract.inbox.renewal.renew.label";

    [TranslationKey]
    public const string RenewalRenewConsequence = "contract.inbox.renewal.renew.consequence";

    [TranslationKey]
    public const string RenewalReleaseLabel = "contract.inbox.renewal.release.label";

    [TranslationKey]
    public const string RenewalReleaseConsequence = "contract.inbox.renewal.release.consequence";

    [TranslationKey]
    public const string RenewalExtendLabel = "contract.inbox.renewal.extend.label";

    [TranslationKey]
    public const string RenewalExtendConsequence = "contract.inbox.renewal.extend.consequence";

    [TranslationKey]
    public const string RenewalGroupSubject = "contract.inbox.renewalGroup.subject";

    [TranslationKey]
    public const string RenewalGroupExtendLabel = "contract.inbox.renewalGroup.extend.label";

    [TranslationKey]
    public const string RenewalGroupExtendConsequence = "contract.inbox.renewalGroup.extend.consequence";

    [TranslationKey]
    public const string RenewalGroupReleaseLabel = "contract.inbox.renewalGroup.release.label";

    [TranslationKey]
    public const string RenewalGroupReleaseConsequence = "contract.inbox.renewalGroup.release.consequence";

    [TranslationKey]
    public const string NoticeExtendFailed = "contract.inbox.extendFailed.subject";

    [TranslationKey]
    public const string NoticePrincipalExtended = "contract.inbox.principalExtended.subject";

    [TranslationKey]
    public const string WantsMore = "negotiation.error.wantsMore";

    [TranslationKey]
    public const string RaiseSubject = "contract.inbox.raise.subject";

    [TranslationKey]
    public const string RaiseAcceptLabel = "contract.inbox.raise.accept.label";

    [TranslationKey]
    public const string RaiseAcceptConsequence = "contract.inbox.raise.accept.consequence";

    [TranslationKey]
    public const string RaiseRefuseLabel = "contract.inbox.raise.refuse.label";

    [TranslationKey]
    public const string RaiseRefuseConsequence = "contract.inbox.raise.refuse.consequence";

    [TranslationKey]
    public const string RaisePartialLabel = "contract.inbox.raise.partial.label";

    [TranslationKey]
    public const string RaisePartialConsequence = "contract.inbox.raise.partial.consequence";

    [TranslationKey]
    public const string NoticeExitExercised = "contract.inbox.exitExercised.subject";

    [TranslationKey]
    public const string NoticeOptionExercised = "contract.inbox.optionExercised.subject";

    [TranslationKey]
    public const string NoticeExpired = "contract.inbox.expired.subject";

    /// <summary>The key that names a negotiation status in the UI.</summary>
    public static string StatusKey(NegotiationStatus status) => status switch
    {
        NegotiationStatus.Open => StatusOpen,
        NegotiationStatus.AwaitingResponse => StatusAwaitingResponse,
        NegotiationStatus.Countered => StatusCountered,
        NegotiationStatus.PersonAgreed => StatusPersonAgreed,
        NegotiationStatus.Considering => StatusConsidering,
        NegotiationStatus.Agreed => StatusAgreed,
        NegotiationStatus.Refused => StatusRefused,
        NegotiationStatus.WalkedAway => StatusWalkedAway,
        NegotiationStatus.Lost => StatusLost,
        NegotiationStatus.Lapsed => StatusLapsed,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown status."),
    };

    /// <summary>The key that names how interested a person still is, from interest in thousandths. Never the number.</summary>
    public static string InterestKey(int interest) =>
        interest >= 750 ? InterestHigh : interest >= 400 ? InterestMedium : InterestLow;

    /// <summary>Every reason key listed here, for the test that keeps it equal to <see cref="NegotiationReasons.All"/>.</summary>
    public static IReadOnlyList<string> ReasonKeys { get; } =
    [
        ReasonSalaryTooLow,
        ReasonStatus,
        ReasonTeamTooWeak,
        ReasonRisk,
        ReasonBetterProspects,
        ReasonNoRealChange,
        ReasonPatienceLost,
        ReasonBetterOffer,
        ReasonDeadlinePassed,
        ReasonSignedElsewhere,
    ];
}
