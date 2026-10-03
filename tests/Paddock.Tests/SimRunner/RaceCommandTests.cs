using System.Globalization;
using Paddock.SimRunner;

namespace Paddock.Tests.SimRunner;

/// <summary><c>race</c>: the report of a weekend and of a season on the synthetic field, in both languages.</summary>
public class RaceCommandTests
{
    [Fact]
    public void OneRace_PrintsQualifyingNarrativeAndClassification()
    {
        var (code, lines, stderr) = Run("race", "--year", "1955", "--round", "1");

        Assert.Equal(0, code);
        Assert.Equal(string.Empty, stderr);
        Assert.Contains(lines, l => l.StartsWith("Race report: 1955 season, round 1", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("SYNTHETIC", StringComparison.Ordinal));
        Assert.Contains("== Qualifying ==", lines);
        Assert.Contains("== Race ==", lines);
        Assert.Contains("== Classification ==", lines);
        Assert.Contains(lines, l => l.Contains("takes pole position", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("RaceStarted", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("Finished", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("RaceEnded", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.EndsWith("points", StringComparison.Ordinal));
    }

    [Fact]
    public void TheNarrative_UsesTheSameLinesAsTheReplayTool_AndFullShowsEveryLap()
    {
        var short1 = Run("race", "--year", "1955", "--round", "1").Lines;
        var full = Run("race", "--year", "1955", "--round", "1", "--full").Lines;

        Assert.True(full.Count > short1.Count);
        Assert.Contains(full, l => System.Text.RegularExpressions.Regex.IsMatch(l, @"LapCompleted driver-\d+ lap .* P2 "));
        Assert.DoesNotContain(short1, l => System.Text.RegularExpressions.Regex.IsMatch(l, @"LapCompleted driver-\d+ lap .* P2 "));
    }

    [Fact]
    public void ThePolishReport_UsesThePolishStrings()
    {
        var (code, lines, _) = Run("race", "--year", "1955", "--round", "1", "--lang", "pl");

        Assert.Equal(0, code);
        Assert.Contains("== Kwalifikacje ==", lines);
        Assert.Contains("== Klasyfikacja ==", lines);
        Assert.Contains(lines, l => l.StartsWith("Relacja z wyścigu: sezon 1955", StringComparison.Ordinal));
    }

    [Fact]
    public void TheSameSeedGivesTheSameReport_AndAnotherSeedAnotherOne()
    {
        var a = Run("race", "--year", "1955", "--round", "2", "--seed", "7").Lines;
        var b = Run("race", "--year", "1955", "--round", "2", "--seed", "7").Lines;
        var c = Run("race", "--year", "1955", "--round", "2", "--seed", "8").Lines;

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Hash_PrintsTheTapeHashOnly()
    {
        var (code, lines, _) = Run("race", "--year", "1955", "--round", "1", "--hash");

        Assert.Equal(0, code);
        var line = Assert.Single(lines);
        Assert.Matches("^[0-9a-f]{64}$", line);
    }

    [Fact]
    public void Spy_AddsTheDevelopersSection_AndTheRaceIsTheSame()
    {
        var plain = Run("race", "--year", "1955", "--round", "1").Lines;
        var spied = Run("race", "--year", "1955", "--round", "1", "--spy").Lines;

        Assert.Equal(plain, spied.Take(plain.Count));
        Assert.Contains(spied, l => l.StartsWith("== Spy", StringComparison.Ordinal));
        Assert.Contains(spied, l => l.StartsWith("True weather:", StringComparison.Ordinal));
        Assert.Contains(spied, l => l.StartsWith("{\"Weekend\"", StringComparison.Ordinal));
    }

    [Fact]
    public void ASeason_PrintsTheTableAfterEveryRound()
    {
        var (code, lines, stderr) = Run("race", "--season", "1955");

        Assert.Equal(0, code);
        Assert.Equal(string.Empty, stderr);
        Assert.StartsWith("Season 1955: 7 rounds", lines[0], StringComparison.Ordinal);
        Assert.Equal(7, lines.Count(l => l.StartsWith("Round ", StringComparison.Ordinal)));
        Assert.Contains("== Final drivers' table ==", lines);
        Assert.Contains("== Final constructors' table ==", lines);
    }

    [Theory]
    [InlineData("race")]
    [InlineData("race", "--year", "1955")]
    [InlineData("race", "--round", "1")]
    [InlineData("race", "--year", "x", "--round", "1")]
    [InlineData("race", "--year", "1955", "--round", "1", "--round", "2")]
    [InlineData("race", "--year", "1955", "--round", "1", "--grid", "real")]
    [InlineData("race", "--year", "1955", "--round", "1", "--lang", "de")]
    [InlineData("race", "--year", "1955", "--round", "1", "--seed", "-1")]
    [InlineData("race", "--year", "1955", "--round", "1", "--bogus")]
    [InlineData("race", "--season", "1955", "--round", "1")]
    [InlineData("race", "--year", "1955", "--round")]
    public void BadArgumentsFail(params string[] args)
    {
        var (code, lines, stderr) = Run(args);

        Assert.Equal(1, code);
        Assert.Empty(lines);
        Assert.NotEqual(string.Empty, stderr);
    }

    [Fact]
    public void ARoundTheCalendarDoesNotHave_FailsWithAMessage()
    {
        var (code, _, stderr) = Run("race", "--year", "1955", "--round", "99");

        Assert.Equal(1, code);
        Assert.Contains("No layout is assigned", stderr, StringComparison.Ordinal);
    }

    private static (int Code, List<string> Lines, string Stderr) Run(params string[] args)
    {
        var stdout = new StringWriter(CultureInfo.InvariantCulture);
        var stderr = new StringWriter(CultureInfo.InvariantCulture);
        var code = RaceCommand.Execute(args, stdout, stderr);
        var lines = stdout.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToList();
        return (code, lines, stderr.ToString());
    }
}
