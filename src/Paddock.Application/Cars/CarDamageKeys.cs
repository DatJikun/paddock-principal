using Paddock.Application.Localization;

namespace Paddock.Application.Cars;

/// <summary>Player-facing keys of crash damage, repairs and spare chassis (#270). Copy lives in <c>strings/pl.json</c> and <c>strings/en.json</c>.</summary>
public static class CarDamageKeys
{
    public const string NoticeKind = "car.damage";

    [TranslationKey]
    public const string LedgerRepair = "car.ledger.repair";

    [TranslationKey]
    public const string LedgerChassis = "car.ledger.chassis";

    [TranslationKey]
    public const string RaceRepairSubject = "car.inbox.raceRepair.subject";

    [TranslationKey]
    public const string RaceChassisSubject = "car.inbox.raceChassis.subject";

    [TranslationKey]
    public const string TestRepairSubject = "car.inbox.testRepair.subject";

    [TranslationKey]
    public const string TestChassisSubject = "car.inbox.testChassis.subject";

    [TranslationKey]
    public const string SpareRunSubject = "car.inbox.spareRun.subject";

    [TranslationKey]
    public const string SpareLostSubject = "car.inbox.spareLost.subject";

    [TranslationKey]
    public const string MissedNoSpareSubject = "car.inbox.missedNoSpare.subject";

    /// <summary>The spare was already used by the team's other damaged car.</summary>
    [TranslationKey]
    public const string MissedSpareBusySubject = "car.inbox.missedSpareBusy.subject";
}
