using Paddock.Application.Localization;

namespace Paddock.Application.Board;

/// <summary>Player-facing keys of the board, reputation and the job search (PP-021). Texts live in <c>strings/pl.json</c> and <c>strings/en.json</c>.</summary>
public static class BoardKeys
{
    // ---- Refusals ----

    [TranslationKey]
    public const string NotUnemployed = "board.error.notUnemployed";

    [TranslationKey]
    public const string NotEmployed = "board.error.notEmployed";

    [TranslationKey]
    public const string OfferGone = "board.error.offerGone";

    [TranslationKey]
    public const string NotAnOffer = "board.error.notAnOffer";

    [TranslationKey]
    public const string FounderCannotResign = "board.error.founderCannotResign";

    [TranslationKey]
    public const string TakeOverOwnTeam = "board.error.takeOver.ownTeam";

    [TranslationKey]
    public const string TakeOverUnknownTeam = "board.error.takeOver.unknownTeam";

    [TranslationKey]
    public const string TakeOverNotATeam = "board.error.takeOver.notATeam";

    [TranslationKey]
    public const string TakeOverNoBoard = "board.error.takeOver.noBoard";

    [TranslationKey]
    public const string TakeOverNotHuman = "board.error.takeOver.notHuman";

    [TranslationKey]
    public const string TakeOverAlreadyEmployed = "board.error.takeOver.alreadyEmployed";

    [TranslationKey]
    public const string TakeOverAlreadyHuman = "board.error.takeOver.alreadyHuman";

    [TranslationKey]
    public const string TakeOverBadTilt = "board.error.takeOver.badTilt";

    [TranslationKey]
    public const string TakeOverBadName = "board.error.takeOver.badName";

    [TranslationKey]
    public const string TakeOverReason = "board.takeOver.reason";

    // ---- Inbox items ----

    [TranslationKey]
    public const string OfferSubject = "board.offer.subject";

    [TranslationKey]
    public const string OfferAcceptLabel = "board.offer.accept.label";

    [TranslationKey]
    public const string OfferAcceptConsequence = "board.offer.accept.consequence";

    [TranslationKey]
    public const string OfferDeclineLabel = "board.offer.decline.label";

    [TranslationKey]
    public const string OfferDeclineConsequence = "board.offer.decline.consequence";

    [TranslationKey]
    public const string SeasonTargetSubject = "board.seasonTarget.subject";

    [TranslationKey]
    public const string SeasonTargetSafeLabel = "board.seasonTarget.safe.label";

    [TranslationKey]
    public const string SeasonTargetSafeConsequence = "board.seasonTarget.safe.consequence";

    [TranslationKey]
    public const string SeasonTargetExpectedLabel = "board.seasonTarget.expected.label";

    [TranslationKey]
    public const string SeasonTargetExpectedConsequence = "board.seasonTarget.expected.consequence";

    [TranslationKey]
    public const string SeasonTargetAmbitiousLabel = "board.seasonTarget.ambitious.label";

    [TranslationKey]
    public const string SeasonTargetAmbitiousConsequence = "board.seasonTarget.ambitious.consequence";

    [TranslationKey]
    public const string NoticeDismissed = "board.notice.dismissed";

    [TranslationKey]
    public const string NoticeResigned = "board.notice.resigned";

    // ---- Objectives the board grants ----

    [TranslationKey]
    public const string ObjectiveSeason = "board.objective.season";

    [TranslationKey]
    public const string ObjectiveMultiYear = "board.objective.multiYear";

    [TranslationKey]
    public const string ReasonSeason = "board.reason.season";

    [TranslationKey]
    public const string ReasonMultiYear = "board.reason.multiYear";

    [TranslationKey]
    public const string EffectConfidenceUp = "board.effect.confidenceUp";

    [TranslationKey]
    public const string EffectConfidenceDown = "board.effect.confidenceDown";

    // ---- The board view ----

    [TranslationKey]
    public const string ConfidenceSecure = "board.confidence.secure";

    [TranslationKey]
    public const string ConfidenceSteady = "board.confidence.steady";

    [TranslationKey]
    public const string ConfidenceShaky = "board.confidence.shaky";

    [TranslationKey]
    public const string ConfidenceDanger = "board.confidence.danger";

    [TranslationKey]
    public const string ForecastProtected = "board.forecast.protected";

    [TranslationKey]
    public const string ForecastSafe = "board.forecast.safe";

    [TranslationKey]
    public const string ForecastAtRisk = "board.forecast.atRisk";

    [TranslationKey]
    public const string ForecastUnknown = "board.forecast.unknown";

    [TranslationKey]
    public const string ObserverStatus = "board.observer.status";

    // ---- Reputation ----

    [TranslationKey]
    public const string ReputationSeason = "reputation.reason.season";

    [TranslationKey]
    public const string ReputationDismissed = "reputation.reason.dismissed";

    [TranslationKey]
    public const string ReputationResigned = "reputation.reason.resigned";

    [TranslationKey]
    public const string BandUnknown = "reputation.band.unknown";

    [TranslationKey]
    public const string BandModest = "reputation.band.modest";

    [TranslationKey]
    public const string BandRespected = "reputation.band.respected";

    [TranslationKey]
    public const string BandRenowned = "reputation.band.renowned";

    [TranslationKey]
    public const string BandLegendary = "reputation.band.legendary";
}
