using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Adds <c>inbox_items.is_read</c> (#320): 1 once the owner has opened the item. Items of an older save load as unread, which
/// is how that save showed them, and an unread item adds nothing to the canonical text, so the save keeps its hash.
/// </summary>
public sealed class V034_InboxRead : ISaveMigration
{
    public int Version => 34;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "ALTER TABLE inbox_items ADD COLUMN is_read INTEGER NOT NULL DEFAULT 0 CHECK (is_read IN (0, 1));";
        command.ExecuteNonQuery();
    }
}
