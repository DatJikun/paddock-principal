using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Adds <c>persons.injured_until</c> (PP-061, #219): the date until which a driver is out due to injury, <c>yyyy-MM-dd</c>,
/// or NULL while they are healthy. Active, uninjured persons start with NULL so older worlds load unchanged.
/// </summary>
public sealed class V024_PersonInjuredUntil : ISaveMigration
{
    public int Version => 24;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            ALTER TABLE persons ADD COLUMN injured_until TEXT CHECK (injured_until IS NULL OR length(injured_until) = 10);

            CREATE TABLE inbox_option_arguments (
                item_number INTEGER NOT NULL,
                option_id TEXT NOT NULL,
                name TEXT NOT NULL CHECK (length(name) > 0),
                value TEXT NOT NULL,
                PRIMARY KEY (item_number, option_id, name),
                FOREIGN KEY (item_number, option_id) REFERENCES inbox_options (item_number, option_id) ON DELETE CASCADE
            ) STRICT, WITHOUT ROWID;
            """;
        command.ExecuteNonQuery();
    }
}
