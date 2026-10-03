using System.Text.RegularExpressions;

namespace Paddock.Application.Localization;

public sealed record ScannedKey(string Key, string File);

/// <summary>
/// Textual scan of C# sources for translation keys. Recognised: a <c>const string</c> directly under a
/// <c>[TranslationKey]</c> attribute, a <c>TranslationKey</c> constructor call on a string literal, and
/// string literals starting with <c>config.error.</c>. Separately, every authored-data validator code
/// (a <c>public const string</c> in <c>AuthoredDataValidator*.cs</c>) implies the key
/// <c>authored.error.&lt;code&gt;</c>.
/// </summary>
public static partial class TranslationKeyScanner
{
    public const string AuthoredErrorPrefix = "authored.error.";

    [GeneratedRegex(@"\[TranslationKey\]\s*(?:(?:public|internal|private|protected|static)\s+)*const\s+string\s+\w+\s*=\s*""([^""]+)""")]
    private static partial Regex AttributedConst();

    [GeneratedRegex(@"new\s+TranslationKey\(\s*""([^""]+)""\s*\)")]
    private static partial Regex Constructed();

    [GeneratedRegex(@"""(config\.error\.[A-Za-z0-9_.\-]+)""")]
    private static partial Regex ConfigError();

    [GeneratedRegex(@"public\s+const\s+string\s+\w+\s*=\s*""([a-z0-9\-]+)""")]
    private static partial Regex ValidatorCode();

    public static IReadOnlyList<ScannedKey> ScanSource(string text, string file)
    {
        ArgumentNullException.ThrowIfNull(text);
        var found = new List<ScannedKey>();
        foreach (var regex in new[] { AttributedConst(), Constructed(), ConfigError() })
        {
            foreach (Match match in regex.Matches(text))
            {
                found.Add(new ScannedKey(match.Groups[1].Value, file));
            }
        }

        return found;
    }

    public static IReadOnlyList<ScannedKey> ScanDirectory(string srcDirectory)
    {
        var found = new List<ScannedKey>();
        foreach (var file in SourceFiles(srcDirectory))
        {
            found.AddRange(ScanSource(File.ReadAllText(file), Path.GetRelativePath(srcDirectory, file)));
        }

        return found;
    }

    public static IReadOnlyList<ScannedKey> ScanAuthoredErrorCodes(string srcDirectory)
    {
        var found = new List<ScannedKey>();
        foreach (var file in SourceFiles(srcDirectory))
        {
            if (!Path.GetFileName(file).StartsWith("AuthoredDataValidator", StringComparison.Ordinal))
            {
                continue;
            }

            var relative = Path.GetRelativePath(srcDirectory, file);
            foreach (Match match in ValidatorCode().Matches(File.ReadAllText(file)))
            {
                found.Add(new ScannedKey(AuthoredErrorPrefix + match.Groups[1].Value, relative));
            }
        }

        return found;
    }

    private static IEnumerable<string> SourceFiles(string directory) =>
        Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part is "bin" or "obj"))
            .OrderBy(path => path, StringComparer.Ordinal);
}
