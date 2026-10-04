using Microsoft.Data.Sqlite;
using Paddock.Domain.Contracts;
using Paddock.Domain.Supply;
using Paddock.Domain.World;
using static Paddock.Persistence.SectionRows;

namespace Paddock.Persistence;

/// <summary>Saves the <c>supply</c> section into the tables made by <see cref="V015_SupplySection"/>.</summary>
public sealed class SupplySectionStore : ISectionStore
{
    public string SectionName => SupplySection.SectionName;

    public int SchemaVersion => SupplySection.Empty.SchemaVersion;

    public void Replace(SqliteConnection connection, SqliteTransaction transaction, IWorldSection? section)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var supply = section switch
        {
            null => null,
            SupplySection typed => typed,
            _ => throw new ArgumentException($"The supply store cannot save a {section.GetType().Name}.", nameof(section)),
        };

        // A file saved while it was still on an older schema has no supply tables, and no section to clear in them.
        if (supply is null && !TableExists(connection, transaction, "supply_counter"))
        {
            return;
        }

        foreach (var table in new[] { "supply_negotiation_reasons", "supply_negotiations", "supply_deals", "supply_counter" })
        {
            Run(connection, transaction, "DELETE FROM " + table);
        }

        if (supply is null)
        {
            return;
        }

        Run(
            connection,
            transaction,
            "INSERT INTO supply_counter (id, next_deal, next_negotiation) VALUES (1, $deal, $negotiation)",
            ("$deal", supply.NextDeal),
            ("$negotiation", supply.NextNegotiation));
        foreach (var deal in supply.Deals)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO supply_deals (number, item, kind, supplier_id, customer_id, first_season, seasons, price_cents, exclusive, signed, status, ended_on, paid_season, engine_name) "
                + "VALUES ($number, $item, $kind, $supplier, $customer, $first, $seasons, $price, $exclusive, $signed, $status, $ended, $paid, $engine)",
                ("$number", deal.Number),
                ("$item", deal.Item.ToString()),
                ("$kind", deal.Kind.ToString()),
                ("$supplier", deal.Supplier.Value),
                ("$customer", deal.Customer.Value),
                ("$first", (long)deal.FirstSeason),
                ("$seasons", (long)deal.Terms.Seasons),
                ("$price", deal.AnnualPriceCents),
                ("$exclusive", deal.Terms.Exclusive ? 1L : 0L),
                ("$signed", deal.Signed.ToString()),
                ("$status", deal.Status.ToString()),
                ("$ended", deal.EndedOn?.ToString()),
                ("$paid", (long)deal.PaidSeason),
                ("$engine", deal.EngineName));
        }

        foreach (var negotiation in supply.Negotiations)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO supply_negotiations (number, manager_id, customer_id, supplier_id, item, kind, first_season, opened, deadline, max_rounds, rounds_used, interest, status, "
                + "offer_price, offer_seasons, offer_exclusive, counter_price, counter_seasons, counter_exclusive, respond_on, closed_on, signed_deal) "
                + "VALUES ($number, $manager, $customer, $supplier, $item, $kind, $first, $opened, $deadline, $max, $used, $interest, $status, "
                + "$offerPrice, $offerSeasons, $offerExclusive, $counterPrice, $counterSeasons, $counterExclusive, $respond, $closed, $signed)",
                ("$number", negotiation.Number),
                ("$manager", negotiation.ManagerId),
                ("$customer", negotiation.Customer.Value),
                ("$supplier", negotiation.Supplier.Value),
                ("$item", negotiation.Item.ToString()),
                ("$kind", negotiation.Kind.ToString()),
                ("$first", (long)negotiation.FirstSeason),
                ("$opened", negotiation.Opened.ToString()),
                ("$deadline", negotiation.Deadline.ToString()),
                ("$max", (long)negotiation.MaxRounds),
                ("$used", (long)negotiation.RoundsUsed),
                ("$interest", (long)negotiation.Interest),
                ("$status", negotiation.Status.ToString()),
                ("$offerPrice", negotiation.Offer.AnnualPriceCents),
                ("$offerSeasons", (long)negotiation.Offer.Seasons),
                ("$offerExclusive", negotiation.Offer.Exclusive ? 1L : 0L),
                ("$counterPrice", negotiation.Counter?.AnnualPriceCents),
                ("$counterSeasons", negotiation.Counter is { } c1 ? (long)c1.Seasons : null),
                ("$counterExclusive", negotiation.Counter is { } c2 ? (c2.Exclusive ? 1L : 0L) : null),
                ("$respond", negotiation.RespondOn?.ToString()),
                ("$closed", negotiation.ClosedOn?.ToString()),
                ("$signed", negotiation.SignedDeal));
            var ordinal = 0L;
            foreach (var reason in negotiation.Reasons)
            {
                Run(
                    connection,
                    transaction,
                    "INSERT INTO supply_negotiation_reasons (negotiation, ordinal, reason_key) VALUES ($negotiation, $ordinal, $reason)",
                    ("$negotiation", negotiation.Number),
                    ("$ordinal", ordinal++),
                    ("$reason", reason));
            }
        }
    }

    public IWorldSection Load(SqliteConnection connection, int storedSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (storedSchemaVersion != 1)
        {
            throw new InvalidDataException($"The stored supply section has schema version {storedSchemaVersion}, which this build cannot read.");
        }

        long nextDeal;
        long nextNegotiation;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT next_deal, next_negotiation FROM supply_counter WHERE id = 1";
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidDataException("The supply section is registered but its counter is missing.");
            }

            nextDeal = reader.GetInt64(0);
            nextNegotiation = reader.GetInt64(1);
        }

        var deals = new List<SupplyDeal>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT number, item, kind, supplier_id, customer_id, first_season, seasons, price_cents, exclusive, signed, status, ended_on, paid_season, engine_name "
                + "FROM supply_deals ORDER BY number";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                deals.Add(new SupplyDeal(
                    reader.GetInt64(0),
                    ParseEnum<SupplyItem>(reader.GetString(1)),
                    ParseEnum<SupplyKind>(reader.GetString(2)),
                    OrganizationIdFrom(reader.GetString(3)),
                    OrganizationIdFrom(reader.GetString(4)),
                    checked((int)reader.GetInt64(5)),
                    new SupplyTerms(reader.GetInt64(7), checked((int)reader.GetInt64(6)), reader.GetInt64(8) == 1),
                    ParseDate(reader.GetString(9)),
                    ParseEnum<SupplyDealStatus>(reader.GetString(10)),
                    ParseOptionalDate(reader, 11),
                    checked((int)reader.GetInt64(12)),
                    reader.IsDBNull(13) ? null : reader.GetString(13)));
            }
        }

        var reasons = new Dictionary<long, List<string>>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT negotiation, reason_key FROM supply_negotiation_reasons ORDER BY negotiation, ordinal";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var number = reader.GetInt64(0);
                if (!reasons.TryGetValue(number, out var list))
                {
                    reasons[number] = list = [];
                }

                list.Add(reader.GetString(1));
            }
        }

        var negotiations = new List<SupplyNegotiation>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT number, manager_id, customer_id, supplier_id, item, kind, first_season, opened, deadline, max_rounds, rounds_used, interest, status, "
                + "offer_price, offer_seasons, offer_exclusive, counter_price, counter_seasons, counter_exclusive, respond_on, closed_on, signed_deal "
                + "FROM supply_negotiations ORDER BY number";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var number = reader.GetInt64(0);
                negotiations.Add(new SupplyNegotiation(
                    number,
                    reader.GetString(1),
                    OrganizationIdFrom(reader.GetString(2)),
                    OrganizationIdFrom(reader.GetString(3)),
                    ParseEnum<SupplyItem>(reader.GetString(4)),
                    ParseEnum<SupplyKind>(reader.GetString(5)),
                    checked((int)reader.GetInt64(6)),
                    ParseDate(reader.GetString(7)),
                    ParseDate(reader.GetString(8)),
                    checked((int)reader.GetInt64(9)),
                    checked((int)reader.GetInt64(10)),
                    checked((int)reader.GetInt64(11)),
                    ParseEnum<NegotiationStatus>(reader.GetString(12)),
                    new SupplyTerms(reader.GetInt64(13), checked((int)reader.GetInt64(14)), reader.GetInt64(15) == 1),
                    reader.IsDBNull(16) ? null : new SupplyTerms(reader.GetInt64(16), checked((int)reader.GetInt64(17)), reader.GetInt64(18) == 1),
                    ParseOptionalDate(reader, 19),
                    reasons.TryGetValue(number, out var list) ? list : [],
                    ParseOptionalDate(reader, 20),
                    reader.IsDBNull(21) ? null : reader.GetInt64(21)));
            }
        }

        return SupplySection.Restore(nextDeal, nextNegotiation, deals, negotiations);
    }
}
