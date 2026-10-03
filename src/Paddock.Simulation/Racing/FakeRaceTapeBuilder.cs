using System.Collections.Immutable;
using Paddock.Domain.Racing;
using Paddock.Domain.Random;

namespace Paddock.Simulation.Racing;

/// <summary>What <see cref="FakeRaceTapeBuilder"/> knows about its own race, computed from raw pace data and not from the tape.</summary>
public sealed record FakeRace(
    RaceTape Tape,
    IReadOnlyList<string> FinishOrder,
    IReadOnlyList<RetirementRecord> Retirements);

/// <summary>
/// STUB, NOT THE RACE ENGINE. Emits a plausible 20-car, 10-lap race from a seed so the event stream,
/// the playback scheduler and the replay tool have something to chew on before the lap model exists.
/// Pace is a placeholder (a flat gap per grid slot plus noise from the <see cref="RngStreamName.LapNoise"/>
/// stream, one optional pit stop, rare retirements); it models no tyres, fuel or strategy and its numbers are
/// uncalibrated guesses. The real engine will replace it and only has to emit the same events.
/// </summary>
public static class FakeRaceTapeBuilder
{
    public const int DriverCount = 20;
    public const int LapCount = 10;

    private const int StubSeason = 1950;
    private const long BaseLapMs = 90_000;
    private const long PaceStepMs = 120;
    private const long NoiseMs = 1_500;
    private const int PitLap = 5;

