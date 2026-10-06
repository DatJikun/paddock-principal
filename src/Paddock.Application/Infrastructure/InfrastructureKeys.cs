using Paddock.Application.Localization;

namespace Paddock.Application.Infrastructure;

/// <summary>Player-facing keys of team facilities, test rentals and logistics. Copy lives in <c>strings/pl.json</c> and <c>strings/en.json</c>.</summary>
public static class InfrastructureKeys
{
    [TranslationKey]
    public const string NoControl = "infrastructure.error.notInControl";

    [TranslationKey]
    public const string UnknownOrganization = "infrastructure.error.unknownOrganization";

    [TranslationKey]
    public const string NotATeam = "infrastructure.error.notATeam";

    [TranslationKey]
    public const string UnknownKind = "infrastructure.error.unknownKind";

    [TranslationKey]
    public const string NoMoney = "infrastructure.error.noMoney";

    [TranslationKey]
    public const string EraNotReached = "infrastructure.error.eraNotReached";

    [TranslationKey]
    public const string AlreadyBuilding = "infrastructure.error.alreadyBuilding";

    [TranslationKey]
    public const string TestsBanned = "infrastructure.error.testsBanned";

    [TranslationKey]
    public const string NoTestsLeft = "infrastructure.error.noTestsLeft";

    [TranslationKey]
    public const string NoCars = "infrastructure.error.noCars";

    [TranslationKey]
    public const string LedgerUpgrade = "infrastructure.ledger.upgrade";

    [TranslationKey]
    public const string LedgerUpkeep = "infrastructure.ledger.upkeep";

    [TranslationKey]
    public const string LedgerTest = "infrastructure.ledger.test";

    [TranslationKey]
    public const string LedgerLogistics = "infrastructure.ledger.logistics";

    public const string NoticeKind = "infrastructure.buildFinished";

    [TranslationKey]
    public const string BuildFinishedSubject = "infrastructure.inbox.buildFinished.subject";

    [TranslationKey]
    public const string KindFactory = "infrastructure.kind.factory";

    [TranslationKey]
    public const string KindWindTunnel = "infrastructure.kind.windTunnel";

    [TranslationKey]
    public const string KindCfd = "infrastructure.kind.cfd";

    [TranslationKey]
    public const string KindSimulator = "infrastructure.kind.simulator";
}
