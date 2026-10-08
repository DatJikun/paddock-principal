using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Simulation.Regulation;

/// <summary>
/// The race calendar as something the teams vote on (#275, owner decision 2). The authored map says which layout each round
/// of a season used; a vote adds a standing policy on top of it, one entry per circuit, and the calendar of every later season is
/// laid out from the authored map with that policy applied. A vote taken in season N is stored for N + 1: the calendar of N + 1 is
/// laid out on 31 December of N, after the votes are resolved, and the calendar already laid out for N is never touched.
/// <para>
/// A circuit's value is one of <see cref="Kept"/> (as authored), <see cref="Dropped"/> (never on the calendar),
/// <c>layout:&lt;id&gt;</c> (on the calendar with that layout instead) or <c>added:&lt;id&gt;</c> (on the calendar with that layout even
/// if the authored season has no race there). The dimension id of a circuit is <see cref="DimensionOf"/>.
/// </para>
/// </summary>
public static class CalendarPolicy
{
    public const string Prefix = "calendar.circuit.";

    public const string Kept = "kept";

    public const string Dropped = "dropped";

    public const string LayoutPrefix = "layout:";

    public const string AddedPrefix = "added:";

    /// <summary>One round of a laid-out season: the old round number (0 for an added race), its layout and its real date when it has one.</summary>
    public sealed record Round(int OldRound, TrackLayout Layout, GameDate? Date);

    /// <summary>One calendar dimension a vote can change in a season: the circuit, what it is now and what it could become.</summary>
    public sealed record Option(
        string DimensionId,
        string CircuitId,
        string Country,
        bool Authored,
        string Current,
        IReadOnlyList<string> Alternatives);

    public static bool IsCalendarDimension(string dimensionId) =>
        dimensionId.StartsWith(Prefix, StringComparison.Ordinal);

    public static string DimensionOf(string circuitId) => Prefix + circuitId;

    public static string CircuitOf(string dimensionId) => dimensionId[Prefix.Length..];

    /// <summary>The traits (ESTIMATES) of a calendar value; <see cref="Kept"/> is the reference with none.</summary>
    public static ValueTraits TraitsOf(string value)
    {
        if (value == Dropped)
        {
            return new ValueTraits(Show: -0.2, Cheap: 0.5);
        }

        if (value.StartsWith(AddedPrefix, StringComparison.Ordinal))
        {
            return new ValueTraits(Show: 0.3, Cheap: -0.5);
        }

        return value.StartsWith(LayoutPrefix, StringComparison.Ordinal)
            ? new ValueTraits(Modern: 0.2, Safety: 0.2)
            : default;
    }

    /// <summary>Whether the circuit is on the calendar under <paramref name="value"/>. <paramref name="authored"/> says whether the authored season has a race there.</summary>
    public static bool IsOnCalendar(string value, bool authored) =>
        value != Dropped && (value != Kept || authored);

    /// <summary>True when the value is one the policy can hold: kept, dropped, or a layout/added entry that names a layout.</summary>
    public static bool IsKnownValue(string value) =>
        value is Kept or Dropped
        || (value.StartsWith(LayoutPrefix, StringComparison.Ordinal) && value.Length > LayoutPrefix.Length)
        || (value.StartsWith(AddedPrefix, StringComparison.Ordinal) && value.Length > AddedPrefix.Length);

    /// <summary>The rounds of <paramref name="season"/> with the policy applied, renumbered from 1. Without a policy they are the authored rounds.</summary>
    public static IReadOnlyList<Round> RoundsFor(
        int season,
        IReadOnlyList<TrackLayout> layouts,
        IReadOnlyList<RaceAssignment> assignments,
        RaceDateBook? dates,
        IReadOnlyDictionary<string, string> policy)
    {
        ArgumentNullException.ThrowIfNull(layouts);
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(policy);
        var byId = LayoutsById(layouts);
        var rows = new List<Round>();
        foreach (var assignment in assignments.Where(a => a.Season == season).OrderBy(a => a.Round))
        {
            var date = dates is not null && dates.TryGet(season, assignment.Round, out var found) ? found : (GameDate?)null;
            rows.Add(new Round(assignment.Round, byId[assignment.LayoutId], date));
        }

        foreach (var (dimension, value) in policy.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!IsCalendarDimension(dimension))
            {
                continue;
            }

            var circuit = CircuitOf(dimension);
            if (value == Dropped)
            {
                rows.RemoveAll(row => row.Layout.CircuitId == circuit);
            }
            else if (value.StartsWith(LayoutPrefix, StringComparison.Ordinal))
            {
                var index = rows.FindIndex(row => row.Layout.CircuitId == circuit);
                if (index >= 0 && byId.TryGetValue(value[LayoutPrefix.Length..], out var layout))
                {
                    rows[index] = rows[index] with { Layout = layout };
                }
            }
            else if (value.StartsWith(AddedPrefix, StringComparison.Ordinal))
            {
                if (!rows.Exists(row => row.Layout.CircuitId == circuit) && byId.TryGetValue(value[AddedPrefix.Length..], out var layout))
                {
                    rows.Add(new Round(0, layout, null));
                }
            }
        }

