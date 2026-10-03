using Microsoft.Data.Sqlite;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;
using Paddock.Simulation.Time;

namespace Paddock.Tests.Persistence;

public class WorldRepositoryTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-world-").FullName;

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void FreshSaveHasNoWorldAndLoadingSaysSo()
    {
        using var save = SaveFile.Create(NewPath(), WorldFixtures.Meta());
        var repository = new WorldRepository(save);

        Assert.False(repository.HasWorld);
        Assert.Throws<InvalidOperationException>(() => repository.LoadWorld());
        Assert.Throws<InvalidOperationException>(() => repository.LoadAll());
    }

    [Fact]
    public void WorldRoundTripsWithAnIdenticalStateHash()
    {
        var world = WorldFixtures.Small();
        Assert.Contains(world.Persons, person => person.IsReal);
        Assert.Contains(world.Persons, person => !person.IsReal);
        Assert.NotEmpty(world.Contracts);
        Assert.NotEmpty(world.Knowledge);

        var path = NewPath();
        using (var save = SaveFile.Create(path, WorldFixtures.Meta()))
        {
            var repository = new WorldRepository(save);
            repository.SaveWorld(world, WorldFixtures.Opening);

            Assert.True(repository.HasWorld);
            var loaded = repository.LoadWorld();
            Assert.Equal(world.StateHash(), loaded.StateHash());
            AssertSameParts(world, loaded);
        }

        using var reopened = SaveFile.Open(path);
        var again = new WorldRepository(reopened).LoadWorld();
        Assert.Equal(world.StateHash(), again.StateHash());
        Assert.Equal(WorldFixtures.Opening, again.CurrentDate);
    }

    [Fact]
    public void IssuedIdsStayBurnedAfterALoad()
    {
        var world = WorldFixtures.Small();
        var removedGenerated = world.Ids.Issued.First(id => id.StartsWith("gen:", StringComparison.Ordinal) && !world.Persons.Any(person => person.Id.Value == id));
        var removedContract = world.Ids.Issued.First(id => id.StartsWith("con:", StringComparison.Ordinal) && !world.Contracts.Any(contract => contract.Id.Value == id));

        using var save = SaveFile.Create(NewPath(), WorldFixtures.Meta());
        var repository = new WorldRepository(save);
        repository.SaveWorld(world, WorldFixtures.Opening);
        var loaded = repository.LoadWorld();

        Assert.True(loaded.Ids.WasIssued(removedGenerated));
        Assert.True(loaded.Ids.WasIssued(removedContract));
        Assert.Equal(world.Ids.NextPerson, loaded.Ids.NextPerson);
        Assert.Equal(world.Ids.NextOrganization, loaded.Ids.NextOrganization);
        Assert.Equal(world.Ids.NextContract, loaded.Ids.NextContract);
        Assert.Equal(world.Ids.Issued, loaded.Ids.Issued);

        var (grown, fresh) = loaded.AddPerson(WorldFixtures.GeneratedSpec());
        Assert.Equal("gen:" + world.Ids.NextPerson, fresh.Value);
        Assert.NotEqual(removedGenerated, fresh.Value);
        Assert.Equal(grown.StateHash(), world.AddPerson(WorldFixtures.GeneratedSpec()).State.StateHash());
    }

    [Fact]
    public void LoadedWorldKeepsItsCurrentDateFromTheBoundaryItWasSavedAt()
    {
        var later = WorldFixtures.Small().WithDate(new GameDate(1955, 6, 1));
        using var save = SaveFile.Create(NewPath(), WorldFixtures.Meta());
        var repository = new WorldRepository(save);
        repository.SaveWorld(later, new GameDate(1955, 6, 1));

        Assert.Equal(new DateOnly(1955, 6, 1), save.ReadMeta().CurrentGameDate);
        Assert.Equal(new GameDate(1955, 6, 1), repository.LoadWorld().CurrentDate);
    }

    [Fact]
    public void MidDayStateIsRefusedAndNothingIsWritten()
    {
        var world = WorldFixtures.Small();
        using var save = SaveFile.Create(NewPath(), WorldFixtures.Meta());
        var repository = new WorldRepository(save);
        var before = save.ReadMeta();

        var refusal = Assert.Throws<UnstableSaveException>(() => repository.SaveWorld(world, new GameDate(1955, 1, 2)));
        Assert.Equal(new GameDate(1955, 1, 2), refusal.Boundary);
        Assert.Equal(WorldFixtures.Opening, refusal.WorldDate);
        Assert.Throws<UnstableSaveException>(() => repository.SaveWorld(world.WithDate(new GameDate(1955, 1, 2)), WorldFixtures.Opening));
        var snapshot = Snapshot(world);
        Assert.Throws<UnstableSaveException>(() => repository.SaveAll(snapshot, new GameDate(1955, 1, 2)));

        Assert.False(repository.HasWorld);
        Assert.Equal(before, save.ReadMeta());
    }

    [Fact]
    public void SavingAgainReplacesTheWorldAsAWhole()
    {
        var first = WorldFixtures.Small();
        var second = first
            .RemoveContract(first.Contracts[0].Id)
            .WithDate(new GameDate(1955, 1, 2));
        var (second2, _) = second.AddPerson(WorldFixtures.GeneratedSpec());

        var path = NewPath();
        using (var save = SaveFile.Create(path, WorldFixtures.Meta()))
        {
            var repository = new WorldRepository(save);
            repository.SaveWorld(first, WorldFixtures.Opening);
            repository.SaveWorld(second2, new GameDate(1955, 1, 2));

            var loaded = repository.LoadWorld();
            Assert.Equal(second2.StateHash(), loaded.StateHash());
            Assert.NotEqual(first.StateHash(), loaded.StateHash());
            Assert.Equal(first.Contracts.Count - 1, loaded.Contracts.Count);
        }

        using var raw = OpenRaw(path);
        Assert.Equal(0, ForeignKeyViolations(raw));
        Assert.Equal(first.Persons.Count + 1, Count(raw, "persons"));
    }

    [Fact]
    public void AFailedSaveLeavesThePreviousWorldIntact()
    {
        var first = WorldFixtures.Small();
        var second = first.RemoveContract(first.Contracts[0].Id);
        var path = NewPath();
        using var save = SaveFile.Create(path, WorldFixtures.Meta());
        var repository = new WorldRepository(save);
        repository.SaveWorld(first, WorldFixtures.Opening);
        var before = save.ReadMeta();

        // A trigger that rejects the last table the save writes, after everything else was already replaced.
        using (var sabotage = OpenRaw(path))
        {
            Run(sabotage, "CREATE TRIGGER boom BEFORE INSERT ON id_counters BEGIN SELECT RAISE(ABORT, 'boom'); END");
        }

        var failure = Assert.Throws<InvalidDataException>(() => repository.SaveWorld(second, WorldFixtures.Opening));
        Assert.IsType<SqliteException>(failure.InnerException);

        using (var repair = OpenRaw(path))
        {
            Run(repair, "DROP TRIGGER boom");
        }

        Assert.Equal(first.StateHash(), repository.LoadWorld().StateHash());
        Assert.Equal(before.CurrentGameDate, save.ReadMeta().CurrentGameDate);
        Assert.Equal(before.SavedAtUtc, save.ReadMeta().SavedAtUtc);
    }

    [Fact]
    public void SaveWorldLeavesTheScheduleManagersAndLogAlone()
    {
        var world = WorldFixtures.Small();
        using var save = SaveFile.Create(NewPath(), WorldFixtures.Meta());
        var repository = new WorldRepository(save);
        var snapshot = Snapshot(world);
        repository.SaveAll(snapshot, WorldFixtures.Opening);

        repository.SaveWorld(world.RemoveContract(world.Contracts[0].Id), WorldFixtures.Opening);

        var loaded = repository.LoadAll();
        Assert.Equal(snapshot.Events, loaded.Events);
        Assert.Equal(snapshot.Managers, loaded.Managers);
        Assert.Equal(snapshot.CommandLog, loaded.CommandLog);
        Assert.Equal(snapshot.NextSubmissionNumber, loaded.NextSubmissionNumber);
    }

    [Fact]
    public void QueueManagersAndCommandLogRoundTripInTheirOwnOrder()
    {
        var world = WorldFixtures.Small();
        var snapshot = Snapshot(world);
        var path = NewPath();
        using (var save = SaveFile.Create(path, WorldFixtures.Meta()))
        {
            new WorldRepository(save).SaveAll(snapshot, WorldFixtures.Opening);
        }

        using var reopened = SaveFile.Open(path);
        var loaded = new WorldRepository(reopened).LoadAll();

        Assert.Equal(world.StateHash(), loaded.World.StateHash());
        Assert.Equal(snapshot.Events, loaded.Events);
        Assert.Equal(snapshot.NextEventId, loaded.NextEventId);
        Assert.Equal(snapshot.NextEventSequence, loaded.NextEventSequence);
        Assert.Equal(snapshot.Managers, loaded.Managers);
        Assert.Equal(snapshot.NextSubmissionNumber, loaded.NextSubmissionNumber);
        // Execution order is kept even where it is not submission order.
        Assert.Equal([2L, 1L, 3L], loaded.CommandLog.Select(command => command.SubmissionNumber).ToArray());
        Assert.Equal(snapshot.CommandLog, loaded.CommandLog);
    }

    [Fact]
    public void ScheduledEventsMapToAndFromTheirStoredForm()
    {
        var race = new ScheduledEvent(EventId.FromCounter(3), new GameDate(1955, 5, 14), ScheduledEventType.Race, new RaceSessionPayload(1955, 2, "layout-a")) { Sequence = 5 };
        var marker = new ScheduledEvent(new EventId("custom"), new GameDate(1955, 5, 14), "marker", new MarkerPayload("renew")) { Sequence = 6 };
        var stored = new[] { ToStored(race), ToStored(marker) };

        using var save = SaveFile.Create(NewPath(), WorldFixtures.Meta());
        var repository = new WorldRepository(save);
        var world = WorldFixtures.Small();
        repository.SaveAll(new WorldSnapshot(world, stored, 4, 7, [], [], 1), WorldFixtures.Opening);
        var loaded = repository.LoadAll();

        Assert.Equal(race, FromStored(loaded.Events[0]));
        Assert.Equal(marker, FromStored(loaded.Events[1]));
        Assert.Equal(4, loaded.NextEventId);
        Assert.Equal(7, loaded.NextEventSequence);
    }

    [Fact]
    public void InconsistentSnapshotsAreRefusedBeforeAnythingIsWritten()
    {
        var world = WorldFixtures.Small();
        using var save = SaveFile.Create(NewPath(), WorldFixtures.Meta());
        var repository = new WorldRepository(save);
        var ghost = new StoredCommand(1, "nobody", new DateOnly(1955, 1, 1), "x", "{}");
        var manager = new StoredManager("m1", "Human", "Ada", null);
        var late = new StoredEvent("e1", WorldFixtures.Opening, 9, "t", "p", "{}");

        Assert.Throws<ArgumentException>(() => repository.SaveAll(new WorldSnapshot(world, [], 1, 0, [manager], [ghost], 2), WorldFixtures.Opening));
        Assert.Throws<ArgumentException>(() => repository.SaveAll(new WorldSnapshot(world, [late], 2, 5, [], [], 1), WorldFixtures.Opening));
        Assert.Throws<ArgumentException>(() => repository.SaveAll(new WorldSnapshot(world, [], 1, 0, [manager with { Kind = "Robot" }], [], 1), WorldFixtures.Opening));
        Assert.Throws<ArgumentException>(() => repository.SaveAll(
            new WorldSnapshot(world, [], 1, 0, [manager], [ghost with { ManagerId = "m1", SubmissionNumber = 5 }], 5), WorldFixtures.Opening));
        Assert.False(repository.HasWorld);
    }

    [Fact]
    public void ForeignKeysRejectRowsThatPointNowhere()
    {
        var path = NewPath();
        using (var save = SaveFile.Create(path, WorldFixtures.Meta()))
        {
            new WorldRepository(save).SaveWorld(WorldFixtures.Small(), WorldFixtures.Opening);
        }

        using var raw = OpenRaw(path);
        Assert.Throws<SqliteException>(() => Run(raw, """
            INSERT INTO contracts (id, person_id, organization_id, role, exclusive, start_date, end_date, salary)
            VALUES ('con:999999', 'gen:999999', 'org:999999', 'driver:Equal', 1, '1955-01-01', '1955-12-31', 1)
            """));
        Assert.Throws<SqliteException>(() => Run(raw, "DELETE FROM persons WHERE id = (SELECT person_id FROM contracts LIMIT 1)"));
        Assert.Throws<SqliteException>(() => Run(raw, "UPDATE person_attributes SET potential = 1, value = 9 WHERE person_id = (SELECT person_id FROM person_attributes LIMIT 1)"));
    }

    [Fact]
    public void StoredRowsThatFormNoValidWorldAreRefusedOnLoad()
    {
        var path = NewPath();
        using var save = SaveFile.Create(path, WorldFixtures.Meta());
        var repository = new WorldRepository(save);
        repository.SaveWorld(WorldFixtures.Small(), WorldFixtures.Opening);

        using (var raw = OpenRaw(path))
        {
            Run(raw, "UPDATE id_counters SET next_value = 1 WHERE name = 'person'");
        }

        Assert.Throws<InvalidDataException>(() => repository.LoadWorld());

        using (var raw = OpenRaw(path))
        {
            Run(raw, "UPDATE id_counters SET next_value = 100000 WHERE name = 'person'");
            Run(raw, "DELETE FROM person_roles WHERE person_id = (SELECT id FROM persons LIMIT 1)");
        }

        Assert.Throws<InvalidDataException>(() => repository.LoadWorld());
    }

    [Fact]
    public void TheSameWorldSavedTwiceGivesTheSameRows()
    {
        var world = WorldFixtures.Small();
        var left = DumpRows(world, "left");
        var right = DumpRows(world, "right");
        Assert.Equal(left, right);
    }

    private string DumpRows(WorldState world, string name)
    {
        var path = Path.Combine(_directory, name + ".paddock");
        using (var save = SaveFile.Create(path, WorldFixtures.Meta()))
        {
            new WorldRepository(save).SaveWorld(world, WorldFixtures.Opening);
        }

        using var raw = OpenRaw(path);
        var rows = new List<string>();
        foreach (var table in new[] { "persons", "person_roles", "person_attributes", "organizations", "org_names", "org_lineage", "contracts", "knowledge", "knowledge_bands", "retired_ids", "id_counters" })
        {
            using var command = raw.CreateCommand();
            command.CommandText = "SELECT * FROM " + table;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var values = new object[reader.FieldCount];
                reader.GetValues(values);
                rows.Add(table + "|" + string.Join("|", values));
            }
        }

        return string.Join("\n", rows);
    }

    private static WorldSnapshot Snapshot(WorldState world) => new(
        world,
        [
            new StoredEvent("e1", new GameDate(1955, 1, 20), 0, "race.practice", "race-session", "1955;1;layout-a"),
            new StoredEvent("e2", new GameDate(1955, 1, 20), 1, "race.race", "race-session", "1955;1;layout-a"),
            new StoredEvent("e3", new GameDate(1955, 3, 1), 2, "marker", "marker", "renew"),
        ],
        4,
        3,
        [new StoredManager("m1", "Human", "Ada", null), new StoredManager("m2", "Ai", "Rival", "negotiation")],
        [
            new StoredCommand(2, "m1", new DateOnly(1955, 1, 1), "sign", "{\"person\":1}"),
            new StoredCommand(1, "m2", new DateOnly(1955, 1, 2), "scout", "{}"),
            new StoredCommand(3, "m1", new DateOnly(1955, 1, 2), "sign", "{\"person\":2}"),
        ],
        4);

    private static StoredEvent ToStored(ScheduledEvent scheduled) => scheduled.Payload switch
    {
        RaceSessionPayload race => new StoredEvent(
            scheduled.Id.Value, scheduled.Date, (long)scheduled.Sequence, scheduled.TypeId, "race-session", $"{race.Season};{race.Round};{race.LayoutId}"),
        MarkerPayload marker => new StoredEvent(
            scheduled.Id.Value, scheduled.Date, (long)scheduled.Sequence, scheduled.TypeId, "marker", marker.Marker),
        _ => throw new InvalidOperationException("Unknown payload."),
    };

    private static ScheduledEvent FromStored(StoredEvent stored)
    {
        EventPayload payload;
        if (stored.PayloadType == "race-session")
        {
            var parts = stored.Payload.Split(';');
            payload = new RaceSessionPayload(int.Parse(parts[0]), int.Parse(parts[1]), parts[2]);
        }
        else
        {
            payload = new MarkerPayload(stored.Payload);
        }

        return new ScheduledEvent(new EventId(stored.Id), stored.Date, stored.TypeId, payload) { Sequence = (ulong)stored.Sequence };
    }

    private static void AssertSameParts(WorldState expected, WorldState actual)
    {
        Assert.Equal(expected.Persons.Select(person => person.Id), actual.Persons.Select(person => person.Id));
        Assert.Equal(expected.Organizations.Select(organization => organization.Id), actual.Organizations.Select(organization => organization.Id));
        Assert.Equal(expected.Contracts.Select(contract => contract.Id), actual.Contracts.Select(contract => contract.Id));
        Assert.Equal(expected.Knowledge.Count, actual.Knowledge.Count);
        foreach (var organization in expected.Organizations)
        {
            Assert.Equal(expected.LineageChain(organization.Id), actual.LineageChain(organization.Id));
        }

        foreach (var person in expected.Persons)
        {
            var other = actual.GetPerson(person.Id);
            Assert.Equal(person.IsReal, other.IsReal);
            Assert.Equal(person.Roles, other.Roles);
            Assert.Equal(person.Truth.Attributes, other.Truth.Attributes);
            Assert.Equal(person.Truth.Potential, other.Truth.Potential);
        }
    }

    private string NewPath() => Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".paddock");

    private static SqliteConnection OpenRaw(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
        }.ToString());
        connection.Open();
        Run(connection, "PRAGMA foreign_keys = ON");
        return connection;
    }

    private static void Run(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static long Count(SqliteConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM " + table;
        return (long)command.ExecuteScalar()!;
    }

    private static int ForeignKeyViolations(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check";
        using var reader = command.ExecuteReader();
        var count = 0;
        while (reader.Read())
        {
            count++;
        }

        return count;
    }
}
