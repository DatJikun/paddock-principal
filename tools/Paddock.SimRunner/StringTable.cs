using System.Text.Json;

namespace Paddock.SimRunner;

public static class StringTable
{
    public static IReadOnlyDictionary<string, string> Load(string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        string path = Find("strings/" + language + ".json");
        string json = File.ReadAllText(path);
        Dictionary<string, string>? map = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        if (map is null || map.Count == 0)
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
