using System.Collections.Immutable;
using Paddock.Domain.Racing;
using Paddock.Domain.Spy;
using Paddock.Simulation.Racing.Points;
using Paddock.Simulation.Racing.Weekend;
using Paddock.SimRunner;
using Paddock.Tests.Racing.Points;

namespace Paddock.Tests.Racing.Weekend;

/// <summary>One race weekend as a function: determinism, the golden tapes, passive Spy, and the sanity of what comes out.</summary>
public class WeekendRunTests
{
    // Golden hashes (SHA-256 of the tape JSON) of three fixture races, run the way the tool runs them (default strategist
    // search, master seed RaceCommand.DefaultSeed, round 1). A change of any ESTIMATE, of the fixture field or of a component
    // changes them on purpose. To update, run the command next to each constant and paste its output:
    //   dotnet run --project tools/Paddock.SimRunner -- race --year <year> --round 1 --hash
    // The numbers come from double arithmetic (TECH §4): the guarantee is the same build on x64; a platform whose maths library
    // rounds differently would change them and the test would say so.
    private const string Golden1955 = "110be3506f9ada7ac30688db0ae2a634228bd25e511e4794d2f2ba588ed2a951";
    private const string Golden1988 = "f8da3f1c4d7fb2c436cbda09166c70c430257b461e8cfc8d89c1047372fae5a7";
    private const string Golden2012 = "94d0bfc9463c2d228e3e1b772548b53f7299eb00b876fb28aad46cea19e4342a";

    [Theory]
    [InlineData(1955, Golden1955)]
    [InlineData(1988, Golden1988)]
    [InlineData(2012, Golden2012)]
    public void FixtureRace_HasTheGoldenTapeHash(int season, string expected)
    {
        var input = RaceCommand.BuildInput(WeekendTestKit.Data, season, 1, RaceCommand.DefaultSeed, SyntheticField.For(season));

        var result = RaceWeekend.Run(input, NullSink.Instance);

        Assert.Equal(expected, result.Tape.Hash);
    }

    [Theory]
    [InlineData(1955)]
    [InlineData(1988)]
    [InlineData(2012)]
    public void SameInputAndSeed_GiveTheSameTapeJsonTwice(int season)
    {
        var a = WeekendTestKit.Run(WeekendTestKit.Input(season));
        var b = WeekendTestKit.Run(WeekendTestKit.Input(season));

        Assert.Equal(a.Tape.ToCanonicalJson(), b.Tape.ToCanonicalJson());
        Assert.Equal(a.Digest(), b.Digest());
    }

    [Fact]
    public void ADifferentSeed_GivesADifferentRace()
    {
        var a = WeekendTestKit.Run(WeekendTestKit.Input(1955, seed: 1));
        var b = WeekendTestKit.Run(WeekendTestKit.Input(1955, seed: 2));

        Assert.NotEqual(a.Tape.Hash, b.Tape.Hash);
    }

