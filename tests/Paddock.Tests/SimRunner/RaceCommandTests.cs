using System.Globalization;
using Paddock.SimRunner;

namespace Paddock.Tests.SimRunner;

/// <summary><c>race</c>: the report of a weekend and of a season on the synthetic field, in both languages.</summary>
public class RaceCommandTests
{
    [Fact]
    public void OneRace_PrintsTheReadableReport_NotTheEventLog()
    {
        var (code, lines, stderr) = Run("race", "--year", "1955", "--round", "1");

        Assert.Equal(0, code);
        Assert.Equal(string.Empty, stderr);
        Assert.StartsWith("Race report: 1955 season, round 1", lines[0], StringComparison.Ordinal);
        Assert.Contains(lines, l => l.Contains("SYNTHETIC", StringComparison.Ordinal));
        Assert.Contains("== Conditions ==", lines);
        Assert.Contains("== Qualifying ==", lines);
        Assert.Contains(lines, l => l.StartsWith("== The opening, laps 1-", StringComparison.Ordinal));
        Assert.Contains("== Result ==", lines);
        Assert.Contains("== Points ==", lines);
        Assert.Contains(lines, l => l.Contains("took pole position", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("wins in", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("Fastest lap:", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.EndsWith("points", StringComparison.Ordinal));
        AssertNoRawEventNames(lines);

        // No per-lap lines in the report itself.
        Assert.DoesNotContain(lines, l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^Lap \d+: driver-\d+ leads"));
        Assert.DoesNotContain("== Lap by lap ==", lines);
    }

    [Fact]
    public void Verbose_AddsTheLapByLapSection()
    {
        var plain = Run("race", "--year", "1955", "--round", "1").Lines;
        var verbose = Run("race", "--year", "1955", "--round", "1", "--verbose").Lines;

        Assert.Equal(plain, verbose.Take(plain.Count));
        Assert.Contains("== Lap by lap ==", verbose);
        Assert.Contains(verbose, l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^Lap 2: driver-\d+ leads, lap time \d+:\d\d\.\d{3}\.$"));
        AssertNoRawEventNames(verbose);
    }

    [Fact]
    public void Log_PrintsTheOldRawLog_AndFullShowsEveryLap()
    {
        var log = Run("race", "--year", "1955", "--round", "1", "--log").Lines;
        var full = Run("race", "--year", "1955", "--round", "1", "--log", "--full").Lines;

        Assert.Contains("== Race ==", log);
        Assert.Contains("== Classification ==", log);
        Assert.Contains(log, l => l.Contains("RaceStarted", StringComparison.Ordinal));
        Assert.Contains(log, l => l.Contains("Finished", StringComparison.Ordinal));
        Assert.DoesNotContain("== Result ==", log);
        Assert.True(full.Count > log.Count);
        Assert.Contains(full, l => System.Text.RegularExpressions.Regex.IsMatch(l, @"LapCompleted driver-\d+ lap .* P2 "));
        Assert.DoesNotContain(log, l => System.Text.RegularExpressions.Regex.IsMatch(l, @"LapCompleted driver-\d+ lap .* P2 "));
    }

    [Theory]
    [InlineData("pl")]
    [InlineData("en")]
    public void TheReport_NeverQuotesAKeyOrAPlaceholder_InAnyEra(string language)
    {
        foreach (var year in new[] { 1950, 1955, 1970, 1988, 2000, 2022 })
        {
            var (code, lines, stderr) = Run("race", "--year", year.ToString(CultureInfo.InvariantCulture), "--round", "1", "--seed", "7", "--lang", language, "--verbose");

            Assert.Equal(0, code);
            Assert.Equal(string.Empty, stderr);
            Assert.DoesNotContain(lines, l => l.Contains("report.", StringComparison.Ordinal) || System.Text.RegularExpressions.Regex.IsMatch(l, @"\{[a-z]+\}"));
            AssertNoRawEventNames(lines);
        }
    }

    [Fact]
    public void ThePolishReport_UsesThePolishStrings()
    {
        var (code, lines, _) = Run("race", "--year", "1955", "--round", "1", "--lang", "pl");

        Assert.Equal(0, code);
        Assert.Contains("== Kwalifikacje ==", lines);
        Assert.Contains("== Wynik ==", lines);
        Assert.Contains(lines, l => l.StartsWith("Relacja z wyścigu: sezon 1955", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("zdobywa pole position", StringComparison.Ordinal));
    }

    [Fact]
    public void TheEraIsFeltInTheData_NotInHardCodedText()
    {
        var fifties = Run("race", "--year", "1955", "--round", "1", "--seed", "7").Lines;
        var eighties = Run("race", "--year", "1988", "--round", "1", "--seed", "7").Lines;

        // 1955: shared cars and no constructors' points; 1988: no shared cars and a constructors' table.
        Assert.Contains("== Shared drives ==", fifties);
        Assert.DoesNotContain("== Shared drives ==", eighties);
        Assert.DoesNotContain(fifties, l => l.Contains("constructor point", StringComparison.Ordinal));
        Assert.Contains(eighties, l => l.Contains("constructor point", StringComparison.Ordinal));
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
    [InlineData("race", "--year", "1955", "--round", "1", "--full")]
    [InlineData("race", "--year", "1955", "--round", "1", "--log", "--verbose")]
    [InlineData("race", "--season", "1955", "--log")]
    [InlineData("race", "--season", "1955", "--verbose")]
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

    private static void AssertNoRawEventNames(IEnumerable<string> lines)
    {
        string[] raw = ["RaceStarted", "LapCompleted", "PitStop", "PositionChange", "Retirement", "WeatherChange", "SafetyCar", "RedFlag", "FastestLap", "RaceEnded"];
        foreach (var line in lines)
        {
            foreach (var name in raw)
            {
                Assert.DoesNotMatch("\\b" + name + "\\b", line);
            }

            Assert.DoesNotMatch(@"\bMechanical\b", line);
        }
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
