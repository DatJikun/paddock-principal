using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Adds the tables of the <c>talent-pool</c> world section: the handle counter, the members (with a funded junior season, if any),
/// the lapsed careers, each organization's scouting focus, and the observation points. The observed bands are not here: they are
/// the world's knowledge table. A save from before this migration has no pool, which loads as no section.
/// Rows follow the rules of the world tables (V003): dates are <c>yyyy-MM-dd</c> text, enums are stored by name, every collection
/// is a table, nothing is stored twice. Person and organization ids are not foreign keys, as in the inbox: the world tables are
/// rewritten as a whole and the section is checked when it is loaded.
/// </summary>
public sealed class V007_TalentPoolSection : ISaveMigration
{
    public int Version => 7;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Script;
        command.ExecuteNonQuery();
    }

    private const string Script = """
        CREATE TABLE pool_counter (
            id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
            next_handle INTEGER NOT NULL CHECK (next_handle >= 1)
        ) STRICT;

        CREATE TABLE pool_members (
            person_id TEXT NOT NULL PRIMARY KEY CHECK (length(person_id) > 0),
            handle INTEGER NOT NULL UNIQUE CHECK (handle >= 1),
            entered TEXT NOT NULL,
            funder_id TEXT,
            programme TEXT CHECK (programme IN ('CheapSlow', 'ExpensiveFast')),
            funding_season INTEGER,
            CHECK ((funder_id IS NULL) = (programme IS NULL) AND (programme IS NULL) = (funding_season IS NULL))
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE pool_lapsed (
            person_id TEXT NOT NULL PRIMARY KEY CHECK (length(person_id) > 0),
            lapsed_on TEXT NOT NULL
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE pool_focus (
            organization_id TEXT NOT NULL PRIMARY KEY CHECK (length(organization_id) > 0),
            kind TEXT NOT NULL CHECK (kind IN ('Pool', 'Person')),
            person_id TEXT,
            CHECK ((kind = 'Person') = (person_id IS NOT NULL))
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE pool_observations (
            organization_id TEXT NOT NULL CHECK (length(organization_id) > 0),
            person_id TEXT NOT NULL CHECK (length(person_id) > 0),
            milli_points INTEGER NOT NULL CHECK (milli_points >= 0),
            PRIMARY KEY (organization_id, person_id)
        ) STRICT, WITHOUT ROWID;
        """;
}
