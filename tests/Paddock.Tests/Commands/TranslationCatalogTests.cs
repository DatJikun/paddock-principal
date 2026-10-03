using Paddock.Application.Localization;

namespace Paddock.Tests.Commands;

/// <summary>
/// Pl/en key parity, placeholders and the scan of <c>TranslationKeys</c> live in the i18n checks
/// (<c>LocalizationTests</c>). This only pins the keys that test code uses without the scanner seeing them.
/// </summary>
public class TranslationCatalogTests
{
    [Fact]
    public void TestOnlyCommandKeysExistInBothLanguages()
    {
        var catalog = TranslationLoader.LoadDirectory(Path.Combine(RepoPaths.Root(), "strings"));
        string[] keys = [SampleText.NameEmpty, SampleText.NoteEmpty, SampleText.TeamRequired];

        Assert.Empty(I18nChecker.CheckKeysPresent(catalog, keys.Select(key => new ScannedKey(key, "SampleCommands.cs"))));
    }
}
