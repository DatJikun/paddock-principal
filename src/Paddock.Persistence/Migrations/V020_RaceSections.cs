using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Adds the tables of the championship and the voted regulations (T47). A save from before this migration has neither
/// section, which loads as no section, so its hash is unchanged. Ids are not foreign keys (rules of V003 and V005).
/// </summary>
public sealed class V020_RaceSections : ISaveMigration
{
    public int Version => 20;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Script;
        command.ExecuteNonQuery();
    }

    private const string Script = """
        CREATE TABLE championship_meta (
            singleton INTEGER NOT NULL PRIMARY KEY CHECK (singleton = 1),
            season INTEGER NOT NULL CHECK (season >= 1950),
            total_rounds INTEGER NOT NULL CHECK (total_rounds >= 1),
            rounds_completed INTEGER NOT NULL CHECK (rounds_completed >= 0 AND rounds_completed <= total_rounds),
            settled INTEGER NOT NULL CHECK (settled IN (0, 1))
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE championship_rows (
            kind TEXT NOT NULL CHECK (kind IN ('driver', 'constructor')),
            entry_id TEXT NOT NULL CHECK (length(entry_id) > 0),
            round_points TEXT NOT NULL,
            positions TEXT NOT NULL,
            PRIMARY KEY (kind, entry_id)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE regulations_meta (
            singleton INTEGER NOT NULL PRIMARY KEY CHECK (singleton = 1),
            season INTEGER NOT NULL CHECK (season >= 1950)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE regulations_values (
            dimension_id TEXT NOT NULL PRIMARY KEY CHECK (length(dimension_id) > 0),
            value TEXT NOT NULL CHECK (length(value) > 0)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE regulations_rejected (
            dimension_id TEXT NOT NULL CHECK (length(dimension_id) > 0),
            value TEXT NOT NULL CHECK (length(value) > 0),
            season INTEGER NOT NULL CHECK (season >= 1950),
            PRIMARY KEY (dimension_id, value, season)
        ) STRICT, WITHOUT ROWID;
        """;
}
