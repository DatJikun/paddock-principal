using Paddock.Domain.Time;

namespace Paddock.Domain.Objectives;

public enum ForecastKind
{
    /// <summary>The fact is unknown, so nothing can be said.</summary>
    Unknown,

    /// <summary>The straight line from the baseline through today's value meets the target at the deadline.</summary>
    OnTrack,

    /// <summary>The straight line misses the target at the deadline.</summary>
    OffTrack,

    /// <summary>The objective is a state to reach (a lineup), not a number, so there is no line to extend.</summary>
    NotProjectable,
}

/// <summary>
/// A projection of one open objective to its deadline.
/// <paramref name="Projected"/> is the number at the deadline when the trend continues, for a numeric objective.
/// </summary>
public readonly record struct ForecastResult(ForecastKind Kind, decimal? Projected);

/// <summary>
/// The FORECAST of an objective (DESIGN §3.2): a straight line from the baseline on the day it was granted through
/// today's value, extended to the deadline. ESTIMATE: linear is the simplest honest guess and ignores the
/// calendar (a season is not an even spread of races); it is a pure function, no RNG and no state (INV-005).
/// </summary>
public static class ObjectiveForecast
{
    /// <summary>Digits kept in the projected number. ESTIMATE: only for display stability.</summary>
    public const int ProjectedDecimals = 2;

    /// <param name="objective">The objective to project.</param>
    /// <param name="current">Today's value of the predicate's number, or null when unknown. Ignored for a non-numeric predicate.</param>
    /// <param name="today">The day the forecast is made.</param>
    public static ForecastResult Project(Objective objective, decimal? current, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(objective);
        if (objective.Predicate is not NumericPredicate predicate)
        {
            return new ForecastResult(ForecastKind.NotProjectable, null);
        }

        if (current is not decimal now || objective.Baseline is not decimal baseline)
        {
            return new ForecastResult(ForecastKind.Unknown, null);
        }

        var total = objective.Created.DaysUntil(objective.Deadline);
        var elapsed = Math.Clamp(objective.Created.DaysUntil(today), 0, total);
        var projected = elapsed == 0 || total == 0
            ? now
            : baseline + ((now - baseline) * total / elapsed);
        if (predicate.LowerIsBetter)
        {
            projected = Math.Max(projected, 1m);
        }

        projected = Math.Round(projected, ProjectedDecimals, MidpointRounding.ToEven);
        return new ForecastResult(predicate.IsMetBy(projected) ? ForecastKind.OnTrack : ForecastKind.OffTrack, projected);
    }
}
