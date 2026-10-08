using System.Globalization;
using Microsoft.Data.Sqlite;
using Paddock.Domain.Sponsors;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Persistence;

/// <summary>Small SQL helpers shared by the T38 stores.</summary>
internal static class StoreSql
{
    public static void Run(SqliteConnection connection, SqliteTransaction transaction, string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        command.ExecuteNonQuery();
    }

    public static bool TableExists(SqliteConnection connection, SqliteTransaction transaction, string table)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name";
        command.Parameters.AddWithValue("$name", table);
        return command.ExecuteScalar() is not null;
    }

    public static GameDate Date(string text)
    {
        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new InvalidDataException($"Date '{text}' is malformed.");
        }

        return new GameDate(date.Year, date.Month, date.Day);
    }

    public static GameDate? OptionalDate(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Date(reader.GetString(ordinal));

    public static TEnum Enum<TEnum>(string text)
        where TEnum : struct, Enum
    {
        if (!System.Enum.TryParse<TEnum>(text, ignoreCase: false, out var value) || !System.Enum.IsDefined(value))
        {
            throw new InvalidDataException($"Value '{text}' is not a {typeof(TEnum).Name}.");
        }

        return value;
    }

    public static SlotKind Kind(string text) =>
        SlotKinds.TryParse(text, out var kind) ? kind : throw new InvalidDataException($"Slot kind '{text}' is unknown.");
}

/// <summary>Saves the <c>sponsors</c> section into the tables made by <see cref="V012_SponsorsAndObjectives"/> and widened by <see cref="V029_SponsorTerms"/>.</summary>
public sealed class SponsorsSectionStore : ISectionStore
{
    public string SectionName => SponsorsSection.SectionName;

    public int SchemaVersion => SponsorsSection.Empty.SchemaVersion;

    public void Replace(SqliteConnection connection, SqliteTransaction transaction, IWorldSection? section)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var sponsors = section switch
        {
            null => null,
            SponsorsSection typed => typed,
            _ => throw new ArgumentException($"The sponsors store cannot save a {section.GetType().Name}.", nameof(section)),
        };

        if (sponsors is null && !StoreSql.TableExists(connection, transaction, "sponsor_state"))
        {
            return;
        }

        foreach (var table in new[] { "sponsor_taken", "sponsor_trust", "sponsor_offers", "sponsor_deals", "sponsor_talks", "sponsor_state" })
        {
            StoreSql.Run(connection, transaction, "DELETE FROM " + table);
        }

        if (sponsors is null)
        {
            return;
        }

        StoreSql.Run(connection, transaction, "INSERT INTO sponsor_state (id, next_number) VALUES (1, $next)", ("$next", sponsors.Next));
        foreach (var talk in sponsors.Talks)
        {
            StoreSql.Run(
                connection,
                transaction,
                "INSERT INTO sponsor_talks (number, organization_id, sponsor_id, slot, kind, manager_id, opened, status, closed_on, rival, cap_milli, full_annual_cents, years, ambition) "
                + "VALUES ($n, $org, $sponsor, $slot, $kind, $manager, $opened, $status, $closed, $rival, $cap, $full, $years, $ambition)",
                ("$n", talk.Number),
                ("$org", talk.Organization.Value),
                ("$sponsor", talk.SponsorId),
                ("$slot", (long)talk.Slot),
                ("$kind", SlotKinds.KeyOf(talk.Kind)),
                ("$manager", talk.ManagerId),
                ("$opened", talk.Opened.ToString()),
                ("$status", talk.Status.ToString()),
                ("$closed", talk.ClosedOn?.ToString()),
                ("$rival", talk.Rival.ToString()),
                ("$cap", (long)talk.CapMilli),
                ("$full", talk.FullAnnualCents),
                ("$years", (long)talk.Years),
                ("$ambition", talk.Ambition.ToString()));
        }

