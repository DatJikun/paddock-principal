namespace Paddock.Application.Localization;

/// <summary>Languages the game ships with (PP-021).</summary>
public enum Language
{
    Pl,
    En,
}

public static class LanguageExtensions
{
    /// <summary>Lower-case file/code name: <c>pl</c> or <c>en</c>.</summary>
    public static string Code(this Language language) => language switch
    {
        Language.Pl => "pl",
        Language.En => "en",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
    };
}
