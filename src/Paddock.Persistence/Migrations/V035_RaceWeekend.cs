using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// The race weekend of today (#316): the pre-race plans and the pit wall orders of a watched race, so a save mid-weekend keeps
/// them. Number is V035, the next free one on main after V034 (inbox read and deleted) when this was written.
/// </summary>
public sealed class V035_RaceWeekend : ISaveMigration
{
    public int Version => 35;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Script;
        command.ExecuteNonQuery();
    }

    private const string Script = """
        CREATE TABLE race_weekend_meta (
            singleton INTEGER NOT NULL PRIMARY KEY CHECK (singleton = 1),
            season INTEGER NOT NULL,
            round INTEGER NOT NULL CHECK (round >= 1),
            watched_hash TEXT NOT NULL
        ) STRICT;

        CREATE TABLE race_weekend_plans (
            car_id TEXT NOT NULL PRIMARY KEY CHECK (length(car_id) > 0),
            team_id TEXT NOT NULL CHECK (length(team_id) > 0),
            start_compound TEXT NOT NULL,
            pace TEXT NOT NULL,
            engine TEXT NOT NULL CHECK (length(engine) > 0),
            stops TEXT NOT NULL CHECK (length(stops) > 0)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE race_weekend_orders (
            sequence INTEGER NOT NULL PRIMARY KEY CHECK (sequence >= 0),
            manager_id TEXT NOT NULL CHECK (length(manager_id) > 0),
            at_ms INTEGER NOT NULL,
            car_id TEXT NOT NULL CHECK (length(car_id) > 0),
            lap INTEGER NOT NULL CHECK (lap >= 1),
            kind TEXT NOT NULL CHECK (length(kind) > 0),
            pace TEXT NOT NULL,
            compound TEXT NOT NULL,
            engine TEXT NOT NULL,
            on_flag INTEGER NOT NULL CHECK (on_flag IN (0, 1))
        ) STRICT;
        """;
}
