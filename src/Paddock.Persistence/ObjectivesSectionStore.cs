using System.Globalization;
using Microsoft.Data.Sqlite;
using Paddock.Domain.Objectives;
using Paddock.Domain.Sponsors;
using Paddock.Domain.World;

namespace Paddock.Persistence;

/// <summary>Saves the <c>objectives</c> section (T36) into the tables made by <see cref="V012_SponsorsAndObjectives"/>.</summary>
public sealed class ObjectivesSectionStore : ISectionStore
{
    public string SectionName => ObjectivesSection.SectionName;

    public int SchemaVersion => ObjectivesSection.Empty.SchemaVersion;

    public void Replace(SqliteConnection connection, SqliteTransaction transaction, IWorldSection? section)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var objectives = section switch
        {
            null => null,
            ObjectivesSection typed => typed,
            _ => throw new ArgumentException($"The objectives store cannot save a {section.GetType().Name}.", nameof(section)),
        };

        if (objectives is null && !StoreSql.TableExists(connection, transaction, "objectives_state"))
        {
            return;
        }

        foreach (var table in new[] { "objective_effect_arguments", "objectives", "objectives_state" })
        {
            StoreSql.Run(connection, transaction, "DELETE FROM " + table);
        }

        if (objectives is null)
        {
            return;
        }

        StoreSql.Run(connection, transaction, "INSERT INTO objectives_state (id, next_number) VALUES (1, $next)", ("$next", objectives.NextNumber));
        foreach (var objective in objectives.Objectives)
        {
            StoreSql.Run(
                connection,
                transaction,
                "INSERT INTO objectives (number, owner_id, grantor_id, kind_key, reason_key, predicate_name, predicate_parameter, baseline, created_on, deadline_on, met_key, failed_key, status, settled_on) "
                + "VALUES ($n, $owner, $grantor, $kind, $reason, $pname, $pparam, $baseline, $created, $deadline, $met, $failed, $status, $settled)",
                ("$n", objective.Number),
                ("$owner", objective.Owner.Value),
                ("$grantor", objective.Grantor.Value),
                ("$kind", objective.KindKey),
                ("$reason", objective.ReasonKey),
                ("$pname", objective.Predicate.Name),
                ("$pparam", objective.Predicate.Parameter),
                ("$baseline", objective.Baseline?.ToString("0.############################", CultureInfo.InvariantCulture)),
                ("$created", objective.Created.ToString()),
                ("$deadline", objective.Deadline.ToString()),
                ("$met", objective.EffectOnMet.Key),
                ("$failed", objective.EffectOnFailed.Key),
                ("$status", objective.Status.ToString()),
                ("$settled", objective.SettledOn?.ToString()));
            foreach (var (effect, arguments) in new[] { ("met", objective.EffectOnMet.Arguments), ("failed", objective.EffectOnFailed.Arguments) })
            {
                foreach (var (name, value) in arguments)
                {
                    StoreSql.Run(
                        connection,
                        transaction,
                        "INSERT INTO objective_effect_arguments (objective_number, effect, name, value) VALUES ($n, $effect, $name, $value)",
                        ("$n", objective.Number),
                        ("$effect", effect),
                        ("$name", name),
                        ("$value", value));
                }
            }
        }
    }

    public IWorldSection Load(SqliteConnection connection, int storedSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (storedSchemaVersion != 1)
        {
            throw new InvalidDataException($"The stored objectives section has schema version {storedSchemaVersion}, which this build cannot read.");
        }

        long next;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT next_number FROM objectives_state WHERE id = 1";
            next = command.ExecuteScalar() as long? ?? throw new InvalidDataException("The objectives section is registered but its state row is missing.");
        }

        var arguments = new Dictionary<(long Number, string Effect), List<KeyValuePair<string, string>>>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT objective_number, effect, name, value FROM objective_effect_arguments ORDER BY objective_number, effect, name";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var key = (reader.GetInt64(0), reader.GetString(1));
                if (!arguments.TryGetValue(key, out var list))
                {
                    list = [];
                    arguments.Add(key, list);
                }

                list.Add(new KeyValuePair<string, string>(reader.GetString(2), reader.GetString(3)));
            }
        }

        var items = new List<Objective>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT number, owner_id, grantor_id, kind_key, reason_key, predicate_name, predicate_parameter, baseline, created_on, deadline_on, met_key, failed_key, status, settled_on FROM objectives ORDER BY number";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var number = reader.GetInt64(0);
                try
                {
                    var draft = new ObjectiveDraft(
                        SponsorsSection.ParseOrganization(reader.GetString(1)),
                        SponsorsSection.ParseOrganization(reader.GetString(2)),
                        reader.GetString(3),
                        reader.GetString(4),
                        ObjectivePredicate.FromParts(reader.GetString(5), reader.GetString(6)),
                        reader.IsDBNull(7) ? null : decimal.Parse(reader.GetString(7), CultureInfo.InvariantCulture),
                        StoreSql.Date(reader.GetString(9)),
                        new ObjectiveEffect(reader.GetString(10), arguments.GetValueOrDefault((number, "met"))),
                        new ObjectiveEffect(reader.GetString(11), arguments.GetValueOrDefault((number, "failed"))));
                    items.Add(new Objective(
                        number,
                        StoreSql.Date(reader.GetString(8)),
                        draft,
                        StoreSql.Enum<ObjectiveStatus>(reader.GetString(12)),
                        StoreSql.OptionalDate(reader, 13)));
                }
                catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
                {
                    throw new InvalidDataException("The stored objective " + number.ToString(CultureInfo.InvariantCulture) + " is invalid: " + exception.Message, exception);
                }
            }
        }

        try
        {
            return ObjectivesSection.Restore(next, items);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException("The stored objectives section is inconsistent: " + exception.Message, exception);
        }
    }
}
