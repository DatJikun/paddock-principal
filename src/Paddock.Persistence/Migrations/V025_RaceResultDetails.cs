using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Adds the details of a finished round to the <c>race-results</c> section (#230): per row the grid position, laps
/// completed, race time at the flag and best lap; per race the laps run, the lap length, the pole sitter and the fastest lap.
/// Every column is nullable. A round stored before this migration keeps NULL, which loads as "details not kept"
/// (the tape is not stored, so they cannot be rebuilt), and its canonical text does not change.
/// </summary>
public sealed class V025_RaceResultDetails : ISaveMigration
{
    public int Version => 25;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            ALTER TABLE race_result_rows ADD COLUMN grid_position INTEGER CHECK (grid_position IS NULL OR grid_position >= 1);
            ALTER TABLE race_result_rows ADD COLUMN laps_completed INTEGER CHECK (laps_completed IS NULL OR laps_completed >= 0);
            ALTER TABLE race_result_rows ADD COLUMN race_time_ms INTEGER CHECK (race_time_ms IS NULL OR race_time_ms >= 0);
            ALTER TABLE race_result_rows ADD COLUMN fastest_lap_ms INTEGER CHECK (fastest_lap_ms IS NULL OR fastest_lap_ms >= 0);

            ALTER TABLE race_results ADD COLUMN laps INTEGER CHECK (laps IS NULL OR laps >= 0);
            ALTER TABLE race_results ADD COLUMN lap_length_m INTEGER CHECK (lap_length_m IS NULL OR lap_length_m >= 0);
            ALTER TABLE race_results ADD COLUMN pole_driver_id TEXT CHECK (pole_driver_id IS NULL OR length(pole_driver_id) > 0);
            ALTER TABLE race_results ADD COLUMN pole_time_ms INTEGER CHECK (pole_time_ms IS NULL OR pole_time_ms >= 0);
            ALTER TABLE race_results ADD COLUMN fastest_lap_driver_id TEXT CHECK (fastest_lap_driver_id IS NULL OR length(fastest_lap_driver_id) > 0);
            ALTER TABLE race_results ADD COLUMN fastest_race_lap_ms INTEGER CHECK (fastest_race_lap_ms IS NULL OR fastest_race_lap_ms >= 0);
            """;
        command.ExecuteNonQuery();
    }
}
