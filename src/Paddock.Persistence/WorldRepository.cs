using System.Globalization;
using Microsoft.Data.Sqlite;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Persistence;

/// <summary>
/// Saves and loads the world through one <see cref="SaveFile"/> (TECH §6.2).
/// <para>
/// A save replaces the stored world as a whole, inside one transaction: a failure leaves the previous world
/// untouched. It is only allowed on a day boundary (INV-007): the caller names the boundary it is at, and a
/// world whose date is anything else is refused with <see cref="UnstableSaveException"/> before anything is written.
/// A successful save also moves the header (<c>meta.current_game_date</c>) to that boundary.
/// </para>
/// <para>
/// Loading rebuilds the state through <see cref="WorldState.Restore"/>, so the loaded world is checked like an
/// edited one and its <see cref="WorldState.StateHash"/> equals the saved world's.
/// </para>
/// </summary>
public sealed partial class WorldRepository
{
    private static readonly string[] WorldCounters = ["person", "organization", "contract"];

    private readonly SaveFile _file;
    private readonly IReadOnlyList<ISectionStore> _sectionStores;

    public WorldRepository(SaveFile file)
        : this(file, SectionStores.Production)
    {
    }

    /// <summary>
    /// A repository that saves the world sections through <paramref name="sectionStores"/>. A world holding a section that
    /// has no store here is refused at save time and a save that holds one is refused at load time (see <see cref="ISectionStore"/>).
    /// </summary>
    public WorldRepository(SaveFile file, IReadOnlyList<ISectionStore> sectionStores)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(sectionStores);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var store in sectionStores)
        {
            ArgumentNullException.ThrowIfNull(store);
            if (!names.Add(store.SectionName))
            {
                throw new ArgumentException($"Two stores handle section '{store.SectionName}'.", nameof(sectionStores));
            }
        }

        _file = file;
        _sectionStores = sectionStores;
    }

    /// <summary>True once a world has been saved. A save migrated from V001 or V002 has none.</summary>
    public bool HasWorld
    {
        get
        {
            using var command = _file.Connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM id_counters WHERE name = 'person'";
            return command.ExecuteScalar() is not null;
        }
    }

    /// <summary>
    /// Replaces the stored world with <paramref name="world"/>. The day clock's queue, the managers, and the
    /// command log are left exactly as they are. Use <see cref="SaveAll"/> to write those too.
    /// </summary>
    public void SaveWorld(WorldState world, GameDate stableDate)
    {
        ArgumentNullException.ThrowIfNull(world);
        RequireStable(world, stableDate);
        RequireStorableSections(world);
        InTransaction(transaction =>
        {
            WriteWorld(transaction, world);
            WriteSections(transaction, world);
            _file.RecordSavePoint(transaction, ToDateOnly(stableDate));
        });
    }

    /// <summary>Replaces the stored world, queue, managers, and command log together, in one transaction.</summary>
    public void SaveAll(WorldSnapshot snapshot, GameDate stableDate)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        RequireStable(snapshot.World, stableDate);
        RequireConsistent(snapshot);
        RequireStorableSections(snapshot.World);
        InTransaction(transaction =>
        {
            WriteWorld(transaction, snapshot.World);
            WriteSections(transaction, snapshot.World);
            WriteSchedule(transaction, snapshot);
            _file.RecordSavePoint(transaction, ToDateOnly(stableDate));
        });
    }

    private static void RequireStable(WorldState world, GameDate stableDate)
    {
        if (world.CurrentDate != stableDate)
        {
            throw new UnstableSaveException(stableDate, world.CurrentDate);
        }
    }

    private void RequireStorableSections(WorldState world)
    {
        foreach (var section in world.Sections)
        {
            var store = _sectionStores.FirstOrDefault(candidate => candidate.SectionName == section.Name)
                ?? throw new InvalidOperationException(
                    $"The world holds section '{section.Name}' and this repository has no store for it, so it cannot be saved.");
            if (section.SchemaVersion != store.SchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Section '{section.Name}' has schema version {section.SchemaVersion} but its store writes {store.SchemaVersion}.");
            }
        }
    }

    /// <summary>
    /// Replaces the stored sections: every store is asked to replace its rows (an absent section clears them),
    /// then the registry lists the sections the world holds.
    /// </summary>
    private void WriteSections(SqliteTransaction transaction, WorldState world)
    {
        var connection = transaction.Connection!;
        Execute(connection, transaction, "DELETE FROM world_sections");
        foreach (var store in _sectionStores)
        {
            var section = world.Section(store.SectionName);
            store.Replace(connection, transaction, section);
            if (section is null)
            {
                continue;
            }

            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO world_sections (name, schema_version) VALUES ($name, $version)";
            insert.Parameters.AddWithValue("$name", section.Name);
            insert.Parameters.AddWithValue("$version", (long)section.SchemaVersion);
            insert.ExecuteNonQuery();
        }
    }

    private void InTransaction(Action<SqliteTransaction> work)
    {
        using var transaction = _file.BeginTransaction();
        try
        {
            work(transaction);
            transaction.Commit();
        }
        catch (SqliteException exception)
        {
            SaveFile.RollbackQuietly(transaction);
            throw new InvalidDataException("The world could not be saved. The previous save is unchanged.", exception);
        }
        catch
        {
            SaveFile.RollbackQuietly(transaction);
            throw;
        }
    }

    private static void RequireConsistent(WorldSnapshot snapshot)
    {
        foreach (var scheduled in snapshot.Events)
        {
            ArgumentNullException.ThrowIfNull(scheduled);
            if (scheduled.Sequence < 0 || scheduled.Sequence >= snapshot.NextEventSequence)
            {
                throw new ArgumentException(
                    $"Event '{scheduled.Id}' has sequence {scheduled.Sequence}, which is not below the queue counter {snapshot.NextEventSequence}.",
                    nameof(snapshot));
            }
        }

        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var manager in snapshot.Managers)
        {
            ArgumentNullException.ThrowIfNull(manager);
            if (manager.Kind is not ("Human" or "Ai"))
            {
                throw new ArgumentException($"Manager '{manager.Id}' has unknown kind '{manager.Kind}'.", nameof(snapshot));
            }

            known.Add(manager.Id);
        }

        foreach (var command in snapshot.CommandLog)
        {
            ArgumentNullException.ThrowIfNull(command);
            if (!known.Contains(command.ManagerId))
            {
                throw new ArgumentException($"Command {command.SubmissionNumber} names unknown manager '{command.ManagerId}'.", nameof(snapshot));
            }

            if (command.SubmissionNumber < 1 || command.SubmissionNumber >= snapshot.NextSubmissionNumber)
            {
                throw new ArgumentException(
                    $"Command {command.SubmissionNumber} is not below the submission counter {snapshot.NextSubmissionNumber}.",
                    nameof(snapshot));
            }
        }
    }

    private static void WriteWorld(SqliteTransaction transaction, WorldState world)
    {
        var connection = transaction.Connection!;

        // Children first, so foreign keys hold at every statement.
        foreach (var table in new[]
                 {
                     "knowledge_bands", "knowledge", "contracts", "org_lineage", "org_names", "organizations",
                     "person_attributes", "person_roles", "persons", "retired_ids",
                 })
        {
            Execute(connection, transaction, "DELETE FROM " + table);
        }

        Execute(connection, transaction, "DELETE FROM id_counters WHERE name IN ('person', 'organization', 'contract')");

        var present = new HashSet<string>(StringComparer.Ordinal);

        using (var persons = new Insert(connection, transaction, "persons", "id", "is_real", "given_name", "family_name", "birth_date", "nationality"))
        using (var roles = new Insert(connection, transaction, "person_roles", "person_id", "role"))
        using (var attributes = new Insert(connection, transaction, "person_attributes", "person_id", "attribute_key", "value", "potential"))
        {
            foreach (var person in world.Persons)
            {
                var id = person.Id.Value;
                present.Add(id);
                persons.Run(id, person.IsReal ? 1L : 0L, person.GivenName, person.FamilyName, person.BirthDate.ToString(), person.Nationality);
                foreach (var role in person.Roles)
                {
                    roles.Run(id, role.ToString());
                }

                var truth = person.Truth;
                for (var i = 0; i < truth.Attributes.Count; i++)
                {
                    attributes.Run(id, truth.Attributes[i].Key, (long)truth.Attributes[i].Value, (long)truth.Potential[i].Value);
                }
            }
        }

        var organizations = world.Organizations;
        using (var rows = new Insert(connection, transaction, "organizations", "id", "kind", "is_real", "founded", "dissolved", "budget"))
        using (var names = new Insert(connection, transaction, "org_names", "org_id", "from_date", "to_date", "name"))
        {
            foreach (var organization in organizations)
            {
                var id = organization.Id.Value;
                present.Add(id);
                rows.Run(id, organization.Kind.ToString(), organization.IsReal ? 1L : 0L, organization.Founded.ToString(), organization.Dissolved?.ToString(), organization.Budget);
                foreach (var span in organization.Names)
                {
                    names.Run(id, span.From.ToString(), span.To?.ToString(), span.Name);
                }
            }
        }

        WriteLineage(connection, transaction, organizations);

        using (var contracts = new Insert(
                   connection,
                   transaction,
                   "contracts",
                   "id", "person_id", "organization_id", "role", "exclusive", "start_date", "end_date", "salary",
                   "option_deadline", "option_extra_years", "release_amount"))
        {
            foreach (var contract in world.Contracts)
            {
                present.Add(contract.Id.Value);
                contracts.Run(
                    contract.Id.Value,
                    contract.PersonId.Value,
                    contract.OrganizationId.Value,
                    contract.Role.ToString(),
                    contract.Exclusive ? 1L : 0L,
                    contract.Start.ToString(),
                    contract.End.ToString(),
                    contract.Salary,
                    contract.Option?.Deadline.ToString(),
                    contract.Option is ContractOption option ? (long)option.ExtraYears : null,
                    contract.ReleaseClause?.Amount);
            }
        }

        using (var beliefs = new Insert(connection, transaction, "knowledge", "observer_id", "subject_id", "potential_low", "potential_high"))
        using (var bands = new Insert(connection, transaction, "knowledge_bands", "observer_id", "subject_id", "attribute_key", "low", "high"))
        {
            foreach (var belief in world.Knowledge)
            {
                var observer = belief.ObserverId.Value;
                var subject = belief.SubjectId.Value;
                beliefs.Run(observer, subject, belief.Potential is AttributeBand p ? (long)p.Low : null, belief.Potential is AttributeBand q ? (long)q.High : null);
                foreach (var attribute in belief.Attributes)
                {
                    bands.Run(observer, subject, attribute.Key, (long)attribute.Band.Low, (long)attribute.Band.High);
                }
            }
        }

        var ids = world.Ids;
        using (var retired = new Insert(connection, transaction, "retired_ids", "id"))
        {
            foreach (var issued in ids.Issued)
            {
                if (!present.Contains(issued))
                {
                    retired.Run(issued);
                }
            }
        }

        using (var counters = new Insert(connection, transaction, "id_counters", "name", "next_value"))
        {
            counters.Run("person", ids.NextPerson);
            counters.Run("organization", ids.NextOrganization);
            counters.Run("contract", ids.NextContract);
        }
    }

    private static void WriteLineage(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<Organization> organizations)
    {
        // The domain keeps each edge on both organizations. Store it once, from the predecessor's side, and
        // refuse a world whose two sides disagree, because the loader would derive them from this one row.
        var successors = new HashSet<(string Predecessor, string Successor, GameDate From, GameDate? To)>();
        using (var edges = new Insert(connection, transaction, "org_lineage", "predecessor_id", "successor_id", "from_date", "to_date"))
        {
            foreach (var organization in organizations)
            {
                foreach (var link in organization.Lineage)
                {
                    if (link.Direction != LineageDirection.Successor)
                    {
                        continue;
                    }

                    successors.Add((organization.Id.Value, link.OtherId.Value, link.From, link.To));
                    edges.Run(organization.Id.Value, link.OtherId.Value, link.From.ToString(), link.To?.ToString());
                }
            }
        }

        var predecessors = 0;
        foreach (var organization in organizations)
        {
            foreach (var link in organization.Lineage)
            {
                if (link.Direction != LineageDirection.Predecessor)
                {
                    continue;
                }

                predecessors++;
                if (!successors.Contains((link.OtherId.Value, organization.Id.Value, link.From, link.To)))
                {
                    throw new InvalidOperationException(
                        $"Lineage of '{organization.Id}' is not mirrored by '{link.OtherId}', so it cannot be stored as one edge.");
                }
            }
        }

        if (predecessors != successors.Count)
        {
            throw new InvalidOperationException("Lineage predecessor and successor links do not pair up.");
        }
    }

    private static void WriteSchedule(SqliteTransaction transaction, WorldSnapshot snapshot)
    {
        var connection = transaction.Connection!;
        foreach (var table in new[] { "command_log", "managers", "scheduled_events" })
        {
            Execute(connection, transaction, "DELETE FROM " + table);
        }

        Execute(connection, transaction, "DELETE FROM id_counters WHERE name IN ('event_id', 'event_sequence', 'submission')");

        using (var events = new Insert(connection, transaction, "scheduled_events", "id", "event_date", "sequence", "type_id", "payload_type", "payload"))
        {
            foreach (var scheduled in snapshot.Events)
            {
                events.Run(scheduled.Id, scheduled.Date.ToString(), scheduled.Sequence, scheduled.TypeId, scheduled.PayloadType, scheduled.Payload);
            }
        }

        using (var managers = new Insert(connection, transaction, "managers", "id", "ordinal", "kind", "display_name", "blocking_kind"))
        {
            for (var i = 0; i < snapshot.Managers.Count; i++)
            {
                var manager = snapshot.Managers[i];
                managers.Run(manager.Id, (long)i, manager.Kind, manager.DisplayName, manager.BlockingKind);
            }
        }

        using (var log = new Insert(connection, transaction, "command_log", "position", "submission_number", "manager_id", "issued_on", "command_type", "payload"))
        {
            for (var i = 0; i < snapshot.CommandLog.Count; i++)
            {
                var command = snapshot.CommandLog[i];
                log.Run((long)i, command.SubmissionNumber, command.ManagerId, FormatDate(command.IssuedOn), command.CommandType, command.Payload);
            }
        }

        using var counters = new Insert(connection, transaction, "id_counters", "name", "next_value");
        counters.Run("event_id", snapshot.NextEventId);
        counters.Run("event_sequence", snapshot.NextEventSequence);
        counters.Run("submission", snapshot.NextSubmissionNumber);
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string FormatDate(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateOnly ToDateOnly(GameDate date) => new(date.Year, date.Month, date.Day);

    /// <summary>One prepared INSERT, run once per row.</summary>
    private sealed class Insert : IDisposable
    {
        private readonly SqliteCommand _command;
        private readonly SqliteParameter[] _parameters;

        public Insert(SqliteConnection connection, SqliteTransaction transaction, string table, params string[] columns)
        {
            _command = connection.CreateCommand();
            _command.Transaction = transaction;
            _command.CommandText =
                "INSERT INTO " + table + " (" + string.Join(", ", columns) + ") VALUES ("
                + string.Join(", ", columns.Select(static (_, i) => "$p" + i.ToString(CultureInfo.InvariantCulture))) + ")";
            _parameters = new SqliteParameter[columns.Length];
            for (var i = 0; i < columns.Length; i++)
            {
                _parameters[i] = _command.CreateParameter();
                _parameters[i].ParameterName = "$p" + i.ToString(CultureInfo.InvariantCulture);
                _command.Parameters.Add(_parameters[i]);
            }

            _command.Prepare();
        }

        public void Run(params object?[] values)
        {
            for (var i = 0; i < _parameters.Length; i++)
            {
                _parameters[i].Value = values[i] ?? DBNull.Value;
            }

            _command.ExecuteNonQuery();
        }

        public void Dispose() => _command.Dispose();
    }
}
