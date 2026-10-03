using System.Globalization;
using Paddock.Domain.Racing;

namespace Paddock.SimRunner;

/// <summary>One text line per race event: <c>[m:ss.mmm] L02 Kind details</c>. Invariant culture.</summary>
public static class RaceEventText
{
    public static string Format(RaceEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var head = string.Create(CultureInfo.InvariantCulture, $"[{Clock(e.RaceTime)}] L{e.Lap:00} {e.Kind}");
        var details = e switch
        {
            RaceStarted x => $"{x.DriverIds.Length} cars, {x.TotalLaps} laps",
            LapCompleted x => $"{x.DriverId} lap {Clock(x.LapTimeMs)} P{x.Position} +{x.GapToLeaderMs / 1000.0:0.000}s",
            PitStop x => $"{x.DriverId} {x.Phase} {x.DurationMs / 1000.0:0.0}s {x.Tyres}",
            PositionChange x => $"{x.DriverId} P{x.FromPosition} -> P{x.ToPosition}",
            Incident x => $"{string.Join(",", x.InvolvedIds)} {x.Severity}",
            Retirement x => $"{x.DriverId} {x.Reason}",
            WeatherChange x => x.Condition,
            SafetyCar x => $"{x.Phase}{(x.IsVirtual ? " (virtual)" : string.Empty)}",
            FastestLap x => $"{x.DriverId} {Clock(x.LapTimeMs)}",
            Finished x => $"{x.DriverId} P{x.Position} {x.LapsCompleted} laps {Clock(x.TotalTimeMs)}",
            _ => string.Empty,
        };
        return details.Length == 0 ? head : string.Create(CultureInfo.InvariantCulture, $"{head} {details}");
    }

    private static string Clock(long ms)
    {
        var minutes = ms / 60_000;
        var rest = ms % 60_000;
        return string.Create(CultureInfo.InvariantCulture, $"{minutes}:{rest / 1000:00}.{rest % 1000:000}");
    }
}
