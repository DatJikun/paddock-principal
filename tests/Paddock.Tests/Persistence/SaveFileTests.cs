using Microsoft.Data.Sqlite;
using Paddock.Domain.Career;
using Paddock.Domain.Random;
using Paddock.Persistence;

namespace Paddock.Tests.Persistence;

public class SaveFileTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-save-").FullName;

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void CreateThenOpenRoundTripsMeta()
    {
        var path = NewPath();
        var meta = SampleMeta();

        SaveMeta written;
        using (var created = SaveFile.Create(path, meta))
        {
            written = created.ReadMeta();
            Assert.Equal(4, written.SchemaVersion);
            Assert.Equal(meta.CareerName, written.CareerName);
            Assert.Equal(meta.ManagerName, written.ManagerName);
            Assert.Equal(meta.PlayerTeamId, written.PlayerTeamId);
            Assert.Equal(meta.CurrentGameDate, written.CurrentGameDate);
            Assert.Equal(meta.WorldDataHash, written.WorldDataHash);
            Assert.Equal(meta.MasterSeed, written.MasterSeed);
            Assert.Equal(written.CreatedAtUtc, written.SavedAtUtc);
            Assert.Equal(TimeSpan.Zero, written.CreatedAtUtc.Offset);
            Assert.Equal("wal", created.JournalMode);
            Assert.Equal(1L, created.ForeignKeysEnabled);
            Assert.Throws<InvalidOperationException>(() => created.ReadRngStates());
        }

        Assert.Throws<IOException>(() => SaveFile.Create(path, meta));

        using var opened = SaveFile.Open(path);
        Assert.Equal(written, opened.ReadMeta());
        Assert.Equal("wal", opened.JournalMode);
        Assert.Equal(1L, opened.ForeignKeysEnabled);
    }

    [Fact]
    public void OpenMissingFileThrows()
    {
        var missing = Path.Combine(_directory, "missing.paddock");
        Assert.Throws<FileNotFoundException>(() => SaveFile.Open(missing));
    }

    [Fact]
    public void RngStatesRoundTripExactly()
    {
        var path = NewPath();
        var meta = SampleMeta();
        var states = new Dictionary<string, RngState>(StringComparer.Ordinal);
        foreach (var name in RngStreamName.All)
        {
            var stream = RngStreams.Derive(meta.MasterSeed, name, meta.CurrentGameDate.Year);
            _ = stream.NextULong();
            states[name] = stream.State;
        }

        states[RngStreamName.Weather] = new RngState(0, 0, 0, ulong.MaxValue);

        SaveMeta beforeWrite;
        using (var created = SaveFile.Create(path, meta))
        {
            beforeWrite = created.ReadMeta();
            var partial = new Dictionary<string, RngState>(StringComparer.Ordinal)
            {
                [RngStreamName.Weather] = states[RngStreamName.Weather],
            };
            Assert.Throws<ArgumentException>(() => created.WriteRngStates(partial));
            Assert.Throws<ArgumentNullException>(() => created.WriteRngStates(null!));
            Assert.Throws<InvalidOperationException>(() => created.ReadRngStates());

            created.WriteRngStates(states);
            Assert.Equal(beforeWrite, created.ReadMeta());
            AssertRoundTrip(states, created.ReadRngStates());
        }

        using var opened = SaveFile.Open(path);
        Assert.Equal(beforeWrite, opened.ReadMeta());
        AssertRoundTrip(states, opened.ReadRngStates());
    }

    [Fact]
    public void PendingMigrationAppliesOnceAndIsANoOpOnReopen()
    {
        var path = NewPath();
        using (var created = SaveFile.Create(path, SampleMeta()))
        {
            Assert.Equal(4, created.ReadMeta().SchemaVersion);
        }

        var v4 = new NoOpV005();
        var migrations = new ISaveMigration[] { new V001_Initial(), new V002_CareerConfig(), new V003_WorldEntities(), new V004_PersonRetirement(), v4 };
        using (var first = SaveFile.Open(path, migrations))
        {
            Assert.Equal(1, v4.Calls);
            Assert.Equal(5, first.ReadMeta().SchemaVersion);
        }

        using var second = SaveFile.Open(path, migrations);
        Assert.Equal(1, v4.Calls);
        Assert.Equal(5, second.ReadMeta().SchemaVersion);
    }

    [Fact]
    public void NewerSchemaIsRefusedWithoutChangingTheSave()
    {
        Assert.Equal(4, SaveMigrations.CurrentVersion);
        var path = NewPath();
        using (var created = SaveFile.Create(path, SampleMeta()))
        {
            Assert.Equal(SampleMeta().CareerName, created.ReadMeta().CareerName);
        }

        SaveMeta upgradedMeta;
        using (var upgraded = SaveFile.Open(path, [new V001_Initial(), new V002_CareerConfig(), new V003_WorldEntities(), new V004_PersonRetirement(), new NoOpV005()]))
        {
            upgradedMeta = upgraded.ReadMeta();
            Assert.Equal(5, upgradedMeta.SchemaVersion);
        }

        var exception = Assert.Throws<SaveSchemaTooNewException>(() => SaveFile.Open(path));
        Assert.Equal(5, exception.FileSchemaVersion);
        Assert.Equal(4, exception.SupportedSchemaVersion);
        Assert.Equal(Path.GetFullPath(path), exception.Path);

        using var again = SaveFile.Open(path, [new V001_Initial(), new V002_CareerConfig(), new V003_WorldEntities(), new V004_PersonRetirement(), new NoOpV005()]);
        Assert.Equal(upgradedMeta, again.ReadMeta());
    }

    [Fact]
    public void FailureDuringMigrationRollsBackToThePreviousVersion()
    {
        var path = NewPath();
        var meta = SampleMeta();
        SaveMeta written;
        using (var created = SaveFile.Create(path, meta))
        {
            written = created.ReadMeta();
        }

        var migration = new CorruptThenFailV005();
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SaveFile.Open(path, [new V001_Initial(), new V002_CareerConfig(), new V003_WorldEntities(), new V004_PersonRetirement(), migration]));
        Assert.Equal("migration failed", exception.Message);
        Assert.Equal(1, migration.RowsUpdated);

        using var opened = SaveFile.Open(path);
        Assert.Equal(written, opened.ReadMeta());
    }

    [Fact]
    public void V001SaveMigratesForwardToTheBalancedPreset()
    {
        var path = NewPath();
        var meta = SampleMeta();
        using (SaveFile.Create(path, meta, [new V001_Initial()]))
        {
        }

        Assert.Equal(1, ReadSchemaVersion(path));
        Assert.DoesNotContain("career_config", ColumnNames(path));

        using (var opened = SaveFile.Open(path))
        {
            var read = opened.ReadMeta();
            Assert.Equal(4, read.SchemaVersion);
            Assert.Equal(CareerConfig.FromPreset(CareerPreset.Balanced), read.CareerConfig);
            Assert.Equal(meta.CareerName, read.CareerName);
            Assert.Equal(meta.ManagerName, read.ManagerName);
            Assert.Equal(meta.PlayerTeamId, read.PlayerTeamId);
            Assert.Equal(meta.CurrentGameDate, read.CurrentGameDate);
            Assert.Equal(meta.WorldDataHash, read.WorldDataHash);
            Assert.Equal(meta.MasterSeed, read.MasterSeed);
            Assert.Equal(
                CareerConfig.FromPreset(CareerPreset.Balanced).ToCanonicalJson(),
                ReadCareerConfigColumn(path));

            opened.WriteCareerConfig(CareerConfig.FromPreset(CareerPreset.Chaos));
        }

        using var again = SaveFile.Open(path);
        Assert.Equal(4, again.ReadMeta().SchemaVersion);
        Assert.Equal(CareerConfig.FromPreset(CareerPreset.Chaos), again.ReadMeta().CareerConfig);
    }

    [Fact]
    public void CareerConfigRoundTripsThroughATempSave()
    {
        var path = NewPath();
        var config = CareerConfig.FromPreset(CareerPreset.Balanced)
            .WithStartYear(1966)
            .WithPlayerTeam("team-ferrari")
            .WithNoNumbers(true);
        Assert.Equal(CareerPreset.Custom, config.PresetName);
        Assert.True(config.Validate().IsValid);

        var meta = SampleMeta(config);
        using (var created = SaveFile.Create(path, meta))
        {
            Assert.Equal(config, created.ReadMeta().CareerConfig);
            Assert.Equal(config.ToCanonicalJson(), ReadCareerConfigColumn(path));
        }

        using (var opened = SaveFile.Open(path))
        {
            Assert.Equal(config, opened.ReadMeta().CareerConfig);
            var invalid = config.WithHistoryStrength(150);
            Assert.False(invalid.Validate().IsValid);
            opened.WriteCareerConfig(invalid);
        }

        using var reread = SaveFile.Open(path);
        Assert.Equal(config.WithHistoryStrength(150), reread.ReadMeta().CareerConfig);
    }

    [Fact]
    public void NonCanonicalCareerConfigIsRefused()
    {
        var path = NewPath();
        using (SaveFile.Create(path, SampleMeta()))
        {
        }

        var canonical = CareerConfig.FromPreset(CareerPreset.Balanced).ToCanonicalJson();
        var reordered = canonical.Replace(
            "\"peopleSource\":\"RealPotential\",\"rulesSource\":\"Historical\"",
            "\"rulesSource\":\"Historical\",\"peopleSource\":\"RealPotential\"",
            StringComparison.Ordinal);
        WriteCareerConfigColumn(path, reordered);

        using var opened = SaveFile.Open(path);
        Assert.Throws<InvalidDataException>(() => opened.ReadMeta());
    }

    private string NewPath() => Path.Combine(_directory, $"{Guid.NewGuid():N}.paddock");

    private static SaveMeta SampleMeta(CareerConfig? careerConfig = null) => new(
        careerName: "Zespół próbny",
        managerName: "Test Manager",
        playerTeamId: "team-1",
        currentGameDate: new DateOnly(1950, 1, 1),
        worldDataHash: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
        masterSeed: ulong.MaxValue,
        careerConfig: careerConfig ?? CareerConfig.FromPreset(CareerPreset.MostHistorical).WithStartYear(1955));

    private static void AssertRoundTrip(
        IReadOnlyDictionary<string, RngState> expected,
        IReadOnlyDictionary<string, RngState> actual)
    {
        Assert.Equal(RngStreamName.All.Count, actual.Count);
        foreach (var name in RngStreamName.All)
        {
            Assert.Equal(expected[name], actual[name]);
            var continued = new Xoshiro256StarStar(actual[name]);
            var fromOriginal = new Xoshiro256StarStar(expected[name]);
            Assert.Equal(fromOriginal.NextULong(), continued.NextULong());
            Assert.Equal(fromOriginal.NextDouble(), continued.NextDouble());
        }
    }

    private static int ReadSchemaVersion(string path)
    {
        using var connection = OpenRaw(path, SqliteOpenMode.ReadOnly);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT schema_version FROM meta WHERE id = 1";
        return (int)(long)command.ExecuteScalar()!;
    }

    private static List<string> ColumnNames(string path)
    {
        using var connection = OpenRaw(path, SqliteOpenMode.ReadOnly);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM pragma_table_info('meta')";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private static string ReadCareerConfigColumn(string path)
    {
        using var connection = OpenRaw(path, SqliteOpenMode.ReadOnly);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT career_config FROM meta WHERE id = 1";
        return (string)command.ExecuteScalar()!;
    }

    private static void WriteCareerConfigColumn(string path, string payload)
    {
        using var connection = OpenRaw(path, SqliteOpenMode.ReadWrite);
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE meta SET career_config = $payload WHERE id = 1";
        command.Parameters.Add("$payload", SqliteType.Text).Value = payload;
        Assert.Equal(1, command.ExecuteNonQuery());
    }

    private static SqliteConnection OpenRaw(string path, SqliteOpenMode mode)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            Pooling = false,
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }

    private sealed class NoOpV005 : ISaveMigration
    {
        public int Version => 5;

        public int Calls { get; private set; }

        public void Apply(SqliteConnection connection, SqliteTransaction transaction)
        {
            Calls++;
        }
    }

    private sealed class CorruptThenFailV005 : ISaveMigration
    {
        public int Version => 5;

        public int RowsUpdated { get; private set; }

        public void Apply(SqliteConnection connection, SqliteTransaction transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE meta
                SET career_name = 'corrupted', career_config = 'corrupted'
                WHERE id = 1
                """;
            RowsUpdated = command.ExecuteNonQuery();
            throw new InvalidOperationException("migration failed");
        }
    }
}
