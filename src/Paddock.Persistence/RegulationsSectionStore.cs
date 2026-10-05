using System.Globalization;
using Microsoft.Data.Sqlite;
using Paddock.Domain.Racing;
using Paddock.Domain.World;
using static Paddock.Persistence.SectionRows;

namespace Paddock.Persistence;

/// <summary>Saves the <c>regulations</c> section into the tables made by <see cref="V020_RaceSections"/>.</summary>
public sealed class RegulationsSectionStore : ISectionStore
{
    public string SectionName => RegulationsSection.SectionName;

    public int SchemaVersion => 1;

    public void Replace(SqliteConnection connection, SqliteTransaction transaction, IWorldSection? section)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var regulations = section switch
        {
            null => null,
            RegulationsSection typed => typed,
            _ => throw new ArgumentException("The regulations store cannot save a " + section.GetType().Name + ".", nameof(section)),
        };

        if (regulations is null && !TableExists(connection, transaction, "regulations_meta"))
        {
            return;
        }

        Run(connection, transaction, "DELETE FROM regulations_rejected");
        Run(connection, transaction, "DELETE FROM regulations_values");
        Run(connection, transaction, "DELETE FROM regulations_meta");
        if (regulations is null)
        {
            return;
        }

        Run(
            connection,
            transaction,
            "INSERT INTO regulations_meta (singleton, season) VALUES (1, $season)",
            ("$season", regulations.Season));
        foreach (var (dimension, value) in regulations.Values)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO regulations_values (dimension_id, value) VALUES ($dimension, $value)",
                ("$dimension", dimension),
                ("$value", value));
        }

        foreach (var rejected in regulations.Rejected)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO regulations_rejected (dimension_id, value, season) VALUES ($dimension, $value, $season)",
                ("$dimension", rejected.DimensionId),
                ("$value", rejected.Value),
                ("$season", rejected.Season));
        }
    }

    public IWorldSection Load(SqliteConnection connection, int storedSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (storedSchemaVersion != SchemaVersion)
        {
            throw new InvalidDataException("Regulations section schema " + storedSchemaVersion.ToString(CultureInfo.InvariantCulture) + " is not readable.");
        }

        using var meta = connection.CreateCommand();
        meta.CommandText = "SELECT season FROM regulations_meta";
        var season = meta.ExecuteScalar() as long? ?? throw new InvalidDataException("The regulations section is registered but has no meta row.");

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        using var rows = connection.CreateCommand();
        rows.CommandText = "SELECT dimension_id, value FROM regulations_values ORDER BY dimension_id";
        using (var body = rows.ExecuteReader())
        {
            while (body.Read())
            {
                values.Add(body.GetString(0), body.GetString(1));
            }
        }

        var rejected = new List<RejectedRegulation>();
        using var memory = connection.CreateCommand();
        memory.CommandText = "SELECT dimension_id, value, season FROM regulations_rejected ORDER BY dimension_id, value, season";
        using (var body = memory.ExecuteReader())
        {
            while (body.Read())
            {
                rejected.Add(new RejectedRegulation(body.GetString(0), body.GetString(1), body.GetInt32(2)));
            }
        }

        return RegulationsSection.Create((int)season, values, rejected);
    }
}
