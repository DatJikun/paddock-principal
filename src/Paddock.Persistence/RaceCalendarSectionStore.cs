using System.Globalization;
using Microsoft.Data.Sqlite;
using Paddock.Domain.Racing;
using Paddock.Domain.World;
using static Paddock.Persistence.SectionRows;

namespace Paddock.Persistence;

/// <summary>Saves the <c>race-calendar</c> section into the table made by <see cref="V026_RaceCalendarSection"/>.</summary>
public sealed class RaceCalendarSectionStore : ISectionStore
{
    public string SectionName => RaceCalendarSection.SectionName;

    public int SchemaVersion => 1;

    public void Replace(SqliteConnection connection, SqliteTransaction transaction, IWorldSection? section)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var calendar = section switch
        {
            null => null,
            RaceCalendarSection typed => typed,
            _ => throw new ArgumentException("The race-calendar store cannot save a " + section.GetType().Name + ".", nameof(section)),
        };

        if (calendar is null && !TableExists(connection, transaction, "race_calendar_sessions"))
        {
            return;
        }

        Run(connection, transaction, "DELETE FROM race_calendar_sessions");
        if (calendar is null)
        {
            return;
        }

        foreach (var session in calendar.Sessions)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO race_calendar_sessions (season, round, type_id, session_date, layout_id) VALUES ($season, $round, $type, $date, $layout)",
                ("$season", (long)session.Season),
                ("$round", (long)session.Round),
                ("$type", session.TypeId),
                ("$date", session.Date.ToString()),
                ("$layout", session.LayoutId));
        }
    }

    public IWorldSection Load(SqliteConnection connection, int storedSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (storedSchemaVersion != SchemaVersion)
        {
            throw new InvalidDataException("Race-calendar section schema " + storedSchemaVersion.ToString(CultureInfo.InvariantCulture) + " is not readable.");
        }

        var sessions = new List<CalendarSession>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT season, round, type_id, session_date, layout_id FROM race_calendar_sessions ORDER BY season, round, type_id";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            sessions.Add(new CalendarSession(
                checked((int)reader.GetInt64(0)),
                checked((int)reader.GetInt64(1)),
                reader.GetString(2),
                ParseDate(reader.GetString(3)),
                reader.GetString(4)));
        }

        return RaceCalendarSection.Restore(sessions);
    }
}
