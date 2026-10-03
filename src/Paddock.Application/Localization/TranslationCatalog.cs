namespace Paddock.Application.Localization;

/// <summary>All loaded entries, per language.</summary>
public sealed class TranslationCatalog
{
    private readonly Dictionary<Language, IReadOnlyDictionary<string, TranslationEntry>> _byLanguage = [];

    public TranslationCatalog(
        IReadOnlyDictionary<string, TranslationEntry> pl,
        IReadOnlyDictionary<string, TranslationEntry> en)
    {
        ArgumentNullException.ThrowIfNull(pl);
        ArgumentNullException.ThrowIfNull(en);
        _byLanguage[Language.Pl] = pl;
        _byLanguage[Language.En] = en;
    }

    public IReadOnlyDictionary<string, TranslationEntry> Entries(Language language) => _byLanguage[language];

    public bool TryGet(Language language, string key, out TranslationEntry entry)
    {
        if (_byLanguage[language].TryGetValue(key, out var found))
        {
            entry = found;
            return true;
        }

        entry = null!;
        return false;
    }
}
