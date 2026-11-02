using Paddock.Application.Localization;

namespace Paddock.Application.Finance;

/// <summary>
/// Player-facing finance keys. Copy lives in <c>strings/pl.json</c> and <c>strings/en.json</c>.
/// Every const is a <c>[TranslationKey]</c> so <c>i18n-check</c> requires it in both files.
/// Reason keys match <see cref="Paddock.Domain.Finance.FinanceReason"/> and <see cref="Paddock.Domain.Finance.RevenueModels.FallbackWarningKey"/>.
/// </summary>
public static class FinanceKeys
{
    [TranslationKey]
    public const string NotYourOrganization = "finance.error.notYourOrganization";

    [TranslationKey]
    public const string UnknownOrganization = "finance.error.unknownOrganization";

    [TranslationKey]
    public const string BooksOpen = "finance.error.booksOpen";

    [TranslationKey]
    public const string NoBooks = "finance.error.noBooks";

    [TranslationKey]
    public const string Insolvent = "finance.error.insolvent";

    [TranslationKey]
    public const string Malformed = "finance.error.malformed";

    [TranslationKey]
    public const string FallbackEstimate = "finance.revenue.fallback_estimate";

    [TranslationKey]
    public const string OpeningCapital = "finance.reason.opening_capital";

    [TranslationKey]
    public const string StartMoney = "finance.reason.start_money";

    [TranslationKey]
    public const string PrizeMoney = "finance.reason.prize_money";

    [TranslationKey]
    public const string RaceRunning = "finance.reason.race_running";

    [TranslationKey]
    public const string Salary = "finance.reason.salary";

    [TranslationKey]
    public const string Termination = "finance.reason.termination";

    [TranslationKey]
    public const string BoardBonus = "finance.reason.board_bonus";

    [TranslationKey]
    public const string WinBonus = "finance.reason.win_bonus";

    [TranslationKey]
    public const string TitleBonus = "finance.reason.title_bonus";

    /// <summary>The ledger line of a contract bonus, naming the driver it was paid to (#355).</summary>
    [TranslationKey]
    public const string WinBonusLine = "finance.reason.win_bonus.driver";

    [TranslationKey]
    public const string TitleBonusLine = "finance.reason.title_bonus.driver";

    /// <summary>The inbox kind of a paid contract bonus, filed under contracts. Not a translation key: the subject carries the text.</summary>
    public const string BonusNoticeKind = "contract.bonus";

    [TranslationKey]
    public const string WinBonusNotice = "finance.inbox.win_bonus.subject";

    [TranslationKey]
    public const string TitleBonusNotice = "finance.inbox.title_bonus.subject";

    [TranslationKey]
    public const string ViewUnknown = "finance.view.unknown";

    [TranslationKey]
    public const string ViewForecast = "finance.view.forecast";
}
