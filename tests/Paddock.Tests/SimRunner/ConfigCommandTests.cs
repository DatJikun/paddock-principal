using Paddock.Domain.Career;
using Paddock.SimRunner;

namespace Paddock.Tests.SimRunner;

public class ConfigCommandTests
{
    [Fact]
    public void PresetPrintsCanonicalJson()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var expected = CareerConfig.FromPreset(CareerPreset.MostHistorical);

        var code = ConfigCommand.Execute(["config", "--preset", "MostHistorical"], stdout, stderr);

        Assert.Equal(0, code);
        Assert.Equal(string.Empty, stderr.ToString());
        Assert.Equal([expected.ToCanonicalJson()], Lines(stdout));
    }

    [Fact]
    public void AxisOverridesAreAppliedInOrderAndCanLandOnAnotherPreset()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var expected = CareerConfig.FromPreset(CareerPreset.Balanced);

        var code = ConfigCommand.Execute(
            [
                "config",
                "--axis", "history-strength=5",
                "--preset", "Chaos",
                "--axis", "people=RealPotential",
                "--axis", "rules=Historical",
                "--axis", "ai=ReactToSituation",
            ],
            stdout,
            stderr);

        Assert.Equal(0, code);
        Assert.Equal(string.Empty, stderr.ToString());
        Assert.Equal([expected.ToCanonicalJson()], Lines(stdout));
    }

    [Fact]
    public void InvalidCombinationPrintsTheConfigAndTheErrorKey()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var expected = CareerConfig.FromPreset(CareerPreset.MostHistorical)
            .WithPeopleSource(PeopleSource.FullyGenerated);

        var code = ConfigCommand.Execute(
            ["config", "--preset", "MostHistorical", "--axis", "people=FullyGenerated"],
            stdout,
            stderr);

        Assert.Equal(1, code);
        Assert.Equal([expected.ToCanonicalJson()], Lines(stdout));
        Assert.Equal(["error " + CareerConfigCodes.ReplayNeedsRealPeople], Lines(stderr));
    }

    [Fact]
    public void PureRandomHistoryStrengthPrintsAWarningAndSucceeds()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var expected = CareerConfig.FromPreset(CareerPreset.Chaos).WithHistoryStrength(10);

        var code = ConfigCommand.Execute(
            ["config", "--preset", "Chaos", "--axis", "history-strength=10"],
            stdout,
            stderr);

        Assert.Equal(0, code);
        Assert.Equal([expected.ToCanonicalJson()], Lines(stdout));
        Assert.Equal(["warning " + CareerConfigCodes.HistoryStrengthWithPureRandom], Lines(stderr));
    }

    [Fact]
    public void HistoryStrengthAboveTenPrintsTheNewRange()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = ConfigCommand.Execute(
            ["config", "--preset", "Balanced", "--axis", "history-strength=50"],
            stdout,
            stderr);

        Assert.Equal(1, code);
        Assert.Equal(
            ["error " + CareerConfigCodes.HistoryStrengthRange + " 0 10"],
            Lines(stderr));
    }

    [Fact]
    public void RouterDispatchesConfigAndRng()
    {
        var configOut = new StringWriter();
        var configErr = new StringWriter();
        Assert.Equal(0, SimRunnerCommands.Execute(["config", "--preset", "Balanced"], configOut, configErr));
        Assert.Equal(
            [CareerConfig.FromPreset(CareerPreset.Balanced).ToCanonicalJson()],
            Lines(configOut));

        var rngOut = new StringWriter();
        var rngErr = new StringWriter();
        Assert.Equal(
            0,
            SimRunnerCommands.Execute(
                ["rng", "--seed", "42", "--stream", "Weather", "--season", "1950", "--count", "1"],
                rngOut,
                rngErr));
        Assert.Equal(["17035888018443443988"], Lines(rngOut));
        Assert.Equal(string.Empty, rngErr.ToString());
    }

    [Theory]
    [MemberData(nameof(InvalidInvocations))]
    public void InvalidInvocationsFailWithoutPrintingConfig(string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = ConfigCommand.Execute(args, stdout, stderr);

        Assert.Equal(1, code);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.False(string.IsNullOrWhiteSpace(stderr.ToString()));
    }

    public static IEnumerable<object[]> InvalidInvocations()
    {
        yield return [Array.Empty<string>()];
        yield return [new[] { "rng" }];
        yield return [new[] { "config" }];
        yield return [new[] { "config", "--preset" }];
        yield return [new[] { "config", "--preset", "Custom" }];
        yield return [new[] { "config", "--preset", "balanced" }];
        yield return [new[] { "config", "--preset", "Balanced", "--preset", "Chaos" }];
        yield return [new[] { "config", "--preset", "Balanced", "--axis", "people" }];
        yield return [new[] { "config", "--preset", "Balanced", "--axis", "people=NoSuch" }];
        yield return [new[] { "config", "--preset", "Balanced", "--axis", "people=RealPotential", "--axis", "people=FullyGenerated" }];
        yield return [new[] { "config", "--preset", "Balanced", "--axis", "history-strength=nope" }];
        yield return [new[] { "config", "--preset", "Balanced", "--axis", "no-numbers=yes" }];
        yield return [new[] { "config", "--preset", "Balanced", "--extra", "1" }];
    }

    [Fact]
    public void UnknownRouterCommandFails()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        Assert.Equal(1, SimRunnerCommands.Execute(["build"], stdout, stderr));
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.Contains("config", stderr.ToString(), StringComparison.Ordinal);
    }

    private static string[] Lines(StringWriter writer)
    {
        return writer.ToString().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
    }
}
