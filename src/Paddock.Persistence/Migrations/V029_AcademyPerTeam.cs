using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Each team recruits its own juniors (#268): a pool member can belong to the academy of one team, and a lapsed career remembers the academy
/// he left. A member whose junior season was paid for by a team is read as recruited by that team, because a programme is only paid for a
/// junior of the payer's academy. Everything else keeps the meaning it had. Number is V029 because V028 (car development v2) is the newest on
/// main; the open PRs that also add a migration are renumbered by whoever merges second, and so is this one if it merges after them.
/// </summary>
public sealed class V029_AcademyPerTeam : ISaveMigration
{
    public int Version => 29;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Script;
        command.ExecuteNonQuery();
    }

    private const string Script = """
        ALTER TABLE pool_members ADD COLUMN academy_id TEXT CHECK (academy_id IS NULL OR length(academy_id) > 0);
        UPDATE pool_members SET academy_id = funder_id WHERE funder_id IS NOT NULL;
        ALTER TABLE pool_lapsed ADD COLUMN academy_id TEXT CHECK (academy_id IS NULL OR length(academy_id) > 0);
        """;
}
