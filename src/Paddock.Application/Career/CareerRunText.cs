using Paddock.Application.Localization;

namespace Paddock.Application.Career;

/// <summary>
/// Player-facing lines of the day-by-day run. Copy lives in <c>strings/pl.json</c> and <c>strings/en.json</c>.
/// The numbers inside <see cref="Estimates"/> come from <c>CareerDayEstimates</c> at print time.
/// </summary>
public static class CareerRunText
{
    [TranslationKey]
    public const string Title = "career.run.title";

    [TranslationKey]
    public const string Estimates = "career.run.estimates";

    [TranslationKey]
    public const string Year = "career.run.year";

    [TranslationKey]
    public const string HashNote = "career.run.hash_note";

    [TranslationKey]
    public const string Ai = "career.run.ai";

    [TranslationKey]
    public const string Saved = "career.run.saved";
}
