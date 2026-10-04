using System.Globalization;
using Microsoft.Data.Sqlite;
using Paddock.Domain.Finance;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Persistence;

/// <summary>Saves the <c>finance</c> section into the tables made by <see cref="V010_FinanceSection"/>.</summary>
public sealed class FinanceSectionStore : ISectionStore
{
    public string SectionName => FinanceSection.SectionName;

    public int SchemaVersion => FinanceSection.Empty.SchemaVersion;

    public void Replace(SqliteConnection connection, SqliteTransaction transaction, IWorldSection? section)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var finance = section switch
        {
            null => null,
            FinanceSection typed => typed,
            _ => throw new ArgumentException($"The finance store cannot save a {section.GetType().Name}.", nameof(section)),
        };

        if (finance is null && !TableExists(connection, transaction, "finance_state"))
        {
            return;
        }

        foreach (var table in new[] { "finance_insolvency", "finance_entries", "finance_books", "finance_state" })
        {
            Run(connection, transaction, "DELETE FROM " + table, []);
        }

        if (finance is null)
        {
            return;
        }

        Run(
            connection,
            transaction,
            "INSERT INTO finance_state (id, next_entry, popularity_milli, season, races, completed, revenue_model, typical_cents, warning_key) "
            + "VALUES (1, $next, $pop, $season, $races, $done, $model, $typical, $warning)",
            [
                ("$next", finance.NextSequence),
                ("$pop", (long)finance.PopularityMilli),
                ("$season", (long)finance.Season),
                ("$races", (long)finance.Races),
                ("$done", (long)finance.Completed),
                ("$model", finance.RevenueModel),
                ("$typical", finance.TypicalCents),
                ("$warning", finance.WarningKey),
            ]);

        foreach (var (organization, count) in finance.EntryCounts())
        {
            Run(
                connection,
                transaction,
                "INSERT INTO finance_books (organization_id, last_race_entries) VALUES ($org, $count)",
                [("$org", organization), ("$count", (long)count)]);
        }

        foreach (var (organization, entry) in finance.Rows())
        {
            Run(
                connection,
                transaction,
                "INSERT INTO finance_entries (sequence, organization_id, on_date, category, counterparty, amount_cents, reason_key) "
                + "VALUES ($seq, $org, $date, $category, $party, $amount, $reason)",
                [
                    ("$seq", entry.Sequence),
                    ("$org", organization),
                    ("$date", entry.Date.ToString()),
                    ("$category", entry.Category),
                    ("$party", entry.Counterparty),
                    ("$amount", entry.AmountCents),
                    ("$reason", entry.ReasonKey),
                ]);
        }

        foreach (var (organization, watch) in finance.Watches())
        {
            Run(
                connection,
                transaction,
                "INSERT INTO finance_insolvency (organization_id, below_since, emitted) VALUES ($org, $since, $emitted)",
                [
                    ("$org", organization),
                    ("$since", watch.BelowSince.ToString()),
                    ("$emitted", watch.Emitted ? 1L : 0L),
                ]);
        }
    }

    public IWorldSection Load(SqliteConnection connection, int storedSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (storedSchemaVersion != 1)
        {
            throw new InvalidDataException($"The stored finance section has schema version {storedSchemaVersion}, which this build cannot read.");
        }

        long next;
        int popularity;
        int season;
        int races;
        int completed;
        string model;
        long typical;
        string? warning;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT next_entry, popularity_milli, season, races, completed, revenue_model, typical_cents, warning_key FROM finance_state WHERE id = 1";
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidDataException("The finance section is registered but its state row is missing.");
            }

            next = reader.GetInt64(0);
            popularity = checked((int)reader.GetInt64(1));
            season = checked((int)reader.GetInt64(2));
            races = checked((int)reader.GetInt64(3));
            completed = checked((int)reader.GetInt64(4));
            model = reader.GetString(5);
            typical = reader.GetInt64(6);
            warning = reader.IsDBNull(7) ? null : reader.GetString(7);
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT organization_id, last_race_entries FROM finance_books ORDER BY organization_id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                counts.Add(reader.GetString(0), checked((int)reader.GetInt64(1)));
            }
        }

        var rows = new List<(string OrganizationId, LedgerEntry Entry)>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT sequence, organization_id, on_date, category, counterparty, amount_cents, reason_key FROM finance_entries ORDER BY sequence";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows.Add((
                    reader.GetString(1),
                    new LedgerEntry(
                        reader.GetInt64(0),
                        ParseDate(reader.GetString(2)),
                        reader.GetString(3),
                        reader.IsDBNull(4) ? null : reader.GetString(4),
                        reader.GetInt64(5),
                        reader.GetString(6))));
            }
        }

        var watches = new List<(string OrganizationId, InsolvencyWatch Watch)>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT organization_id, below_since, emitted FROM finance_insolvency ORDER BY organization_id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                watches.Add((
                    reader.GetString(0),
                    new InsolvencyWatch(ParseDate(reader.GetString(1)), reader.GetInt64(2) == 1)));
            }
        }

        try
        {
            return FinanceSection.RestoreRows(next, popularity, season, races, completed, model, typical, warning, rows, counts, watches);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw new InvalidDataException("The stored finance section is inconsistent: " + exception.Message, exception);
        }
    }

    private static GameDate ParseDate(string text)
    {
        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new InvalidDataException($"Date '{text}' is malformed.");
        }

        return new GameDate(date.Year, date.Month, date.Day);
    }

    private static bool TableExists(SqliteConnection connection, SqliteTransaction transaction, string table)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name";
        command.Parameters.AddWithValue("$name", table);
        return command.ExecuteScalar() is not null;
    }

    private static void Run(SqliteConnection connection, SqliteTransaction transaction, string sql, (string Name, object? Value)[] parameters)
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
}