    public static FakeRace Build(ulong seed)
    {
        var rng = RngStreams.Derive(seed, RngStreamName.LapNoise, StubSeason);
        var ids = Enumerable.Range(1, DriverCount).Select(n => $"driver-{n:00}").ToImmutableArray();

        // Per-driver pace data. Every draw is made unconditionally so the stream layout does not depend on outcomes.
        var cumulative = new long[DriverCount, LapCount + 1];
        var lapTimes = new long[DriverCount, LapCount + 1];
        var retireLap = new int[DriverCount];
        var reasons = new RetirementReason[DriverCount];
        var pitDuration = new long[DriverCount];
        for (var d = 0; d < DriverCount; d++)
        {
            var retires = rng.NextDouble() < 0.06;
            var lap = rng.NextInt(2, LapCount + 1);
            reasons[d] = (RetirementReason)rng.NextInt(0, 3);
            retireLap[d] = retires ? lap : 0;
            var pits = rng.NextDouble() < 0.3;
            var duration = 21_000 + rng.NextInt(0, 3_000);
            pitDuration[d] = pits ? duration : 0;
            for (var l = 1; l <= LapCount; l++)
            {
                var time = BaseLapMs + (d * PaceStepMs) + rng.NextInt(0, (int)NoiseMs);
                if (l == PitLap)
                {
                    time += pitDuration[d];
                }

                lapTimes[d, l] = time;
                cumulative[d, l] = cumulative[d, l - 1] + time;
            }
        }

        int LastLap(int d) => retireLap[d] == 0 ? LapCount : retireLap[d] - 1;

        // Arrival order per lap decides position and gap.
        var position = new int[DriverCount, LapCount + 1];
        var gap = new long[DriverCount, LapCount + 1];
        for (var l = 1; l <= LapCount; l++)
        {
            var arrivals = Enumerable.Range(0, DriverCount)
                .Where(d => LastLap(d) >= l)
                .OrderBy(d => cumulative[d, l])
                .ThenBy(d => d)
                .ToList();
            for (var rank = 0; rank < arrivals.Count; rank++)
            {
                position[arrivals[rank], l] = rank + 1;
                gap[arrivals[rank], l] = cumulative[arrivals[rank], l] - cumulative[arrivals[0], l];
            }
        }

        var items = new List<Item>();
        for (var d = 0; d < DriverCount; d++)
        {
            var previousPosition = d + 1;
            for (var l = 1; l <= LastLap(d); l++)
            {
                var t = cumulative[d, l];
                if (l == PitLap && pitDuration[d] > 0)
                {
                    items.Add(new Item(t, d, 0, ItemKind.PitIn, l));
                    items.Add(new Item(t, d, 1, ItemKind.PitOut, l));
                }

                items.Add(new Item(t, d, 2, ItemKind.Lap, l));
                if (position[d, l] != previousPosition)
                {
                    items.Add(new Item(t, d, 3, ItemKind.Position, l, previousPosition));
                }

                previousPosition = position[d, l];
                if (l == LapCount)
                {
                    items.Add(new Item(t, d, 5, ItemKind.Finish, l));
                }
            }

            if (retireLap[d] != 0)
            {
                var r = retireLap[d];
                var t = cumulative[d, r - 1] + (lapTimes[d, r] / 2);
                if (reasons[d] == RetirementReason.Accident)
                {
                    items.Add(new Item(t, d, 0, ItemKind.Incident, r));
                }

                items.Add(new Item(t, d, 1, ItemKind.Retire, r));
            }
        }

        items.Sort(static (a, b) =>
        {
            var c = a.Time.CompareTo(b.Time);
            if (c != 0)
            {
                return c;
            }

            c = a.Driver.CompareTo(b.Driver);
            return c != 0 ? c : a.Sub.CompareTo(b.Sub);
        });

        var builder = new RaceTape.Builder();
        var seq = 0;
        builder.Append(new RaceStarted(seq++, 0, 0, LapCount, ids));
        var best = long.MaxValue;
        long last = 0;
        foreach (var item in items)
        {
            var d = item.Driver;
            var id = ids[d];
            var l = item.Lap;
            last = item.Time;
            switch (item.Kind)
            {
                case ItemKind.PitIn:
                    builder.Append(new PitStop(seq++, l, item.Time, id, PitLanePhase.In, 0, "medium"));
                    break;
                case ItemKind.PitOut:
                    builder.Append(new PitStop(seq++, l, item.Time, id, PitLanePhase.Out, pitDuration[d], "hard"));
                    break;
                case ItemKind.Lap:
                    builder.Append(new LapCompleted(seq++, l, item.Time, id, lapTimes[d, l], position[d, l], gap[d, l]));
                    if (lapTimes[d, l] < best)
                    {
                        best = lapTimes[d, l];
                        builder.Append(new FastestLap(seq++, l, item.Time, id, best));
                    }

                    break;
                case ItemKind.Position:
                    builder.Append(new PositionChange(seq++, l, item.Time, id, item.Extra, position[d, l]));
                    break;
                case ItemKind.Finish:
                    builder.Append(new Finished(seq++, l, item.Time, id, position[d, l], LapCount, cumulative[d, l]));
                    break;
                case ItemKind.Incident:
                    builder.Append(new Incident(seq++, l, item.Time, [id], IncidentSeverity.Major));
                    break;
                case ItemKind.Retire:
                    builder.Append(new Retirement(seq++, l, item.Time, id, reasons[d]));
                    break;
            }
        }

        builder.Append(new RaceEnded(seq, LapCount, last));

        var finishers = Enumerable.Range(0, DriverCount)
            .Where(d => retireLap[d] == 0)
            .OrderBy(d => cumulative[d, LapCount])
            .ThenBy(d => d)
            .Select(d => ids[d])
            .ToList();
        var retired = Enumerable.Range(0, DriverCount)
            .Where(d => retireLap[d] != 0)
            .OrderBy(d => cumulative[d, retireLap[d] - 1] + (lapTimes[d, retireLap[d]] / 2))
            .ThenBy(d => d)
            .Select(d => new RetirementRecord(ids[d], retireLap[d], reasons[d]))
            .ToList();
        return new FakeRace(builder.Build(), finishers, retired);
    }

    private enum ItemKind
    {
        PitIn,
        PitOut,
        Lap,
        Position,
        Finish,
        Incident,
        Retire,
    }

    private readonly record struct Item(long Time, int Driver, int Sub, ItemKind Kind, int Lap, int Extra = 0);
}
