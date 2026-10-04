using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Adds what a day-by-day run keeps beside the world so a save resumes to the same future (issue #123, INV-002):
/// <c>career_run</c> (one row: the opening year, which decides which seasons draw generated intake, and the contract-expiry
/// and intake tallies), <c>career_years</c> (the per-season summaries printed by the runner) and <c>talent_pool</c>
/// (the placeholder pool of T18, one row per person; the pool is not part of <c>WorldState</c> and not of its hash).
/// <c>talent_pool</c> is deliberately a table of its own with no link to another table, so the talent-pool task (T40) can
/// replace it, or move it into a world section, without touching the rest. A save from before this migration has no
/// <c>career_run</c> row, which loads as "no run state": it opens and its world loads, but it cannot be resumed exactly.
/// RNG stream states use the <c>meta.rng_states</c> column that V001 created.
/// </summary>
public sealed class V007_CareerRun : ISaveMigration
{
    public int Version => 7;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Script;
        command.ExecuteNonQuery();
    }

    private const string Script = """
        CREATE TABLE career_run (
            id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
            opened_year INTEGER NOT NULL CHECK (opened_year >= 1),
            contract_expiries INTEGER NOT NULL CHECK (contract_expiries >= 0),
            intakes INTEGER NOT NULL CHECK (intakes >= 0)
        ) STRICT;

        CREATE TABLE career_years (
            year INTEGER NOT NULL PRIMARY KEY CHECK (year >= 1),
            alive INTEGER NOT NULL CHECK (alive >= 0),
            retired INTEGER NOT NULL CHECK (retired >= 0),
            pool INTEGER NOT NULL CHECK (pool >= 0),
            contracts INTEGER NOT NULL CHECK (contracts >= 0),
            state_hash TEXT NOT NULL CHECK (length(state_hash) > 0)
        ) STRICT;

        CREATE TABLE talent_pool (
            person_id TEXT NOT NULL PRIMARY KEY CHECK (length(person_id) > 0)
        ) STRICT, WITHOUT ROWID;
        """;
}
