using System.Text.RegularExpressions;

namespace Paddock.Application.Localization;

internal static partial class Placeholders
{
    [GeneratedRegex(@"\{\{|\}\}|\{([A-Za-z_][A-Za-z0-9_]*)\}")]
    private static partial Regex Pattern();

    public static IReadOnlySet<string> Names(string text)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Pattern().Matches(text))
        {
            if (match.Groups[1].Success)
            {
                names.Add(match.Groups[1].Value);
            }
        }

        return names;
    }

    public static string Replace(string text, Func<string, string?> resolve) =>
        Pattern().Replace(text, match =>
        {
            if (!match.Groups[1].Success)
            {
                return match.Value == "{{" ? "{" : "}";
            }

            return resolve(match.Groups[1].Value) ?? match.Value;
        });
}
