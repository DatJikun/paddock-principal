namespace Paddock.Application.Localization;

/// <summary>
/// Turns a key and named arguments into player-facing text in one language. Placeholders are written
/// <c>{name}</c> in the text; <c>{{</c> and <c>}}</c> are literal braces. Floating-point arguments are
/// formatted with <see cref="NumberFormatter.FormatNumber"/>, everything else with the invariant culture.
/// </summary>
public interface ILocalizer
{
    Language Language { get; }

    string Get(string key, IReadOnlyDictionary<string, object?>? args = null);

    /// <summary>
    /// Picks the plural form for <paramref name="count"/> (see <see cref="PluralRules"/>). The count is also
    /// available to the text as <c>{count}</c> unless <paramref name="args"/> defines it.
    /// </summary>
    string GetPlural(string key, double count, IReadOnlyDictionary<string, object?>? args = null);
}
