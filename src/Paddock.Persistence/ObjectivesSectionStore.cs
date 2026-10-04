using System.Globalization;
using Microsoft.Data.Sqlite;
using Paddock.Domain.Objectives;
using Paddock.Domain.World;
using static Paddock.Persistence.SectionRows;

namespace Paddock.Persistence;

/// <summary>Saves the <c>objectives</c> section into the tables made by <see cref="V010_BoardAndObjectivesSections"/>.</summary>
public sealed class ObjectivesSectionStore : ISectionStore
{
    private const string Met = "met";

    private const string Failed = "failed";

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

        // A file saved while it was still on an older schema has no objectives tables, and no section to clear in them.
        if (objectives is null && !TableExists(connection, transaction, "objectives_counter"))
        {
            return;
        }

        foreach (var table in new[] { "objective_effect_arguments", "objectives", "objectives_counter" })
        {
            Run(connection, transaction, "DELETE FROM " + table);
        }

        if (objectives is null)
        {
            return;
        }

        Run(connection, transaction, "INSERT INTO objectives_counter (id, next_number) VALUES (1, $next)", ("$next", objectives.NextNumber));
        foreach (var objective in objectives.Objectives)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO objectives (number, owner_id, grantor_id, kind_key, reason_key, predicate_name, predicate_parameter, baseline, "
                + "created, deadline, met_key, failed_key, status, settled_on) "
                + "VALUES ($n, $owner, $grantor, $kind, $reason, $pname, $pparam, $baseline, $created, $deadline, $met, $failed, $status, $settled)",
                ("$n", objective.Number),
                ("$owner", objective.Owner.Value),
                ("$grantor", objective.Grantor.Value),
                ("$kind", objective.KindKey),
                ("$reason", objective.ReasonKey),
                ("$pname", objective.Predicate.Name),
                ("$pparam", objective.Predicate.Parameter),
                ("$baseline", objective.Baseline is decimal baseline ? baseline.ToString("0.############################", CultureInfo.InvariantCulture) : null),
                ("$created", objective.Created.ToString()),
                ("$deadline", objective.Deadline.ToString()),
                ("$met", objective.EffectOnMet.Key),
                ("$failed", objective.EffectOnFailed.Key),
                ("$status", objective.Status.ToString()),
                ("$settled", objective.SettledOn?.ToString()));
            WriteArguments(connection, transaction, objective.Number, Met, objective.EffectOnMet);
            WriteArguments(connection, transaction, objective.Number, Failed, objective.EffectOnFailed);
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
            command.CommandText = "SELECT next_number FROM objectives_counter WHERE id = 1";
            next = command.ExecuteScalar() is long value
                ? value
                : throw new InvalidDataException("The objectives section is registered but its counter is missing.");
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

        var objectives = new List<Objective>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT number, owner_id, grantor_id, kind_key, reason_key, predicate_name, predicate_parameter, baseline, created, deadline, "
                + "met_key, failed_key, status, settled_on FROM objectives ORDER BY number";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var number = reader.GetInt64(0);
                var draft = new ObjectiveDraft(
                    OrganizationIdFrom(reader.GetString(1)),
                    OrganizationIdFrom(reader.GetString(2)),
                    reader.GetString(3),
                    reader.GetString(4),
                    ParsePredicate(reader.GetString(5), reader.GetString(6)),
                    reader.IsDBNull(7) ? null : ParseDecimal(reader.GetString(7)),
                    ParseDate(reader.GetString(9)),
                    new ObjectiveEffect(reader.GetString(10), arguments.GetValueOrDefault((number, Met))),
                    new ObjectiveEffect(reader.GetString(11), arguments.GetValueOrDefault((number, Failed))));
                objectives.Add(new Objective(
                    number,
                    ParseDate(reader.GetString(8)),
                    draft,
                    ParseEnum<ObjectiveStatus>(reader.GetString(12)),
                    ParseOptionalDate(reader, 13)));
            }
        }

        return ObjectivesSection.Restore(next, objectives);
    }

    private static void WriteArguments(SqliteConnection connection, SqliteTransaction transaction, long number, string effect, ObjectiveEffect value)
    {
        foreach (var argument in value.Arguments)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO objective_effect_arguments (objective_number, effect, name, value) VALUES ($n, $effect, $name, $value)",
                ("$n", number),
                ("$effect", effect),
                ("$name", argument.Key),
                ("$value", argument.Value));
        }
    }

    private static ObjectivePredicate ParsePredicate(string name, string parameter)
    {
        try
        {
            return ObjectivePredicates.Parse(name, parameter);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    private static decimal ParseDecimal(string text) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidDataException($"Baseline '{text}' is malformed.");
}
