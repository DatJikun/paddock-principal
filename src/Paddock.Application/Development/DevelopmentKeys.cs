using Paddock.Application.Localization;
using Paddock.Domain.Development;

namespace Paddock.Application.Development;

/// <summary>
/// Player-facing keys of car development. Copy lives in <c>strings/pl.json</c> and <c>strings/en.json</c>. Area, kind and
/// status names are built from a prefix and the enum name (<see cref="AreaName"/>, <see cref="KindName"/>,
/// <see cref="StatusName"/>); they are listed here so the i18n check sees them.
/// </summary>
public static class DevelopmentKeys
{
    [TranslationKey]
    public const string NoControl = "development.error.notInControl";

    [TranslationKey]
    public const string UnknownOrganization = "development.error.unknownOrganization";

    [TranslationKey]
    public const string NotATeam = "development.error.notATeam";

    [TranslationKey]
    public const string BadSplit = "development.error.badSplit";

    [TranslationKey]
    public const string BadPriority = "development.error.badPriority";

    [TranslationKey]
    public const string UnknownProject = "development.error.unknownProject";

    [TranslationKey]
    public const string NotActive = "development.error.notActive";

    [TranslationKey]
    public const string NotConcept = "development.error.notConcept";

    [TranslationKey]
    public const string NotDeployable = "development.error.notDeployable";

    [TranslationKey]
    public const string BadTiming = "development.error.badTiming";

    [TranslationKey]
    public const string NotReady = "development.error.notReady";

    /// <summary>Ledger reason of a committed concept's production cost.</summary>
    [TranslationKey]
    public const string LedgerProduction = "development.ledger.production";

    [TranslationKey]
    public const string ConceptSubject = "development.inbox.concept.subject";

    [TranslationKey]
    public const string ConceptCommitLabel = "development.inbox.concept.commit.label";

    [TranslationKey]
    public const string ConceptCommitConsequence = "development.inbox.concept.commit.consequence";

    [TranslationKey]
    public const string ConceptWaitLabel = "development.inbox.concept.wait.label";

    [TranslationKey]
    public const string ConceptWaitConsequence = "development.inbox.concept.wait.consequence";

    [TranslationKey]
    public const string StatusInProduction = "development.status.InProduction";

    /// <summary>Ledger reason of the weekly development spending.</summary>
    [TranslationKey]
    public const string LedgerSpend = "development.ledger.spend";

    [TranslationKey]
    public const string ReplySubject = "development.inbox.reply.subject";

    [TranslationKey]
    public const string ReplyKeepLabel = "development.inbox.reply.keep.label";

    [TranslationKey]
    public const string ReplyKeepConsequence = "development.inbox.reply.keep.consequence";

    [TranslationKey]
    public const string ReplyCutLabel = "development.inbox.reply.cut.label";

    [TranslationKey]
    public const string ReplyCutConsequence = "development.inbox.reply.cut.consequence";

    [TranslationKey]
    public const string AreaAero = "development.area.Aero";

    [TranslationKey]
    public const string AreaChassis = "development.area.Chassis";

    [TranslationKey]
    public const string AreaReliability = "development.area.Reliability";

    [TranslationKey]
    public const string AreaTyresHandling = "development.area.TyresHandling";

    [TranslationKey]
    public const string KindUpgrade = "development.kind.Upgrade";

    [TranslationKey]
    public const string KindResearch = "development.kind.Research";

    [TranslationKey]
    public const string KindConcept = "development.kind.Concept";

    [TranslationKey]
    public const string StatusActive = "development.status.Active";

    [TranslationKey]
    public const string StatusReady = "development.status.Ready";

    [TranslationKey]
    public const string StatusCompleted = "development.status.Completed";

    [TranslationKey]
    public const string StatusFailed = "development.status.Failed";

    [TranslationKey]
    public const string StatusCut = "development.status.Cut";

    [TranslationKey]
    public const string StatusDeployed = "development.status.Deployed";

    [TranslationKey]
    public const string ForecastNote = "development.view.forecastNote";

    /// <summary>Inbox kind of the engineers' reply to a changed split. A stable code, not text.</summary>
    public const string InboxKind = "development.reply";

    /// <summary>Inbox kind of the decision "commit this concept now or keep developing?". A stable code, not text.</summary>
    public const string ConceptInboxKind = "development.concept";

    public const string OptionCommit = "commit";

    public const string OptionWait = "wait";

    public const string OptionKeep = "keep";

    public const string OptionCut = "cut";

    public const string ProjectArgument = "project";

    public static string AreaName(DevArea area) => "development.area." + area;

    public static string KindName(DevKind kind) => "development.kind." + kind;

    public static string StatusName(ProjectStatus status) => "development.status." + status;
}
