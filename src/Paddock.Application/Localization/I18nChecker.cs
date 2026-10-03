namespace Paddock.Application.Localization;

/// <summary>The checks behind the i18n tests and <c>SimRunner i18n-check</c>. Each problem is one line.</summary>
public static class I18nChecker
{
    public static IReadOnlyList<string> CheckRepository(string repoRoot)
    {
        ArgumentException.ThrowIfNullOrEmpty(repoRoot);
        TranslationCatalog catalog;
        try
        {
            catalog = TranslationLoader.LoadDirectory(Path.Combine(repoRoot, "data", "i18n"));
        }
        catch (LocalizationLoadException ex)
        {
            return [ex.Message];
        }

        var problems = new List<string>(CheckCatalog(catalog));
        var src = Path.Combine(repoRoot, "src");
        if (!Directory.Exists(src))
        {
            problems.Add($"{src}: directory not found, cannot scan translation keys");
            return problems;
        }

        var scanned = TranslationKeyScanner.ScanDirectory(src)
            .Concat(TranslationKeyScanner.ScanAuthoredErrorCodes(src));
        problems.AddRange(CheckKeysPresent(catalog, scanned));
        return problems;
    }

    public static IReadOnlyList<string> CheckCatalog(TranslationCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var problems = new List<string>();
        var pl = catalog.Entries(Language.Pl);
        var en = catalog.Entries(Language.En);

        foreach (var key in pl.Keys.Except(en.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            problems.Add($"key '{key}' is in pl.json but not in en.json");
        }

        foreach (var key in en.Keys.Except(pl.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            problems.Add($"key '{key}' is in en.json but not in pl.json");
        }

        foreach (var language in new[] { Language.Pl, Language.En })
        {
            var file = language.Code() + ".json";
            foreach (var (key, entry) in catalog.Entries(language).OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (entry.IsPlural)
                {
                    foreach (var category in PluralRules.RequiredCategories(language))
                    {
                        if (!entry.Forms!.ContainsKey(category))
                        {
                            problems.Add($"{file}: '{key}' lacks the '{PluralRules.Name(category)}' plural form");
                        }
                    }

                    foreach (var (category, text) in entry.Forms!)
                    {
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            problems.Add($"{file}: '{key}.{PluralRules.Name(category)}' is empty");
                        }
                    }
                }
                else if (string.IsNullOrWhiteSpace(entry.Text))
                {
                    problems.Add($"{file}: '{key}' is empty");
                }
            }
        }

        foreach (var key in pl.Keys.Intersect(en.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var plEntry = pl[key];
            var enEntry = en[key];
            if (plEntry.IsPlural != enEntry.IsPlural)
            {
                problems.Add($"key '{key}' is a plural entry in one language and plain text in the other");
            }

            var plNames = PlaceholderUnion(plEntry);
            var enNames = PlaceholderUnion(enEntry);
            foreach (var name in plNames.Except(enNames, StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                problems.Add($"key '{key}': placeholder {{{name}}} is in pl but not in en");
            }

            foreach (var name in enNames.Except(plNames, StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                problems.Add($"key '{key}': placeholder {{{name}}} is in en but not in pl");
            }
        }

        return problems;
    }

    public static IReadOnlyList<string> CheckKeysPresent(TranslationCatalog catalog, IEnumerable<ScannedKey> scanned)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(scanned);
        var problems = new List<string>();
        foreach (var item in scanned.DistinctBy(s => (s.Key, s.File)).OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            foreach (var language in new[] { Language.Pl, Language.En })
            {
                if (!catalog.TryGet(language, item.Key, out _))
                {
                    problems.Add($"key '{item.Key}' (used in {item.File}) is missing in {language.Code()}.json");
                }
            }
        }

        return problems;
    }

    private static HashSet<string> PlaceholderUnion(TranslationEntry entry)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var text in entry.AllTexts())
        {
            names.UnionWith(Placeholders.Names(text));
        }

        return names;
    }
}
