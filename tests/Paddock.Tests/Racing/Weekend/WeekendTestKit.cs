using System.Collections.Immutable;
using Paddock.Data.Authored;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Weekend;
using Paddock.SimRunner;
using Paddock.Tests.Racing.Points;

namespace Paddock.Tests.Racing.Weekend;

/// <summary>Shared helpers: the real authored data, the synthetic fixture field, and seeded runs. Every number is ESTIMATE.</summary>
internal static class WeekendTestKit
{
    public const ulong Seed = 20_261_003UL;

    /// <summary>A cheap strategist search so hundreds of seeded races stay fast: at most two stops, no shifted variants.</summary>
    public static readonly StrategistOptions CheapSearch = new(MaxStops: 1, StopShiftVariants: 0);

    public static AuthoredData Data => PointsTestKit.Real;

    public static RaceWeekendInput Input(
        int season,
        int round = 1,
        ulong seed = Seed,
        ImmutableArray<RaceEntry>? entries = null,
        StrategistOptions? options = null)
    {
        var input = RaceCommand.BuildInput(Data, season, round, seed, entries ?? SyntheticField.For(season));
        return input with { StrategistOptions = options ?? CheapSearch };
    }

    public static RaceWeekendResult Run(RaceWeekendInput input) =>
        RaceWeekend.Run(input, Paddock.Domain.Spy.NullSink.Instance);

    /// <summary>Runs <paramref name="count"/> races of an era in parallel: seed i, rounds 1 to 4 in turn.</summary>
    public static RaceWeekendResult[] Races(int season, int count) =>
        Enumerable.Range(0, count)
            .AsParallel()
            .AsOrdered()
            .Select(i => Run(Input(season, round: (i % 4) + 1, seed: 1_000UL + (ulong)i)))
            .ToArray();
}
