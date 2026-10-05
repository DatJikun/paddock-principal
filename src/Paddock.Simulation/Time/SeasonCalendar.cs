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

    /// <summary>One championship session, not yet given an event id.</summary>
    public readonly record struct PlannedSession(GameDate Date, string TypeId, int Season, int Round, string LayoutId);

    /// <summary>The practice, qualifying and race days of one season. Empty when the season has no rounds. Does not touch a clock.</summary>
    public static IReadOnlyList<PlannedSession> Plan(
        int season,
        IReadOnlyList<TrackLayout> layouts,
        IReadOnlyList<RaceAssignment> assignments)
    {
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
            return [];
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

        var planned = new PlannedSession[rounds.Count * 3];
        var index = 0;
        for (var i = 0; i < rounds.Count; i++)
        {
            var round = rounds[i];
            var layout = RaceCalendar.LayoutFor(season, round, layouts, assignments);
            var race = windowStart.AddDays(lead + (i * step));
            var practice = race.AddDays(-PracticeDaysBeforeRace);
            var qualifying = race.AddDays(-QualifyingDaysBeforeRace);
            planned[index++] = new PlannedSession(practice, ScheduledEventType.Practice, season, round, layout.Id);
            planned[index++] = new PlannedSession(qualifying, ScheduledEventType.Qualifying, season, round, layout.Id);
            planned[index++] = new PlannedSession(race, ScheduledEventType.Race, season, round, layout.Id);
        }

        return planned;
    }

    public static WorldClockState Schedule(
        WorldClockState state,
        int season,
        IReadOnlyList<TrackLayout> layouts,
        IReadOnlyList<RaceAssignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(state.Queue);
        var planned = Plan(season, layouts, assignments);
        if (planned.Count == 0)
        {
            return state;
        }

        foreach (var session in planned)
        {
            if (session.Date < state.Date)
            {
                throw new InvalidOperationException(
                    $"Season {season.ToString(CultureInfo.InvariantCulture)} round {session.Round.ToString(CultureInfo.InvariantCulture)} {session.TypeId} falls on {session.Date}, before the clock date {state.Date}.");
            }
        }

        return Enqueue(state, planned);
    }

    /// <summary>Queues sessions that are still ahead of the clock. A session already in the past is left out, so a resumed season does not replay it.</summary>
    public static WorldClockState Enqueue(WorldClockState state, IReadOnlyList<PlannedSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(state.Queue);
        ArgumentNullException.ThrowIfNull(sessions);
        var ahead = new List<PlannedSession>(sessions.Count);
        foreach (var session in sessions)
        {
            if (session.Date >= state.Date)
            {
                ahead.Add(session);
            }
        }

        if (ahead.Count == 0)
        {
            return state;
        }

        var nextId = state.NextEventId;
        var drafts = new ScheduledEvent[ahead.Count];
        for (var i = 0; i < ahead.Count; i++)
        {
            var session = ahead[i];
            drafts[i] = Session(ref nextId, session.Date, session.TypeId, session.Season, session.Round, session.LayoutId);
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
