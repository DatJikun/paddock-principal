using Paddock.SimRunner;

namespace Paddock.Tests.SimRunner;

public class InitWorldCommandTests
{
    [Fact]
    public void PrintsCountsGapsAndAHashThatIsStableForTheSameSeed()
    {
        var first = Run(["init-world", "--preset", "Chaos", "--year", "1988", "--seed", "42"], out var code);
        var second = Run(["init-world", "--seed", "42", "--year", "1988", "--preset", "Chaos"], out _);
        var other = Run(["init-world", "--preset", "Chaos", "--year", "1988", "--seed", "43"], out _);

        Assert.Equal(0, code);
        Assert.Equal(first, second);
        Assert.StartsWith("World initialized for 1988 (authored data season 1988), people source: FullyGenerated.", first[0], StringComparison.Ordinal);
        Assert.Contains("Teams: 19", first);
        Assert.Contains("Drivers under contract: 36", first);
        Assert.Contains(first, line => line.StartsWith("State hash: ", StringComparison.Ordinal) && line.Length == "State hash: ".Length + 64);
        Assert.DoesNotContain(first, line => line.Contains("Teams with no key staff in the data", StringComparison.Ordinal));
        Assert.NotEqual(first[^1], other[^1]);
    }

    [Fact]
    public void RealPresetsWithoutACacheSeatGeneratedDriversTwoPerTeam()
    {
        var lines = Run(["init-world", "--preset", "MostHistorical", "--year", "1988", "--seed", "1"], out var code);

        Assert.Equal(0, code);
        Assert.Contains(lines, line => line.StartsWith("No people schedule found", StringComparison.Ordinal));
        Assert.Contains("Drivers under contract: 36", lines);
        var staff = lines.Single(line => line.StartsWith("Key staff: ", StringComparison.Ordinal));
        Assert.True(int.Parse(staff["Key staff: ".Length..], System.Globalization.CultureInfo.InvariantCulture) > 13);
    }

    [Fact]
    public void PolishLabelsDoNotChangeTheNumbers()
    {
        var english = Run(["init-world", "--preset", "Chaos", "--year", "1950", "--seed", "5", "--lang", "en"], out _);
        var polish = Run(["init-world", "--preset", "Chaos", "--year", "1950", "--seed", "5", "--lang", "pl"], out var code);

        Assert.Equal(0, code);
        Assert.Contains("Zespoły: 10", polish);
        Assert.Equal(english[^1].Split(": ")[1], polish[^1].Split(": ")[1]);
    }

    [Theory]
    [MemberData(nameof(InvalidInvocations))]
    public void InvalidInvocationsFailWithoutAReport(string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = InitWorldCommand.Execute(args, stdout, stderr);

        Assert.Equal(1, code);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.NotEqual(string.Empty, stderr.ToString());
    }

    public static TheoryData<string[]> InvalidInvocations() => new()
    {
        { ["gen-people"] },
        { ["init-world", "--preset", "Chaos", "--year", "1988"] },
        { ["init-world", "--preset", "Custom", "--year", "1988", "--seed", "1"] },
        { ["init-world", "--preset", "Chaos", "--year", "x", "--seed", "1"] },
        { ["init-world", "--preset", "Chaos", "--year", "1988", "--seed", "-1"] },
        { ["init-world", "--preset", "Chaos", "--year", "1988", "--seed", "1", "--seed", "2"] },
        { ["init-world", "--preset", "Chaos", "--year", "1988", "--seed", "1", "--lang", "de"] },
        { ["init-world", "--preset", "Chaos", "--year", "1988", "--seed", "1", "--schedule", "a.json"] },
        { ["init-world", "--preset", "Chaos", "--year", "1949", "--seed", "1"] },
        { ["init-world", "--preset", "MostHistorical", "--year", "2030", "--seed", "1"] },
        { ["init-world", "--preset", "Chaos", "--year", "1988", "--seed", "1", "--bogus", "1"] },
    };

    private static string[] Run(string[] args, out int code)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        code = InitWorldCommand.Execute(args, stdout, stderr);
        Assert.Equal(string.Empty, stderr.ToString());
        return stdout.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