    [Theory]
    [InlineData(1955)]
    [InlineData(2012)]
    public void SpyIsPassive_TheResultIsTheSameWithAMemorySinkAndWithANullSink(int season)
    {
        var input = WeekendTestKit.Input(season);
        var memory = new MemorySink(capacity: 100_000);

        var spied = RaceWeekend.Run(input, memory);
        var plain = RaceWeekend.Run(input, NullSink.Instance);

        Assert.Equal(plain.Tape.ToCanonicalJson(), spied.Tape.ToCanonicalJson());
        Assert.Equal(plain.Digest(), spied.Digest());
        Assert.NotEmpty(memory.Traces);
        Assert.All(memory.Traces, trace =>
        {
            Assert.Equal(new WeekendKey(season, 1), trace.Weekend);
            Assert.StartsWith("strategist:car-", trace.Who, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData(1955)]
    [InlineData(1988)]
    [InlineData(2012)]
    public void TheTape_IsConsistentWithTheCumulativeTimes(int season)
    {
        var result = WeekendTestKit.Run(WeekendTestKit.Input(season, seed: 77));
        var events = result.Tape.Events;

        // Per lap, in the order the cars crossed the line: positions rise, the gap to the leader is the time behind him.
        foreach (var lap in events.OfType<LapCompleted>().GroupBy(e => e.Lap))
        {
            var crossings = lap.OrderBy(e => e.Seq).ToList();
            var leader = crossings[0];
            Assert.Equal(1, leader.Position);
            Assert.Equal(0, leader.GapToLeaderMs);
            for (var i = 0; i < crossings.Count; i++)
            {
                Assert.True(i == 0 || crossings[i].Position > crossings[i - 1].Position, $"lap {lap.Key}: positions do not follow the crossings");
                Assert.True(crossings[i].GapToLeaderMs >= 0, $"lap {lap.Key}: negative gap");
                Assert.Equal(crossings[i].RaceTime - leader.RaceTime, crossings[i].GapToLeaderMs);
                Assert.True(i == 0 || crossings[i].RaceTime >= crossings[i - 1].RaceTime);
            }
        }

        // Position changes are real: from the position the car had, to the one it has.
        var last = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < result.CarResults.Length; i++)
        {
            last[result.Tape.Events.OfType<RaceStarted>().Single().DriverIds[i]] = i + 1;
        }

        foreach (var e in events)
        {
            switch (e)
            {
                case LapCompleted lc:
                    last[lc.DriverId] = lc.Position;
                    break;
                case PositionChange pc:
                    Assert.NotEqual(pc.FromPosition, pc.ToPosition);
                    Assert.Equal(last[pc.DriverId], pc.ToPosition);
                    break;
            }
        }

        // The race is over once, nobody runs after the flag, and the fastest lap is the best lap on the tape.
        Assert.IsType<RaceEnded>(events[^1]);
        var best = events.OfType<LapCompleted>().Min(e => e.LapTimeMs);
        Assert.Equal(best, events.OfType<FastestLap>().Last().LapTimeMs);
    }

    [Theory]
    [InlineData(1955)]
    [InlineData(1988)]
    [InlineData(2012)]
    public void EveryRetirementHasAReason_AndTheClassificationIsTheClassifiersOnTheResults(int season)
    {
        foreach (var seed in new ulong[] { 3, 4, 5 })
        {
            var input = WeekendTestKit.Input(season, seed: seed);
            var result = WeekendTestKit.Run(input);

            foreach (var retirement in result.Tape.Events.OfType<Retirement>())
            {
                Assert.True(Enum.IsDefined(retirement.Reason));
                var car = result.CarResults.Single(c => c.DriverId == retirement.DriverId);
                Assert.NotEqual(FinishStatus.Classified, car.Status);
                Assert.Equal(retirement.Lap, car.RetiredOnLap);
                Assert.Equal(retirement.Reason, car.Status switch
                {
                    FinishStatus.Mechanical => RetirementReason.Mechanical,
                    FinishStatus.Accident => RetirementReason.Accident,
                    _ => RetirementReason.Other,
                });
            }

            // Every car that does not finish has exactly one retirement on the tape; every finisher has a Finished event.
            Assert.Equal(
                result.CarResults.Count(c => c.Status != FinishStatus.Classified),
                result.Tape.Events.OfType<Retirement>().Count());
            Assert.Equal(
                result.CarResults.Count(c => c.Status == FinishStatus.Classified),
                result.Tape.Events.OfType<Finished>().Count());

            var expected = RaceClassifier.Classify(
                PointsRules.For(input.Rules),
                [.. result.CarResults.Select(c => c.ToInput())],
                new RaceContext(input.TotalLaps, input.IsFinalRound));
            Assert.Equal(WeekendDump(expected), WeekendDump(result.Classification));

            foreach (var finished in result.Tape.Events.OfType<Finished>())
            {
                var place = result.Classification.Cars.Single(c => c.DriverIds[0] == finished.DriverId).Position;
                Assert.Equal(place, finished.Position);
            }

            var winner = result.CarResults[0];
            Assert.Equal(FinishStatus.Classified, winner.Status);
            Assert.Equal(result.LapsRun, winner.LapsCompleted);
            Assert.Equal(1, result.Classification.Cars.Single(c => c.DriverIds[0] == winner.DriverId).Position);
        }
    }

    [Fact]
    public void AnEntryNeedsAUniqueCarAndUniqueDrivers_AndTheFieldCannotBeEmpty()
    {
        var entries = SyntheticField.For(1955);

        Assert.Throws<ArgumentException>(() => WeekendTestKit.Run(WeekendTestKit.Input(1955, entries: [entries[0], entries[0]])));
        Assert.Throws<ArgumentException>(() => WeekendTestKit.Run(WeekendTestKit.Input(1955, entries: [])));
        var clash = entries[1] with { CarId = "car-x", Drivers = entries[0].Drivers };
        Assert.Throws<ArgumentException>(() => WeekendTestKit.Run(WeekendTestKit.Input(1955, entries: [entries[0], clash])));
    }

    [Fact]
    public void ASingleCar_CanRunTheRace()
    {
        var entries = ImmutableArray.Create(SyntheticField.For(1955)[0]);

        var result = WeekendTestKit.Run(WeekendTestKit.Input(1955, entries: entries));

        Assert.Single(result.CarResults);
        Assert.Single(result.Qualifying.Grid);
        Assert.IsType<RaceEnded>(result.Tape.Events[^1]);
    }

    private static string WeekendDump(RaceClassification race) => PointsTestKit.Dump(race);
}
