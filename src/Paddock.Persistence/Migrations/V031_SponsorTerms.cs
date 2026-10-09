using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Sponsor deals with terms (#268): a deal runs one to three years, has a condition of a chosen difficulty and can carry a nationality wish;
/// talks and renewal offers remember the terms on the table and an offer counts the rounds the player has used. Existing rows read as the
/// one-year, standard-condition deal they always were. <c>sponsor_deals</c> is rebuilt because its instalment CHECK (0 to 12) must allow 36.
/// Number is V031 because V030 (person gender, #265) is the newest on main and versions must be contiguous; the open PRs that
/// also add a migration (#300, #308) are renumbered by whoever merges second, and so is this one if it merges after them.
/// </summary>
public sealed class V031_SponsorTerms : ISaveMigration
{
    public int Version => 31;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Script;
        command.ExecuteNonQuery();
    }

    private const string Script = """
        ALTER TABLE sponsor_talks ADD COLUMN years INTEGER NOT NULL DEFAULT 1 CHECK (years BETWEEN 1 AND 3);
        ALTER TABLE sponsor_talks ADD COLUMN ambition TEXT NOT NULL DEFAULT 'Standard' CHECK (ambition IN ('Lighter', 'Standard', 'Harder'));

        ALTER TABLE sponsor_offers ADD COLUMN years INTEGER NOT NULL DEFAULT 1 CHECK (years BETWEEN 1 AND 3);
        ALTER TABLE sponsor_offers ADD COLUMN ambition TEXT NOT NULL DEFAULT 'Standard' CHECK (ambition IN ('Lighter', 'Standard', 'Harder'));
        ALTER TABLE sponsor_offers ADD COLUMN rounds INTEGER NOT NULL DEFAULT 0 CHECK (rounds >= 0);

        CREATE TABLE sponsor_deals_v30 (
            number INTEGER NOT NULL PRIMARY KEY CHECK (number >= 1),
            organization_id TEXT NOT NULL CHECK (length(organization_id) > 0),
            sponsor_id TEXT NOT NULL CHECK (length(sponsor_id) > 0),
            slot INTEGER NOT NULL CHECK (slot BETWEEN 1 AND 3),
            kind TEXT NOT NULL CHECK (kind IN ('technical', 'main', 'secondary')),
            start_on TEXT NOT NULL,
            end_on TEXT NOT NULL,
            annual_cents INTEGER NOT NULL CHECK (annual_cents >= 0),
            instalments_paid INTEGER NOT NULL CHECK (instalments_paid BETWEEN 0 AND 36),
            objective_id TEXT,
            outcome TEXT NOT NULL CHECK (outcome IN ('None', 'Met', 'Failed')),
            bonus_cents INTEGER NOT NULL CHECK (bonus_cents >= 0),
            status TEXT NOT NULL CHECK (status IN ('Active', 'Completed', 'Ended')),
            ended_on TEXT,
            years INTEGER NOT NULL DEFAULT 1 CHECK (years BETWEEN 1 AND 3),
            ambition TEXT NOT NULL DEFAULT 'Standard' CHECK (ambition IN ('Lighter', 'Standard', 'Harder')),
            wish_nationality TEXT,
            wish_race_seat INTEGER NOT NULL DEFAULT 0 CHECK (wish_race_seat IN (0, 1))
        ) STRICT;

        INSERT INTO sponsor_deals_v30 (number, organization_id, sponsor_id, slot, kind, start_on, end_on, annual_cents, instalments_paid,
            objective_id, outcome, bonus_cents, status, ended_on)
        SELECT number, organization_id, sponsor_id, slot, kind, start_on, end_on, annual_cents, instalments_paid,
            objective_id, outcome, bonus_cents, status, ended_on
        FROM sponsor_deals;

        DROP INDEX IF EXISTS sponsor_deals_by_org;
        DROP TABLE sponsor_deals;
        ALTER TABLE sponsor_deals_v30 RENAME TO sponsor_deals;
        CREATE INDEX sponsor_deals_by_org ON sponsor_deals (organization_id, number);
        """;
}
