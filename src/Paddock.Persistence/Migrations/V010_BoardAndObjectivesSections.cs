using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Adds the tables of two world sections that the board (T45) is the first to need: <c>objectives</c> (the objectives granted
/// to organizations, with the arguments of their effects and the counter of their ids) and <c>board</c> (the board of each team
/// with who runs it, every reputation with its history, and the managers without a team).
/// A save from before this migration has neither section, which loads as no section (not an empty one), so its hash is unchanged.
/// Rows follow the rules of V003 and V005: dates are <c>yyyy-MM-dd</c> text, enums are stored by name, every collection is a
/// table, nothing is stored twice. Reputation and confidence are integer tenths of a point.
/// </summary>
public sealed class V010_BoardAndObjectivesSections : ISaveMigration
{
    public int Version => 10;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Script;
        command.ExecuteNonQuery();
    }

    private const string Script = """
        CREATE TABLE objectives_counter (
            id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
            next_number INTEGER NOT NULL CHECK (next_number >= 1)
        ) STRICT;

        CREATE TABLE objectives (
            number INTEGER NOT NULL PRIMARY KEY CHECK (number >= 1),
            owner_id TEXT NOT NULL CHECK (length(owner_id) > 0),
            grantor_id TEXT NOT NULL CHECK (length(grantor_id) > 0),
            kind_key TEXT NOT NULL CHECK (length(kind_key) > 0),
            reason_key TEXT NOT NULL CHECK (length(reason_key) > 0),
            predicate_name TEXT NOT NULL CHECK (length(predicate_name) > 0),
            predicate_parameter TEXT NOT NULL,
            baseline TEXT,
            created TEXT NOT NULL,
            deadline TEXT NOT NULL,
            met_key TEXT NOT NULL CHECK (length(met_key) > 0),
            failed_key TEXT NOT NULL CHECK (length(failed_key) > 0),
            status TEXT NOT NULL CHECK (status IN ('Open', 'Met', 'Failed')),
            settled_on TEXT,
            CHECK ((status = 'Open') = (settled_on IS NULL))
        ) STRICT;

        CREATE TABLE objective_effect_arguments (
            objective_number INTEGER NOT NULL REFERENCES objectives (number) ON DELETE CASCADE,
            effect TEXT NOT NULL CHECK (effect IN ('met', 'failed')),
            name TEXT NOT NULL CHECK (length(name) > 0),
            value TEXT NOT NULL,
            PRIMARY KEY (objective_number, effect, name)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE boards (
            organization_id TEXT NOT NULL PRIMARY KEY CHECK (length(organization_id) > 0),
            patience INTEGER NOT NULL CHECK (patience >= 0 AND patience <= 100),
            confidence INTEGER NOT NULL CHECK (confidence >= 0 AND confidence <= 1000),
            low_streak INTEGER NOT NULL CHECK (low_streak >= 0),
            expected_position INTEGER NOT NULL CHECK (expected_position >= 0),
            archetype TEXT NOT NULL CHECK (length(archetype) > 0),
            last_review TEXT,
            last_target INTEGER,
            last_cash INTEGER,
            principal_kind TEXT CHECK (principal_kind IS NULL OR principal_kind IN ('Human', 'Ai')),
            principal_subject TEXT,
            principal_since TEXT,
            principal_protected_until TEXT,
            principal_founder INTEGER CHECK (principal_founder IS NULL OR principal_founder IN (0, 1)),
            CHECK ((principal_kind IS NULL) = (principal_subject IS NULL)
                AND (principal_kind IS NULL) = (principal_since IS NULL)
                AND (principal_kind IS NULL) = (principal_protected_until IS NULL)
                AND (principal_kind IS NULL) = (principal_founder IS NULL))
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE board_reputations (
            subject TEXT NOT NULL PRIMARY KEY CHECK (length(subject) > 0),
            tenths INTEGER NOT NULL CHECK (tenths >= 0 AND tenths <= 1000)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE board_reputation_changes (
            ordinal INTEGER NOT NULL PRIMARY KEY CHECK (ordinal >= 0),
            subject TEXT NOT NULL CHECK (length(subject) > 0),
            on_date TEXT NOT NULL,
            delta INTEGER NOT NULL,
            result INTEGER NOT NULL CHECK (result >= 0 AND result <= 1000),
            reason_key TEXT NOT NULL CHECK (length(reason_key) > 0)
        ) STRICT;

        CREATE TABLE board_unemployed (
            manager_id TEXT NOT NULL PRIMARY KEY CHECK (length(manager_id) > 0),
            since TEXT NOT NULL,
            former_organization TEXT,
            severance INTEGER NOT NULL CHECK (severance >= 0),
            offers_made INTEGER NOT NULL CHECK (offers_made >= 0),
            last_offer_on TEXT
        ) STRICT, WITHOUT ROWID;
        """;
}
