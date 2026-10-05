using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>The <c>raises</c> section (PP-057): one row per driver and team. A save from before this migration has no table.</summary>
public sealed class V019_RaisesSection : ISaveMigration
{
    public int Version => 19;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE driver_raises (
                person_id TEXT NOT NULL CHECK (length(person_id) > 0),
                organization_id TEXT NOT NULL CHECK (length(organization_id) > 0),
                season INTEGER NOT NULL CHECK (season >= 1950),
                asked_salary INTEGER NOT NULL CHECK (asked_salary >= 0),
                trust INTEGER NOT NULL CHECK (trust >= 0 AND trust <= 100),
                leave_bias INTEGER NOT NULL CHECK (leave_bias >= 0 AND leave_bias <= 100),
                PRIMARY KEY (person_id, organization_id)
            ) STRICT, WITHOUT ROWID;
            """;
        command.ExecuteNonQuery();
    }
}
