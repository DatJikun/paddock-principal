using System.Collections.ObjectModel;

namespace Paddock.Application.Commands;

/// <summary>
/// A player-facing reason: translation key plus parameters. Not a finished sentence (PP-021).
/// </summary>
public sealed record TranslationMessage
{
    public TranslationMessage(string key, IReadOnlyDictionary<string, string> parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(parameters);
        Key = key;
        Parameters = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(parameters, StringComparer.Ordinal));
    }

    public string Key { get; }

    public IReadOnlyDictionary<string, string> Parameters { get; }

    public static TranslationMessage Of(string key, params (string Name, string Value)[] parameters)
    {
        var dictionary = new Dictionary<string, string>(parameters.Length, StringComparer.Ordinal);
        foreach (var (name, value) in parameters)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(value);
            dictionary.Add(name, value);
        }

        return new TranslationMessage(key, dictionary);
    }
}
