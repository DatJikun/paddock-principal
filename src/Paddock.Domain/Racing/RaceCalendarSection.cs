using System.Globalization;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Racing;

/// <summary>One practice, qualifying or race day of a stored season plan. <see cref="TypeId"/> is the scheduled event type.</summary>
public sealed record CalendarSession
{
    public CalendarSession(int season, int round, string typeId, GameDate date, string layoutId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(season, 1950);
        ArgumentOutOfRangeException.ThrowIfLessThan(round, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutId);
        Season = season;
        Round = round;
        TypeId = typeId;
        Date = date;
        LayoutId = layoutId;
    }

    public int Season { get; }

    public int Round { get; }

    public string TypeId { get; }

    public GameDate Date { get; }

    public string LayoutId { get; }
}

/// <summary>
/// The <c>race-calendar</c> world section (#229): the dates of each season's weekends as they were planned when the season was
/// first scheduled (real dates when the host had them, even spacing otherwise). Once stored a season's plan never changes, so
/// a finished round keeps its date whatever data the host has later. A season with no stored plan (a save from before this
/// section) is read with the even spacing it was scheduled with.
/// <para>
/// Canonical text (schema 1):
/// <code>
/// sessions &lt;count&gt;
/// session &lt;season&gt; &lt;round&gt; &lt;date&gt; &lt;len&gt;:&lt;type&gt; &lt;len&gt;:&lt;layout&gt;
/// </code>
/// </para>
/// </summary>
public sealed class RaceCalendarSection : IWorldSection
{
    public const string SectionName = "race-calendar";

    private readonly CalendarSession[] _sessions;

    private RaceCalendarSection(CalendarSession[] sessions)
    {
        _sessions = sessions;
    }

    public static RaceCalendarSection Empty { get; } = new([]);

    public string Name => SectionName;

    public int SchemaVersion => 1;

    public IReadOnlyList<CalendarSession> Sessions => _sessions;

    public static RaceCalendarSection Restore(IReadOnlyList<CalendarSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        var copy = new CalendarSession[sessions.Count];
        for (var i = 0; i < copy.Length; i++)
        {
            copy[i] = sessions[i] ?? throw new ArgumentException("A calendar session is missing.", nameof(sessions));
        }

        Array.Sort(copy, Compare);
        for (var i = 1; i < copy.Length; i++)
        {
            if (copy[i].Season == copy[i - 1].Season && copy[i].Round == copy[i - 1].Round
                && string.Equals(copy[i].TypeId, copy[i - 1].TypeId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Season {copy[i].Season.ToString(CultureInfo.InvariantCulture)} round {copy[i].Round.ToString(CultureInfo.InvariantCulture)} lists {copy[i].TypeId} twice.",
                    nameof(sessions));
            }
        }

        return new RaceCalendarSection(copy);
    }

    public bool HasSeason(int season)
    {
        foreach (var session in _sessions)
        {
            if (session.Season == season)
            {
                return true;
            }
        }

        return false;
    }

    public IReadOnlyList<CalendarSession> SeasonSessions(int season) =>
        _sessions.Where(session => session.Season == season).ToArray();

    /// <summary>The section with <paramref name="season"/>'s plan set to <paramref name="sessions"/>. Other seasons are kept.</summary>
    public RaceCalendarSection WithSeason(int season, IReadOnlyList<CalendarSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        foreach (var session in sessions)
        {
            if (session.Season != season)
            {
                throw new ArgumentException("Every session must belong to season " + season.ToString(CultureInfo.InvariantCulture) + ".", nameof(sessions));
            }
        }

        return Restore(_sessions.Where(session => session.Season != season).Concat(sessions).ToArray());
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Count("sessions", _sessions.Length);
        foreach (var session in _sessions)
        {
            writer.Begin("session");
            writer.Raw(session.Season.ToString(CultureInfo.InvariantCulture));
            writer.Raw(" " + session.Round.ToString(CultureInfo.InvariantCulture));
            writer.Raw(" " + session.Date);
            writer.Space();
            writer.Field(session.TypeId);
            writer.Space();
            writer.Field(session.LayoutId);
            writer.End();
        }
    }

    private static int Compare(CalendarSession left, CalendarSession right)
    {
        var season = left.Season.CompareTo(right.Season);
        if (season != 0)
        {
            return season;
        }

        var round = left.Round.CompareTo(right.Round);
        if (round != 0)
        {
            return round;
        }

        var date = left.Date.CompareTo(right.Date);
        return date != 0 ? date : string.CompareOrdinal(left.TypeId, right.TypeId);
    }
}
