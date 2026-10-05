using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// A season objective can be withdrawn when a human takes the team before its first race (#212). SQLite cannot change a CHECK,
/// and the V012 check of <c>objectives.status</c> only knows Open, Met and Failed, so the table is rebuilt with the wider check.
/// <c>objective_effect_arguments</c> references <c>objectives</c> with ON DELETE CASCADE, so it is set aside first and restored
/// after the rebuild; dropping the parent with the child in place would delete every argument. Rows are copied as they are,
/// so an older save loads with the same section text and hash.
/// </summary>
public sealed class V022_ObjectiveWithdrawn : ISaveMigration
{
    public int Version => 22;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Script;
        command.ExecuteNonQuery();
    }

    private const string Script = """
        CREATE TABLE objective_effect_arguments_v22 (
            objective_number INTEGER NOT NULL,
            effect TEXT NOT NULL,
            name TEXT NOT NULL,
            value TEXT NOT NULL
        ) STRICT;

        INSERT INTO objective_effect_arguments_v22 (objective_number, effect, name, value)
        SELECT objective_number, effect, name, value FROM objective_effect_arguments;

        DROP TABLE objective_effect_arguments;

        CREATE TABLE objectives_v22 (
            number INTEGER NOT NULL PRIMARY KEY CHECK (number >= 1),
            owner_id TEXT NOT NULL CHECK (length(owner_id) > 0),
            grantor_id TEXT NOT NULL CHECK (length(grantor_id) > 0),
            kind_key TEXT NOT NULL CHECK (length(kind_key) > 0),
            reason_key TEXT NOT NULL CHECK (length(reason_key) > 0),
            predicate_name TEXT NOT NULL CHECK (length(predicate_name) > 0),
            predicate_parameter TEXT NOT NULL,
            baseline TEXT,
            created_on TEXT NOT NULL,
            deadline_on TEXT NOT NULL,
            met_key TEXT NOT NULL CHECK (length(met_key) > 0),
            failed_key TEXT NOT NULL CHECK (length(failed_key) > 0),
            status TEXT NOT NULL CHECK (status IN ('Open', 'Met', 'Failed', 'Withdrawn')),
            settled_on TEXT
        ) STRICT;

        INSERT INTO objectives_v22 (number, owner_id, grantor_id, kind_key, reason_key, predicate_name, predicate_parameter, baseline,
            created_on, deadline_on, met_key, failed_key, status, settled_on)
        SELECT number, owner_id, grantor_id, kind_key, reason_key, predicate_name, predicate_parameter, baseline,
            created_on, deadline_on, met_key, failed_key, status, settled_on
        FROM objectives;

        DROP TABLE objectives;

        ALTER TABLE objectives_v22 RENAME TO objectives;

        CREATE TABLE objective_effect_arguments (
            objective_number INTEGER NOT NULL REFERENCES objectives (number) ON DELETE CASCADE,
            effect TEXT NOT NULL CHECK (effect IN ('met', 'failed')),
            name TEXT NOT NULL CHECK (length(name) > 0),
            value TEXT NOT NULL,
            PRIMARY KEY (objective_number, effect, name)
        ) STRICT, WITHOUT ROWID;

        INSERT INTO objective_effect_arguments (objective_number, effect, name, value)
        SELECT objective_number, effect, name, value FROM objective_effect_arguments_v22;

        DROP TABLE objective_effect_arguments_v22;
        """;
}
