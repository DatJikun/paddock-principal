using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Adds the table of the <c>race-calendar</c> world section (#229): the planned practice, qualifying and race days of a season.
/// A save from before this migration has no such section, which loads as no section, so its hash is unchanged; its seasons keep
/// the even spacing they were scheduled with, because a season without a stored plan is read with that spacing.
/// </summary>
public sealed class V026_RaceCalendarSection : ISaveMigration
{
    public int Version => 26;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE race_calendar_sessions (
                season INTEGER NOT NULL CHECK (season >= 1950),
                round INTEGER NOT NULL CHECK (round >= 1),
                type_id TEXT NOT NULL CHECK (length(type_id) > 0),
                session_date TEXT NOT NULL CHECK (length(session_date) = 10),
                layout_id TEXT NOT NULL CHECK (length(layout_id) > 0),
                PRIMARY KEY (season, round, type_id)
            ) STRICT, WITHOUT ROWID;
            """;
        command.ExecuteNonQuery();
    }
}
