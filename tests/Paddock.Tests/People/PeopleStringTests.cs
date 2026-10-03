using System.Text.Json;

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
    public void PolishAndEnglishHaveTheSameKeys()
    {
        Dictionary<string, string> english = Load("en");
        Dictionary<string, string> polish = Load("pl");

        Assert.Equal(english.Keys.OrderBy(key => key, StringComparer.Ordinal), polish.Keys.OrderBy(key => key, StringComparer.Ordinal));
        foreach (string key in RequiredKeys)
        {
            Assert.False(string.IsNullOrWhiteSpace(english[key]));
            Assert.False(string.IsNullOrWhiteSpace(polish[key]));
        }

        Assert.Contains("{starThreshold}", english["people.gen.rating_explanation"], StringComparison.Ordinal);
        Assert.Contains("{starThreshold}", polish["people.gen.rating_explanation"], StringComparison.Ordinal);
        Assert.Contains("{min}", english["world.attribute.scale_explanation"], StringComparison.Ordinal);
        Assert.Contains("{max}", polish["world.potential.explanation"], StringComparison.Ordinal);
        Assert.Contains("{low}", english["world.knowledge.band_explanation"], StringComparison.Ordinal);
        Assert.Contains("{high}", polish["world.knowledge.band_explanation"], StringComparison.Ordinal);
        Assert.Contains("estimate", english["world.salary.explanation"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("szacun", polish["world.budget.explanation"], StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> Load(string language)
    {
        string path = Path.Combine(RepoPaths.Root(), "strings", language + ".json");
        Dictionary<string, string>? table = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
        Assert.NotNull(table);
        return table;
    }
}
