using System.Globalization;
using Paddock.Domain.Random;
using Paddock.SimRunner;

namespace Paddock.Tests.SimRunner;

public class RngCommandTests
{
    // Independent of RngStreams.Derive so a hash change fails this smoke check.
    private static readonly string[] SmokeOutput =
    [
        "17035888018443443988",
        "438843545158099311",
        "8694759760124983350",
        "8164797055735540399",
        "6720316601577942619",
    ];

    [Fact]
    public void SmokeInvocationPrintsFiveNumbers()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = RngCommand.Execute(
            ["rng", "--seed", "42", "--stream", "Weather", "--season", "1950", "--count", "5"],
            stdout,
            stderr);

        Assert.Equal(0, code);
        Assert.Equal(string.Empty, stderr.ToString());
        Assert.Equal(SmokeOutput, Lines(stdout));
    }

    [Fact]
    public void SmokeInvocationMatchesTheDomainStream()
    {
        var rng = RngStreams.Derive(42, RngStreamName.Weather, 1950);
        var expected = new string[5];
        for (var i = 0; i < expected.Length; i++)
        {
            expected[i] = rng.NextULong().ToString(CultureInfo.InvariantCulture);
        }

        Assert.Equal(SmokeOutput, expected);
    }

    [Fact]
    public void FlagsMayAppearInAnyOrderAndRoundIsOptional()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = RngCommand.Execute(
            ["rng", "--count", "2", "--season", "1950", "--stream", "Weather", "--round", "4", "--seed", "42"],
            stdout,
            stderr);

        Assert.Equal(0, code);
        Assert.Equal(string.Empty, stderr.ToString());

        var rng = RngStreams.Derive(42, RngStreamName.Weather, 1950, round: 4);
        var expected = new[]
        {
            rng.NextULong().ToString(CultureInfo.InvariantCulture),
            rng.NextULong().ToString(CultureInfo.InvariantCulture),
        };
        Assert.Equal(expected, Lines(stdout));
        Assert.NotEqual(SmokeOutput[0], expected[0]);
    }

    [Theory]
    [MemberData(nameof(InvalidInvocations))]
    public void InvalidInvocationsFailWithoutWritingNumbers(string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = RngCommand.Execute(args, stdout, stderr);

        Assert.Equal(1, code);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.False(string.IsNullOrWhiteSpace(stderr.ToString()));
    }

    public static IEnumerable<object[]> InvalidInvocations()
    {
        yield return [Array.Empty<string>()];
        yield return [new[] { "build" }];
        yield return [new[] { "rng", "--seed", "42", "--stream", "Weather", "--season", "1950" }];
        yield return [new[] { "rng", "--seed", "nope", "--stream", "Weather", "--season", "1950", "--count", "1" }];
        yield return [new[] { "rng", "--seed", "42", "--stream", "Weather", "--season", "1950", "--count", "0" }];
        yield return [new[] { "rng", "--seed", "42", "--stream", "Weather", "--season", "1950", "--count", "1", "--extra" }];
    }

    private static string[] Lines(StringWriter writer)
    {
        return writer.ToString().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
    }
}
