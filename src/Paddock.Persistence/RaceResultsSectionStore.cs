using System.Globalization;
using Microsoft.Data.Sqlite;
using Paddock.Domain.Racing;
using Paddock.Domain.World;
using static Paddock.Persistence.SectionRows;

namespace Paddock.Persistence;

/// <summary>Saves the <c>race-results</c> section into the tables made by <see cref="V023_RaceResultsSection"/>.</summary>
public sealed class RaceResultsSectionStore : ISectionStore
{
    public string SectionName => RaceResultsSection.SectionName;

    public int SchemaVersion => RaceResultsSection.Empty.SchemaVersion;

    public void Replace(SqliteConnection connection, SqliteTransaction transaction, IWorldSection? section)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var results = section switch
        {
            null => null,
            RaceResultsSection typed => typed,
            _ => throw new ArgumentException("The race-results store cannot save a " + section.GetType().Name + ".", nameof(section)),
        };

        if (results is null && !TableExists(connection, transaction, "race_results"))
        {
            return;
        }

        Run(connection, transaction, "DELETE FROM race_report_args");
        Run(connection, transaction, "DELETE FROM race_report_lines");
        Run(connection, transaction, "DELETE FROM race_result_rows");
        Run(connection, transaction, "DELETE FROM race_results");
        if (results is null)
        {
            return;
        }

