using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Team infrastructure (PP-064, #231): facility and booked-test tables, and a wider ledger category CHECK so
/// <c>infrastructure</c> and <c>logistics</c> lines can be saved. SQLite cannot change a CHECK, so
/// <c>finance_entries</c> is rebuilt; existing rows copy as they are and hashes of saves without those categories
/// stay the same. Number is V027 because V025 is race-result details (#246) and V026 is the race calendar (#249).
/// </summary>
public sealed class V027_InfrastructureSection : ISaveMigration
{
    public int Version => 27;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Script;
        command.ExecuteNonQuery();
    }

    private const string Script = """
        CREATE TABLE infrastructure_facilities (
            organization_id TEXT NOT NULL CHECK (length(organization_id) > 0),
            kind TEXT NOT NULL CHECK (kind IN ('Factory', 'WindTunnel', 'Cfd', 'Simulator')),
            quality_milli INTEGER NOT NULL CHECK (quality_milli >= 0 AND quality_milli <= 400000),
            build_started TEXT,
            build_ends TEXT,
            target_quality_milli INTEGER CHECK (target_quality_milli IS NULL OR (target_quality_milli >= 0 AND target_quality_milli <= 400000)),
            cost_cents INTEGER NOT NULL CHECK (cost_cents >= 0),
            PRIMARY KEY (organization_id, kind)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE infrastructure_tests (
            organization_id TEXT NOT NULL CHECK (length(organization_id) > 0),
            booked_on TEXT NOT NULL,
            cost_cents INTEGER NOT NULL CHECK (cost_cents >= 1)
        ) STRICT;

        CREATE TABLE finance_entries_v27 (
            sequence INTEGER NOT NULL PRIMARY KEY CHECK (sequence >= 1),
            organization_id TEXT NOT NULL REFERENCES finance_books (organization_id) ON DELETE CASCADE,
            on_date TEXT NOT NULL,
            category TEXT NOT NULL CHECK (category IN (
                'start_money', 'prize_money', 'sponsor', 'owner_funds', 'salary',
                'race_running', 'car_build', 'development', 'supply', 'infrastructure', 'logistics', 'other')),
            counterparty TEXT,
            amount_cents INTEGER NOT NULL CHECK (amount_cents <> 0),
            reason_key TEXT NOT NULL CHECK (length(reason_key) > 0)
        ) STRICT;

        INSERT INTO finance_entries_v27
        SELECT sequence, organization_id, on_date, category, counterparty, amount_cents, reason_key
        FROM finance_entries;

        DROP INDEX IF EXISTS finance_entries_by_org;
        DROP TABLE finance_entries;
        ALTER TABLE finance_entries_v27 RENAME TO finance_entries;
        CREATE INDEX finance_entries_by_org ON finance_entries (organization_id, sequence);
        """;
}
