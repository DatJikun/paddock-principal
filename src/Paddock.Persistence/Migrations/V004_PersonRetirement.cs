using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Adds <c>persons.retired_on</c> (T18): the day a person left the sport, <c>yyyy-MM-dd</c>, or NULL while they are active.
/// A retired person keeps their row (TECH §6.2: real people always stay). Every row already stored is active, so the column
/// starts NULL for all of them and a V003 world loads unchanged.
/// </summary>
public sealed class V004_PersonRetirement : ISaveMigration
{
    public int Version => 4;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "ALTER TABLE persons ADD COLUMN retired_on TEXT CHECK (retired_on IS NULL OR length(retired_on) = 10)";
        command.ExecuteNonQuery();
    }
}
