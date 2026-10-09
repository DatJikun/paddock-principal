using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Crash damage and spare chassis (#270): one row per damaged car and one per team spare. Number is V033, the next free one on
/// main after V032 (academy per team, #268) when this was merged.
/// </summary>
public sealed class V033_CarDamage : ISaveMigration
{
    public int Version => 33;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Script;
        command.ExecuteNonQuery();
    }

    private const string Script = """
        CREATE TABLE car_damages (
            car_id TEXT NOT NULL PRIMARY KEY CHECK (length(car_id) > 0),
            organization_id TEXT NOT NULL CHECK (length(organization_id) > 0),
            kind TEXT NOT NULL CHECK (kind IN ('Light', 'Heavy', 'Wrecked')),
            source TEXT NOT NULL CHECK (source IN ('Race', 'Test')),
            damaged_on TEXT NOT NULL,
            ready_on TEXT NOT NULL,
            cost_cents INTEGER NOT NULL CHECK (cost_cents >= 0)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE car_spares (
            organization_id TEXT NOT NULL PRIMARY KEY CHECK (length(organization_id) > 0),
            season INTEGER NOT NULL,
            aero INTEGER NOT NULL, philosophy INTEGER NOT NULL, window_axis INTEGER NOT NULL,
            cooling INTEGER NOT NULL, tyre INTEGER NOT NULL, integration INTEGER NOT NULL,
            power INTEGER NOT NULL, downforce INTEGER NOT NULL, grip INTEGER NOT NULL,
            braking INTEGER NOT NULL, reliability INTEGER NOT NULL
        ) STRICT, WITHOUT ROWID;
        """;
}
