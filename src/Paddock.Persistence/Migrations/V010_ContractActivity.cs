using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Adds the per-season contract activity the career runner prints (issue #129): signed, renewed and expired, both on the
/// completed season rows and on the in-progress season kept in <c>career_run</c>. A save from before this migration loads
/// those counts as zero. The columns are not part of the world hash.
/// </summary>
public sealed class V010_ContractActivity : ISaveMigration
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
        ALTER TABLE career_run ADD COLUMN season_signed INTEGER NOT NULL DEFAULT 0 CHECK (season_signed >= 0);
        ALTER TABLE career_run ADD COLUMN season_renewed INTEGER NOT NULL DEFAULT 0 CHECK (season_renewed >= 0);
        ALTER TABLE career_run ADD COLUMN season_expired INTEGER NOT NULL DEFAULT 0 CHECK (season_expired >= 0);
        ALTER TABLE career_years ADD COLUMN signed INTEGER NOT NULL DEFAULT 0 CHECK (signed >= 0);
        ALTER TABLE career_years ADD COLUMN renewed INTEGER NOT NULL DEFAULT 0 CHECK (renewed >= 0);
        ALTER TABLE career_years ADD COLUMN expired INTEGER NOT NULL DEFAULT 0 CHECK (expired >= 0);
        """;
}
