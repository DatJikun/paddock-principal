using Microsoft.Data.Sqlite;
using Paddock.Domain.Contracts;
using Paddock.Domain.World;
using static Paddock.Persistence.SectionRows;

namespace Paddock.Persistence;

/// <summary>Saves the <c>raises</c> section into the table made by <see cref="V019_RaisesSection"/>.</summary>
public sealed class RaisesSectionStore : ISectionStore
{
    public string SectionName => RaisesSection.SectionName;

    public int SchemaVersion => RaisesSection.Empty.SchemaVersion;

    public void Replace(SqliteConnection connection, SqliteTransaction transaction, IWorldSection? section)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var raises = section switch
        {
            null => null,
            RaisesSection typed => typed,
            _ => throw new ArgumentException($"The raises store cannot save a {section.GetType().Name}.", nameof(section)),
        };

        if (raises is null && !TableExists(connection, transaction, "driver_raises"))
        {
            return;
        }

        Run(connection, transaction, "DELETE FROM driver_raises");
        if (raises is null)
        {
            return;
        }

        foreach (var row in raises.Records)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO driver_raises (person_id, organization_id, season, asked_salary, trust, leave_bias) VALUES ($person, $org, $season, $asked, $trust, $bias)",
                ("$person", row.Person.Value),
                ("$org", row.Team.Value),
                ("$season", (long)row.Season),
                ("$asked", row.AskedSalary),
                ("$trust", (long)row.Trust),
                ("$bias", (long)row.LeaveBias));
        }
    }

    public IWorldSection Load(SqliteConnection connection, int storedSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (storedSchemaVersion != 1)
        {
            throw new InvalidDataException($"The stored raises section has schema version {storedSchemaVersion}, which this build cannot read.");
        }

        var records = new List<RaiseRecord>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT person_id, organization_id, season, asked_salary, trust, leave_bias FROM driver_raises ORDER BY person_id, organization_id";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            records.Add(new RaiseRecord(
                PersonIdFrom(reader.GetString(0)),
                OrganizationIdFrom(reader.GetString(1)),
                reader.GetInt32(2),
                reader.GetInt64(3),
                reader.GetInt32(4),
                reader.GetInt32(5)));
        }

        return RaisesSection.From(records);
    }

    private static PersonId PersonIdFrom(string text)
    {
        var id = text.StartsWith("gen:", StringComparison.Ordinal) ? PersonId.Generated(ParseSequence(text)) : PersonId.Real(text);
        return id.Value == text ? id : throw new InvalidDataException($"Id '{text}' is not canonical.");
    }

    private static long ParseSequence(string text)
    {
        if (!long.TryParse(text.AsSpan("gen:".Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var sequence) || sequence < 1)
        {
            throw new InvalidDataException($"Id '{text}' is not a generated person.");
        }

        return sequence;
    }
}
