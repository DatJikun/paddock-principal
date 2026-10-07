using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Car development v2 (PP-066): the character of the next concept in each plan, the year the concept in the car came in (it names the
/// concept), the character, drawn ceiling and flags of a concept project, and the short log of what the team learned about its car.
/// Existing rows take the defaults, so a save from before this migration loads with nothing changed and its section text, and hash,
/// do not move.
/// </summary>
public sealed class V028_CarDevelopmentV2 : ISaveMigration
{
    public int Version => 28;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Script;
        command.ExecuteNonQuery();
    }

    private const string Script = """
        ALTER TABLE development_plans ADD COLUMN next_philosophy_milli INTEGER NOT NULL DEFAULT -1000 CHECK (next_philosophy_milli >= -1000 AND next_philosophy_milli <= 1000);
        ALTER TABLE development_plans ADD COLUMN next_aero_milli INTEGER NOT NULL DEFAULT 0 CHECK (next_aero_milli >= -1000 AND next_aero_milli <= 1000);
        ALTER TABLE development_accounts ADD COLUMN concept_year INTEGER NOT NULL DEFAULT 0 CHECK (concept_year >= 0);
        ALTER TABLE development_projects ADD COLUMN philosophy_milli INTEGER NOT NULL DEFAULT 0 CHECK (philosophy_milli >= -1000 AND philosophy_milli <= 1000);
        ALTER TABLE development_projects ADD COLUMN aero_milli INTEGER NOT NULL DEFAULT 0 CHECK (aero_milli >= -1000 AND aero_milli <= 1000);
        ALTER TABLE development_projects ADD COLUMN ceiling_milli INTEGER NOT NULL DEFAULT 0 CHECK (ceiling_milli >= 0 AND ceiling_milli <= 100000);
        ALTER TABLE development_projects ADD COLUMN flags INTEGER NOT NULL DEFAULT 0 CHECK (flags >= 0);

        CREATE TABLE development_notes (
            seq INTEGER NOT NULL PRIMARY KEY,
            organization_id TEXT NOT NULL CHECK (length(organization_id) > 0),
            on_date TEXT NOT NULL,
            source TEXT NOT NULL CHECK (length(source) > 0),
            delta_milli INTEGER NOT NULL
        ) STRICT;
        """;
}
