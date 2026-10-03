namespace Paddock.Application.Localization;

/// <summary>A translation key such as <c>authored.error.gap</c>.</summary>
public readonly record struct TranslationKey(string Value)
{
    public static implicit operator string(TranslationKey key) => key.Value;

    public override string ToString() => Value;
}

/// <summary>
/// Marks a <c>const string</c> field whose value is a translation key. The i18n scan
/// (<see cref="TranslationKeyScanner"/>, run by the tests and by <c>SimRunner i18n-check</c>) collects
/// the value of every such field under <c>src/</c> and requires it in both <c>strings/pl.json</c>
/// and <c>strings/en.json</c>. The scan is textual, so the attribute must be written exactly as
/// <c>[TranslationKey]</c> directly above the field. Keys created with a <c>TranslationKey</c> constructor
/// call on a string literal and literals starting with <c>config.error.</c> are collected too.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class TranslationKeyAttribute : Attribute
{
}