        foreach (var race in results.Races)
        {
            Run(
                connection,
                transaction,
                """
                INSERT INTO race_results (season, round, layout_id, laps, lap_length_m, pole_driver_id, pole_time_ms, fastest_lap_driver_id, fastest_race_lap_ms)
                VALUES ($season, $round, $layout, $laps, $length, $pole, $poleTime, $fastest, $fastestTime)
                """,
                ("$season", (long)race.Season),
                ("$round", (long)race.Round),
                ("$layout", race.LayoutId),
                ("$laps", race.Facts is null ? null : (long)race.Facts.Laps),
                ("$length", race.Facts is null ? null : (long)race.Facts.LapLengthMeters),
                ("$pole", race.Facts?.PoleDriverId),
                ("$poleTime", race.Facts?.PoleTimeMs),
                ("$fastest", race.Facts?.FastestLapDriverId),
                ("$fastestTime", race.Facts?.FastestLapMs));
            foreach (var row in race.Rows)
            {
                Run(
                    connection,
                    transaction,
                    """
                    INSERT INTO race_result_rows (season, round, position, classified, driver_id, team_id, points, retirement_key,
                        grid_position, laps_completed, race_time_ms, fastest_lap_ms)
                    VALUES ($season, $round, $position, $classified, $driver, $team, $points, $reason,
                        $grid, $laps, $time, $fastest)
                    """,
                    ("$season", (long)race.Season),
                    ("$round", (long)race.Round),
                    ("$position", (long)row.Position),
                    ("$classified", row.Classified ? 1L : 0L),
                    ("$driver", row.DriverId),
                    ("$team", row.TeamId),
                    ("$points", row.Points),
                    ("$reason", row.RetirementKey),
                    ("$grid", row.Detail is null ? null : (long)row.Detail.GridPosition),
                    ("$laps", row.Detail is null ? null : (long)row.Detail.LapsCompleted),
                    ("$time", row.Detail?.TimeMs),
                    ("$fastest", row.Detail?.FastestLapMs));
            }

            for (var sectionIndex = 0; sectionIndex < race.Sections.Count; sectionIndex++)
            {
                var block = race.Sections[sectionIndex];
                WriteLine(connection, transaction, race, sectionIndex, 0, block.Title);
                for (var lineIndex = 0; lineIndex < block.Lines.Count; lineIndex++)
                {
                    WriteLine(connection, transaction, race, sectionIndex, lineIndex + 1, block.Lines[lineIndex]);
                }
            }
        }
    }

    public IWorldSection Load(SqliteConnection connection, int storedSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (storedSchemaVersion is not (1 or 2))
        {
            throw new InvalidDataException(
                "The stored race-results section has schema version "
                + storedSchemaVersion.ToString(CultureInfo.InvariantCulture)
                + ", which this build cannot read.");
        }

        var races = new List<(int Season, int Round, string Layout, RaceFacts? Facts)>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT season, round, layout_id, laps, lap_length_m, pole_driver_id, pole_time_ms, fastest_lap_driver_id, fastest_race_lap_ms
                FROM race_results
                ORDER BY season, round
                """;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                RaceFacts? facts = reader.IsDBNull(3)
                    ? null
                    : new RaceFacts(
                        checked((int)reader.GetInt64(3)),
                        reader.IsDBNull(4) ? 0 : checked((int)reader.GetInt64(4)),
                        reader.IsDBNull(5) ? null : reader.GetString(5),
                        reader.IsDBNull(6) ? null : reader.GetInt64(6),
                        reader.IsDBNull(7) ? null : reader.GetString(7),
                        reader.IsDBNull(8) ? null : reader.GetInt64(8));
                races.Add((checked((int)reader.GetInt64(0)), checked((int)reader.GetInt64(1)), reader.GetString(2), facts));
            }
        }

        var stored = new List<StoredRace>(races.Count);
        foreach (var race in races)
        {
            stored.Add(new StoredRace(race.Season, race.Round, race.Layout, Rows(connection, race.Season, race.Round), Sections(connection, race.Season, race.Round), race.Facts));
        }

        return RaceResultsSection.Restore(stored);
    }

    private static void WriteLine(
        SqliteConnection connection,
        SqliteTransaction transaction,
        StoredRace race,
        int sectionIndex,
        int lineIndex,
        StoredReportLine line)
    {
        Run(
            connection,
            transaction,
            """
            INSERT INTO race_report_lines (season, round, section_index, line_index, line_key, line_count)
            VALUES ($season, $round, $section, $line, $key, $count)
            """,
            ("$season", (long)race.Season),
            ("$round", (long)race.Round),
            ("$section", (long)sectionIndex),
            ("$line", (long)lineIndex),
            ("$key", line.Key),
            ("$count", line.Count ?? ""));
        for (var i = 0; i < line.Args.Count; i++)
        {
            var arg = line.Args[i];
            Run(
                connection,
                transaction,
                """
                INSERT INTO race_report_args (season, round, section_index, line_index, arg_index, arg_name, arg_value)
                VALUES ($season, $round, $section, $line, $arg, $name, $value)
                """,
                ("$season", (long)race.Season),
                ("$round", (long)race.Round),
                ("$section", (long)sectionIndex),
                ("$line", (long)lineIndex),
                ("$arg", (long)i),
                ("$name", arg.Name),
                ("$value", arg.Value));
        }
    }

    private static List<RaceResultRow> Rows(SqliteConnection connection, int season, int round)
    {
        var rows = new List<RaceResultRow>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT position, classified, driver_id, team_id, points, retirement_key,
                grid_position, laps_completed, race_time_ms, fastest_lap_ms
            FROM race_result_rows
            WHERE season = $season AND round = $round
            ORDER BY position
            """;
        command.Parameters.AddWithValue("$season", (long)season);
        command.Parameters.AddWithValue("$round", (long)round);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new RaceResultRow(
                checked((int)reader.GetInt64(0)),
                reader.GetInt64(1) == 1,
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6)
                    ? null
                    : new RaceRowDetail(
                        checked((int)reader.GetInt64(6)),
                        reader.IsDBNull(7) ? 0 : checked((int)reader.GetInt64(7)),
                        reader.IsDBNull(8) ? null : reader.GetInt64(8),
                        reader.IsDBNull(9) ? null : reader.GetInt64(9))));
        }

        return rows;
    }

    private static List<StoredReportSection> Sections(SqliteConnection connection, int season, int round)
    {
        var lines = new List<(int Section, int Line, string Key, string? Count, List<RaceReportArg> Args)>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT section_index, line_index, line_key, line_count
                FROM race_report_lines
                WHERE season = $season AND round = $round
                ORDER BY section_index, line_index
                """;
            command.Parameters.AddWithValue("$season", (long)season);
            command.Parameters.AddWithValue("$round", (long)round);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var count = reader.GetString(3);
                lines.Add((
                    checked((int)reader.GetInt64(0)),
                    checked((int)reader.GetInt64(1)),
                    reader.GetString(2),
                    count.Length == 0 ? null : count,
                    []));
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT section_index, line_index, arg_name, arg_value
                FROM race_report_args
                WHERE season = $season AND round = $round
                ORDER BY section_index, line_index, arg_index
                """;
            command.Parameters.AddWithValue("$season", (long)season);
            command.Parameters.AddWithValue("$round", (long)round);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var section = checked((int)reader.GetInt64(0));
                var line = checked((int)reader.GetInt64(1));
                var match = lines.FindIndex(item => item.Section == section && item.Line == line);
                if (match < 0)
                {
                    throw new InvalidDataException("A report argument has no line.");
                }

                lines[match].Args.Add(new RaceReportArg(reader.GetString(2), reader.GetString(3)));
            }
        }

        var sections = new List<StoredReportSection>();
        var index = 0;
        while (index < lines.Count)
        {
            var section = lines[index].Section;
            if (lines[index].Line != 0)
            {
                throw new InvalidDataException("A report section is missing its title.");
            }

            var title = ToLine(lines[index]);
            index++;
            var body = new List<StoredReportLine>();
            while (index < lines.Count && lines[index].Section == section)
            {
                body.Add(ToLine(lines[index]));
                index++;
            }

            sections.Add(new StoredReportSection(title, body));
        }

        return sections;
    }

    private static StoredReportLine ToLine((int Section, int Line, string Key, string? Count, List<RaceReportArg> Args) line) =>
        new(line.Key, line.Count, line.Args);
}
