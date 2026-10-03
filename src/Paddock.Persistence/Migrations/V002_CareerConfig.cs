using Microsoft.Data.Sqlite;
using Paddock.Domain.Career;

namespace Paddock.Persistence;

/// <summary>
/// Adds <c>meta.career_config</c>, one canonical JSON object.
/// A single column keeps the stored bytes identical to <see cref="CareerConfig.ToCanonicalJson"/>,
/// which is the reproducible hash input. A later axis is a new migration of that document,
/// not another set of columns.
/// A V001 save has no config; this step installs the Balanced preset.
/// </summary>
public sealed class V002_CareerConfig : ISaveMigration
{
    public int Version => 2;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using (var alter = connection.CreateCommand())
        {
            alter.Transaction = transaction;
            alter.CommandText = """
                ALTER TABLE meta ADD COLUMN career_config TEXT NOT NULL DEFAULT '';
                """;
            alter.ExecuteNonQuery();
        }

        using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            UPDATE meta
            SET career_config = $config
            WHERE career_config = '';
            """;
        update.Parameters.Add("$config", SqliteType.Text).Value =
            CareerConfig.FromPreset(CareerPreset.Balanced).ToCanonicalJson();
        update.ExecuteNonQuery();
    }
}
