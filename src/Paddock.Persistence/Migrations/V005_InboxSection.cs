using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Adds the tables of the <c>inbox</c> world section: items, their argument values, their options, and the item counter.
/// A save from before this migration has no inbox, which loads as no section (not an empty one), so its hash is unchanged.
/// Rows follow the same rules as the world tables (V003): dates are <c>yyyy-MM-dd</c> text, enums are stored by name,
/// every collection is a table, nothing is stored twice.
/// </summary>
public sealed class V005_InboxSection : ISaveMigration
{
    public int Version => 5;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Script;
        command.ExecuteNonQuery();
    }

    private const string Script = """
        CREATE TABLE inbox_counter (
            id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
            next_number INTEGER NOT NULL CHECK (next_number >= 1)
        ) STRICT;

        CREATE TABLE inbox_items (
            number INTEGER NOT NULL PRIMARY KEY CHECK (number >= 1),
            manager_id TEXT NOT NULL CHECK (length(manager_id) > 0),
            created TEXT NOT NULL,
            kind TEXT NOT NULL CHECK (length(kind) > 0),
            subject_key TEXT NOT NULL CHECK (length(subject_key) > 0),
            valid_until TEXT,
            default_option TEXT,
            status TEXT NOT NULL CHECK (status IN ('Open', 'Resolved', 'Dismissed', 'Expired')),
            closed_on TEXT,
            chosen_option TEXT,
            CHECK ((status = 'Open') = (closed_on IS NULL))
        ) STRICT;

        CREATE INDEX inbox_items_by_manager ON inbox_items (manager_id, number);

        CREATE TABLE inbox_arguments (
            item_number INTEGER NOT NULL REFERENCES inbox_items (number) ON DELETE CASCADE,
            name TEXT NOT NULL CHECK (length(name) > 0),
            value TEXT NOT NULL,
            PRIMARY KEY (item_number, name)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE inbox_options (
            item_number INTEGER NOT NULL REFERENCES inbox_items (number) ON DELETE CASCADE,
            ordinal INTEGER NOT NULL CHECK (ordinal >= 0),
            option_id TEXT NOT NULL CHECK (length(option_id) > 0),
            label_key TEXT NOT NULL CHECK (length(label_key) > 0),
            consequence_key TEXT NOT NULL CHECK (length(consequence_key) > 0),
            PRIMARY KEY (item_number, ordinal),
            UNIQUE (item_number, option_id)
        ) STRICT, WITHOUT ROWID;
        """;
}
