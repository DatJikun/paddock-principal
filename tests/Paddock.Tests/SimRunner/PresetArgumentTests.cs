using Paddock.SimRunner;

namespace Paddock.Tests.SimRunner;

public class PresetArgumentTests
{
    [Theory]
    [InlineData("run", "--from", "1950", "--to", "1950", "--seed", "7")]
    [InlineData("init-world", "--year", "1950", "--seed", "7")]
    [InlineData("config")]
    public void AnUnknownPresetNamesTheValidOnes(string command, params string[] rest)
    {
        var args = new List<string> { command, "--preset", "historical" };
        args.AddRange(rest);
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = command switch
        {
            "run" => RunCommand.Execute([.. args], stdout, stderr),
            "init-world" => InitWorldCommand.Execute([.. args], stdout, stderr),
            _ => ConfigCommand.Execute([.. args], stdout, stderr),
        };

        Assert.Equal(1, code);
        var error = stderr.ToString();
        Assert.Contains("Invalid --preset value: historical", error, StringComparison.Ordinal);
        Assert.Contains("MostHistorical, Balanced, Chaos", error, StringComparison.Ordinal);
        Assert.DoesNotContain("Custom", error, StringComparison.Ordinal);
    }
}
