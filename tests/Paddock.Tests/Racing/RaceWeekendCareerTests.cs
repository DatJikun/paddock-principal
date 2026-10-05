using Paddock.Application.Career;
using Paddock.Application.Development;
using Paddock.Application.Racing;
using Paddock.Domain.Career;
using Paddock.Domain.Finance;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Career;
using Paddock.Simulation.Racing.Points;
using Paddock.Simulation.Racing.Tyres;
using Paddock.SimRunner;
using Paddock.Tests.Career;

namespace Paddock.Tests.Racing;

/// <summary>
/// T47: one championship round inside the career loop. The grid, the money and the table come from the race, not from the
/// January placeholder (PP-055).
/// </summary>
public class RaceWeekendCareerTests
{
    private const ulong Seed = 7;

    [Fact]
    public void TheFirst1955RacePostsResultsAndTheSameSeedMatches()
    {
        var first = RunToFirstRace(Seed);
        var second = RunToFirstRace(Seed);

        Assert.Equal(first.Hash, second.Hash);
        Assert.Equal(1, first.Completed);
        Assert.Equal(1955, first.Season);
        Assert.True(first.StartMoney > 0, "a race posts start money");
        Assert.True(first.Drivers > 1);
        Assert.NotNull(first.NextRace);
        Assert.True(first.NextRace > new GameDate(1955, 3, 4));
        Assert.NotEqual(first.Hash, RunToFirstRace(Seed + 1).Hash);
    }

    [Fact]
    public void FuelMappingFollowsTheFormula()
    {
        Assert.Equal(FuelEngineType.NaturallyAspirated, RaceFieldBuilder.FuelOf("2500na_or_750_supercharged", null));
        Assert.Equal(FuelEngineType.Turbo, RaceFieldBuilder.FuelOf("1500_forced_only", null));
        Assert.Equal(FuelEngineType.Hybrid, RaceFieldBuilder.FuelOf("1600_v6_turbo_hybrid", null));
        Assert.Equal(FuelEngineType.Turbo, RaceFieldBuilder.FuelOf("3000na_or_1500_forced", "Renault turbo"));
    }

    [Fact]
    public void WatchAtTenTimesUsesThePlaybackSchedulerAndLessClockThanRealtime()
    {
        var tape = RaceTape.From(
        [
            new RaceStarted(0, 0, 0, 2, ["a"]),
            new LapCompleted(1, 1, 10_000, "a", 10_000, 1, 0),
            new RaceEnded(2, 1, 20_000),
        ]);
        var seen = new List<int>();
        RacePlayback.Play(tape, 10, raceEvent => seen.Add(raceEvent.Seq));

        Assert.Equal([0, 1, 2], seen);
        Assert.True(RacePlayback.ClockMs(tape, 10) < RacePlayback.ClockMs(tape, 1));
    }

    [Fact]
    public void ARestoredTableMatchesTheTableThatWasSnapshotted()
    {
        var rules = PointsRules.For(CareerKit.Data.RuleSetFor(1955));
        var start = Standings.Start(rules, 7);
        var round = new RaceClassification(
            [new ClassifiedCar(1, true, "alfa", ["d1"], 8m, 8m), new ClassifiedCar(2, true, "ferrari", ["d2"], 6m, 6m)],
            [new DriverScore("d1", 8m, 1), new DriverScore("d2", 6m, 2)],
            [new ConstructorScore("alfa", 8m, [1]), new ConstructorScore("ferrari", 6m, [2])]);
        var raced = start.Apply(round);
        var (drivers, constructors) = raced.Snapshot();
        var restored = Standings.Restore(rules, raced.TotalRounds, raced.RoundsCompleted, drivers, constructors);

        Assert.Equal(raced.Drivers().Select(row => row.Id + ":" + row.CountedPoints).ToArray(), restored.Drivers().Select(row => row.Id + ":" + row.CountedPoints).ToArray());
        var restoredSnapshot = restored.Snapshot();
        Assert.Equal(constructors.Select(row => row.Id).ToArray(), restoredSnapshot.Constructors.Select(row => row.Id).ToArray());
    }

    private static RaceProbe RunToFirstRace(ulong seed)
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1955, seed);
        var result = CareerHost.RunUntil(session, new GameDate(1955, 3, 4), null, CareerKit.Options);
        var championship = session.World.Section<ChampionshipSection>(ChampionshipSection.SectionName);
        var finance = session.World.Section<FinanceSection>(FinanceSection.SectionName)!;
        var start = 0;
        foreach (var team in CareerTeams.Active(session.World, session.Date))
        {
            foreach (var entry in finance.EntriesOf(team.Id))
            {
                if (entry.Category == LedgerCategories.StartMoney)
                {
                    start++;
                }
            }
        }

        var next = result.Modules.Require<INextRaceSource>().NextRaceOnOrAfter(OrganizationId.Real("mercedes"), session.Date);
        return new RaceProbe(
            session.World.StateHash(),
            championship?.Season ?? 0,
            championship?.RoundsCompleted ?? 0,
            championship?.Drivers.Count ?? 0,
            start,
            next);
    }

    private sealed record RaceProbe(string Hash, int Season, int Completed, int Drivers, int StartMoney, GameDate? NextRace);
}
