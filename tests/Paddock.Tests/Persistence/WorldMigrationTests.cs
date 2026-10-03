using Microsoft.Data.Sqlite;
using Paddock.Persistence;

namespace Paddock.Tests.Persistence;

/// <summary>V003 on top of V001 and V002 saves, with the refusal and rollback rules of the migration runner.</summary>
public class WorldMigrationTests : IDisposable
{
    private static readonly string[] WorldTables =
    [
        "persons", "person_roles", "person_attributes", "organizations", "org_names", "org_lineage", "contracts",
        "knowledge", "knowledge_bands", "id_counters", "retired_ids", "scheduled_events", "managers", "command_log",
    ];

    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-v3-").FullName;

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void V001SaveMigratesToV003WithEmptyWorldTables()
    {
        var path = NewPath();
        var meta = WorldFixtures.Meta();
        using (SaveFile.Create(path, meta, [new V001_Initial()]))
        {
        }

        Assert.Empty(Tables(path).Intersect(WorldTables));

        using var opened = SaveFile.Open(path);
        var read = opened.ReadMeta();
        Assert.Equal(SaveMigrations.CurrentVersion, read.SchemaVersion);
        Assert.Equal(meta.CareerName, read.CareerName);
        Assert.Equal(meta.CurrentGameDate, read.CurrentGameDate);
        Assert.Equal(meta.MasterSeed, read.MasterSeed);
        AssertEmptyWorldTables(path);

        var repository = new WorldRepository(opened);
        Assert.False(repository.HasWorld);
        var world = WorldFixtures.Small();
        repository.SaveWorld(world, WorldFixtures.Opening);
        Assert.Equal(world.StateHash(), repository.LoadWorld().StateHash());
    }

    [Fact]
    public void V002SaveMigratesToV003AndKeepsItsCareerConfig()
    {
        var path = NewPath();
        var meta = WorldFixtures.Meta();
        using (SaveFile.Create(path, meta, [new V001_Initial(), new V002_CareerConfig()]))
        {
        }

        using (var opened = SaveFile.Open(path))
        {
            var read = opened.ReadMeta();
            Assert.Equal(SaveMigrations.CurrentVersion, read.SchemaVersion);
            Assert.Equal(meta.CareerConfig, read.CareerConfig);
            Assert.False(new WorldRepository(opened).HasWorld);
        }

        AssertEmptyWorldTables(path);
        using var again = SaveFile.Open(path);
        Assert.Equal(SaveMigrations.CurrentVersion, again.ReadMeta().SchemaVersion);
    }

    [Fact]
    public void V005SaveMigratesToV006WithAnEmptyRetirementColumn()
    {
        var path = NewPath();
        using (SaveFile.Create(path, WorldFixtures.Meta(), [.. SaveMigrations.Production.Take(5)]))
        {
        }

        Assert.DoesNotContain("retired_on", PersonColumns(path));
        using (var opened = SaveFile.Open(path))
        {
            Assert.Equal(SaveMigrations.CurrentVersion, opened.ReadMeta().SchemaVersion);
        }

        Assert.Contains("retired_on", PersonColumns(path));
    }

    [Fact]
    public void FreshSavesCarryTheWorldTablesAndTheirIndexes()
    {
        var path = NewPath();
        using (SaveFile.Create(path, WorldFixtures.Meta()))
        {
        }

        Assert.Equal(WorldTables.Order(StringComparer.Ordinal), Tables(path).Intersect(WorldTables).Order(StringComparer.Ordinal));
        using var raw = Open(path, SqliteOpenMode.ReadOnly);
        using var command = raw.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'index' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        using var reader = command.ExecuteReader();
        var indexes = new List<string>();
        while (reader.Read())
        {
            indexes.Add(reader.GetString(0));
        }

        Assert.Equal(
            ["command_log_by_manager", "contracts_by_organization", "contracts_by_person", "inbox_items_by_manager", "knowledge_by_subject", "scheduled_events_by_date"],
            indexes);
    }

    [Fact]
    public void ANewerSchemaIsStillRefusedAndItsWorldStaysReadableByTheNewerBuild()
    {
        var path = NewPath();
        var world = WorldFixtures.Small();
        ISaveMigration[] future = [.. SaveMigrations.Production, new FutureV004()];
        using (var created = SaveFile.Create(path, WorldFixtures.Meta(), future))
        {
            Assert.Equal(SaveMigrations.CurrentVersion + 1, created.ReadMeta().SchemaVersion);
            new WorldRepository(created).SaveWorld(world, WorldFixtures.Opening);
        }

        var refusal = Assert.Throws<SaveSchemaTooNewException>(() => SaveFile.Open(path));
        Assert.Equal(SaveMigrations.CurrentVersion + 1, refusal.FileSchemaVersion);
        Assert.Equal(SaveMigrations.CurrentVersion, refusal.SupportedSchemaVersion);

        using var newer = SaveFile.Open(path, future);
        Assert.Equal(world.StateHash(), new WorldRepository(newer).LoadWorld().StateHash());
    }

    [Fact]
    public void AFailureAfterV003BuiltItsTablesRollsTheWholeStepBack()
    {
        var path = NewPath();
        var meta = WorldFixtures.Meta();
        SaveMeta written;
        using (var created = SaveFile.Create(path, meta, [new V001_Initial(), new V002_CareerConfig()]))
        {
            written = created.ReadMeta();
        }

        var failing = Assert.Throws<InvalidOperationException>(() =>
            SaveFile.Open(path, [new V001_Initial(), new V002_CareerConfig(), new BuildThenFailV003()]));
        Assert.Equal("migration failed after the tables were built", failing.Message);

        Assert.Empty(Tables(path).Intersect(WorldTables));
        using (var raw = Open(path, SqliteOpenMode.ReadOnly))
        {
            using var command = raw.CreateCommand();
            command.CommandText = "SELECT schema_version FROM meta";
            Assert.Equal(2L, command.ExecuteScalar());
        }

        using var retried = SaveFile.Open(path);
        Assert.Equal(written with { SchemaVersion = SaveMigrations.CurrentVersion }, retried.ReadMeta() with { SavedAtUtc = written.SavedAtUtc });
        Assert.Equal(WorldTables.Order(StringComparer.Ordinal), Tables(path).Intersect(WorldTables).Order(StringComparer.Ordinal));
    }

    private string NewPath() => Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".paddock");

    private static SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            Pooling = false,
        }.ToString());
        connection.Open();
        return connection;
    }

    private static List<string> PersonColumns(string path)
    {
        using var raw = Open(path, SqliteOpenMode.ReadOnly);
        using var command = raw.CreateCommand();
        command.CommandText = "SELECT name FROM pragma_table_info('persons')";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private static List<string> Tables(string path)
    {
        using var raw = Open(path, SqliteOpenMode.ReadOnly);
        using var command = raw.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private static void AssertEmptyWorldTables(string path)
    {
        using var raw = Open(path, SqliteOpenMode.ReadOnly);
        foreach (var table in WorldTables)
        {
            using var command = raw.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM " + table;
            Assert.Equal(0L, command.ExecuteScalar());
        }
    }

    private sealed class FutureV004 : ISaveMigration
    {
        public int Version => SaveMigrations.CurrentVersion + 1;

        public void Apply(SqliteConnection connection, SqliteTransaction transaction)
        {
        }
    }

    private sealed class BuildThenFailV003 : ISaveMigration
    {
        public int Version => 3;

        public void Apply(SqliteConnection connection, SqliteTransaction transaction)
        {
            new V003_WorldEntities().Apply(connection, transaction);
            throw new InvalidOperationException("migration failed after the tables were built");
        }
    }
}
