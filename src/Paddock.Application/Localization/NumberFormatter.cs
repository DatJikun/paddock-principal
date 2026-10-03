using System.Globalization;

namespace Paddock.Application.Localization;

public static class NumberFormatter
{
    /// <summary>
    /// Formats a number for display. <c>pl</c>: decimal comma, thousands separated by a no-break space
    /// (U+00A0). <c>en</c>: decimal point, thousands separated by a comma. At most 15 fractional digits,
    /// no trailing zeros; NaN and infinities use the invariant text.
    /// </summary>
    public static string FormatNumber(double value, Language language)
    {
        if (!double.IsFinite(value))
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        var invariant = value.ToString("#,##0.###############", CultureInfo.InvariantCulture);
        if (language == Language.En)
        {
            return invariant;
        }

        return string.Create(invariant.Length, invariant, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = source[i] switch
                {
                    ',' => ' ',
                    '.' => ',',
                    var c => c,
                };
            }
        });
    }
}
