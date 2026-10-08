using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Adds <c>persons.is_female</c> (#265): the gender of a person, 1 for a woman and 0 otherwise, so the texts about a person can
/// pick a pronoun. Every person of an older world starts at 0: the real drivers of the 1950s data are men, and a generated
/// person of an older save keeps the pronoun the game used before.
/// </summary>
public sealed class V030_PersonGender : ISaveMigration
{
    public int Version => 30;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "ALTER TABLE persons ADD COLUMN is_female INTEGER NOT NULL DEFAULT 0 CHECK (is_female IN (0, 1));";
        command.ExecuteNonQuery();
    }
}
