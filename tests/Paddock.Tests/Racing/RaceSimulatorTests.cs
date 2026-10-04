using Paddock.Application.Career;
using Paddock.Domain.Racing;
using Paddock.Domain.Spy;
using Paddock.Simulation.Racing;
using Paddock.Simulation.Racing.Weekend;
using Paddock.SimRunner;
using Paddock.Tests.Racing.Weekend;

namespace Paddock.Tests.Racing;

/// <summary>
/// The race seam (PP-052): the lap engine's tape matches the weekend it wraps, and a stub simulator can stand in
/// on the career and SimRunner paths.
/// </summary>
public class RaceSimulatorTests
{
    // The same golden hashes WeekendRunTests pins for the fixture races. The simulator must reproduce them.
    private const string Golden1955 = "85012ebb9000cb3f1d1a603b5ce5779d1e2aad7e4dfffe2214529014108af6a0";
    private const string Golden1988 = "34a1c0648cea7d5a85bdaab78989c2cbf42ab3f32b0226b46bb471deaa920d31";
    private const string Golden2012 = "11272410aa97a7b654169bca4732297e06a42900cf0b6d4fe5a72cc1a95305bb";

    [Theory]
    [InlineData(1955, Golden1955)]
    [InlineData(1988, Golden1988)]
    [InlineData(2012, Golden2012)]
    public void LapEngine_MatchesTheWeekendTape_ForTheGoldenSeeds(int season, string expected)
    {
        var input = RaceCommand.BuildInput(WeekendTestKit.Data, season, 1, RaceCommand.DefaultSeed, SyntheticField.For(season));
        var direct = RaceWeekend.Run(input, NullSink.Instance).Tape;
        var via = RaceSession.Run(RaceSimulationRequest.ForWeekend(input, NullSink.Instance), RaceEngineKind.Lap);

        Assert.Equal(direct.ToCanonicalJson(), via.ToCanonicalJson());
        Assert.Equal(expected, via.Hash);
    }

    [Theory]
    [InlineData(1955, 1UL)]
    [InlineData(1988, 7UL)]
    [InlineData(2012, 42UL)]
    public void LapEngine_MatchesTheWeekendTape_ForMoreSeeds(int season, ulong seed)
    {
        var input = WeekendTestKit.Input(season, seed: seed);
        var direct = RaceWeekend.Run(input, NullSink.Instance).Tape;
        var request = RaceSimulationRequest.ForWeekend(input, NullSink.Instance);
        var via = RaceSession.Run(request, RaceEngineKind.Lap);

        Assert.Equal(direct.ToCanonicalJson(), via.ToCanonicalJson());
        Assert.Equal(direct.Hash, request.Published!.Tape.Hash);
        Assert.DoesNotContain(typeof(RacePublishedFacts).GetProperties(), property => property.Name == "LapRecords");
    }

    [Fact]
    public void TheDefaultEngine_IsTheLapEngine()
    {
        Assert.IsType<LapRaceSimulator>(RaceSimulators.Resolve());
        Assert.Equal(typeof(RaceTape), typeof(IRaceSimulator).GetMethod(nameof(IRaceSimulator.Simulate))!.ReturnType);
    }

    [Fact]
    public void ACannedSimulator_RunsOnTheCareerAndSimRunnerPaths()
    {
        var canned = FakeRaceTapeBuilder.Build(42).Tape;
        IRaceSimulator stub = new CannedSimulator(canned);
        var input = WeekendTestKit.Input(1955);
        var request = RaceSimulationRequest.ForWeekend(input, NullSink.Instance);

        var career = CareerHost.SimulateRace(stub, request);
        var tool = RaceCommand.Simulate(stub, input, NullSink.Instance);

        Assert.Equal(canned.ToCanonicalJson(), career.ToCanonicalJson());
        Assert.Equal(canned.ToCanonicalJson(), tool.ToCanonicalJson());
        Assert.Null(request.Published);
    }

    private sealed class CannedSimulator(RaceTape tape) : IRaceSimulator
    {
        public RaceTape Simulate(RaceSimulationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return tape;
        }
    }
}
