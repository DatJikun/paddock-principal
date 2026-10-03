namespace Paddock.Application.Localization;

public enum PluralCategory
{
    One,
    Few,
    Many,
    Other,
}

/// <summary>CLDR cardinal plural rules for the supported languages.</summary>
public static class PluralRules
{
    /// <summary>Categories a pluralised entry must provide in <paramref name="language"/>.</summary>
    public static IReadOnlyList<PluralCategory> RequiredCategories(Language language) => language switch
    {
        Language.Pl => [PluralCategory.One, PluralCategory.Few, PluralCategory.Many, PluralCategory.Other],
        Language.En => [PluralCategory.One, PluralCategory.Other],
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
    };

    /// <summary>
    /// Polish: one = 1; few = 2-4 except 12-14 (by the last digits); many = other integers (including 0);
    /// other = fractions. English: one = 1; other = everything else. The sign is ignored.
    /// </summary>
    public static PluralCategory Select(Language language, double count)
    {
        var integer = double.IsFinite(count) && Math.Floor(count) == count;
        var abs = Math.Abs(count);
        switch (language)
        {
            case Language.En:
                return integer && abs == 1 ? PluralCategory.One : PluralCategory.Other;
            case Language.Pl:
                if (!integer)
                {
                    return PluralCategory.Other;
                }

                if (abs == 1)
                {
                    return PluralCategory.One;
                }

                var lastDigit = abs % 10;
                var lastTwo = abs % 100;
                return lastDigit is >= 2 and <= 4 && lastTwo is < 12 or > 14
                    ? PluralCategory.Few
                    : PluralCategory.Many;
            default:
                throw new ArgumentOutOfRangeException(nameof(language), language, null);
        }
    }

    public static string Name(PluralCategory category) => category switch
    {
        PluralCategory.One => "one",
        PluralCategory.Few => "few",
        PluralCategory.Many => "many",
        PluralCategory.Other => "other",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };

    public static bool TryParse(string name, out PluralCategory category)
    {
        switch (name)
        {
            case "one": category = PluralCategory.One; return true;
            case "few": category = PluralCategory.Few; return true;
            case "many": category = PluralCategory.Many; return true;
            case "other": category = PluralCategory.Other; return true;
            default: category = default; return false;
        }
    }
}
