using System.Globalization;

namespace Paddock.Domain.World;

/// <summary>
/// Looks up which layout was used for a championship round.
/// </summary>
public static class RaceCalendar
{
    public static TrackLayout LayoutFor(
        int season,
        int round,
        IReadOnlyList<TrackLayout> layouts,
        IReadOnlyList<RaceAssignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(layouts);
        ArgumentNullException.ThrowIfNull(assignments);

        var byId = new Dictionary<string, TrackLayout>(layouts.Count, StringComparer.Ordinal);
        foreach (var layout in layouts)
        {
            if (!byId.TryAdd(layout.Id, layout))
            {
                throw new InvalidOperationException($"Layout id '{layout.Id}' is defined more than once.");
            }
        }

        TrackLayout? found = null;
        var matches = 0;
        foreach (var assignment in assignments)
        {
            if (assignment.Season != season || assignment.Round != round)
            {
                continue;
            }

            matches++;
            if (!byId.TryGetValue(assignment.LayoutId, out var layout))
            {
                throw new InvalidOperationException(
                    $"Race {season.ToString(CultureInfo.InvariantCulture)} round {round.ToString(CultureInfo.InvariantCulture)} points at missing layout '{assignment.LayoutId}'.");
            }

            found = layout;
        }

        if (matches == 0 || found is null)
        {
            throw new InvalidOperationException(
                $"No layout is assigned to season {season.ToString(CultureInfo.InvariantCulture)} round {round.ToString(CultureInfo.InvariantCulture)}.");
        }

        if (matches > 1)
        {
            throw new InvalidOperationException(
                $"Season {season.ToString(CultureInfo.InvariantCulture)} round {round.ToString(CultureInfo.InvariantCulture)} has {matches.ToString(CultureInfo.InvariantCulture)} layout assignments.");
        }

        return found;
    }
}
