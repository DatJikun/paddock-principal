using System.Globalization;
using Microsoft.Data.Sqlite;
using Paddock.Domain.Pool;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Persistence;

/// <summary>Saves the <c>talent-pool</c> section into the tables made by <see cref="V006_TalentPoolSection"/>.</summary>
public sealed class TalentPoolSectionStore : ISectionStore
{
    private const string PersonPrefix = "gen:";
    private const string OrganizationPrefix = "org:";

    public string SectionName => TalentPoolSection.SectionName;

    public int SchemaVersion => TalentPoolSection.Empty.SchemaVersion;

    public void Replace(SqliteConnection connection, SqliteTransaction transaction, IWorldSection? section)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var pool = section switch
        {
            null => null,
            TalentPoolSection typed => typed,
            _ => throw new ArgumentException($"The talent pool store cannot save a {section.GetType().Name}.", nameof(section)),
        };

        foreach (var table in new[] { "pool_observations", "pool_focus", "pool_lapsed", "pool_members", "pool_counter" })
        {
            Run(connection, transaction, "DELETE FROM " + table, []);
        }

        if (pool is null)
        {
            return;
        }

        Run(connection, transaction, "INSERT INTO pool_counter (id, next_handle) VALUES (1, $next)", [("$next", pool.NextHandle)]);
        foreach (var member in pool.Members)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO pool_members (person_id, handle, entered, funder_id, programme, funding_season) "
                + "VALUES ($person, $handle, $entered, $funder, $programme, $season)",
                [
                    ("$person", member.Id.Value),
                    ("$handle", member.Handle),
                    ("$entered", member.EnteredOn.ToString()),
                    ("$funder", member.Funding?.Funder.Value),
                    ("$programme", member.Funding?.Programme.ToString()),
                    ("$season", member.Funding is { } funding ? (long)funding.Season : null),
                ]);
        }

        foreach (var career in pool.Lapsed)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO pool_lapsed (person_id, lapsed_on) VALUES ($person, $on)",
                [("$person", career.Id.Value), ("$on", career.On.ToString())]);
        }

        foreach (var focus in pool.Focuses)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO pool_focus (organization_id, kind, person_id) VALUES ($organization, $kind, $person)",
                [("$organization", focus.Organization.Value), ("$kind", focus.Kind.ToString()), ("$person", focus.Person?.Value)]);
        }

        foreach (var row in pool.Observations)
        {
            Run(
                connection,
                transaction,
                "INSERT INTO pool_observations (organization_id, person_id, milli_points) VALUES ($organization, $person, $milli)",
                [("$organization", row.Organization.Value), ("$person", row.Person.Value), ("$milli", row.Milli)]);
        }
    }

    public IWorldSection Load(SqliteConnection connection, int storedSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (storedSchemaVersion != 1)
        {
            throw new InvalidDataException($"The stored talent pool has schema version {storedSchemaVersion}, which this build cannot read.");
        }

        long next;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT next_handle FROM pool_counter WHERE id = 1";
            next = command.ExecuteScalar() is long value
                ? value
                : throw new InvalidDataException("The talent pool is registered but its counter is missing.");
        }

        var members = new List<PoolMember>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT person_id, handle, entered, funder_id, programme, funding_season FROM pool_members ORDER BY person_id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                JuniorFunding? funding = null;
                if (!reader.IsDBNull(3))
                {
                    if (!Enum.TryParse<JuniorProgramme>(reader.GetString(4), out var programme) || !Enum.IsDefined(programme))
                    {
                        throw new InvalidDataException($"Pool member '{reader.GetString(0)}' has unknown programme '{reader.GetString(4)}'.");
                    }

                    funding = new JuniorFunding(OrganizationIdFrom(reader.GetString(3)), programme, checked((int)reader.GetInt64(5)));
                }

                members.Add(new PoolMember(PersonIdFrom(reader.GetString(0)), reader.GetInt64(1), ParseDate(reader.GetString(2)), funding));
            }
        }

        var lapsed = new List<LapsedCareer>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT person_id, lapsed_on FROM pool_lapsed ORDER BY person_id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                lapsed.Add(new LapsedCareer(PersonIdFrom(reader.GetString(0)), ParseDate(reader.GetString(1))));
            }
        }

        var focuses = new List<ScoutFocus>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT organization_id, kind, person_id FROM pool_focus ORDER BY organization_id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!Enum.TryParse<ScoutFocusKind>(reader.GetString(1), out var kind) || !Enum.IsDefined(kind))
                {
                    throw new InvalidDataException($"A pool focus has unknown kind '{reader.GetString(1)}'.");
                }

                focuses.Add(new ScoutFocus(
                    OrganizationIdFrom(reader.GetString(0)),
                    kind,
                    reader.IsDBNull(2) ? null : PersonIdFrom(reader.GetString(2))));
            }
        }

        var observations = new List<ObservationRow>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT organization_id, person_id, milli_points FROM pool_observations ORDER BY organization_id, person_id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                observations.Add(new ObservationRow(
                    OrganizationIdFrom(reader.GetString(0)),
                    PersonIdFrom(reader.GetString(1)),
                    reader.GetInt64(2)));
            }
        }

        try
        {
            return TalentPoolSection.Restore(next, members, lapsed, focuses, observations);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException("The stored talent pool is inconsistent: " + exception.Message, exception);
        }
    }

    private static PersonId PersonIdFrom(string text)
    {
        var id = text.StartsWith(PersonPrefix, StringComparison.Ordinal)
            ? PersonId.Generated(ParseSequence(text, PersonPrefix))
            : PersonId.Real(text);
        return string.Equals(id.Value, text, StringComparison.Ordinal) ? id : throw new InvalidDataException($"Id '{text}' is not canonical.");
    }

    private static OrganizationId OrganizationIdFrom(string text)
    {
        var id = text.StartsWith(OrganizationPrefix, StringComparison.Ordinal)
            ? OrganizationId.Generated(ParseSequence(text, OrganizationPrefix))
            : OrganizationId.Real(text);
        return string.Equals(id.Value, text, StringComparison.Ordinal) ? id : throw new InvalidDataException($"Id '{text}' is not canonical.");
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
