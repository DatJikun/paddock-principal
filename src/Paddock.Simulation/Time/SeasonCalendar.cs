using System.Globalization;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Simulation.Time;

/// <summary>
/// Championship weekends built from <see cref="RaceCalendar"/>. Race days are the real ones when the host gives a
/// <see cref="RaceDateBook"/> that covers the season (#229); otherwise the placeholder spacing below applies.
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
        IReadOnlyList<RaceAssignment> assignments,
        RaceDateBook? dates = null)
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

        var lead = Math.Max(PracticeDaysBeforeRace, QualifyingDaysBeforeRace);
        var races = RealRaceDays(season, rounds, dates, lead) ?? EvenRaceDays(season, rounds.Count, lead);

        var planned = new PlannedSession[rounds.Count * 3];
        var index = 0;
        for (var i = 0; i < rounds.Count; i++)
        {
            var round = rounds[i];
            var layout = RaceCalendar.LayoutFor(season, round, layouts, assignments);
            var race = races[i];
            var practice = race.AddDays(-PracticeDaysBeforeRace);
            var qualifying = race.AddDays(-QualifyingDaysBeforeRace);
            planned[index++] = new PlannedSession(practice, ScheduledEventType.Practice, season, round, layout.Id);
            planned[index++] = new PlannedSession(qualifying, ScheduledEventType.Qualifying, season, round, layout.Id);
            planned[index++] = new PlannedSession(race, ScheduledEventType.Race, season, round, layout.Id);
        }

        return planned;
    }

    /// <summary>
    /// The real race days of <paramref name="rounds"/> when <paramref name="dates"/> has every one of them and a weekend fits
    /// before each race inside the season without touching the previous weekend. Otherwise null, and the even spacing applies
    /// (a season that cannot keep its real dates falls back as a whole, so its rounds never mix two kinds of dates).
    /// </summary>
    private static GameDate[]? RealRaceDays(int season, List<int> rounds, RaceDateBook? dates, int lead)
    {
        if (dates is null)
        {
            return null;
        }

        var days = new GameDate[rounds.Count];
        for (var i = 0; i < days.Length; i++)
        {
            if (!dates.TryGet(season, rounds[i], out var date) || date.Year != season)
            {
                return null;
            }

            days[i] = date;
            if (i == 0 && date.AddDays(-lead).Year != season)
            {
                return null;
            }

            if (i > 0 && days[i - 1].DaysUntil(date) < lead + 1)
            {
                return null;
            }
        }

        return days;
    }

    private static GameDate[] EvenRaceDays(int season, int count, int lead)
    {
        var windowStart = new GameDate(season, WindowStartMonth, WindowStartDay);
        var windowEnd = new GameDate(season, WindowEndMonth, WindowEndDay);
        var span = windowStart.DaysUntil(windowEnd);
        var step = 0;
        if (count > 1)
        {
            var available = span - lead;
            var minimumGap = lead + 1;
            step = available / (count - 1);
            if (available < 0 || step < minimumGap)
            {
                throw new InvalidOperationException(
                    $"Season {season.ToString(CultureInfo.InvariantCulture)} has {count.ToString(CultureInfo.InvariantCulture)} rounds, which do not fit in the placeholder window without overlapping weekends.");
            }
        }

        var days = new GameDate[count];
        for (var i = 0; i < count; i++)
        {
            days[i] = windowStart.AddDays(lead + (i * step));
        }

        return days;
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
