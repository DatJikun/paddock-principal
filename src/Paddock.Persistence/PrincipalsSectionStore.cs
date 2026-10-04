using System.Globalization;
using Microsoft.Data.Sqlite;
using Paddock.Domain.Principals;
using Paddock.Domain.World;
using static Paddock.Persistence.SectionRows;

namespace Paddock.Persistence;

/// <summary>Saves the <c>principals</c> section into the table made by <see cref="V017_PrincipalsSection"/>.</summary>
public sealed class PrincipalsSectionStore : ISectionStore
{
    public string SectionName => PrincipalsSection.SectionName;

    public int SchemaVersion => PrincipalsSection.Empty.SchemaVersion;

    public void Replace(SqliteConnection connection, SqliteTransaction transaction, IWorldSection? section)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var principals = section switch
        {
            null => null,
            PrincipalsSection typed => typed,
            _ => throw new ArgumentException($"The principals store cannot save a {section.GetType().Name}.", nameof(section)),
        };

        // A file saved while it was still on an older schema has no principals table, and no section to clear in it.
        if (principals is null && !TableExists(connection, transaction, "ai_principals"))
        {
            return;
        }

        Run(connection, transaction, "DELETE FROM ai_principals");
        if (principals is null)
        {
            return;
        }

        foreach (var record in principals.Records)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO ai_principals (organization_id, archetype, person_id, assigned_on, last_review, next_review, sacrificed_season, scout_season, staff_roles) "
                + "VALUES ($org, $archetype, $person, $assigned, $last, $next, $sacrificed, $scout, $roles)",
                ("$org", record.Organization.Value),
                ("$archetype", record.Archetype),
                ("$person", record.Person?.Value),
                ("$assigned", record.AssignedOn.ToString()),
                ("$last", record.LastReview?.ToString()),
                ("$next", record.NextReview.ToString()),
                ("$sacrificed", (long)record.SacrificedSeason),
                ("$scout", (long)record.ScoutSeason),
                ("$roles", record.StaffRoles));
        }
    }

    public IWorldSection Load(SqliteConnection connection, int storedSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (storedSchemaVersion != 1)
        {
            throw new InvalidDataException($"The stored principals section has schema version {storedSchemaVersion}, which this build cannot read.");
        }

        var records = new List<AiPrincipalRecord>();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT organization_id, archetype, person_id, assigned_on, last_review, next_review, sacrificed_season, scout_season, staff_roles "
            + "FROM ai_principals ORDER BY organization_id";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            records.Add(new AiPrincipalRecord(
                OrganizationIdFrom(reader.GetString(0)),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : PersonIdFrom(reader.GetString(2)),
                ParseDate(reader.GetString(3)),
                ParseOptionalDate(reader, 4),
                ParseDate(reader.GetString(5)),
                checked((int)reader.GetInt64(6)),
                checked((int)reader.GetInt64(7)),
                reader.GetString(8)));
        }

        return PrincipalsSection.Restore(records);
    }

    private static PersonId PersonIdFrom(string text)
    {
        var id = text.StartsWith("gen:", StringComparison.Ordinal) ? PersonId.Generated(ParseSequence(text, "gen:")) : PersonId.Real(text);
        return id.Value == text ? id : throw new InvalidDataException($"Id '{text}' is not canonical.");
    }

    private static long ParseSequence(string text, string prefix)
    {
        var tail = text.AsSpan(prefix.Length);
        if (tail.Length == 0 || (tail.Length > 1 && tail[0] == '0')
            || !long.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) || sequence < 1)
        {
            throw new InvalidDataException($"Id '{text}' has a malformed sequence.");
        }

        return sequence;
    }
}
