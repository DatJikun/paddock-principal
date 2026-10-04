using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Adds the tables of two world sections (T38). The <c>sponsors</c> section: counter, talks, deals, renewal offers, trust and
/// the sponsors a rival has taken. The <c>objectives</c> section (T36), which had no store until a system granted objectives:
/// the objectives and the arguments of their effects. Dates are <c>yyyy-MM-dd</c> text, money is cents, a missing value is NULL.
/// A save from before this migration has neither section, which loads as no section, so its hash is unchanged.
/// </summary>
public sealed class V012_SponsorsAndObjectives : ISaveMigration
{
    public int Version => 12;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Script;
        command.ExecuteNonQuery();
    }

    private const string Script = """
        CREATE TABLE sponsor_state (
            id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
            next_number INTEGER NOT NULL CHECK (next_number >= 1)
        ) STRICT;

        CREATE TABLE sponsor_talks (
            number INTEGER NOT NULL PRIMARY KEY CHECK (number >= 1),
            organization_id TEXT NOT NULL CHECK (length(organization_id) > 0),
            sponsor_id TEXT NOT NULL CHECK (length(sponsor_id) > 0),
            slot INTEGER NOT NULL CHECK (slot BETWEEN 1 AND 3),
            kind TEXT NOT NULL CHECK (kind IN ('technical', 'main', 'secondary')),
            manager_id TEXT NOT NULL CHECK (length(manager_id) > 0),
            opened TEXT NOT NULL,
            status TEXT NOT NULL CHECK (status IN ('Open', 'Signed', 'WalkedAway', 'LostToRival')),
            closed_on TEXT,
            rival TEXT NOT NULL CHECK (rival IN ('Undecided', 'Absent', 'Present')),
            cap_milli INTEGER NOT NULL CHECK (cap_milli > 0),
            full_annual_cents INTEGER NOT NULL CHECK (full_annual_cents >= 0)
        ) STRICT;

        CREATE TABLE sponsor_deals (
            number INTEGER NOT NULL PRIMARY KEY CHECK (number >= 1),
            organization_id TEXT NOT NULL CHECK (length(organization_id) > 0),
            sponsor_id TEXT NOT NULL CHECK (length(sponsor_id) > 0),
            slot INTEGER NOT NULL CHECK (slot BETWEEN 1 AND 3),
            kind TEXT NOT NULL CHECK (kind IN ('technical', 'main', 'secondary')),
            start_on TEXT NOT NULL,
            end_on TEXT NOT NULL,
            annual_cents INTEGER NOT NULL CHECK (annual_cents >= 0),
            instalments_paid INTEGER NOT NULL CHECK (instalments_paid BETWEEN 0 AND 12),
            objective_id TEXT,
            outcome TEXT NOT NULL CHECK (outcome IN ('None', 'Met', 'Failed')),
            bonus_cents INTEGER NOT NULL CHECK (bonus_cents >= 0),
            status TEXT NOT NULL CHECK (status IN ('Active', 'Completed', 'Ended')),
            ended_on TEXT
        ) STRICT;

        CREATE INDEX sponsor_deals_by_org ON sponsor_deals (organization_id, number);

        CREATE TABLE sponsor_offers (
            number INTEGER NOT NULL PRIMARY KEY CHECK (number >= 1),
            deal_number INTEGER NOT NULL,
            organization_id TEXT NOT NULL CHECK (length(organization_id) > 0),
            sponsor_id TEXT NOT NULL CHECK (length(sponsor_id) > 0),
            slot INTEGER NOT NULL CHECK (slot BETWEEN 1 AND 3),
            kind TEXT NOT NULL CHECK (kind IN ('technical', 'main', 'secondary')),
            annual_cents INTEGER NOT NULL CHECK (annual_cents >= 0),
            opened TEXT NOT NULL,
            valid_until TEXT NOT NULL,
            status TEXT NOT NULL CHECK (status IN ('Open', 'Accepted', 'Declined', 'Lapsed')),
            closed_on TEXT
        ) STRICT;

        CREATE TABLE sponsor_trust (
            sponsor_id TEXT NOT NULL,
            organization_id TEXT NOT NULL,
            trust INTEGER NOT NULL CHECK (trust BETWEEN 0 AND 100),
            PRIMARY KEY (sponsor_id, organization_id)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE sponsor_taken (
            sponsor_id TEXT NOT NULL PRIMARY KEY,
            until_on TEXT NOT NULL
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE objectives_state (
            id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
            next_number INTEGER NOT NULL CHECK (next_number >= 1)
        ) STRICT;

        CREATE TABLE objectives (
            number INTEGER NOT NULL PRIMARY KEY CHECK (number >= 1),
            owner_id TEXT NOT NULL CHECK (length(owner_id) > 0),
            grantor_id TEXT NOT NULL CHECK (length(grantor_id) > 0),
            kind_key TEXT NOT NULL CHECK (length(kind_key) > 0),
            reason_key TEXT NOT NULL CHECK (length(reason_key) > 0),
            predicate_name TEXT NOT NULL CHECK (length(predicate_name) > 0),
            predicate_parameter TEXT NOT NULL,
            baseline TEXT,
            created_on TEXT NOT NULL,
            deadline_on TEXT NOT NULL,
            met_key TEXT NOT NULL CHECK (length(met_key) > 0),
            failed_key TEXT NOT NULL CHECK (length(failed_key) > 0),
            status TEXT NOT NULL CHECK (status IN ('Open', 'Met', 'Failed')),
            settled_on TEXT
        ) STRICT;

        CREATE TABLE objective_effect_arguments (
            objective_number INTEGER NOT NULL REFERENCES objectives (number) ON DELETE CASCADE,
            effect TEXT NOT NULL CHECK (effect IN ('met', 'failed')),
            name TEXT NOT NULL CHECK (length(name) > 0),
            value TEXT NOT NULL,
            PRIMARY KEY (objective_number, effect, name)
        ) STRICT, WITHOUT ROWID;
        """;
}
