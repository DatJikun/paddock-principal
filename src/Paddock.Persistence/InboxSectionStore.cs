using System.Globalization;
using Microsoft.Data.Sqlite;
using Paddock.Domain.Inbox;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Persistence;

/// <summary>Saves the <c>inbox</c> section into the tables made by <see cref="V005_InboxSection"/>.</summary>
public sealed class InboxSectionStore : ISectionStore
{
    public string SectionName => InboxSection.SectionName;

    public int SchemaVersion => InboxSection.Empty.SchemaVersion;

    public void Replace(SqliteConnection connection, SqliteTransaction transaction, IWorldSection? section)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var inbox = section switch
        {
            null => null,
            InboxSection typed => typed,
            _ => throw new ArgumentException($"The inbox store cannot save a {section.GetType().Name}.", nameof(section)),
        };

        // Children first, so foreign keys hold at every statement.
        foreach (var table in new[] { "inbox_option_arguments", "inbox_arguments", "inbox_options", "inbox_items", "inbox_counter" })
        {
            Run(connection, transaction, "DELETE FROM " + table, []);
        }

        if (inbox is null)
        {
            return;
        }

        Run(connection, transaction, "INSERT INTO inbox_counter (id, next_number) VALUES (1, $next)", [("$next", inbox.NextNumber)]);
        foreach (var item in inbox.Items)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO inbox_items (number, manager_id, created, kind, subject_key, valid_until, default_option, status, closed_on, chosen_option) "
                + "VALUES ($n, $manager, $created, $kind, $subject, $until, $default, $status, $closed, $chosen)",
                [
                    ("$n", item.Number),
                    ("$manager", item.ManagerId),
                    ("$created", item.Created.ToString()),
                    ("$kind", item.Kind),
                    ("$subject", item.SubjectKey),
                    ("$until", item.ValidUntil?.ToString()),
                    ("$default", item.DefaultOptionId),
                    ("$status", item.Status.ToString()),
                    ("$closed", item.ClosedOn?.ToString()),
                    ("$chosen", item.ChosenOptionId),
                ]);
            foreach (var argument in item.Arguments)
            {
                Run(
                    connection,
                    transaction,
                    "INSERT INTO inbox_arguments (item_number, name, value) VALUES ($n, $name, $value)",
                    [("$n", item.Number), ("$name", argument.Key), ("$value", argument.Value)]);
            }

            for (var i = 0; i < item.Options.Count; i++)
            {
                var option = item.Options[i];
                Run(
                    connection,
                    transaction,
                    "INSERT INTO inbox_options (item_number, ordinal, option_id, label_key, consequence_key) VALUES ($n, $ordinal, $id, $label, $consequence)",
                    [("$n", item.Number), ("$ordinal", (long)i), ("$id", option.Id), ("$label", option.LabelKey), ("$consequence", option.ConsequenceKey)]);
                foreach (var argument in option.Arguments)
                {
                    Run(
                        connection,
                        transaction,
                        "INSERT INTO inbox_option_arguments (item_number, option_id, name, value) VALUES ($n, $id, $name, $value)",
                        [("$n", item.Number), ("$id", option.Id), ("$name", argument.Key), ("$value", argument.Value)]);
                }
            }
        }
    }

    public IWorldSection Load(SqliteConnection connection, int storedSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (storedSchemaVersion != 1)
        {
            throw new InvalidDataException($"The stored inbox has schema version {storedSchemaVersion}, which this build cannot read.");
        }

        long next;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT next_number FROM inbox_counter WHERE id = 1";
            next = command.ExecuteScalar() is long value
                ? value
                : throw new InvalidDataException("The inbox is registered but its counter is missing.");
        }

        var arguments = new Dictionary<long, List<KeyValuePair<string, string>>>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT item_number, name, value FROM inbox_arguments ORDER BY item_number, name";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                Bucket(arguments, reader.GetInt64(0)).Add(new(reader.GetString(1), reader.GetString(2)));
            }
        }

        var optionArguments = new Dictionary<(long ItemNumber, string OptionId), List<KeyValuePair<string, string>>>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT item_number, option_id, name, value FROM inbox_option_arguments ORDER BY item_number, option_id, name";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var key = (reader.GetInt64(0), reader.GetString(1));
                if (!optionArguments.TryGetValue(key, out var list))
                {
                    list = [];
                    optionArguments.Add(key, list);
                }

                list.Add(new(reader.GetString(2), reader.GetString(3)));
            }
        }

        var options = new Dictionary<long, List<InboxOption>>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT item_number, option_id, label_key, consequence_key FROM inbox_options ORDER BY item_number, ordinal";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var itemNumber = reader.GetInt64(0);
                var optionId = reader.GetString(1);
                var labelKey = reader.GetString(2);
                var consequenceKey = reader.GetString(3);
                var args = optionArguments.GetValueOrDefault((itemNumber, optionId));
                Bucket(options, itemNumber).Add(new InboxOption(optionId, labelKey, consequenceKey, args));
            }
        }

        var items = new List<InboxItem>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT number, manager_id, created, kind, subject_key, valid_until, default_option, status, closed_on, chosen_option "
                + "FROM inbox_items ORDER BY number";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var number = reader.GetInt64(0);
                var draft = new InboxItemDraft(
                    reader.GetString(3),
                    reader.GetString(4),
                    arguments.GetValueOrDefault(number),
                    options.GetValueOrDefault(number),
                    reader.IsDBNull(5) ? null : ParseDate(reader.GetString(5)),
                    reader.IsDBNull(6) ? null : reader.GetString(6));
                if (!Enum.TryParse<InboxStatus>(reader.GetString(7), out var status) || !Enum.IsDefined(status))
                {
                    throw new InvalidDataException($"Inbox item {number} has unknown status '{reader.GetString(7)}'.");
                }

                items.Add(new InboxItem(
                    number,
                    reader.GetString(1),
                    ParseDate(reader.GetString(2)),
                    draft,
                    status,
                    reader.IsDBNull(8) ? null : ParseDate(reader.GetString(8)),
                    reader.IsDBNull(9) ? null : reader.GetString(9)));
            }
        }

        return InboxSection.Restore(next, items);
    }

    private static List<T> Bucket<T>(Dictionary<long, List<T>> map, long key)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map.Add(key, list);
        }

        return list;
    }

    private static GameDate ParseDate(string text)
    {
        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new InvalidDataException($"Date '{text}' is malformed.");
        }

        return new GameDate(date.Year, date.Month, date.Day);
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
