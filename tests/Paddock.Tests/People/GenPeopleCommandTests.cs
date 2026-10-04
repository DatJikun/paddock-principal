using System.Globalization;
using Paddock.Domain.People;
using Paddock.SimRunner;

namespace Paddock.Tests.People;

public class GenPeopleCommandTests
{
    [Fact]
    public void PrintsADeterministicTableAndExplainsTheRatings()
    {
        string[] first = Run(["gen-people", "--seed", "42", "--count", "3", "--season", "1960"]);
        string[] second = Run(["gen-people", "--seed", "42", "--season", "1960", "--count", "3"]);
        string[] other = Run(["gen-people", "--seed", "43", "--count", "3", "--season", "1960"]);

        Assert.Equal(first, second);
        Assert.Equal(5, first.Length);
        Assert.Equal(Explanation("en"), first[0]);
        Assert.Contains(GenerationEstimates.StarPotential.ToString(CultureInfo.InvariantCulture), first[0], StringComparison.Ordinal);
        Assert.Equal("id\tname\tnationality\tborn\toverall\tpotential\tpersonality", first[1]);
        Assert.StartsWith("gen:1\t", first[2], StringComparison.Ordinal);
        Assert.StartsWith("gen:3\t", first[4], StringComparison.Ordinal);
        AssertPotentialAtLeastOverall(first[2]);
        Assert.NotEqual(first[2], other[2]);
    }

    [Fact]
    public void PolishLabelsDoNotChangeTheGeneratedNumbers()
    {
        string[] english = Run(["gen-people", "--seed", "7", "--count", "1", "--season", "2001", "--lang", "en"]);
        string[] polish = Run(["gen-people", "--lang", "pl", "--seed", "7", "--count", "1", "--season", "2001"]);

        Assert.Equal(Explanation("pl"), polish[0]);
        Assert.Contains("narodowość", polish[1], StringComparison.Ordinal);
        Assert.Equal(Numbers(english[2]), Numbers(polish[2]));
        Assert.NotEqual(english[1], polish[1]);
    }

    [Theory]
    [MemberData(nameof(InvalidInvocations))]
    public void InvalidInvocationsFailWithoutATable(string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        int code = GenPeopleCommand.Execute(args, stdout, stderr);

        Assert.Equal(1, code);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.False(string.IsNullOrWhiteSpace(stderr.ToString()));
    }

    public static IEnumerable<object[]> InvalidInvocations()
    {
        yield return [Array.Empty<string>()];
        yield return [new[] { "rng" }];
        yield return [new[] { "gen-people", "--seed", "1", "--count", "1" }];
        yield return [new[] { "gen-people", "--seed", "1", "--count", "0", "--season", "1950" }];
        yield return [new[] { "gen-people", "--seed", "nope", "--count", "1", "--season", "1950" }];
        yield return [new[] { "gen-people", "--seed", "1", "--count", "1", "--season", "1949" }];
        yield return [new[] { "gen-people", "--seed", "1", "--count", "1", "--season", "1950", "--lang", "de" }];
        yield return [new[] { "gen-people", "--seed", "1", "--seed", "2", "--count", "1", "--season", "1950" }];
        yield return [new[] { "gen-people", "--seed", "1", "--count", "1", "--season", "1950", "--lang", "en", "--lang", "pl" }];
        yield return [new[] { "gen-people", "--seed", "1", "--count", "1", "--season", "1950", "--extra" }];
    }

    private static string Explanation(string language)
    {
        string path = Path.Combine(RepoPaths.Root(), "strings", language + ".json");
        // The catalog loader, not a flat string map: the catalog now has plural entries (objects) as well.
        var table = Paddock.Application.Localization.TranslationLoader.LoadFile(path);
        return table[GenPeopleCommand.RatingExplanationKey].Text!.Replace(
            "{starThreshold}",
            GenerationEstimates.StarPotential.ToString(CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    private static void AssertPotentialAtLeastOverall(string row)
    {
        string[] cells = row.Split('\t');
        int overall = int.Parse(cells[4], CultureInfo.InvariantCulture);
        int potential = int.Parse(cells[5], CultureInfo.InvariantCulture);
        Assert.True(potential >= overall);
    }

    private static string Numbers(string row)
    {
        string[] cells = row.Split('\t');
        return cells[0] + cells[3] + cells[4] + cells[5];
    }

    private static string[] Run(string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        int code = GenPeopleCommand.Execute(args, stdout, stderr);
        Assert.Equal(0, code);
        Assert.Equal(string.Empty, stderr.ToString());
        return stdout.ToString().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
    }
}
