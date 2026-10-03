using System.Globalization;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Simulation.Time;

/// <summary>
/// Placeholder championship weekends built from <see cref="RaceCalendar"/>.
/// The authored layout map has no race dates, so each round becomes three days and no race is simulated.
/// The window is <see cref="WindowStartMonth"/>/<see cref="WindowStartDay"/> through
/// <see cref="WindowEndMonth"/>/<see cref="WindowEndDay"/> of the season year.
/// Round 1's earliest session falls on the window start. Later rounds are spaced evenly.
/// Weekends never overlap: the gap between race days is at least one day more than the longest session lead.
/// </summary>
public static class SeasonCalendar
{
    public const int WindowStartMonth = 3;

    public const int WindowStartDay = 1;

    public const int WindowEndMonth = 11;

    public const int WindowEndDay = 30;

    public const int PracticeDaysBeforeRace = 2;

    public const int QualifyingDaysBeforeRace = 1;

    public static WorldClockState Schedule(
        WorldClockState state,
        int season,
        IReadOnlyList<TrackLayout> layouts,
        IReadOnlyList<RaceAssignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(state.Queue);
        ArgumentNullException.ThrowIfNull(layouts);
        ArgumentNullException.ThrowIfNull(assignments);

        var rounds = new List<int>();
        foreach (var assignment in assignments)
        {
            if (assignment.Season == season)
            {
                rounds.Add(assignment.Round);
            }
        }

        rounds.Sort();
        if (rounds.Count == 0)
        {
            return state;
        }

        var windowStart = new GameDate(season, WindowStartMonth, WindowStartDay);
        var windowEnd = new GameDate(season, WindowEndMonth, WindowEndDay);
        var lead = Math.Max(PracticeDaysBeforeRace, QualifyingDaysBeforeRace);
        var span = windowStart.DaysUntil(windowEnd);
        var step = 0;
        if (rounds.Count > 1)
        {
            var available = span - lead;
            var minimumGap = lead + 1;
            step = available / (rounds.Count - 1);
            if (available < 0 || step < minimumGap)
            {
                throw new InvalidOperationException(
                    $"Season {season.ToString(CultureInfo.InvariantCulture)} has {rounds.Count.ToString(CultureInfo.InvariantCulture)} rounds, which do not fit in the placeholder window without overlapping weekends.");
            }
        }

        var nextId = state.NextEventId;
        var drafts = new ScheduledEvent[rounds.Count * 3];
        var index = 0;
        for (var i = 0; i < rounds.Count; i++)
        {
            var round = rounds[i];
            var layout = RaceCalendar.LayoutFor(season, round, layouts, assignments);
            var race = windowStart.AddDays(lead + (i * step));
            var practice = race.AddDays(-PracticeDaysBeforeRace);
            var qualifying = race.AddDays(-QualifyingDaysBeforeRace);
            if (practice < state.Date)
            {
                throw new InvalidOperationException(
                    $"Season {season.ToString(CultureInfo.InvariantCulture)} round {round.ToString(CultureInfo.InvariantCulture)} practice falls on {practice}, before the clock date {state.Date}.");
            }

            drafts[index++] = Session(ref nextId, practice, ScheduledEventType.Practice, season, round, layout.Id);
            drafts[index++] = Session(ref nextId, qualifying, ScheduledEventType.Qualifying, season, round, layout.Id);
            drafts[index++] = Session(ref nextId, race, ScheduledEventType.Race, season, round, layout.Id);
        }

        return state with
        {
            Queue = state.Queue.EnqueueRange(drafts),
            NextEventId = nextId,
        };
    }

    private static ScheduledEvent Session(
        ref ulong nextId,
        GameDate date,
        string typeId,
        int season,
        int round,
        string layoutId)
    {
        var id = EventId.FromCounter(nextId);
        nextId = checked(nextId + 1);
        return new ScheduledEvent(id, date, typeId, new RaceSessionPayload(season, round, layoutId));
    }
}
