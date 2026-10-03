using System.Globalization;

namespace Paddock.Application.Localization;

/// <summary>
/// Fallback chain: requested language, then <c>en</c>, then the key itself. Every fallback is reported to
/// the <see cref="IMissingKeySink"/>.
/// </summary>
public sealed class Localizer : ILocalizer
{
    private readonly TranslationCatalog _catalog;
    private readonly IMissingKeySink _sink;

    public Localizer(TranslationCatalog catalog, Language language, IMissingKeySink sink)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(sink);
        _catalog = catalog;
        Language = language;
        _sink = sink;
    }

    public Language Language { get; }

    public string Get(string key, IReadOnlyDictionary<string, object?>? args = null)
    {
        if (!Resolve(key, out var entry, out var language))
        {
            return key;
        }

        return Format(entry.IsPlural ? entry.Forms![PluralCategory.Other] : entry.Text!, key, language, args);
    }

    public string GetPlural(string key, double count, IReadOnlyDictionary<string, object?>? args = null)
    {
        if (!Resolve(key, out var entry, out var language))
        {
            return key;
        }

        var merged = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (args is not null)
        {
            foreach (var pair in args)
            {
                merged[pair.Key] = pair.Value;
            }
        }

        merged.TryAdd("count", count);

        if (!entry.IsPlural)
        {
            return Format(entry.Text!, key, language, merged);
        }

        var category = PluralRules.Select(language, count);
        if (!entry.Forms!.TryGetValue(category, out var form))
        {
            _sink.Report(new MissingKeyReport(MissingKeyKind.MissingPluralForm, key, language, PluralRules.Name(category)));
            form = entry.Forms[PluralCategory.Other];
        }

        return Format(form, key, language, merged);
    }

    private bool Resolve(string key, out TranslationEntry entry, out Language language)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (_catalog.TryGet(Language, key, out entry!))
        {
            language = Language;
            return true;
        }

        if (Language != Language.En && _catalog.TryGet(Language.En, key, out entry!))
        {
            _sink.Report(new MissingKeyReport(MissingKeyKind.FallbackToEnglish, key, Language));
            language = Language.En;
            return true;
        }

        _sink.Report(new MissingKeyReport(MissingKeyKind.MissingEverywhere, key, Language));
        language = Language;
        return false;
    }

    private string Format(string text, string key, Language language, IReadOnlyDictionary<string, object?>? args) =>
        Placeholders.Replace(text, name =>
        {
            if (args is null || !args.TryGetValue(name, out var value))
            {
                _sink.Report(new MissingKeyReport(MissingKeyKind.MissingArgument, key, language, name));
                return null;
            }

            return value switch
            {
                null => string.Empty,
                double d => NumberFormatter.FormatNumber(d, language),
                float f => NumberFormatter.FormatNumber(f, language),
                decimal m => NumberFormatter.FormatNumber((double)m, language),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty,
            };
        });
}
