using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Concept production (T42c): a committed concept has a finish date and a production cost. SQLite cannot change a CHECK, and the
/// V016 checks of <c>development_projects</c> neither know the status <c>InProduction</c> nor the timing <c>Hold</c> (T42b), so
/// the table is rebuilt with the wider checks and the two new columns. Existing rows are copied as they are; a save from before
/// this migration loads with no production anywhere, and its section text and hash do not change.
/// </summary>
public sealed class V017_ConceptProduction : ISaveMigration
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
        CREATE TABLE development_projects_v17 (
            number INTEGER NOT NULL PRIMARY KEY CHECK (number >= 1),
            organization_id TEXT NOT NULL CHECK (length(organization_id) > 0),
            kind TEXT NOT NULL CHECK (kind IN ('Upgrade', 'Research', 'Concept')),
            area TEXT CHECK (area IS NULL OR area IN ('Aero', 'Chassis', 'Reliability', 'TyresHandling')),
            engineer TEXT NOT NULL CHECK (length(engineer) > 0),
            started TEXT NOT NULL,
            duration_days INTEGER NOT NULL CHECK (duration_days >= 1),
            progress_days INTEGER NOT NULL CHECK (progress_days >= 0),
            cost INTEGER NOT NULL CHECK (cost >= 0),
            posted INTEGER NOT NULL CHECK (posted >= 0),
            share_milli INTEGER NOT NULL CHECK (share_milli >= 0 AND share_milli <= 1000),
            risk_milli INTEGER NOT NULL CHECK (risk_milli >= 0 AND risk_milli <= 1000),
            outcome_milli INTEGER CHECK (outcome_milli IS NULL OR (outcome_milli >= 0 AND outcome_milli <= 1000)),
            status TEXT NOT NULL CHECK (status IN ('Active', 'Ready', 'Completed', 'Failed', 'Cut', 'Deployed', 'InProduction')),
            timing TEXT NOT NULL CHECK (timing IN ('WhenReady', 'AfterRaces', 'NextSeason', 'Hold')),
            timing_races INTEGER NOT NULL CHECK (timing_races >= 0),
            races_waited INTEGER NOT NULL CHECK (races_waited >= 0),
            closed_on TEXT,
            production_ends TEXT,
            production_cost INTEGER NOT NULL DEFAULT 0 CHECK (production_cost >= 0),
            CHECK (status <> 'InProduction' OR production_ends IS NOT NULL)
        ) STRICT;

        INSERT INTO development_projects_v17 (number, organization_id, kind, area, engineer, started, duration_days, progress_days, cost, posted,
            share_milli, risk_milli, outcome_milli, status, timing, timing_races, races_waited, closed_on)
        SELECT number, organization_id, kind, area, engineer, started, duration_days, progress_days, cost, posted,
            share_milli, risk_milli, outcome_milli, status, timing, timing_races, races_waited, closed_on
        FROM development_projects;

        DROP TABLE development_projects;
        ALTER TABLE development_projects_v17 RENAME TO development_projects;
        """;
}
