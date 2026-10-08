using Paddock.Application.Localization;
using Paddock.Domain.Pool;

namespace Paddock.Application.Pool;

/// <summary>
/// Player-facing keys of the talent pool and scouting. Copy lives in <c>strings/pl.json</c> and <c>strings/en.json</c>.
/// Every const is marked <c>[TranslationKey]</c> so <c>i18n-check</c> requires it in both files.
/// </summary>
public static class PoolKeys
{
    [TranslationKey]
    public const string PersonUnknown = "pool.error.personUnknown";

    [TranslationKey]
    public const string NoOrganization = "pool.error.noOrganization";

    [TranslationKey]
    public const string AlreadyFunded = "pool.error.alreadyFunded";

    [TranslationKey]
    public const string SigningUnavailable = "pool.error.signingUnavailable";

    [TranslationKey]
    public const string AcademyFull = "pool.error.academyFull";

    [TranslationKey]
    public const string NotOnYourList = "pool.error.notOnYourList";

    [TranslationKey]
    public const string TakenByAnother = "pool.error.takenByAnother";

    [TranslationKey]
    public const string NotYourJunior = "pool.error.notYourJunior";

    [TranslationKey]
    public const string AlreadyRecruited = "pool.error.alreadyRecruited";

    [TranslationKey]
    public const string InboxLapsedSubject = "pool.inbox.lapsed.subject";

    /// <summary>Inbox kind of the academy notices. A stable code, not text.</summary>
    public const string InboxKind = "pool.notice";

    [TranslationKey]
    public const string ProgrammeCheapSlow = "pool.programme.cheapSlow";

    [TranslationKey]
    public const string ProgrammeExpensiveFast = "pool.programme.expensiveFast";

    [TranslationKey]
    public const string RoleTest = "pool.role.test";

    [TranslationKey]
    public const string RoleJunior = "pool.role.junior";

    /// <summary>The reason a finance ledger entry carries for a junior programme (the ledger is T37).</summary>
    [TranslationKey]
    public const string FundingReason = "pool.funding.reason";

    [TranslationKey]
    public const string Estimates = "pool.estimates";

    [TranslationKey]
    public const string FocusPool = "scouting.focus.pool";

    [TranslationKey]
    public const string FocusPerson = "scouting.focus.person";

    [TranslationKey]
    public const string BandUnknown = "scouting.band.unknown";

    [TranslationKey]
    public const string ScoutingEstimates = "scouting.estimates";

    public static string ProgrammeKey(JuniorProgramme programme) => programme switch
    {
        JuniorProgramme.CheapSlow => ProgrammeCheapSlow,
        JuniorProgramme.ExpensiveFast => ProgrammeExpensiveFast,
        _ => throw new ArgumentOutOfRangeException(nameof(programme), programme, "Unknown junior programme."),
    };

    public static string RoleKey(PoolSigningRole role) => role switch
    {
        PoolSigningRole.Test => RoleTest,
        PoolSigningRole.Junior => RoleJunior,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown signing role."),
    };

    public static string FocusKey(ScoutFocusKind kind) => kind switch
    {
        ScoutFocusKind.Pool => FocusPool,
        ScoutFocusKind.Person => FocusPerson,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown focus kind."),
    };
}
