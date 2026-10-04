using Paddock.Application.Localization;

namespace Paddock.SimRunner;

/// <summary>Plain-text view of <c>strings/&lt;language&gt;.json</c> for CLI output; plural entries are skipped.</summary>
public static class StringTable
{
    public static IReadOnlyDictionary<string, string> Load(string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        string path = Find("strings/" + language + ".json");
        IReadOnlyDictionary<string, TranslationEntry> entries = TranslationLoader.LoadFile(path);
        Dictionary<string, string> map = entries
            .Where(entry => !entry.Value.IsPlural)
            .ToDictionary(entry => entry.Key, entry => entry.Value.Text!, StringComparer.Ordinal);
        if (map.Count == 0)
        {
            throw new InvalidOperationException("String table '" + language + "' is empty.");
        }

        foreach (KeyValuePair<string, string> entry in map)
        {
            if (string.IsNullOrWhiteSpace(entry.Value))
            {
                throw new InvalidOperationException("String '" + entry.Key + "' is empty.");
            }
        }

        return map;
    }

    /// <summary>Both languages with their plural forms, for the commands that print through <see cref="Localizer"/>.</summary>
    public static TranslationCatalog LoadCatalog() =>
        TranslationLoader.LoadDirectory(Path.GetDirectoryName(Find("strings/en.json"))!);

    public static string Required(this IReadOnlyDictionary<string, string> table, string key)
    {
        if (!table.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Missing string '" + key + "'.");
        }

        return value;
    }

    private static string Find(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find " + relativePath + ".");
    }
}
