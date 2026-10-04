using Microsoft.Data.Sqlite;
using Paddock.Domain.Random;
using Paddock.Persistence;
using Paddock.SimRunner;

namespace Paddock.Tests.Persistence;

/// <summary>V007 and the run state of a save: RNG stream states, the talent pool, the tallies and the season summaries (issue #123).</summary>
public sealed class CareerRunStoreTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-v7-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void ARunStateAndRngStatesRoundTripWithTheWorld()
    {
        using var save = SaveFile.Create(Path.Combine(_directory, "a.paddock"), WorldFixtures.Meta());
        var repository = new WorldRepository(save);
        var world = WorldFixtures.Small();
        var states = Rng(1);
        var run = Run();

        repository.SaveAll(Snapshot(world, run, states), WorldFixtures.Opening);
        var loaded = repository.LoadAll();

        Assert.Equal(world.StateHash(), loaded.World.StateHash());
        Assert.Equal(run.OpenedYear, loaded.Run!.OpenedYear);
        Assert.Equal(run.ContractExpiries, loaded.Run.ContractExpiries);
        Assert.Equal(run.Intakes, loaded.Run.Intakes);
        Assert.Equal(run.Years, loaded.Run.Years);
        Assert.NotNull(loaded.RngStates);
        Assert.Equal(states.Count, loaded.RngStates!.Count);
        foreach (var pair in states)
        {
            Assert.Equal(pair.Value, loaded.RngStates[pair.Key]);
        }

        // Closing and reopening the file keeps it.
        save.Dispose();
        using var reopened = SaveFile.Open(Path.Combine(_directory, "a.paddock"));
        Assert.Equal(run.Years, new WorldRepository(reopened).LoadAll().Run!.Years);
        Assert.Equal(states["People"], reopened.ReadRngStates()["People"]);
    }

    [Fact]
    public void ASaveWithoutRunStateReplacesWhatAnEarlierSaveLeft()
    {
        using var save = SaveFile.Create(Path.Combine(_directory, "b.paddock"), WorldFixtures.Meta());
        var repository = new WorldRepository(save);
        var world = WorldFixtures.Small();
        repository.SaveAll(Snapshot(world, Run(), Rng(2)), WorldFixtures.Opening);

        repository.SaveAll(Snapshot(world, null, null), WorldFixtures.Opening);
        var loaded = repository.LoadAll();

        Assert.Null(loaded.Run);
        Assert.Null(loaded.RngStates);
        Assert.Throws<InvalidOperationException>(() => save.ReadRngStates());
        Assert.Equal(0, Count(save, "career_years"));
        Assert.Equal(0, Count(save, "career_run"));
    }

    [Fact]
    public void AWorldOnlySaveLeavesTheRunStateAlone()
    {
        using var save = SaveFile.Create(Path.Combine(_directory, "c.paddock"), WorldFixtures.Meta());
        var repository = new WorldRepository(save);
        var world = WorldFixtures.Small();
        var run = Run();
        repository.SaveAll(Snapshot(world, run, Rng(3)), WorldFixtures.Opening);

        repository.SaveWorld(world, WorldFixtures.Opening);

        Assert.Equal(run.Years, repository.LoadAll().Run!.Years);
        Assert.Equal(run.OpenedYear, repository.LoadAll().Run!.OpenedYear);
    }

    [Fact]
    public void AFailedSaveLeavesTheRunStateAndTheRngStatesOfThePreviousSave()
    {
        using var save = SaveFile.Create(Path.Combine(_directory, "d.paddock"), WorldFixtures.Meta());
        var repository = new WorldRepository(save);
        var world = WorldFixtures.Small();
        var run = Run();
        var states = Rng(4);
        repository.SaveAll(Snapshot(world, run, states), WorldFixtures.Opening);

        var broken = run with { Years = [.. run.Years, run.Years[0]] };
        Assert.Throws<InvalidDataException>(() => repository.SaveAll(Snapshot(world, broken, Rng(5)), WorldFixtures.Opening));

        var loaded = repository.LoadAll();
        Assert.Equal(run.Years, loaded.Run!.Years);
        Assert.Equal(states["Weather"], loaded.RngStates!["Weather"]);
    }

    [Fact]
    public void RngStatesMustNameEveryStreamOnce()
    {
        using var save = SaveFile.Create(Path.Combine(_directory, "e.paddock"), WorldFixtures.Meta());
        var repository = new WorldRepository(save);
        var partial = Rng(6).Where(pair => pair.Key != "Weather").ToDictionary(pair => pair.Key, pair => pair.Value);

        Assert.Throws<ArgumentException>(() => repository.SaveAll(Snapshot(WorldFixtures.Small(), Run(), partial), WorldFixtures.Opening));
    }

    [Fact]
    public void AV006SaveMigratesToV007HasNoRunStateAndRefusesToResume()
    {
        var path = Path.Combine(_directory, "old.paddock");
        var world = WorldFixtures.Small();
        using (var created = SaveFile.Create(path, WorldFixtures.Meta(), [.. SaveMigrations.Production.Take(6)]))
        {
            Assert.Equal(6, created.ReadMeta().SchemaVersion);
            new WorldRepository(created, []).SaveWorld(world, WorldFixtures.Opening);
        }

        Assert.DoesNotContain("career_run", Tables(path));
        using (var opened = SaveFile.Open(path))
        {
            Assert.Equal(SaveMigrations.CurrentVersion, opened.ReadMeta().SchemaVersion);
            var repository = new WorldRepository(opened);
            Assert.True(repository.HasWorld);
            var snapshot = repository.LoadAll();
            Assert.Equal(world.StateHash(), snapshot.World.StateHash());
            Assert.Null(snapshot.Run);
            Assert.Null(snapshot.RngStates);
            Assert.Equal(0, Count(opened, "career_run"));

            // The migrated file takes a full save from now on.
            repository.SaveAll(Snapshot(world, Run(), Rng(7)), WorldFixtures.Opening);
            Assert.NotNull(repository.LoadAll().Run);
        }

        Assert.Contains("career_run", Tables(path));
        Assert.Contains("career_years", Tables(path));
        Assert.DoesNotContain("talent_pool", Tables(path));
        Assert.Contains("pool_members", Tables(path));

        var older = Path.Combine(_directory, "old2.paddock");
        using (var created = SaveFile.Create(older, WorldFixtures.Meta(), [.. SaveMigrations.Production.Take(6)]))
        {
            new WorldRepository(created, []).SaveWorld(world, WorldFixtures.Opening);
        }

        var refusal = Assert.Throws<SaveNotResumableException>(() => CareerSaveReader.Read(older));
        Assert.Contains("cannot be resumed exactly", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void V007ChecksItsRows()
    {
        using var save = SaveFile.Create(Path.Combine(_directory, "f.paddock"), WorldFixtures.Meta());
        Assert.Throws<SqliteException>(() => Exec(save, "INSERT INTO career_run (id, opened_year, contract_expiries, intakes) VALUES (2, 1950, 0, 0)"));
        Assert.Throws<SqliteException>(() => Exec(save, "INSERT INTO career_run (id, opened_year, contract_expiries, intakes) VALUES (1, 1950, -1, 0)"));
        Assert.Throws<SqliteException>(() => Exec(save, "INSERT INTO career_years (year, alive, retired, pool, contracts, state_hash) VALUES (1950, 1, 0, 0, 0, '')"));
    }

    private static WorldSnapshot Snapshot(Paddock.Domain.World.WorldState world, CareerRunState? run, IReadOnlyDictionary<string, RngState>? states) =>
        new(world, [], 1, 0, [new StoredManager("ai:paddock", "Ai", "AI", null)], [], 1)
        {
            Run = run,
            RngStates = states,
        };

    private static CareerRunState Run() => new(
        1950,
        7,
        31,
        [new StoredYear(1950, 4, 0, 0, 5, new string('a', 64), 1, 1, 0), new StoredYear(1951, 6, 1, 2, 3, new string('b', 64), 0, 0, 2)]);

    private static Dictionary<string, RngState> Rng(ulong seed)
    {
        var states = new Dictionary<string, RngState>(StringComparer.Ordinal);
        foreach (var name in RngStreamName.All)
        {
            states[name] = RngStreams.Derive(seed, name, 1955).State;
        }

        return states;
    }

    private static long Count(SaveFile save, string table)
    {
        using var command = save.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM " + table;
        return (long)command.ExecuteScalar()!;
    }

    private static void Exec(SaveFile save, string sql)
    {
        using var command = save.Connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static List<string> Tables(string path)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}
