using System.Collections.Immutable;
using Paddock.Domain.Racing;
using Paddock.Simulation.Racing;

namespace Paddock.Tests.Racing;

public class RaceSummaryTests
{
    [Fact]
    public void HandMadeTapeGivesTheExpectedSummary()
    {
        ImmutableArray<string> drivers = ["a", "b", "c", "d"];
        var tape = RaceTape.From(
        [
            new RaceStarted(0, 0, 0, 2, drivers),
            new LapCompleted(1, 1, 90_000, "a", 90_000, 1, 0),
            new FastestLap(2, 1, 90_000, "a", 90_000),
            new LapCompleted(3, 1, 91_000, "b", 91_000, 2, 1_000),
            new Retirement(4, 2, 100_000, "d", RetirementReason.Accident),
            new LapCompleted(5, 2, 179_000, "a", 89_000, 1, 0),
            new FastestLap(6, 2, 179_000, "a", 89_000),
            new Finished(7, 2, 179_000, "a", 1, 2, 179_000),
            new Retirement(8, 2, 185_000, "c", RetirementReason.Mechanical),
            new LapCompleted(9, 2, 190_000, "b", 99_000, 2, 11_000),
            new Finished(10, 2, 190_000, "b", 2, 2, 190_000),
            new RaceEnded(11, 2, 190_000),
        ]);

        var summary = RaceSummary.From(tape);

        Assert.Equal(["a", "b", "d", "c"], summary.Classification.Select(c => c.DriverId));
        Assert.Equal([1, 2, 3, 4], summary.Classification.Select(c => c.Position));
        Assert.Equal([2, 2, 0, 0], summary.Classification.Select(c => c.LapsCompleted));
        Assert.Equal([true, true, false, false], summary.Classification.Select(c => c.Finished));
        Assert.Equal(new FastestLapResult("a", 89_000, 2), summary.FastestLap);
        Assert.Equal(
            [new RetirementRecord("d", 2, RetirementReason.Accident), new RetirementRecord("c", 2, RetirementReason.Mechanical)],
            summary.Retirements.ToList());
    }

    [Fact]
    public void RetireesAreOrderedByLapsCompletedThenRetirementOrder()
    {
        ImmutableArray<string> drivers = ["a", "b", "c"];
        var tape = RaceTape.From(
        [
            new RaceStarted(0, 0, 0, 3, drivers),
            new LapCompleted(1, 1, 90_000, "b", 90_000, 1, 0),
            new LapCompleted(2, 1, 91_000, "c", 91_000, 2, 1_000),
            new Retirement(3, 2, 100_000, "a", RetirementReason.Other),
            new Retirement(4, 2, 110_000, "c", RetirementReason.Other),
            new Retirement(5, 2, 120_000, "b", RetirementReason.Other),
        ]);

        var summary = RaceSummary.From(tape);

        Assert.Equal(["c", "b", "a"], summary.Classification.Select(c => c.DriverId));
        Assert.Null(summary.FastestLap);
    }

    [Fact]
    public void EmptyTapeGivesAnEmptySummary()
    {
        var summary = RaceSummary.From(RaceTape.Empty);

        Assert.Empty(summary.Classification);
        Assert.Null(summary.FastestLap);
        Assert.Empty(summary.Retirements);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(42UL)]
    [InlineData(2024UL)]
    public void SummaryMatchesTheBuildersKnownOutcome(ulong seed)
    {
        var race = FakeRaceTapeBuilder.Build(seed);

        var summary = RaceSummary.From(race.Tape);

        var finishers = summary.Classification.Where(c => c.Finished).Select(c => c.DriverId).ToList();
        Assert.Equal(race.FinishOrder, finishers);
        Assert.Equal(race.Retirements, summary.Retirements.ToList());
        Assert.Equal(20, summary.Classification.Length);
        var retiredLaps = summary.Classification.Where(c => !c.Finished).Select(c => c.LapsCompleted).ToList();
        Assert.Equal(retiredLaps.OrderByDescending(n => n), retiredLaps);

        var bestLap = race.Tape.Events.OfType<LapCompleted>().Min(l => l.LapTimeMs);
        Assert.Equal(bestLap, summary.FastestLap?.LapTimeMs);
    }
}
