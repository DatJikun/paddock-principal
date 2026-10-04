using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Adds the tables of the <c>development</c> world section (T42): the project counter, the plan of each team, its development
/// account and every project. A save from before this migration has no development section, which loads as no section, so its
/// hash is unchanged. Rows follow the rules of V003 and V005: dates are <c>yyyy-MM-dd</c> text, enums are stored by name, money is
/// integer cents, shares and stock are milli-units. Organization and person ids are not foreign keys.
/// </summary>
public sealed class V015_DevelopmentSection : ISaveMigration
{
    public int Version => 15;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Script;
        command.ExecuteNonQuery();
    }

    private const string Script = """
        CREATE TABLE development_counter (
            id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
            next_project INTEGER NOT NULL CHECK (next_project >= 1)
        ) STRICT;

        CREATE TABLE development_plans (
            organization_id TEXT NOT NULL PRIMARY KEY CHECK (length(organization_id) > 0),
            current_percent INTEGER NOT NULL CHECK (current_percent >= 0 AND current_percent <= 100),
            account_percent INTEGER NOT NULL CHECK (account_percent >= 0 AND account_percent <= 100),
            next_year_percent INTEGER NOT NULL CHECK (next_year_percent >= 0 AND next_year_percent <= 100),
            aero_priority INTEGER NOT NULL CHECK (aero_priority >= 0 AND aero_priority <= 10),
            chassis_priority INTEGER NOT NULL CHECK (chassis_priority >= 0 AND chassis_priority <= 10),
            reliability_priority INTEGER NOT NULL CHECK (reliability_priority >= 0 AND reliability_priority <= 10),
            tyres_priority INTEGER NOT NULL CHECK (tyres_priority >= 0 AND tyres_priority <= 10),
            spent_current INTEGER NOT NULL CHECK (spent_current >= 0),
            spent_account INTEGER NOT NULL CHECK (spent_account >= 0),
            spent_next_year INTEGER NOT NULL CHECK (spent_next_year >= 0),
            changed_on TEXT,
            CHECK (current_percent + account_percent + next_year_percent = 100)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE development_accounts (
            organization_id TEXT NOT NULL PRIMARY KEY CHECK (length(organization_id) > 0),
            stock_milli INTEGER NOT NULL CHECK (stock_milli >= 0 AND stock_milli <= 100000),
            next_year_share_milli INTEGER NOT NULL CHECK (next_year_share_milli >= 0 AND next_year_share_milli <= 1000),
            rules_year INTEGER NOT NULL CHECK (rules_year >= 0)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE development_projects (
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
            status TEXT NOT NULL CHECK (status IN ('Active', 'Ready', 'Completed', 'Failed', 'Cut', 'Deployed')),
            timing TEXT NOT NULL CHECK (timing IN ('WhenReady', 'AfterRaces', 'NextSeason')),
            timing_races INTEGER NOT NULL CHECK (timing_races >= 0),
            races_waited INTEGER NOT NULL CHECK (races_waited >= 0),
            closed_on TEXT
        ) STRICT;
        """;
}
