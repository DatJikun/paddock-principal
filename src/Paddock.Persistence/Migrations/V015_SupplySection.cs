using Microsoft.Data.Sqlite;

namespace Paddock.Persistence;

/// <summary>
/// Adds the tables of the <c>supply</c> world section (T43): the counters, the supply deals, the negotiations and the reasons the
/// supplier gave. A save from before this migration has no supply section, which loads as no section (not an empty one), so its
/// hash is unchanged. Rows follow the rules of V003 and V005: dates are <c>yyyy-MM-dd</c> text, enums are stored by name, every
/// collection is a table, nothing is stored twice. Money is integer cents.
/// </summary>
public sealed class V015_SupplySection : ISaveMigration
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
        CREATE TABLE supply_counter (
            id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
            next_deal INTEGER NOT NULL CHECK (next_deal >= 1),
            next_negotiation INTEGER NOT NULL CHECK (next_negotiation >= 1)
        ) STRICT;

        CREATE TABLE supply_deals (
            number INTEGER NOT NULL PRIMARY KEY CHECK (number >= 1),
            item TEXT NOT NULL CHECK (item IN ('Engine', 'Tyres', 'Fuel')),
            kind TEXT NOT NULL CHECK (kind IN ('Works', 'Partner', 'Customer', 'LastYearEngine')),
            supplier_id TEXT NOT NULL CHECK (length(supplier_id) > 0),
            customer_id TEXT NOT NULL CHECK (length(customer_id) > 0),
            first_season INTEGER NOT NULL CHECK (first_season >= 1950),
            seasons INTEGER NOT NULL CHECK (seasons >= 1),
            price_cents INTEGER NOT NULL CHECK (price_cents >= 1),
            exclusive INTEGER NOT NULL CHECK (exclusive IN (0, 1)),
            signed TEXT NOT NULL,
            status TEXT NOT NULL CHECK (status IN ('Active', 'Ended', 'Voided')),
            ended_on TEXT,
            paid_season INTEGER NOT NULL CHECK (paid_season >= 0),
            engine_name TEXT,
            CHECK ((status = 'Active') = (ended_on IS NULL))
        ) STRICT;

        CREATE TABLE supply_negotiations (
            number INTEGER NOT NULL PRIMARY KEY CHECK (number >= 1),
            manager_id TEXT NOT NULL CHECK (length(manager_id) > 0),
            customer_id TEXT NOT NULL CHECK (length(customer_id) > 0),
            supplier_id TEXT NOT NULL CHECK (length(supplier_id) > 0),
            item TEXT NOT NULL CHECK (item IN ('Engine', 'Tyres', 'Fuel')),
            kind TEXT NOT NULL CHECK (kind IN ('Works', 'Partner', 'Customer', 'LastYearEngine')),
            first_season INTEGER NOT NULL CHECK (first_season >= 1950),
            opened TEXT NOT NULL,
            deadline TEXT NOT NULL,
            max_rounds INTEGER NOT NULL CHECK (max_rounds >= 1),
            rounds_used INTEGER NOT NULL CHECK (rounds_used >= 0),
            interest INTEGER NOT NULL CHECK (interest >= 0 AND interest <= 1000),
            status TEXT NOT NULL CHECK (status IN ('AwaitingResponse', 'Countered', 'Agreed', 'Refused', 'WalkedAway', 'Lapsed')),
            offer_price INTEGER NOT NULL CHECK (offer_price >= 1),
            offer_seasons INTEGER NOT NULL CHECK (offer_seasons >= 1),
            offer_exclusive INTEGER NOT NULL CHECK (offer_exclusive IN (0, 1)),
            counter_price INTEGER,
            counter_seasons INTEGER,
            counter_exclusive INTEGER,
            respond_on TEXT,
            closed_on TEXT,
            signed_deal INTEGER,
            CHECK ((counter_price IS NULL) = (counter_seasons IS NULL) AND (counter_price IS NULL) = (counter_exclusive IS NULL))
        ) STRICT;

        CREATE TABLE supply_negotiation_reasons (
            negotiation INTEGER NOT NULL,
            ordinal INTEGER NOT NULL CHECK (ordinal >= 0),
            reason_key TEXT NOT NULL CHECK (length(reason_key) > 0),
            PRIMARY KEY (negotiation, ordinal)
        ) STRICT, WITHOUT ROWID;
        """;
}
