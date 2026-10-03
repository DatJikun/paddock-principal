using System.Globalization;

namespace Paddock.Simulation.Racing.Qualifying;

/// <summary>A driver's best lap in one session or segment.</summary>
public sealed record SegmentTime(string SegmentId, double BestLapSeconds);

/// <summary>A car that starts the race.</summary>
/// <param name="DriverId">The driver.</param>
/// <param name="ConstructorId">The constructor.</param>
/// <param name="GridPosition">1 for pole. 0 means the pit lane (the Jolpica convention for "pit lane start").</param>
/// <param name="ScoreSeconds">
/// The time the position is based on: the best lap, the sum of the counting laps (aggregate), or the time in the
/// last segment the car reached (knockout).
/// </param>
/// <param name="BestLapSeconds">The car's fastest lap of the whole qualifying (pre-qualifying not included).</param>
/// <param name="SegmentTimes">Best lap per session or segment the car set a time in, in session order.</param>
public sealed record QualifyingGridSlot(
    string DriverId,
    string ConstructorId,
    int GridPosition,
    double ScoreSeconds,
    double BestLapSeconds,
    IReadOnlyList<SegmentTime> SegmentTimes)
{
    public bool StartsFromPitLane => GridPosition == 0;
}

public enum NonQualifierReason
{
    /// <summary>The car could not run (reliability flag) and set no time.</summary>
    CarNotRunning,

    /// <summary>The car did not get through pre-qualifying.</summary>
    FailedPreQualifying,

    /// <summary>The car's time was outside the 107 percent cutoff.</summary>
    OutsideCutoff,

    /// <summary>The car was quick enough to be timed but the grid is full.</summary>
    GridLimit,
}

/// <summary>A car that does not start.</summary>
/// <param name="DriverId">The driver.</param>
/// <param name="ConstructorId">The constructor.</param>
/// <param name="Reason">Why the car did not qualify.</param>
/// <param name="BestLapSeconds">Its fastest lap, or null when it set none. For <see cref="NonQualifierReason.FailedPreQualifying"/> this is the pre-qualifying lap.</param>
/// <param name="SegmentTimes">Best lap per session or segment the car set a time in.</param>
public sealed record QualifyingNonQualifier(
    string DriverId,
    string ConstructorId,
    NonQualifierReason Reason,
    double? BestLapSeconds,
    IReadOnlyList<SegmentTime> SegmentTimes);

/// <summary>One car's pre-qualifying result.</summary>
public sealed record PreQualifyingEntry(string DriverId, double BestLapSeconds, bool Advanced);

/// <summary>
/// One line of the text summary as a translation key plus its arguments, so the text goes through the
/// translation catalog (strings/*.json) and not through formatted English here. <c>driver</c> arguments are
/// driver IDs: the caller swaps them for names it is allowed to show. <c>time</c> arguments are
/// <see cref="QualifyingResult.FormatLapTime"/> text.
/// </summary>
public sealed record QualifyingSummaryLine(string Key, IReadOnlyDictionary<string, object?> Args);

/// <summary>What qualifying produced.</summary>
/// <param name="Grid">Cars that start: grid positions 1, 2, ... in order, then the pit-lane starters (position 0).</param>
/// <param name="NonQualifiers">Cars that do not start.</param>
/// <param name="PreQualifying">Pre-qualifying order (fastest first), empty when none was held.</param>
/// <param name="SimulatedLaps">How many timed laps were simulated in total (never more than entrants times the rules' per-driver cap).</param>
/// <param name="Summary">Text-friendly summary lines.</param>
public sealed record QualifyingResult(
    IReadOnlyList<QualifyingGridSlot> Grid,
    IReadOnlyList<QualifyingNonQualifier> NonQualifiers,
    IReadOnlyList<PreQualifyingEntry> PreQualifying,
    int SimulatedLaps,
    IReadOnlyList<QualifyingSummaryLine> Summary)
{
    /// <summary>Driver IDs in start order: the grid by position, then the pit-lane starters.</summary>
    public IReadOnlyList<string> GridOrder => Grid.Select(s => s.DriverId).ToArray();

    /// <summary>Cars that start from the pit lane (grid position 0).</summary>
    public IReadOnlyList<QualifyingGridSlot> PitLaneStarters => Grid.Where(s => s.StartsFromPitLane).ToArray();

    /// <summary>The driver on pole, or null when nobody qualified.</summary>
    public QualifyingGridSlot? Pole => Grid.FirstOrDefault(s => s.GridPosition == 1);

    /// <summary>Lap time as <c>m:ss.mmm</c> (culture independent).</summary>
    public static string FormatLapTime(double seconds)
    {
        long ms = (long)Math.Round(seconds * 1000d, MidpointRounding.AwayFromZero);
        long minutes = ms / 60_000;
        double rest = (ms % 60_000) / 1000d;
        return string.Create(CultureInfo.InvariantCulture, $"{minutes}:{rest:00.000}");
    }

    internal static IReadOnlyList<QualifyingSummaryLine> BuildSummary(
        IReadOnlyList<QualifyingGridSlot> grid,
        IReadOnlyList<QualifyingNonQualifier> nonQualifiers)
    {
        var lines = new List<QualifyingSummaryLine>();
        if (grid.FirstOrDefault(s => s.GridPosition == 1) is { } pole)
        {
            lines.Add(Line("qualifying.summary.pole", ("driver", pole.DriverId), ("time", FormatLapTime(pole.ScoreSeconds))));
        }

        lines.Add(Line("qualifying.summary.gridSize", ("count", grid.Count)));
        foreach (var slot in grid.Where(s => s.StartsFromPitLane))
        {
            lines.Add(Line("qualifying.summary.pitLane", ("driver", slot.DriverId)));
        }

        foreach (var nq in nonQualifiers)
        {
            string key = nq.Reason switch
            {
                NonQualifierReason.CarNotRunning => "qualifying.summary.dnq.carNotRunning",
                NonQualifierReason.FailedPreQualifying => "qualifying.summary.dnq.failedPreQualifying",
                NonQualifierReason.OutsideCutoff => "qualifying.summary.dnq.outsideCutoff",
                _ => "qualifying.summary.dnq.gridLimit",
            };
            lines.Add(nq.BestLapSeconds is { } best
                ? Line(key, ("driver", nq.DriverId), ("time", FormatLapTime(best)))
                : Line(key, ("driver", nq.DriverId)));
        }

        return lines;
    }

    private static QualifyingSummaryLine Line(string key, params (string Name, object? Value)[] args) =>
        new(key, args.ToDictionary(a => a.Name, a => a.Value));
}
