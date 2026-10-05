using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Adds the table of the <c>staff</c> world section (PP-059): which race engineer works with which driver, and the
/// relationship. A save from before this migration has no staff section, which loads as no section.
/// </summary>
public sealed class V019_StaffSection : ISaveMigration
{
    public int Version => 19;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Script;
        command.ExecuteNonQuery();
    }

    private const string Script = """
        CREATE TABLE engineer_links (
            engineer_id TEXT NOT NULL PRIMARY KEY CHECK (length(engineer_id) > 0),
            team_id TEXT NOT NULL CHECK (length(team_id) > 0),
            driver_id TEXT NOT NULL CHECK (length(driver_id) > 0),
            relationship INTEGER NOT NULL CHECK (relationship BETWEEN 1 AND 100),
            season INTEGER NOT NULL CHECK (season >= 1950)
        ) STRICT, WITHOUT ROWID;
        """;
}
