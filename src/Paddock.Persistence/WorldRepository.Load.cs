using System.Globalization;
using Microsoft.Data.Sqlite;
using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Persistence;

public sealed partial class WorldRepository
{
    private const string PersonPrefix = "gen:";
    private const string OrganizationPrefix = "org:";
    private const string ContractPrefix = "con:";

    /// <summary>
    /// Loads the stored world. Throws <see cref="InvalidOperationException"/> when none was saved
    /// (see <see cref="HasWorld"/>) and <see cref="InvalidDataException"/> when the stored rows do not form a valid world.
    /// </summary>
    public WorldState LoadWorld()
    {
        RequireWorld();
        return Guard(() => ReadWorld(_file.Connection, _file.ReadMeta()));
    }

    /// <summary>Loads the world with the queue, managers, and command log written by <see cref="SaveAll"/>.</summary>
    public WorldSnapshot LoadAll()
    {
        RequireWorld();
        return Guard(() =>
        {
            var connection = _file.Connection;
            var world = ReadWorld(connection, _file.ReadMeta());
            var counters = ReadCounters(connection);
            var events = new List<StoredEvent>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id, event_date, sequence, type_id, payload_type, payload FROM scheduled_events ORDER BY event_date, sequence";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    events.Add(new StoredEvent(
                        reader.GetString(0),
                        ParseDate(reader.GetString(1)),
                        reader.GetInt64(2),
                        reader.GetString(3),
                        reader.GetString(4),
                        reader.GetString(5)));
                }
            }

            var managers = new List<StoredManager>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id, kind, display_name, blocking_kind FROM managers ORDER BY ordinal";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    managers.Add(new StoredManager(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
                }
            }

            var log = new List<StoredCommand>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT submission_number, manager_id, issued_on, command_type, payload FROM command_log ORDER BY position";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    log.Add(new StoredCommand(
                        reader.GetInt64(0),
                        reader.GetString(1),
                        ParseDateOnly(reader.GetString(2)),
                        reader.GetString(3),
                        reader.GetString(4)));
                }
            }

            return new WorldSnapshot(
                world,
                events,
                Counter(counters, "event_id", 1),
                Counter(counters, "event_sequence", 0),
                managers,
                log,
                Counter(counters, "submission", 1));
        });
    }

    private void RequireWorld()
    {
        if (!HasWorld)
        {
            throw new InvalidOperationException("This save has no world yet. Save one first.");
        }
    }

    private static T Guard<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new InvalidDataException("The stored world is not valid: " + exception.Message, exception);
        }
    }

    private static long Counter(Dictionary<string, long> counters, string name, long fallback) =>
        counters.TryGetValue(name, out var value) ? value : fallback;

    private static Dictionary<string, long> ReadCounters(SqliteConnection connection)
    {
        var counters = new Dictionary<string, long>(StringComparer.Ordinal);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, next_value FROM id_counters";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            counters[reader.GetString(0)] = reader.GetInt64(1);
        }

        return counters;
    }

    private WorldState ReadWorld(SqliteConnection connection, SaveMeta meta)
    {
        var date = new GameDate(meta.CurrentGameDate.Year, meta.CurrentGameDate.Month, meta.CurrentGameDate.Day);
        var counters = ReadCounters(connection);
        foreach (var name in WorldCounters)
        {
            if (!counters.ContainsKey(name))
            {
                throw new InvalidDataException($"The world id counter '{name}' is missing.");
            }
        }

        var persons = ReadPersons(connection);
        var organizations = ReadOrganizations(connection);
        var contracts = ReadContracts(connection);
        var knowledge = ReadKnowledge(connection);

        var issued = new List<string>(persons.Count + organizations.Count + contracts.Count);
        foreach (var person in persons)
        {
            issued.Add(person.Id.Value);
        }

        foreach (var organization in organizations)
        {
            issued.Add(organization.Id.Value);
        }

        foreach (var contract in contracts)
        {
            issued.Add(contract.Id.Value);
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id FROM retired_ids";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                issued.Add(reader.GetString(0));
            }
        }

        var ids = new IdAllocator(counters["person"], counters["organization"], counters["contract"], issued);
        return WorldState.Restore(date, ids, persons, organizations, contracts, knowledge, ReadSections(connection));
    }

    private List<IWorldSection> ReadSections(SqliteConnection connection)
    {
        var registered = new List<(string Name, int Version)>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name, schema_version FROM world_sections ORDER BY name";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                registered.Add((reader.GetString(0), ToInt(reader.GetInt64(1))));
            }
        }

        var sections = new List<IWorldSection>(registered.Count);
        foreach (var (name, version) in registered)
        {
            var store = _sectionStores.FirstOrDefault(candidate => candidate.SectionName == name)
                ?? throw new InvalidDataException($"The save holds section '{name}' and this build has no store for it.");
            if (version > store.SchemaVersion)
            {
                throw new InvalidDataException(
                    $"Section '{name}' was saved with schema version {version}, newer than this build's {store.SchemaVersion}.");
            }

            var section = store.Load(connection, version);
            if (section.Name != name)
            {
                throw new InvalidDataException($"The store for '{name}' returned section '{section.Name}'.");
            }

            sections.Add(section);
        }

        return sections;
    }

    private static List<Person> ReadPersons(SqliteConnection connection)
    {
        var roles = new Dictionary<string, List<PersonRole>>(StringComparer.Ordinal);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT person_id, role FROM person_roles";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                Bucket(roles, reader.GetString(0)).Add(ParsePersonRole(reader.GetString(1)));
            }
        }

        var attributes = new Dictionary<string, List<NamedAttribute>>(StringComparer.Ordinal);
        var potentials = new Dictionary<string, List<NamedAttribute>>(StringComparer.Ordinal);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT person_id, attribute_key, value, potential FROM person_attributes";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetString(0);
                var key = reader.GetString(1);
                Bucket(attributes, id).Add(new NamedAttribute(key, ToInt(reader.GetInt64(2))));
                Bucket(potentials, id).Add(new NamedAttribute(key, ToInt(reader.GetInt64(3))));
            }
        }

        var persons = new List<Person>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id, is_real, given_name, family_name, birth_date, nationality FROM persons ORDER BY id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var text = reader.GetString(0);
                var isReal = reader.GetInt64(1) == 1;
                var id = PersonIdFrom(text);
                if (id.IsReal != isReal)
                {
                    throw new InvalidDataException($"Person '{text}' has a flag that disagrees with its id.");
                }

                if (!roles.TryGetValue(text, out var personRoles) || !attributes.TryGetValue(text, out var current))
                {
                    throw new InvalidDataException($"Person '{text}' has no roles or no attributes.");
                }

                persons.Add(new Person(
                    id,
                    reader.GetString(2),
                    reader.GetString(3),
                    ParseDate(reader.GetString(4)),
                    reader.GetString(5),
                    isReal,
                    personRoles,
                    new PersonTruth(current, potentials[text])));
            }
        }

        return persons;
    }

    private static List<Organization> ReadOrganizations(SqliteConnection connection)
    {
        var names = new Dictionary<string, List<OrganizationNameSpan>>(StringComparer.Ordinal);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT org_id, from_date, to_date, name FROM org_names";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                Bucket(names, reader.GetString(0)).Add(new OrganizationNameSpan(
                    reader.GetString(3),
                    ParseDate(reader.GetString(1)),
                    reader.IsDBNull(2) ? null : ParseDate(reader.GetString(2))));
            }
        }

        // One stored edge becomes a successor link on one side and a predecessor link on the other.
        var links = new Dictionary<string, List<LineageLink>>(StringComparer.Ordinal);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT predecessor_id, successor_id, from_date, to_date FROM org_lineage";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var predecessor = reader.GetString(0);
                var successor = reader.GetString(1);
                var from = ParseDate(reader.GetString(2));
                GameDate? to = reader.IsDBNull(3) ? null : ParseDate(reader.GetString(3));
                Bucket(links, predecessor).Add(new LineageLink(OrganizationIdFrom(successor), LineageDirection.Successor, from, to));
                Bucket(links, successor).Add(new LineageLink(OrganizationIdFrom(predecessor), LineageDirection.Predecessor, from, to));
            }
        }

        var organizations = new List<Organization>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id, kind, is_real, founded, dissolved, budget FROM organizations ORDER BY id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var text = reader.GetString(0);
                var isReal = reader.GetInt64(2) == 1;
                var id = OrganizationIdFrom(text);
                if (id.IsReal != isReal)
                {
                    throw new InvalidDataException($"Organization '{text}' has a flag that disagrees with its id.");
                }

                if (!names.TryGetValue(text, out var spans))
                {
                    throw new InvalidDataException($"Organization '{text}' has no name.");
                }

                organizations.Add(new Organization(
                    id,
                    ParseEnum<OrganizationKind>(reader.GetString(1)),
                    isReal,
                    ParseDate(reader.GetString(3)),
                    reader.IsDBNull(4) ? null : ParseDate(reader.GetString(4)),
                    reader.GetInt64(5),
                    spans,
                    links.TryGetValue(text, out var own) ? own : []));
            }
        }

        return organizations;
    }

    private static List<Contract> ReadContracts(SqliteConnection connection)
    {
        var contracts = new List<Contract>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, person_id, organization_id, role, exclusive, start_date, end_date, salary,
                   option_deadline, option_extra_years, release_amount
            FROM contracts
            ORDER BY id
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var text = reader.GetString(0);
            if (!text.StartsWith(ContractPrefix, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Contract id '{text}' is not a contract id.");
            }

            ContractOption? option = reader.IsDBNull(8)
                ? null
                : new ContractOption(ParseDate(reader.GetString(8)), ToInt(reader.GetInt64(9)));
            ReleaseClause? release = reader.IsDBNull(10) ? null : new ReleaseClause(reader.GetInt64(10));
            contracts.Add(new Contract(
                ContractId.Generated(ParseSequence(text, ContractPrefix)),
                PersonIdFrom(reader.GetString(1)),
                OrganizationIdFrom(reader.GetString(2)),
                ParseContractRole(reader.GetString(3)),
                ParseDate(reader.GetString(5)),
                ParseDate(reader.GetString(6)),
                reader.GetInt64(7),
                reader.GetInt64(4) == 1,
                option,
                release));
        }

        return contracts;
    }

    private static List<PersonKnowledge> ReadKnowledge(SqliteConnection connection)
    {
        var bands = new Dictionary<(string Observer, string Subject), List<KnownAttribute>>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT observer_id, subject_id, attribute_key, low, high FROM knowledge_bands";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var key = (reader.GetString(0), reader.GetString(1));
                if (!bands.TryGetValue(key, out var list))
                {
                    list = [];
                    bands.Add(key, list);
                }

                list.Add(new KnownAttribute(reader.GetString(2), new AttributeBand(ToInt(reader.GetInt64(3)), ToInt(reader.GetInt64(4)))));
            }
        }

        var knowledge = new List<PersonKnowledge>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT observer_id, subject_id, potential_low, potential_high FROM knowledge ORDER BY observer_id, subject_id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var observer = reader.GetString(0);
                var subject = reader.GetString(1);
                AttributeBand? potential = reader.IsDBNull(2)
                    ? null
                    : new AttributeBand(ToInt(reader.GetInt64(2)), ToInt(reader.GetInt64(3)));
                knowledge.Add(new PersonKnowledge(
                    OrganizationIdFrom(observer),
                    PersonIdFrom(subject),
                    bands.TryGetValue((observer, subject), out var list) ? list : [],
                    potential));
            }
        }

        return knowledge;
    }

    private static List<T> Bucket<T>(Dictionary<string, List<T>> map, string key)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map.Add(key, list);
        }

        return list;
    }

    private static int ToInt(long value) => checked((int)value);

    private static PersonId PersonIdFrom(string text) =>
        text.StartsWith(PersonPrefix, StringComparison.Ordinal)
            ? Canonical(PersonId.Generated(ParseSequence(text, PersonPrefix)), text)
            : Canonical(PersonId.Real(text), text);

    private static OrganizationId OrganizationIdFrom(string text) =>
        text.StartsWith(OrganizationPrefix, StringComparison.Ordinal)
            ? Canonical(OrganizationId.Generated(ParseSequence(text, OrganizationPrefix)), text)
            : Canonical(OrganizationId.Real(text), text);

    private static PersonId Canonical(PersonId id, string text) =>
        string.Equals(id.Value, text, StringComparison.Ordinal) ? id : throw new InvalidDataException($"Id '{text}' is not canonical.");

    private static OrganizationId Canonical(OrganizationId id, string text) =>
        string.Equals(id.Value, text, StringComparison.Ordinal) ? id : throw new InvalidDataException($"Id '{text}' is not canonical.");

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
        var date = ParseDateOnly(text);
        return new GameDate(date.Year, date.Month, date.Day);
    }

    private static DateOnly ParseDateOnly(string text)
    {
        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new InvalidDataException($"Date '{text}' is malformed.");
        }

        return date;
    }

    private static T ParseEnum<T>(string text)
        where T : struct, Enum
    {
        if (!Enum.TryParse<T>(text, ignoreCase: false, out var value)
            || !Enum.IsDefined(value)
            || !string.Equals(value.ToString(), text, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"'{text}' is not a valid {typeof(T).Name}.");
        }

        return value;
    }

    private static PersonRole ParsePersonRole(string text)
    {
        if (string.Equals(text, "driver", StringComparison.Ordinal))
        {
            return PersonRole.Driver;
        }

        const string staff = "staff:";
        if (text.StartsWith(staff, StringComparison.Ordinal))
        {
            return PersonRole.Staff(ParseEnum<StaffRole>(text[staff.Length..]));
        }

        throw new InvalidDataException($"Role '{text}' is not a person role.");
    }

    private static ContractRole ParseContractRole(string text)
    {
        const string driver = "driver:";
        const string staff = "staff:";
        if (text.StartsWith(driver, StringComparison.Ordinal))
        {
            return ContractRole.Driver(ParseEnum<SeatStatus>(text[driver.Length..]));
        }

        if (text.StartsWith(staff, StringComparison.Ordinal))
        {
            return ContractRole.Staff(ParseEnum<StaffRole>(text[staff.Length..]));
        }

        throw new InvalidDataException($"Role '{text}' is not a contract role.");
    }
}
