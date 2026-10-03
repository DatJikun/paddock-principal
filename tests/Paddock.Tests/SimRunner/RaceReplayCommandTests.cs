using Paddock.Simulation.Racing;
using Paddock.Simulation.Racing.Playback;
using Paddock.SimRunner;

namespace Paddock.Tests.SimRunner;

public class RaceReplayCommandTests
{
    [Fact]
    public void PrintsTheWholeStubRaceAsTextLines()
    {
        var (code, lines, stderr) = Run("race-replay", "--seed", "42", "--speed", "20");

        Assert.Equal(0, code);
        Assert.Equal(string.Empty, stderr);
        var tape = FakeRaceTapeBuilder.Build(42).Tape;
        Assert.Equal(tape.Count, lines.Count);
        Assert.Contains("RaceStarted 20 cars, 10 laps", lines[0], StringComparison.Ordinal);
        Assert.Contains("RaceEnded", lines[^1], StringComparison.Ordinal);
        Assert.Equal(tape.Events.Select(RaceEventText.Format), lines);
    }

    [Fact]
    public void SameSeedGivesTheSameOutput()
    {
        Assert.Equal(Run("race-replay", "--seed", "3").Lines, Run("race-replay", "--seed", "3", "--speed", "500").Lines);
    }

    [Theory]
    [InlineData("race-replay")]
    [InlineData("race-replay", "--seed", "x")]
    [InlineData("race-replay", "--seed", "1", "--speed", "0")]
    [InlineData("race-replay", "--seed", "1", "--speed", "abc")]
    [InlineData("race-replay", "--seed", "1", "--seed", "2")]
    [InlineData("race-replay", "--seed", "1", "--bogus", "2")]
    [InlineData("race-replay", "--seed")]
    public void BadArgumentsFail(params string[] args)
    {
        var (code, lines, stderr) = Run(args);

        Assert.Equal(1, code);
        Assert.Empty(lines);
        Assert.NotEqual(string.Empty, stderr);
    }

    private static (int Code, List<string> Lines, string Stderr) Run(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var clock = new ManualClock();
        var code = RaceReplayCommand.Execute(args, stdout, stderr, clock, clock.Advance);
        var lines = stdout.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToList();
        return (code, lines, stderr.ToString());
    }

    private sealed class ManualClock : IClock
    {
        public long NowMs { get; private set; }

        public void Advance(long ms) => NowMs += ms;
    }
}
