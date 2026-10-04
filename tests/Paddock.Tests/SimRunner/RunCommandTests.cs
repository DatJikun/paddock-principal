using System.Text.RegularExpressions;
using Paddock.Domain.Career;
using Paddock.Domain.Pool;
using Paddock.SimRunner;

namespace Paddock.Tests.SimRunner;

public class RunCommandTests
{
    [Fact]
    [Trait("Category", "Slow")]
    public void OneSeasonPrintsTheEstimateLineAndAStableHash()
    {
        var first = Run(["run", "--preset", "Chaos", "--from", "1950", "--to", "1950", "--seed", "7"], out var code);
        var second = Run(["run", "--seed", "7", "--to", "1950", "--from", "1950", "--preset", "Chaos"], out _);
        var polish = Run(["run", "--preset", "Chaos", "--from", "1950", "--to", "1950", "--seed", "7", "--lang", "pl"], out var polishCode);

        Assert.Equal(0, code);
        Assert.Equal(0, polishCode);
        Assert.Equal(first, second);
        Assert.Contains(first, line => line.StartsWith("Chaos from 1950 to 1950, seed 7, people source FullyGenerated.", StringComparison.Ordinal));
        var estimates = Assert.Single(first, line => line.Contains("estimates, not measured facts", StringComparison.Ordinal));
        Assert.Contains(
            CareerDayEstimates.DriverRetirementFromAge.ToString(System.Globalization.CultureInfo.InvariantCulture),
            estimates,
            StringComparison.Ordinal);
        Assert.Contains(
            CareerDayEstimates.DriverRetirementCertainAge.ToString(System.Globalization.CultureInfo.InvariantCulture),
            estimates,
            StringComparison.Ordinal);
        Assert.Contains(
            CareerDayEstimates.StaffRetirementFromAge.ToString(System.Globalization.CultureInfo.InvariantCulture),
            estimates,
            StringComparison.Ordinal);
        Assert.Contains(
            CareerDayEstimates.StaffRetirementCertainAge.ToString(System.Globalization.CultureInfo.InvariantCulture),
            estimates,
            StringComparison.Ordinal);
        Assert.Contains(
            PoolEstimates.TargetSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
            estimates,
            StringComparison.Ordinal);
        Assert.Contains(CareerDayEstimates.PoolEntryDateText(), estimates, StringComparison.Ordinal);
        Assert.Contains(first, line => line.StartsWith("AI managers: 1. Human managers: 0.", StringComparison.Ordinal));
        var season = Assert.Single(first, line => line.StartsWith("Season 1950:", StringComparison.Ordinal));
        Assert.Matches("state hash [0-9a-f]{64}\\.$", season);
        Assert.Equal(HashOf(season), HashOf(Assert.Single(polish, line => line.StartsWith("Sezon 1950:", StringComparison.Ordinal))));
    }

    [Theory]
    [InlineData("Chaos")]
    [InlineData("MostHistorical")]
    public void The1955SeasonRunTwiceGivesTheSameHashAndPrintsTheEconomyInBothLanguages(string preset)
    {
        string[] args = ["run", "--preset", preset, "--from", "1955", "--to", "1955", "--seed", "7"];

        var first = Run(args, out var code);
        var second = Run(args, out _);
        var polish = Run([.. args, "--lang", "pl"], out _);

        Assert.Equal(0, code);
        Assert.Equal(first, second);
        var season = Assert.Single(first, line => line.StartsWith("Season 1955:", StringComparison.Ordinal));
        Assert.Equal(HashOf(season), HashOf(Assert.Single(polish, line => line.StartsWith("Sezon 1955:", StringComparison.Ordinal))));
        var economy = Assert.Single(first, line => line.StartsWith("Economy at the end of the run", StringComparison.Ordinal));
        Assert.Contains("estimates", economy, StringComparison.Ordinal);
        Assert.Matches("[0-9]+ teams with books, [0-9]+ insolvent, cash from -?[0-9]+ to -?[0-9]+ dollars; [0-9]+ sponsor deals; [0-9]+ boards, [0-9]+ dismissals; [0-9]+ cars\\.$", economy);
        Assert.Contains(polish, line => line.StartsWith("Ekonomia na koniec biegu", StringComparison.Ordinal));
    }

    [Fact]
    public void SaveWritesAWorldThatLoadsToThePrintedHash()
    {
        var directory = Directory.CreateTempSubdirectory("paddock-run-cmd-");
        try
        {
            var path = Path.Combine(directory.FullName, "career.paddock");
            var lines = Run(
                ["run", "--preset", "Chaos", "--from", "1950", "--to", "1950", "--seed", "7", "--save", path],
                out var code);

            Assert.Equal(0, code);
            Assert.Contains(lines, line => line.Contains(path, StringComparison.Ordinal));
            using var save = Paddock.Persistence.SaveFile.Open(path);
            var loaded = new Paddock.Persistence.WorldRepository(save).LoadWorld();
            var printed = HashOf(Assert.Single(lines, line => line.StartsWith("Season 1950:", StringComparison.Ordinal)));
            Assert.Equal(printed, loaded.StateHash());
            Assert.Equal(new DateOnly(1951, 1, 1), save.ReadMeta().CurrentGameDate);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Theory]
    [MemberData(nameof(InvalidInvocations))]
    public void InvalidInvocationsFailWithoutAReport(string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = RunCommand.Execute(args, stdout, stderr);

        Assert.Equal(1, code);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.NotEqual(string.Empty, stderr.ToString());
    }

    public static TheoryData<string[]> InvalidInvocations() => new()
    {
        { ["init-world"] },
        { ["run", "--preset", "Chaos", "--from", "1950", "--seed", "1"] },
        { ["run", "--preset", "Custom", "--from", "1950", "--to", "1950", "--seed", "1"] },
        { ["run", "--preset", "Chaos", "--from", "1951", "--to", "1950", "--seed", "1"] },
        { ["run", "--preset", "Chaos", "--from", "x", "--to", "1950", "--seed", "1"] },
        { ["run", "--preset", "Chaos", "--from", "1950", "--to", "1950", "--seed", "-1"] },
        { ["run", "--preset", "Chaos", "--from", "1950", "--to", "1950", "--seed", "1", "--lang", "de"] },
        { ["run", "--preset", "Chaos", "--from", "1950", "--to", "1950", "--seed", "1", "--schedule", "a.json"] },
        { ["run", "--preset", "Chaos", "--from", "1949", "--to", "1950", "--seed", "1"] },
        { ["run", "--preset", "Chaos", "--from", "1950", "--to", "1950", "--seed", "1", "--bogus", "1"] },
    };

    private static string HashOf(string line)
    {
        var match = Regex.Match(line, "[0-9a-f]{64}");
        Assert.True(match.Success, line);
        return match.Value;
    }

    private static string[] Run(string[] args, out int code)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        code = RunCommand.Execute(args, stdout, stderr);
        Assert.Equal(string.Empty, stderr.ToString());
        return stdout.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
