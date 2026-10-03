using System.Globalization;
using Paddock.Domain.World;

namespace Paddock.Simulation.Racing.Weekend;

/// <summary>
/// How many laps a race is scheduled for, from the <c>race_distance_rule</c> regulation and the circuit length. The
/// time caps (<c>race_time_cap</c>) are not applied: the race always runs the scheduled laps (NOT DONE, noted in the PR).
/// </summary>
public static class RaceDistance
{
    public const string Dimension = "race_distance_rule";

    /// <summary>The scheduled distance in km the rule asks for on a circuit. Unknown values throw, so a new catalog value is not silently ignored.</summary>
    public static double DistanceKm(RuleSet rules, string circuitId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentException.ThrowIfNullOrWhiteSpace(circuitId);
        var rule = rules.Values.TryGetValue(Dimension, out var value) ? value : "unknown";
        return rule switch
        {
            "unknown" => WeekendConstants.UnknownDistanceKm,
            "min_300km_or_two_hours" => WeekendConstants.MinimumDistanceKm,
            "standard_305km" or "least_laps_over_305km" => WeekendConstants.StandardDistanceKm,
            "least_laps_over_305km_monaco_260" => string.Equals(circuitId, "monaco", StringComparison.Ordinal)
                ? WeekendConstants.MonacoDistanceKm
                : WeekendConstants.StandardDistanceKm,
            _ => throw new InvalidOperationException(
                string.Create(CultureInfo.InvariantCulture, $"Dimension '{Dimension}' has the value '{rule}', which is not understood.")),
        };
    }

    /// <summary>The least number of whole laps that covers the distance (at least 1).</summary>
    public static int LapsFor(RuleSet rules, TrackLayout track)
    {
        ArgumentNullException.ThrowIfNull(track);
        return Math.Max(1, (int)Math.Ceiling(DistanceKm(rules, track.CircuitId) / track.LengthKm));
    }
}
