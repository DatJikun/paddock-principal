using Paddock.Data.Authored;
using Paddock.Domain.Racing;
using Paddock.Domain.Spy;
using Paddock.Simulation.Racing;
using Paddock.Simulation.Racing.Playback;
using Paddock.SimRunner;

namespace Paddock.Tests.SimRunner;

public class RaceReplayCommandTests
{
    [Fact]
    public void PrintsTheRealWeekendTapeAsTextLines()
    {
        var (code, lines, stderr) = Run("race-replay", "--seed", "42", "--season", "1955", "--round", "1", "--speed", "2000");

        Assert.Equal(0, code);
        Assert.Equal(string.Empty, stderr);
        var tape = RaceReplayCommand.BuildTape(LoadData(), 42, 1955, 1);
        Assert.Equal(tape.Events.Select(RaceEventText.Format), lines);
        Assert.NotEqual(FakeRaceTapeBuilder.Build(42).Tape.Hash, tape.Hash);
    }

    [Fact]
    public void SameSeedGivesTheSameTapeTwice()
    {
        var data = LoadData();
        var first = RaceReplayCommand.BuildTape(data, 7, 1955, 2);
        var second = RaceReplayCommand.BuildTape(data, 7, 1955, 2);

        Assert.Equal(first.Hash, second.Hash);
        Assert.NotEqual(first.Hash, RaceReplayCommand.BuildTape(data, 8, 1955, 2).Hash);
    }

    [Fact]
    public void TapeHoldsEveryStarterOfTheWeekendResult()
    {
        var data = LoadData();
        var input = RaceCommand.BuildInput(data, 1955, 1, 5, SyntheticField.For(1955));
        var request = RaceSimulationRequest.ForWeekend(input, NullSink.Instance);
        var tape = RaceSession.Run(request, RaceEngineKind.Lap);
        var starters = request.Published!.CarResults.Select(c => c.DriverId).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(tape.Hash, RaceReplayCommand.BuildTape(data, 5, 1955, 1).Hash);
        var onTape = tape.Events.OfType<RaceStarted>().Single().DriverIds.ToHashSet(StringComparer.Ordinal);
        Assert.True(starters.SetEquals(onTape));
        Assert.True(starters.Count >= 18);
    }

    [Fact]
    public void WithoutSeasonAndRoundItIsTheFirstRaceOfTheStartingSeason()
    {
        var data = LoadData();
        var first = data.RaceAssignments.Where(a => a.Season == RaceReplayCommand.DefaultSeason).Min(a => a.Round);

        Assert.Equal(
            RaceReplayCommand.BuildTape(data, 3, RaceReplayCommand.DefaultSeason, first).Hash,
            RaceReplayCommand.BuildTape(data, 3, null, null).Hash);
    }

    [Fact]
    public void UnknownRoundFailsWithAMessage()
    {
        var (code, lines, stderr) = Run("race-replay", "--seed", "1", "--season", "1955", "--round", "999");

        Assert.Equal(1, code);
        Assert.Empty(lines);
        Assert.Contains("no round 999", stderr, StringComparison.Ordinal);
    }

    private static AuthoredData LoadData()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (!File.Exists(Path.Combine(dir.FullName, "PaddockPrincipal.sln")))
        {
            dir = dir.Parent ?? throw new InvalidOperationException("PaddockPrincipal.sln not found.");
        }

        return AuthoredDataLoader.Load(Path.Combine(dir.FullName, "data"));
    }

    [Theory]
    [InlineData("race-replay")]
    [InlineData("race-replay", "--seed", "x")]
    [InlineData("race-replay", "--seed", "1", "--speed", "0")]
    [InlineData("race-replay", "--seed", "1", "--speed", "abc")]
    [InlineData("race-replay", "--seed", "1", "--seed", "2")]
    [InlineData("race-replay", "--seed", "1", "--bogus", "2")]
    [InlineData("race-replay", "--seed")]
    [InlineData("race-replay", "--seed", "1", "--round", "0")]
    [InlineData("race-replay", "--seed", "1", "--season", "x")]
    [InlineData("race-replay", "--seed", "1", "--season", "1955", "--season", "1956")]
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
