using System.Text.Json;

namespace Paddock.Application.Localization;

public sealed class LocalizationLoadException(string message) : Exception(message);

/// <summary>
/// Loads <c>data/i18n/pl.json</c> and <c>data/i18n/en.json</c>.
/// <para>
/// Format: one flat JSON object. Each property name is a dotted key (<c>authored.error.gap</c>); there is
/// no nesting. A value is either a string (<c>"Hello {driver}"</c>) or a plural object whose properties
/// are the categories <c>one</c>, <c>few</c>, <c>many</c>, <c>other</c> with string values; <c>other</c> is
/// mandatory in the loader, the full set per language is enforced by <see cref="I18nChecker"/> (pl: all
/// four, en: one and other). Placeholders are <c>{name}</c>; <c>{{</c> and <c>}}</c> are literal braces.
/// Duplicate keys are rejected.
/// </para>
/// </summary>
public static class TranslationLoader
{
    public static TranslationCatalog LoadDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        return new TranslationCatalog(
            LoadFile(Path.Combine(directory, "pl.json")),
            LoadFile(Path.Combine(directory, "en.json")));
    }

    public static IReadOnlyDictionary<string, TranslationEntry> LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!File.Exists(path))
        {
            throw new LocalizationLoadException($"{path}: file not found");
        }

        return Parse(File.ReadAllText(path), path);
    }

    public static IReadOnlyDictionary<string, TranslationEntry> Parse(string json, string source)
    {
        ArgumentNullException.ThrowIfNull(json);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new LocalizationLoadException($"{source}: invalid JSON ({ex.Message})");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new LocalizationLoadException($"{source}: root must be an object");
            }

            var entries = new Dictionary<string, TranslationEntry>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var key = property.Name;
                if (key.Length == 0)
                {
                    throw new LocalizationLoadException($"{source}: empty key");
                }

                if (entries.ContainsKey(key))
                {
                    throw new LocalizationLoadException($"{source}: duplicate key '{key}'");
                }

                entries[key] = ParseEntry(property.Value, key, source);
            }

            return entries;
        }
    }

    private static TranslationEntry ParseEntry(JsonElement value, string key, string source)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                return TranslationEntry.Plain(value.GetString()!);
            case JsonValueKind.Object:
                var forms = new Dictionary<PluralCategory, string>();
                foreach (var form in value.EnumerateObject())
                {
                    if (!PluralRules.TryParse(form.Name, out var category))
                    {
                        throw new LocalizationLoadException($"{source}: '{key}' has unknown plural category '{form.Name}'");
                    }

                    if (form.Value.ValueKind != JsonValueKind.String)
                    {
                        throw new LocalizationLoadException($"{source}: '{key}.{form.Name}' must be a string");
                    }

                    if (!forms.TryAdd(category, form.Value.GetString()!))
                    {
                        throw new LocalizationLoadException($"{source}: '{key}' repeats plural category '{form.Name}'");
                    }
                }

                if (!forms.ContainsKey(PluralCategory.Other))
                {
                    throw new LocalizationLoadException($"{source}: '{key}' has no 'other' plural form");
                }

                return TranslationEntry.Plural(forms);
            default:
                throw new LocalizationLoadException($"{source}: '{key}' must be a string or a plural object");
        }
    }
}
