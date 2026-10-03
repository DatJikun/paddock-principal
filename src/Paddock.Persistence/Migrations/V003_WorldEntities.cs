using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Adds the tables that hold the world (TECH §6.2): persons, organizations, contracts, beliefs, the id
/// counters, scheduled events, the command log, and the manager registry.
/// A V001 or V002 save has no world, so these tables start empty and <see cref="WorldRepository.HasWorld"/> is false.
/// <para>
/// Layout rules. Dates are <c>yyyy-MM-dd</c> text, which sorts in date order. Enums are stored by name.
/// Every row of a collection is a table row, not a JSON blob, so the file can be queried and compacted later.
/// Nothing is stored twice: truth and potential share one row per attribute, a lineage edge is one row
/// (the predecessor and successor sides are derived), and an issued id is stored only while its entity is
/// gone (<c>retired_ids</c>), because the present ones are the primary keys of the entity tables.
/// </para>
/// </summary>
public sealed class V003_WorldEntities : ISaveMigration
{
    public int Version => 3;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Script;
        command.ExecuteNonQuery();
    }

    // Statements run one script, inside the migration transaction.
    private const string Script = """
        CREATE TABLE persons (
            id TEXT NOT NULL PRIMARY KEY,
            is_real INTEGER NOT NULL CHECK (is_real IN (0, 1)),
            given_name TEXT NOT NULL CHECK (length(given_name) > 0),
            family_name TEXT NOT NULL CHECK (length(family_name) > 0),
            birth_date TEXT NOT NULL,
            nationality TEXT NOT NULL CHECK (length(nationality) > 0),
            CHECK ((is_real = 1) = (substr(id, 1, 4) <> 'gen:'))
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE person_roles (
            person_id TEXT NOT NULL REFERENCES persons (id) ON DELETE CASCADE,
            role TEXT NOT NULL CHECK (length(role) > 0),
            PRIMARY KEY (person_id, role)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE person_attributes (
            person_id TEXT NOT NULL REFERENCES persons (id) ON DELETE CASCADE,
            attribute_key TEXT NOT NULL CHECK (length(attribute_key) > 0),
            value INTEGER NOT NULL CHECK (value BETWEEN 1 AND 20),
            potential INTEGER NOT NULL CHECK (potential BETWEEN 1 AND 20 AND potential >= value),
            PRIMARY KEY (person_id, attribute_key)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE organizations (
            id TEXT NOT NULL PRIMARY KEY,
            kind TEXT NOT NULL CHECK (kind IN ('Team', 'EngineSupplier', 'Sponsor', 'SeriesBody')),
            is_real INTEGER NOT NULL CHECK (is_real IN (0, 1)),
            founded TEXT NOT NULL,
            dissolved TEXT,
            budget INTEGER NOT NULL,
            CHECK ((is_real = 1) = (substr(id, 1, 4) <> 'org:')),
            CHECK (dissolved IS NULL OR dissolved >= founded)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE org_names (
            org_id TEXT NOT NULL REFERENCES organizations (id) ON DELETE CASCADE,
            from_date TEXT NOT NULL,
            to_date TEXT,
            name TEXT NOT NULL CHECK (length(name) > 0),
            PRIMARY KEY (org_id, from_date),
            CHECK (to_date IS NULL OR to_date >= from_date)
        ) STRICT, WITHOUT ROWID;

        -- One row per lineage edge. The domain keeps a link on both organizations; the loader derives both.
        -- A lineage is a single chain, so an organization has at most one successor and one predecessor.
        CREATE TABLE org_lineage (
            predecessor_id TEXT NOT NULL PRIMARY KEY REFERENCES organizations (id),
            successor_id TEXT NOT NULL UNIQUE REFERENCES organizations (id),
            from_date TEXT NOT NULL,
            to_date TEXT,
            CHECK (predecessor_id <> successor_id),
            CHECK (to_date IS NULL OR to_date >= from_date)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE contracts (
            id TEXT NOT NULL PRIMARY KEY,
            person_id TEXT NOT NULL REFERENCES persons (id),
            organization_id TEXT NOT NULL REFERENCES organizations (id),
            role TEXT NOT NULL CHECK (length(role) > 0),
            exclusive INTEGER NOT NULL CHECK (exclusive IN (0, 1)),
            start_date TEXT NOT NULL,
            end_date TEXT NOT NULL,
            salary INTEGER NOT NULL CHECK (salary >= 0),
            option_deadline TEXT,
            option_extra_years INTEGER,
            release_amount INTEGER CHECK (release_amount IS NULL OR release_amount >= 0),
            CHECK (substr(id, 1, 4) = 'con:'),
            CHECK (end_date >= start_date),
            CHECK ((option_deadline IS NULL) = (option_extra_years IS NULL)),
            CHECK (option_extra_years IS NULL OR option_extra_years >= 1)
        ) STRICT, WITHOUT ROWID;
        CREATE INDEX contracts_by_person ON contracts (person_id);
        CREATE INDEX contracts_by_organization ON contracts (organization_id);

        -- What one organization believes about one person (INV-003). Bands only, never truth.
        CREATE TABLE knowledge (
            observer_id TEXT NOT NULL REFERENCES organizations (id),
            subject_id TEXT NOT NULL REFERENCES persons (id),
            potential_low INTEGER,
            potential_high INTEGER,
            PRIMARY KEY (observer_id, subject_id),
            CHECK ((potential_low IS NULL) = (potential_high IS NULL)),
            CHECK (potential_low IS NULL OR (potential_low BETWEEN 1 AND 20 AND potential_high BETWEEN potential_low AND 20))
        ) STRICT, WITHOUT ROWID;
        CREATE INDEX knowledge_by_subject ON knowledge (subject_id);

        CREATE TABLE knowledge_bands (
            observer_id TEXT NOT NULL,
            subject_id TEXT NOT NULL,
            attribute_key TEXT NOT NULL CHECK (length(attribute_key) > 0),
            low INTEGER NOT NULL CHECK (low BETWEEN 1 AND 20),
            high INTEGER NOT NULL CHECK (high BETWEEN low AND 20),
            PRIMARY KEY (observer_id, subject_id, attribute_key),
            FOREIGN KEY (observer_id, subject_id) REFERENCES knowledge (observer_id, subject_id) ON DELETE CASCADE
        ) STRICT, WITHOUT ROWID;

        -- Next value of each counter that never goes backwards (INV-009). The first three belong to the world
        -- allocator. The others belong to the day clock and the command queue. A save without a 'person' row has no world.
        CREATE TABLE id_counters (
            name TEXT NOT NULL PRIMARY KEY CHECK (name IN ('person', 'organization', 'contract', 'event_id', 'event_sequence', 'submission')),
            next_value INTEGER NOT NULL CHECK (next_value >= 1)
        ) STRICT, WITHOUT ROWID;

        -- Ids that were issued and whose entity is gone. They stay burned so no id is ever reused.
        CREATE TABLE retired_ids (
            id TEXT NOT NULL PRIMARY KEY CHECK (length(id) > 0)
        ) STRICT, WITHOUT ROWID;

        -- The day clock's queue. The payload is opaque to this layer: a type tag and the text its owner wrote.
        CREATE TABLE scheduled_events (
            id TEXT NOT NULL PRIMARY KEY,
            event_date TEXT NOT NULL,
            sequence INTEGER NOT NULL UNIQUE CHECK (sequence >= 0),
            type_id TEXT NOT NULL CHECK (length(type_id) > 0),
            payload_type TEXT NOT NULL CHECK (length(payload_type) > 0),
            payload TEXT NOT NULL
        ) STRICT, WITHOUT ROWID;
        CREATE INDEX scheduled_events_by_date ON scheduled_events (event_date, sequence);

        CREATE TABLE managers (
            id TEXT NOT NULL PRIMARY KEY CHECK (length(id) > 0),
            ordinal INTEGER NOT NULL UNIQUE CHECK (ordinal >= 0),
            kind TEXT NOT NULL CHECK (kind IN ('Human', 'Ai')),
            display_name TEXT NOT NULL CHECK (length(display_name) > 0),
            blocking_kind TEXT
        ) STRICT, WITHOUT ROWID;

        -- Accepted commands. position is the execution order, which is not always submission order because the
        -- queue sorts by issue date first. The command body is opaque here, like an event payload.
        CREATE TABLE command_log (
            position INTEGER NOT NULL PRIMARY KEY CHECK (position >= 0),
            submission_number INTEGER NOT NULL UNIQUE CHECK (submission_number >= 1),
            manager_id TEXT NOT NULL REFERENCES managers (id),
            issued_on TEXT NOT NULL,
            command_type TEXT NOT NULL CHECK (length(command_type) > 0),
            payload TEXT NOT NULL
        ) STRICT;
        CREATE INDEX command_log_by_manager ON command_log (manager_id);
        """;
}
