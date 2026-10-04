using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Adds the tables of the <c>contracts</c> world section (T39): the terms of signed contracts beyond the T15 fields, the
/// negotiations with their offers, history and stated reasons, the renewal prompts already sent, and the negotiation counter.
/// A save from before this migration has no contracts section, which loads as no section (not an empty one), so its hash is unchanged.
/// Rows follow the rules of V003 and V005: dates are <c>yyyy-MM-dd</c> text, enums are stored by name, every collection is a
/// table, nothing is stored twice. Contract ids are not foreign keys: the world tables are written first in the same
/// transaction, and a retired person's contracts are dropped from the world while their terms are pruned a season later.
/// </summary>
public sealed class V009_ContractsSection : ISaveMigration
{
    public int Version => 9;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Script;
        command.ExecuteNonQuery();
    }

    private const string Script = """
        CREATE TABLE contracts_counter (
            id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
            next_negotiation INTEGER NOT NULL CHECK (next_negotiation >= 1)
        ) STRICT;

        CREATE TABLE contract_terms (
            contract_id TEXT NOT NULL PRIMARY KEY CHECK (length(contract_id) > 0),
            points_bonus INTEGER NOT NULL CHECK (points_bonus >= 0),
            win_bonus INTEGER NOT NULL CHECK (win_bonus >= 0),
            title_bonus INTEGER NOT NULL CHECK (title_bonus >= 0),
            option_holder TEXT CHECK (option_holder IS NULL OR option_holder IN ('Team', 'Person')),
            exit_position INTEGER CHECK (exit_position IS NULL OR exit_position >= 1)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE contract_renewal_prompts (
            contract_id TEXT NOT NULL PRIMARY KEY CHECK (length(contract_id) > 0)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE negotiations (
            number INTEGER NOT NULL PRIMARY KEY CHECK (number >= 1),
            manager_id TEXT NOT NULL CHECK (length(manager_id) > 0),
            proposer_id TEXT NOT NULL CHECK (length(proposer_id) > 0),
            person_id TEXT NOT NULL CHECK (length(person_id) > 0),
            subject TEXT NOT NULL CHECK (length(subject) > 0),
            renewal_of TEXT,
            opened TEXT NOT NULL,
            deadline TEXT NOT NULL,
            max_rounds INTEGER NOT NULL CHECK (max_rounds >= 1),
            rounds_used INTEGER NOT NULL CHECK (rounds_used >= 0 AND rounds_used <= max_rounds),
            interest INTEGER NOT NULL CHECK (interest >= 0 AND interest <= 1000),
            status TEXT NOT NULL CHECK (status IN (
                'Open', 'AwaitingResponse', 'Countered', 'PersonAgreed', 'Considering',
                'Agreed', 'Refused', 'WalkedAway', 'Lost', 'Lapsed')),
            respond_on TEXT,
            considered_utility INTEGER NOT NULL,
            closed_on TEXT,
            signed_contract TEXT
        ) STRICT;

        CREATE INDEX negotiations_by_manager ON negotiations (manager_id, number);
        CREATE INDEX negotiations_by_person ON negotiations (person_id, number);

        -- One row per set of terms. slot is 'offer', 'counter', or 'round:{ordinal}' for a history line.
        CREATE TABLE negotiation_terms (
            negotiation_number INTEGER NOT NULL REFERENCES negotiations (number) ON DELETE CASCADE,
            slot TEXT NOT NULL CHECK (length(slot) > 0),
            salary INTEGER NOT NULL CHECK (salary >= 0),
            points_bonus INTEGER NOT NULL CHECK (points_bonus >= 0),
            win_bonus INTEGER NOT NULL CHECK (win_bonus >= 0),
            title_bonus INTEGER NOT NULL CHECK (title_bonus >= 0),
            years INTEGER NOT NULL CHECK (years >= 1),
            seat TEXT CHECK (seat IS NULL OR seat IN ('NumberOne', 'Equal', 'NumberTwo', 'Reserve')),
            option_holder TEXT CHECK (option_holder IS NULL OR option_holder IN ('Team', 'Person')),
            option_years INTEGER CHECK (option_years IS NULL OR option_years >= 1),
            exit_position INTEGER CHECK (exit_position IS NULL OR exit_position >= 1),
            CHECK ((option_holder IS NULL) = (option_years IS NULL)),
            PRIMARY KEY (negotiation_number, slot)
        ) STRICT, WITHOUT ROWID;

        CREATE TABLE negotiation_rounds (
            negotiation_number INTEGER NOT NULL REFERENCES negotiations (number) ON DELETE CASCADE,
            ordinal INTEGER NOT NULL CHECK (ordinal >= 0),
            round_number INTEGER NOT NULL CHECK (round_number >= 1),
            kind TEXT NOT NULL CHECK (kind IN ('Offer', 'Counter', 'Acceptance', 'Refusal')),
            on_date TEXT NOT NULL,
            PRIMARY KEY (negotiation_number, ordinal)
        ) STRICT, WITHOUT ROWID;

        -- Reasons a person stated. scope is 'last' for the latest answer, or 'round:{ordinal}' for a history line.
        CREATE TABLE negotiation_reasons (
            negotiation_number INTEGER NOT NULL REFERENCES negotiations (number) ON DELETE CASCADE,
            scope TEXT NOT NULL CHECK (length(scope) > 0),
            ordinal INTEGER NOT NULL CHECK (ordinal >= 0),
            reason_key TEXT NOT NULL CHECK (length(reason_key) > 0),
            PRIMARY KEY (negotiation_number, scope, ordinal)
        ) STRICT, WITHOUT ROWID;
        """;
}
