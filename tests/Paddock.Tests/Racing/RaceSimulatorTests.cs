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
    // Same tapes as WeekendRunTests. Moved in #191 because sudden failures drop the pre-failure pace loss.
    private const string Golden1955 = "4bc04338fb3c1006496eb9f70324d7cc585c8831153bd23b50a7e061c27f12a2";
    private const string Golden1988 = "b28417dab12b75b2b5c547f8c6a5af7988d34d871e0aa30dd0a45736506e9c30";
    private const string Golden2012 = "8d1b5c55766db4e1edff8f4bf3732cc0629d3fb8e132a1dd9aae7a3945ece93b";

    [Theory]
    [Trait("Category", "Slow")]
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
        Assert.Equal(FrameAccuracy.Approximate, via.FrameAccuracy);
        Assert.NotEmpty(via.Frames);
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
