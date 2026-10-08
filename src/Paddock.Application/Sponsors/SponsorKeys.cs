using Paddock.Application.Localization;

namespace Paddock.Application.Sponsors;

/// <summary>
/// Player-facing sponsor keys. Copy lives in <c>strings/pl.json</c> and <c>strings/en.json</c>. The ledger reason keys match
/// <see cref="Paddock.Domain.Sponsors.SponsorReason"/>. Industry and slot names are built from a prefix and the data key
/// (<see cref="IndustryName"/>, <see cref="SlotName"/>); those are listed here so the i18n check sees them.
/// </summary>
public static class SponsorKeys
{
    [TranslationKey]
    public const string NotYourOrganization = "sponsor.error.notYourOrganization";

    [TranslationKey]
    public const string UnknownOrganization = "sponsor.error.unknownOrganization";

    [TranslationKey]
    public const string NoBooks = "sponsor.error.noBooks";

    [TranslationKey]
    public const string UnknownSponsor = "sponsor.error.unknownSponsor";

    [TranslationKey]
    public const string SponsorUnavailable = "sponsor.error.sponsorUnavailable";

    [TranslationKey]
    public const string EraForbids = "sponsor.error.eraForbids";

    [TranslationKey]
    public const string IndustryConflict = "sponsor.error.industryConflict";

    [TranslationKey]
    public const string PrestigeTooLow = "sponsor.error.prestigeTooLow";

    [TranslationKey]
    public const string BadSlot = "sponsor.error.badSlot";

    [TranslationKey]
    public const string SlotKindMismatch = "sponsor.error.slotKindMismatch";

    [TranslationKey]
    public const string SlotBusy = "sponsor.error.slotBusy";

    [TranslationKey]
    public const string TooManyTalks = "sponsor.error.tooManyTalks";

    [TranslationKey]
    public const string UnknownTalk = "sponsor.error.unknownTalk";

    [TranslationKey]
    public const string TalkClosed = "sponsor.error.talkClosed";

    [TranslationKey]
    public const string UnknownOffer = "sponsor.error.unknownOffer";

    [TranslationKey]
    public const string OfferClosed = "sponsor.error.offerClosed";

    [TranslationKey]
    public const string Malformed = "sponsor.error.malformed";

    [TranslationKey]
    public const string BadTerms = "sponsor.error.badTerms";

    [TranslationKey]
    public const string NoRoundsLeft = "sponsor.error.noRoundsLeft";

    [TranslationKey]
    public const string SameTerms = "sponsor.error.sameTerms";

    [TranslationKey]
    public const string ReasonInstalment = "sponsor.reason.instalment";

    [TranslationKey]
    public const string ReasonBonus = "sponsor.reason.bonus";

    [TranslationKey]
    public const string ReasonNationality = "sponsor.reason.nationality";

    [TranslationKey]
    public const string ReasonInKind = "sponsor.reason.inKind";

    [TranslationKey]
    public const string ReasonSigning = "sponsor.reason.signing";

    [TranslationKey]
    public const string ObjectiveKind = "sponsor.objective.kind";

    [TranslationKey]
    public const string ObjectiveReason = "sponsor.objective.reason";

    [TranslationKey]
    public const string ObjectiveOnMet = "sponsor.objective.onMet";

    [TranslationKey]
    public const string ObjectiveOnFailed = "sponsor.objective.onFailed";

    [TranslationKey]
    public const string InboxLostSubject = "sponsor.inbox.lost.subject";

    [TranslationKey]
    public const string InboxMetSubject = "sponsor.inbox.met.subject";

    [TranslationKey]
    public const string InboxFailedSubject = "sponsor.inbox.failed.subject";

    [TranslationKey]
    public const string InboxCompletedSubject = "sponsor.inbox.completed.subject";

    [TranslationKey]
    public const string InboxOfferSubject = "sponsor.inbox.offer.subject";

    [TranslationKey]
    public const string InboxOfferSubject2 = "sponsor.inbox.offer.subject.2";

    [TranslationKey]
    public const string InboxOfferSubject3 = "sponsor.inbox.offer.subject.3";

    [TranslationKey]
    public const string ViewUnknown = "sponsor.view.unknown";

    [TranslationKey]
    public const string ViewRivalKnown = "sponsor.view.rivalKnown";

    [TranslationKey]
    public const string ViewTermsEstimate = "sponsor.view.termsEstimate";

    [TranslationKey]
    public const string SlotTechnical = "sponsor.slot.technical";

    [TranslationKey]
    public const string SlotMain = "sponsor.slot.main";

    [TranslationKey]
    public const string SlotSecondary = "sponsor.slot.secondary";

    [TranslationKey]
    public const string IndustryFuel = "sponsor.industry.fuel";

    [TranslationKey]
    public const string IndustryOil = "sponsor.industry.oil";

    [TranslationKey]
    public const string IndustryTyres = "sponsor.industry.tyres";

    [TranslationKey]
    public const string IndustryPatron = "sponsor.industry.patron";

    [TranslationKey]
    public const string IndustryMotorClub = "sponsor.industry.motor_club";

    [TranslationKey]
    public const string IndustryAutomotive = "sponsor.industry.automotive";

    [TranslationKey]
    public const string IndustryTobacco = "sponsor.industry.tobacco";

    [TranslationKey]
    public const string IndustryAlcohol = "sponsor.industry.alcohol";

    [TranslationKey]
    public const string IndustryConsumer = "sponsor.industry.consumer";

    [TranslationKey]
    public const string IndustryFinance = "sponsor.industry.finance";

    [TranslationKey]
    public const string IndustryElectronics = "sponsor.industry.electronics";

    [TranslationKey]
    public const string OfferAcceptLabel = "sponsor.offer.accept.label";

    [TranslationKey]
    public const string OfferAcceptConsequence = "sponsor.offer.accept.consequence";

    [TranslationKey]
    public const string OfferDeclineLabel = "sponsor.offer.decline.label";

    [TranslationKey]
    public const string OfferDeclineConsequence = "sponsor.offer.decline.consequence";

    [TranslationKey]
    public const string InboxEndingSubject = "sponsor.inbox.ending.subject";

    /// <summary>Inbox kind of every sponsor notice. A stable code, not text.</summary>
    public const string InboxKind = "sponsor.notice";

    /// <summary>The subject of a renewal offer for a deal of this many years (one, two or three).</summary>
    public static string OfferSubject(int years) => years switch
    {
        2 => InboxOfferSubject2,
        3 => InboxOfferSubject3,
        _ => InboxOfferSubject,
    };

    public static string IndustryName(string industry) => "sponsor.industry." + industry;

    public static string SlotName(Paddock.Domain.Sponsors.SlotKind kind) => "sponsor.slot." + Paddock.Domain.Sponsors.SlotKinds.KeyOf(kind);
}