        foreach (var deal in sponsors.Deals)
        {
            StoreSql.Run(
                connection,
                transaction,
                "INSERT INTO sponsor_deals (number, organization_id, sponsor_id, slot, kind, start_on, end_on, annual_cents, instalments_paid, objective_id, outcome, bonus_cents, status, ended_on, years, ambition, wish_nationality, wish_race_seat) "
                + "VALUES ($n, $org, $sponsor, $slot, $kind, $start, $end, $annual, $paid, $objective, $outcome, $bonus, $status, $ended, $years, $ambition, $wish, $seat)",
                ("$n", deal.Number),
                ("$org", deal.Organization.Value),
                ("$sponsor", deal.SponsorId),
                ("$slot", (long)deal.Slot),
                ("$kind", SlotKinds.KeyOf(deal.Kind)),
                ("$start", deal.Start.ToString()),
                ("$end", deal.End.ToString()),
                ("$annual", deal.AnnualCents),
                ("$paid", (long)deal.InstalmentsPaid),
                ("$objective", deal.ObjectiveId),
                ("$outcome", deal.Outcome.ToString()),
                ("$bonus", deal.BonusCents),
                ("$status", deal.Status.ToString()),
                ("$ended", deal.EndedOn?.ToString()),
                ("$years", (long)deal.Years),
                ("$ambition", deal.Ambition.ToString()),
                ("$wish", deal.WishNationality),
                ("$seat", deal.WishRaceSeat ? 1L : 0L));
        }

        foreach (var offer in sponsors.Offers)
        {
            StoreSql.Run(
                connection,
                transaction,
                "INSERT INTO sponsor_offers (number, deal_number, organization_id, sponsor_id, slot, kind, annual_cents, opened, valid_until, status, closed_on, years, ambition, rounds) "
                + "VALUES ($n, $deal, $org, $sponsor, $slot, $kind, $annual, $opened, $until, $status, $closed, $years, $ambition, $rounds)",
                ("$n", offer.Number),
                ("$deal", offer.DealNumber),
                ("$org", offer.Organization.Value),
                ("$sponsor", offer.SponsorId),
                ("$slot", (long)offer.Slot),
                ("$kind", SlotKinds.KeyOf(offer.Kind)),
                ("$annual", offer.AnnualCents),
                ("$opened", offer.Opened.ToString()),
                ("$until", offer.ValidUntil.ToString()),
                ("$status", offer.Status.ToString()),
                ("$closed", offer.ClosedOn?.ToString()),
                ("$years", (long)offer.Years),
                ("$ambition", offer.Ambition.ToString()),
                ("$rounds", (long)offer.Rounds));
        }

        foreach (var (sponsor, organization, trust) in sponsors.TrustRows())
        {
            StoreSql.Run(
                connection,
                transaction,
                "INSERT INTO sponsor_trust (sponsor_id, organization_id, trust) VALUES ($sponsor, $org, $trust)",
                ("$sponsor", sponsor),
                ("$org", organization),
                ("$trust", (long)trust));
        }

