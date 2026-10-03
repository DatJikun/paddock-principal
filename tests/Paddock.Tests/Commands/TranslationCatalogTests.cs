using System.Text.Json;
using System.Text.RegularExpressions;
using Paddock.Application.Commands;

namespace Paddock.Tests.Commands;

public class TranslationCatalogTests
{
    private static readonly string[] ExpectedKeys =
    [
        TranslationKeys.ManagerUnknown,
        SampleText.NameEmpty,
        SampleText.NoteEmpty,
        SampleText.TeamRequired,
        TranslationKeys.UnknownCommand,
        TranslationKeys.BlockingItem,
        TranslationKeys.HumansNotReady,
        TranslationKeys.WaitingFor,
        "people.gen.column.born",
        "people.gen.column.id",
        "people.gen.column.name",
        "people.gen.column.nationality",
        "people.gen.column.overall",
        "people.gen.column.personality",
        "people.gen.column.potential",
        "people.gen.rating_explanation",
        "people.personality.ambitious",
        "people.personality.loyal",
        "people.personality.mentor",
        "people.personality.mercenary",
        "people.personality.prestige",
        "people.personality.seeks_security",
        "people.personality.short_term",
        "people.personality.team_player",
    ];

    [Fact]
    public void PolishAndEnglishCatalogsShareKeysAndPlaceholders()
    {
        var polish = Load("pl");
        var english = Load("en");

        Assert.Equal(ExpectedKeys.OrderBy(key => key, StringComparer.Ordinal), polish.Keys.OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(ExpectedKeys.OrderBy(key => key, StringComparer.Ordinal), english.Keys.OrderBy(key => key, StringComparer.Ordinal));

        foreach (var key in ExpectedKeys)
        {
            Assert.False(string.IsNullOrWhiteSpace(polish[key]));
            Assert.False(string.IsNullOrWhiteSpace(english[key]));
            Assert.Equal(Holes(english[key]), Holes(polish[key]));
        }
    }

    private static Dictionary<string, string> Load(string language)
    {
        var path = Path.Combine(RepoPaths.Root(), "strings", language + ".json");
        var json = File.ReadAllText(path);
        var catalog = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        Assert.NotNull(catalog);
        return catalog;
    }

    private static string[] Holes(string text)
    {
        return Regex.Matches(text, @"\{[A-Za-z0-9]+\}")
            .Select(match => match.Value)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }
}
