using System.Globalization;
using Microsoft.Data.Sqlite;
using Paddock.Domain.People;
using Paddock.Domain.World;
using static Paddock.Persistence.SectionRows;

namespace Paddock.Persistence;

/// <summary>Saves the <c>staff</c> section into the table made by <see cref="V020_StaffSection"/>.</summary>
public sealed class StaffSectionStore : ISectionStore
{
    public string SectionName => StaffSection.SectionName;

    public int SchemaVersion => StaffSection.Empty.SchemaVersion;

    public void Replace(SqliteConnection connection, SqliteTransaction transaction, IWorldSection? section)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var staff = section switch
        {
            null => null,
            StaffSection typed => typed,
            _ => throw new ArgumentException($"The staff store cannot save a {section.GetType().Name}.", nameof(section)),
        };

        if (staff is null && !TableExists(connection, transaction, "engineer_links"))
        {
            return;
        }

        Run(connection, transaction, "DELETE FROM engineer_links");
        if (staff is null)
        {
            return;
        }

        foreach (var link in staff.Links)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO engineer_links (engineer_id, team_id, driver_id, relationship, season) VALUES ($engineer, $team, $driver, $relationship, $season)",
                ("$engineer", link.Engineer.Value),
                ("$team", link.Team.Value),
                ("$driver", link.Driver.Value),
                ("$relationship", (long)link.Relationship),
                ("$season", (long)link.Season));
        }
    }

    public IWorldSection Load(SqliteConnection connection, int storedSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (storedSchemaVersion != 1)
        {
            throw new InvalidDataException($"The stored staff section has schema version {storedSchemaVersion}, which this build cannot read.");
        }

        var links = new List<RaceEngineerLink>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT team_id, engineer_id, driver_id, relationship, season FROM engineer_links ORDER BY engineer_id";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            links.Add(new RaceEngineerLink(
                OrganizationIdFrom(reader.GetString(0)),
                PersonIdFrom(reader.GetString(1)),
                PersonIdFrom(reader.GetString(2)),
                checked((int)reader.GetInt64(3)),
                checked((int)reader.GetInt64(4))));
        }

        return StaffSection.Restore(links);
    }

    private static PersonId PersonIdFrom(string text)
    {
        var id = text.StartsWith("gen:", StringComparison.Ordinal) ? PersonId.Generated(ParseSequence(text)) : PersonId.Real(text);
        return id.Value == text ? id : throw new InvalidDataException($"Id '{text}' is not canonical.");
    }

    private static long ParseSequence(string text)
    {
        var tail = text.AsSpan("gen:".Length);
        if (tail.Length == 0 || (tail.Length > 1 && tail[0] == '0')
            || !long.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) || sequence < 1)
        {
            throw new InvalidDataException($"Id '{text}' has a malformed sequence.");
        }

        return sequence;
    }
}