        foreach (var (sponsor, until) in sponsors.TakenRows())
        {
            StoreSql.Run(
                connection,
                transaction,
                "INSERT INTO sponsor_taken (sponsor_id, until_on) VALUES ($sponsor, $until)",
                ("$sponsor", sponsor),
                ("$until", until.ToString()));
        }
    }

    public IWorldSection Load(SqliteConnection connection, int storedSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (storedSchemaVersion is not (1 or 2))
        {
            throw new InvalidDataException($"The stored sponsors section has schema version {storedSchemaVersion}, which this build cannot read.");
        }

        long next;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT next_number FROM sponsor_state WHERE id = 1";
            next = command.ExecuteScalar() as long? ?? throw new InvalidDataException("The sponsors section is registered but its state row is missing.");
        }

        var talks = new List<SponsorTalk>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT number, organization_id, sponsor_id, slot, kind, manager_id, opened, status, closed_on, rival, cap_milli, full_annual_cents, years, ambition FROM sponsor_talks ORDER BY number";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                talks.Add(new SponsorTalk(
                    reader.GetInt64(0),
                    SponsorsSection.ParseOrganization(reader.GetString(1)),
                    reader.GetString(2),
                    checked((int)reader.GetInt64(3)),
                    StoreSql.Kind(reader.GetString(4)),
                    reader.GetString(5),
                    StoreSql.Date(reader.GetString(6)),
                    StoreSql.Enum<TalkStatus>(reader.GetString(7)),
                    StoreSql.OptionalDate(reader, 8),
                    StoreSql.Enum<RivalState>(reader.GetString(9)),
                    checked((int)reader.GetInt64(10)),
                    reader.GetInt64(11),
                    checked((int)reader.GetInt64(12)),
                    StoreSql.Enum<SponsorAmbition>(reader.GetString(13))));
            }
        }

        var deals = new List<SponsorDeal>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT number, organization_id, sponsor_id, slot, kind, start_on, end_on, annual_cents, instalments_paid, objective_id, outcome, bonus_cents, status, ended_on, years, ambition, wish_nationality, wish_race_seat FROM sponsor_deals ORDER BY number";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                deals.Add(new SponsorDeal(
                    reader.GetInt64(0),
                    SponsorsSection.ParseOrganization(reader.GetString(1)),
                    reader.GetString(2),
                    checked((int)reader.GetInt64(3)),
                    StoreSql.Kind(reader.GetString(4)),
                    StoreSql.Date(reader.GetString(5)),
                    StoreSql.Date(reader.GetString(6)),
                    reader.GetInt64(7),
                    checked((int)reader.GetInt64(8)),
                    reader.IsDBNull(9) ? null : reader.GetString(9),
                    StoreSql.Enum<DealObjectiveOutcome>(reader.GetString(10)),
                    reader.GetInt64(11),
                    StoreSql.Enum<DealStatus>(reader.GetString(12)),
                    StoreSql.OptionalDate(reader, 13),
                    checked((int)reader.GetInt64(14)),
                    StoreSql.Enum<SponsorAmbition>(reader.GetString(15)),
                    reader.IsDBNull(16) ? null : reader.GetString(16),
                    reader.GetInt64(17) == 1));
            }
        }

        var offers = new List<SponsorOffer>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT number, deal_number, organization_id, sponsor_id, slot, kind, annual_cents, opened, valid_until, status, closed_on, years, ambition, rounds FROM sponsor_offers ORDER BY number";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                offers.Add(new SponsorOffer(
                    reader.GetInt64(0),
                    reader.GetInt64(1),
                    SponsorsSection.ParseOrganization(reader.GetString(2)),
                    reader.GetString(3),
                    checked((int)reader.GetInt64(4)),
                    StoreSql.Kind(reader.GetString(5)),
                    reader.GetInt64(6),
                    StoreSql.Date(reader.GetString(7)),
                    StoreSql.Date(reader.GetString(8)),
                    StoreSql.Enum<OfferStatus>(reader.GetString(9)),
                    StoreSql.OptionalDate(reader, 10),
                    checked((int)reader.GetInt64(11)),
                    StoreSql.Enum<SponsorAmbition>(reader.GetString(12)),
                    checked((int)reader.GetInt64(13))));
            }
        }

        var trust = new List<(string, string, int)>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT sponsor_id, organization_id, trust FROM sponsor_trust ORDER BY sponsor_id, organization_id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                trust.Add((reader.GetString(0), reader.GetString(1), checked((int)reader.GetInt64(2))));
            }
        }

        var taken = new List<(string, GameDate)>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT sponsor_id, until_on FROM sponsor_taken ORDER BY sponsor_id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                taken.Add((reader.GetString(0), StoreSql.Date(reader.GetString(1))));
            }
        }

        try
        {
            return SponsorsSection.Restore(next, talks, deals, offers, trust, taken);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw new InvalidDataException("The stored sponsors section is inconsistent: " + exception.Message, exception);
        }
    }
}
