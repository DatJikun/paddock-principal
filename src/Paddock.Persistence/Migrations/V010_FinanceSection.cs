using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Adds the tables of the <c>finance</c> world section (T37): one state row (sequence counter, popularity, the season's
/// revenue snapshot), one row per organization's book, the append-only ledger lines, and the insolvency watches.
/// A save from before this migration has no finance section, which loads as no section, so its hash is unchanged.
/// Dates are <c>yyyy-MM-dd</c> text. Amounts are cents. A missing counterparty or warning is NULL.
/// </summary>
public sealed class V010_FinanceSection : ISaveMigration
{
    public int Version => 10;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Script;
        command.ExecuteNonQuery();
    }

    private const string Script = """
        CREATE TABLE finance_state (
            id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
            next_entry INTEGER NOT NULL CHECK (next_entry >= 1),
            popularity_milli INTEGER NOT NULL,
            season INTEGER NOT NULL CHECK (season >= 0),
            races INTEGER NOT NULL CHECK (races >= 0),
            completed INTEGER NOT NULL CHECK (completed >= 0),
            revenue_model TEXT NOT NULL,
            typical_cents INTEGER NOT NULL CHECK (typical_cents >= 0),
            warning_key TEXT
        ) STRICT;

        CREATE TABLE finance_books (
            organization_id TEXT NOT NULL PRIMARY KEY CHECK (length(organization_id) > 0),
            last_race_entries INTEGER NOT NULL CHECK (last_race_entries >= 0)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE finance_entries (
            sequence INTEGER NOT NULL PRIMARY KEY CHECK (sequence >= 1),
            organization_id TEXT NOT NULL REFERENCES finance_books (organization_id) ON DELETE CASCADE,
            on_date TEXT NOT NULL,
            category TEXT NOT NULL CHECK (category IN (
                'start_money', 'prize_money', 'sponsor', 'owner_funds', 'salary',
                'race_running', 'car_build', 'development', 'supply', 'other')),
            counterparty TEXT,
            amount_cents INTEGER NOT NULL CHECK (amount_cents <> 0),
            reason_key TEXT NOT NULL CHECK (length(reason_key) > 0)
        ) STRICT;

        CREATE INDEX finance_entries_by_org ON finance_entries (organization_id, sequence);

        CREATE TABLE finance_insolvency (
            organization_id TEXT NOT NULL PRIMARY KEY REFERENCES finance_books (organization_id) ON DELETE CASCADE,
            below_since TEXT NOT NULL,
            emitted INTEGER NOT NULL CHECK (emitted IN (0, 1))
        ) STRICT, WITHOUT ROWID;
        """;
}
