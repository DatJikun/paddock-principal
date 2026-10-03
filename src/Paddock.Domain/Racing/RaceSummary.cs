using System.Collections.Immutable;

namespace Paddock.Domain.Racing;

public sealed record ClassificationEntry(int Position, string DriverId, int LapsCompleted, bool Finished, RetirementReason? RetiredReason);

public sealed record FastestLapResult(string DriverId, long LapTimeMs, int Lap);

public sealed record RetirementRecord(string DriverId, int Lap, RetirementReason Reason);

/// <summary>
/// Result of a race derived only from its events. Classification: finishers in the order of
/// <see cref="Finished.Position"/>, then retirees by laps completed (descending), then by retirement
/// order. Laps completed = number of <see cref="LapCompleted"/> events of the driver. The fastest lap is
/// the last <see cref="FastestLap"/> event (each one improves on the previous).
/// </summary>
public sealed record RaceSummary(
    ImmutableArray<ClassificationEntry> Classification,
    FastestLapResult? FastestLap,
    ImmutableArray<RetirementRecord> Retirements)
{
    public static RaceSummary From(RaceTape tape)
    {
        ArgumentNullException.ThrowIfNull(tape);

        var laps = new Dictionary<string, int>(StringComparer.Ordinal);
        var finished = new List<Finished>();
        var retired = new List<Retirement>();
        FastestLap? fastest = null;
        foreach (var e in tape.Events)
        {
            switch (e)
            {
                case LapCompleted lap:
                    laps[lap.DriverId] = laps.GetValueOrDefault(lap.DriverId) + 1;
                    break;
                case Finished f:
                    finished.Add(f);
                    break;
                case Retirement r:
                    retired.Add(r);
                    break;
                case FastestLap fl:
                    fastest = fl;
                    break;
            }
        }

        var classification = ImmutableArray.CreateBuilder<ClassificationEntry>();
        foreach (var f in finished.OrderBy(x => x.Position))
        {
            classification.Add(new ClassificationEntry(classification.Count + 1, f.DriverId, laps.GetValueOrDefault(f.DriverId), true, null));
        }

        var retiredOrder = retired
            .Select((r, index) => (Event: r, Index: index))
            .OrderByDescending(x => laps.GetValueOrDefault(x.Event.DriverId))
            .ThenBy(x => x.Index);
        foreach (var (retirement, _) in retiredOrder)
        {
            classification.Add(new ClassificationEntry(
                classification.Count + 1,
                retirement.DriverId,
                laps.GetValueOrDefault(retirement.DriverId),
                false,
                retirement.Reason));
        }

        return new RaceSummary(
            classification.ToImmutable(),
            fastest is null ? null : new FastestLapResult(fastest.DriverId, fastest.LapTimeMs, fastest.Lap),
            [.. retired.Select(r => new RetirementRecord(r.DriverId, r.Lap, r.Reason))]);
    }
}
