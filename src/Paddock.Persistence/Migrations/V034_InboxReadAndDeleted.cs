using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Adds <c>inbox_items.is_read</c> (#320): 1 once the owner has opened the item, and <c>is_deleted</c>: 1 once they deleted an
/// answered or lapsed item from the mailbox. Items of an older save load as unread and not deleted, which is how that save showed
/// them, and neither mark adds anything to the canonical text while unset, so the save keeps its hash.
/// </summary>
public sealed class V034_InboxReadAndDeleted : ISaveMigration
{
    public int Version => 34;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            ALTER TABLE inbox_items ADD COLUMN is_read INTEGER NOT NULL DEFAULT 0 CHECK (is_read IN (0, 1));
            ALTER TABLE inbox_items ADD COLUMN is_deleted INTEGER NOT NULL DEFAULT 0 CHECK (is_deleted IN (0, 1));
            """;
        command.ExecuteNonQuery();
    }
}
