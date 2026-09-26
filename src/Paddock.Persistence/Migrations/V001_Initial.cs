using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Creates the single-row <c>meta</c> table. No game-entity tables.
/// </summary>
public sealed class V001_Initial : ISaveMigration
{
    public int Version => 1;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE meta (
                id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
                schema_version INTEGER NOT NULL CHECK (schema_version >= 1),
                created_at_utc TEXT NOT NULL,
                saved_at_utc TEXT NOT NULL,
                career_name TEXT NOT NULL,
                manager_name TEXT NOT NULL,
                player_team_id TEXT NOT NULL,
                current_game_date TEXT NOT NULL,
                world_data_hash TEXT NOT NULL,
                master_seed TEXT NOT NULL,
                rng_states TEXT
            ) STRICT;
            """;
        command.ExecuteNonQuery();
    }
}
