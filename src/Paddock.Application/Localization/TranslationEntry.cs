namespace Paddock.Application.Localization;

/// <summary>One translated entry: either a single text or per-plural-category forms.</summary>
public sealed class TranslationEntry
{
    private TranslationEntry(string? text, IReadOnlyDictionary<PluralCategory, string>? forms)
    {
        Text = text;
        Forms = forms;
    }

    public string? Text { get; }

    public IReadOnlyDictionary<PluralCategory, string>? Forms { get; }

    public bool IsPlural => Forms is not null;

    public static TranslationEntry Plain(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new TranslationEntry(text, null);
    }

    public static TranslationEntry Plural(IReadOnlyDictionary<PluralCategory, string> forms)
    {
        ArgumentNullException.ThrowIfNull(forms);
        return new TranslationEntry(null, forms);
    }

    public IEnumerable<string> AllTexts() => Forms is null ? [Text!] : Forms.Values;
}
