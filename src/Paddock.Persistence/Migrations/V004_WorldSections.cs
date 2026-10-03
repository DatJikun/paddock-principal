using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Adds <c>world_sections</c>: one row for each world section the save holds (name and schema version).
/// The section's own rows live in tables that its own migration creates (see <see cref="V005_InboxSection"/>);
/// this table is how the loader knows which sections exist, so a section that was saved is never silently skipped.
/// The pattern for a new section is on <see cref="ISectionStore"/>.
/// </summary>
public sealed class V004_WorldSections : ISaveMigration
{
    public int Version => 4;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE world_sections (
                name TEXT NOT NULL PRIMARY KEY CHECK (length(name) > 0),
                schema_version INTEGER NOT NULL CHECK (schema_version >= 1)
            ) STRICT, WITHOUT ROWID;
            """;
        command.ExecuteNonQuery();
    }
}