        return rows;
    }

    /// <summary>
    /// The assignments and real dates to plan <paramref name="season"/> from, with the policy applied. Rounds are renumbered from 1;
    /// a dropped or re-laid-out race keeps its real date, and a season with an added race has none for every round, so it falls
    /// back to even spacing as a whole. An empty policy returns the inputs untouched.
    /// </summary>
    public static (IReadOnlyList<RaceAssignment> Assignments, RaceDateBook? Dates) Resolve(
        int season,
        IReadOnlyList<TrackLayout> layouts,
        IReadOnlyList<RaceAssignment> assignments,
        RaceDateBook? dates,
        IReadOnlyDictionary<string, string> policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.Count == 0)
        {
            return (assignments, dates);
        }

        var rows = RoundsFor(season, layouts, assignments, dates, policy);
        var list = new List<RaceAssignment>(assignments.Count + 4);
        foreach (var assignment in assignments)
        {
            if (assignment.Season != season)
            {
                list.Add(assignment);
            }
        }

        var dated = new List<(int Season, int Round, GameDate Date)>(rows.Count);
        var allDated = rows.Count > 0;
        for (var i = 0; i < rows.Count; i++)
        {
            list.Add(new RaceAssignment(season, i + 1, rows[i].Layout.Id));
            if (rows[i].Date is { } date)
            {
                dated.Add((season, i + 1, date));
            }
            else
            {
                allDated = false;
            }
        }

        return (list, allDated ? RaceDateBook.Create(dated) : RaceDateBook.Empty);
    }

    /// <summary>
    /// The calendar dimensions a vote may change for <paramref name="season"/>, given the policy already in force. A circuit on the
    /// authored calendar can be dropped (while enough rounds remain), given another layout, or restored; a circuit that raced in the
    /// seasons around it but is not on the authored calendar can be added. Ordered by circuit id.
    /// </summary>
    public static IReadOnlyList<Option> Options(
        int season,
        IReadOnlyList<TrackLayout> layouts,
        IReadOnlyList<RaceAssignment> assignments,
        IReadOnlyDictionary<string, string> policy)
    {
        ArgumentNullException.ThrowIfNull(layouts);
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(policy);
        var byId = LayoutsById(layouts);
        var authored = RoundsFor(season, layouts, assignments, null, new Dictionary<string, string>());
        if (authored.Count == 0)
        {
            return [];
        }

        var now = RoundsFor(season, layouts, assignments, null, policy);
        var window = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var assignment in assignments)
        {
            if (Math.Abs(assignment.Season - season) > RegulationEstimates.CalendarWindowSeasons
                || !byId.TryGetValue(assignment.LayoutId, out var layout))
            {
                continue;
            }

            if (!window.TryGetValue(layout.CircuitId, out var set))
            {
                set = new SortedSet<string>(StringComparer.Ordinal);
                window.Add(layout.CircuitId, set);
            }

            set.Add(layout.Id);
        }

        var authoredCircuits = new HashSet<string>(authored.Select(row => row.Layout.CircuitId), StringComparer.Ordinal);
        var options = new List<Option>();
        foreach (var (circuit, layoutIds) in window)
        {
            var current = policy.TryGetValue(DimensionOf(circuit), out var held) ? held : Kept;
            var isAuthored = authoredCircuits.Contains(circuit);
            var onNow = now.FirstOrDefault(row => row.Layout.CircuitId == circuit);
            var alternatives = new List<string>();
            if (current != Kept)
            {
                alternatives.Add(Kept);
            }

            if (isAuthored)
            {
                if (current != Dropped && onNow is not null && now.Count > RegulationEstimates.MinimumRounds)
                {
                    alternatives.Add(Dropped);
                }

                foreach (var id in layoutIds)
                {
                    var value = LayoutPrefix + id;
                    if (value != current && (onNow is null || onNow.Layout.Id != id))
                    {
                        alternatives.Add(value);
                    }
                }
            }
            else
            {
                foreach (var id in layoutIds)
                {
                    var value = AddedPrefix + id;
                    if (value != current)
                    {
                        alternatives.Add(value);
                    }
                }
            }

            if (alternatives.Count == 0)
            {
                continue;
            }

            var country = (onNow?.Layout ?? byId[layoutIds.First()]).Country;
            options.Add(new Option(DimensionOf(circuit), circuit, country, isAuthored, current, alternatives));
        }

        return options;
    }

    /// <summary>
    /// How a change of one circuit moves a team's home race: +1 when a race in the team's own country appears, -1 when it goes,
    /// 0 otherwise or when the team or circuit has no country.
    /// </summary>
    public static double HomeEffect(Option option, string proposed, string? teamCountry)
    {
        if (string.IsNullOrEmpty(teamCountry) || option.Country.Length == 0
            || !string.Equals(option.Country, teamCountry, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var before = IsOnCalendar(option.Current, option.Authored) ? 1 : 0;
        var after = IsOnCalendar(proposed, option.Authored) ? 1 : 0;
        return after - before;
    }

    /// <summary>True when applying <paramref name="value"/> to the dimension leaves the season at least <see cref="RegulationEstimates.MinimumRounds"/> rounds.</summary>
    public static bool KeepsEnoughRounds(
        int season,
        IReadOnlyList<TrackLayout> layouts,
        IReadOnlyList<RaceAssignment> assignments,
        IReadOnlyDictionary<string, string> policy,
        string dimensionId,
        string value)
    {
        var changed = new Dictionary<string, string>(policy, StringComparer.Ordinal);
        if (value == Kept)
        {
            changed.Remove(dimensionId);
        }
        else
        {
            changed[dimensionId] = value;
        }

        return RoundsFor(season, layouts, assignments, null, changed).Count >= RegulationEstimates.MinimumRounds;
    }

    private static Dictionary<string, TrackLayout> LayoutsById(IReadOnlyList<TrackLayout> layouts)
    {
        var byId = new Dictionary<string, TrackLayout>(layouts.Count, StringComparer.Ordinal);
        foreach (var layout in layouts)
        {
            byId[layout.Id] = layout;
        }

        return byId;
    }
}
