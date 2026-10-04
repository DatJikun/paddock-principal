using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Adds the table of the <c>principals</c> world section (T44): the archetype of each AI team principal, the day of its next
/// review and what it remembers between reviews. A save from before this migration has no principals section, which loads as no
/// section, so its hash is unchanged. Dates are <c>yyyy-MM-dd</c> text and ids are not foreign keys (rules of V003 and V005).
/// </summary>
public sealed class V017_PrincipalsSection : ISaveMigration
{
    public int Version => 17;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Script;
        command.ExecuteNonQuery();
    }

    private const string Script = """
        CREATE TABLE ai_principals (
            organization_id TEXT NOT NULL PRIMARY KEY CHECK (length(organization_id) > 0),
            archetype TEXT NOT NULL CHECK (archetype IN ('Contender', 'Builder', 'Opportunist', 'Survivor')),
            person_id TEXT CHECK (person_id IS NULL OR length(person_id) > 0),
            assigned_on TEXT NOT NULL,
            last_review TEXT,
            next_review TEXT NOT NULL,
            sacrificed_season INTEGER NOT NULL CHECK (sacrificed_season >= 0),
            scout_season INTEGER NOT NULL CHECK (scout_season >= 0),
            staff_roles TEXT NOT NULL
        ) STRICT, WITHOUT ROWID;
        """;
}
