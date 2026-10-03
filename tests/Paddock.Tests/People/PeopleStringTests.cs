using Paddock.Application.Localization;

namespace Paddock.Tests.People;

public class PeopleStringTests
{
    private static readonly string[] RequiredKeys =
    [
        "people.gen.rating_explanation",
        "people.gen.column.id",
        "people.gen.column.name",
        "people.gen.column.nationality",
        "people.gen.column.born",
        "people.gen.column.overall",
        "people.gen.column.potential",
        "people.gen.column.personality",
        "people.personality.seeks_security",
        "people.personality.mercenary",
        "people.personality.loyal",
        "people.personality.prestige",
        "people.personality.short_term",
        "people.personality.ambitious",
        "people.personality.mentor",
        "people.personality.team_player",
        "world.attribute.scale_explanation",
        "world.knowledge.band_explanation",
        "world.potential.explanation",
        "world.salary.explanation",
        "world.budget.explanation",
    ];

    [Fact]
    public void PeopleKeysExistInBothLanguagesAndKeepTheStarThresholdPlaceholder()
    {
        TranslationCatalog catalog = TranslationLoader.LoadDirectory(Path.Combine(RepoPaths.Root(), "strings"));

        Assert.Empty(I18nChecker.CheckKeysPresent(catalog, RequiredKeys.Select(key => new ScannedKey(key, "GenPeopleCommand.cs"))));
        foreach (Language language in new[] { Language.Pl, Language.En })
        {
            Assert.True(catalog.TryGet(language, "people.gen.rating_explanation", out TranslationEntry? explanation));
            Assert.Contains("{starThreshold}", explanation!.Text, StringComparison.Ordinal);
        }
        foreach (Language language in new[] { Language.Pl, Language.En })
        {
            Assert.True(catalog.TryGet(language, "world.attribute.scale_explanation", out TranslationEntry? scale));
            Assert.Contains("{min}", scale!.Text, StringComparison.Ordinal);
            Assert.True(catalog.TryGet(language, "world.potential.explanation", out TranslationEntry? potential));
            Assert.Contains("{max}", potential!.Text, StringComparison.Ordinal);
            Assert.True(catalog.TryGet(language, "world.knowledge.band_explanation", out TranslationEntry? band));
            Assert.Contains("{low}", band!.Text, StringComparison.Ordinal);
            Assert.Contains("{high}", band.Text, StringComparison.Ordinal);
        }
    }
}
