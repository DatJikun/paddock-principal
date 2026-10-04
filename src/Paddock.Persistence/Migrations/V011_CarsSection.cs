using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Adds the tables of the <c>cars</c> world section (T41): the car counter, one row per car, driver-fit records and
/// laps per track. A save from before this migration has no cars section, which loads as no section, so its hash is unchanged.
/// Axes are milli-units of −1..1. Ratings are milli-units of 0..100. Person and organization ids are not foreign keys.
/// </summary>
public sealed class V011_CarsSection : ISaveMigration
{
    public int Version => 11;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Script;
        command.ExecuteNonQuery();
    }

    private const string Script = """
        CREATE TABLE cars_counter (
            id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
            next_car INTEGER NOT NULL CHECK (next_car >= 1),
            next_draw INTEGER NOT NULL CHECK (next_draw >= 1)
        ) STRICT;

        CREATE TABLE cars (
            number INTEGER NOT NULL PRIMARY KEY CHECK (number >= 1),
            organization_id TEXT NOT NULL CHECK (length(organization_id) > 0),
            season INTEGER NOT NULL,
            aero INTEGER NOT NULL CHECK (aero BETWEEN -1000 AND 1000),
            philosophy INTEGER NOT NULL CHECK (philosophy BETWEEN -1000 AND 1000),
            window_axis INTEGER NOT NULL CHECK (window_axis BETWEEN -1000 AND 1000),
            cooling INTEGER NOT NULL CHECK (cooling BETWEEN -1000 AND 1000),
            tyre INTEGER NOT NULL CHECK (tyre BETWEEN -1000 AND 1000),
            integration INTEGER NOT NULL CHECK (integration BETWEEN -1000 AND 1000),
            power INTEGER NOT NULL CHECK (power BETWEEN 0 AND 100000),
            downforce INTEGER NOT NULL CHECK (downforce BETWEEN 0 AND 100000),
            grip INTEGER NOT NULL CHECK (grip BETWEEN 0 AND 100000),
            braking INTEGER NOT NULL CHECK (braking BETWEEN 0 AND 100000),
            reliability INTEGER NOT NULL CHECK (reliability BETWEEN 0 AND 100000),
            ceiling INTEGER NOT NULL CHECK (ceiling BETWEEN 0 AND 100000),
            understanding INTEGER NOT NULL CHECK (understanding BETWEEN 0 AND 100000),
            wear INTEGER NOT NULL CHECK (wear > 0 AND wear <= 5000),
            supplier_cost INTEGER NOT NULL CHECK (supplier_cost >= 0),
            engine_key TEXT,
            driver_id TEXT
        ) STRICT;

        CREATE TABLE driver_fits (
            person_id TEXT NOT NULL PRIMARY KEY CHECK (length(person_id) > 0),
            balance INTEGER NOT NULL CHECK (balance BETWEEN -1000 AND 1000),
            traction INTEGER NOT NULL CHECK (traction BETWEEN -1000 AND 1000),
            braking_style TEXT NOT NULL CHECK (braking_style IN ('Early', 'Normal', 'Late')),
            starts INTEGER NOT NULL CHECK (starts >= 0),
            wet_races INTEGER NOT NULL CHECK (wet_races >= 0 AND wet_races <= starts),
            seasons_with_team INTEGER NOT NULL CHECK (seasons_with_team >= 0)
        ) STRICT;

        CREATE TABLE driver_track_laps (
            person_id TEXT NOT NULL REFERENCES driver_fits (person_id) ON DELETE CASCADE,
            track_id TEXT NOT NULL CHECK (length(track_id) > 0),
            laps INTEGER NOT NULL CHECK (laps >= 0),
            PRIMARY KEY (person_id, track_id)
        ) STRICT, WITHOUT ROWID;
        """;
}
