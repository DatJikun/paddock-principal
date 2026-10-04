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
    public const string ViewUnknown = "finance.view.unknown";

    [TranslationKey]
    public const string ViewForecast = "finance.view.forecast";
}
