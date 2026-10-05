using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Adds the tables of the <c>race-results</c> world section: the classification and the report of each finished round.
/// A save from before this migration has no such section, which loads as no section, so its hash is unchanged.
/// </summary>
public sealed class V022_RaceResultsSection : ISaveMigration
{
    public int Version => 22;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Script;
        command.ExecuteNonQuery();
    }

    private const string Script = """
        CREATE TABLE race_results (
            season INTEGER NOT NULL CHECK (season >= 1950),
            round INTEGER NOT NULL CHECK (round >= 1),
            layout_id TEXT NOT NULL CHECK (length(layout_id) > 0),
            PRIMARY KEY (season, round)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE race_result_rows (
            season INTEGER NOT NULL,
            round INTEGER NOT NULL,
            position INTEGER NOT NULL CHECK (position >= 1),
            classified INTEGER NOT NULL CHECK (classified IN (0, 1)),
            driver_id TEXT NOT NULL CHECK (length(driver_id) > 0),
            team_id TEXT NOT NULL CHECK (length(team_id) > 0),
            points TEXT NOT NULL,
            retirement_key TEXT NOT NULL,
            PRIMARY KEY (season, round, position),
            FOREIGN KEY (season, round) REFERENCES race_results (season, round)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE race_report_lines (
            season INTEGER NOT NULL,
            round INTEGER NOT NULL,
            section_index INTEGER NOT NULL CHECK (section_index >= 0),
            line_index INTEGER NOT NULL CHECK (line_index >= 0),
            line_key TEXT NOT NULL CHECK (length(line_key) > 0),
            line_count TEXT NOT NULL,
            PRIMARY KEY (season, round, section_index, line_index),
            FOREIGN KEY (season, round) REFERENCES race_results (season, round)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE race_report_args (
            season INTEGER NOT NULL,
            round INTEGER NOT NULL,
            section_index INTEGER NOT NULL,
            line_index INTEGER NOT NULL,
            arg_index INTEGER NOT NULL CHECK (arg_index >= 0),
            arg_name TEXT NOT NULL CHECK (length(arg_name) > 0),
            arg_value TEXT NOT NULL,
            PRIMARY KEY (season, round, section_index, line_index, arg_index),
            FOREIGN KEY (season, round, section_index, line_index)
                REFERENCES race_report_lines (season, round, section_index, line_index)
        ) STRICT, WITHOUT ROWID;
        """;
}
