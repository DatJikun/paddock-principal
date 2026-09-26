using Microsoft.Data.Sqlite;
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
            Assert.Equal(1, written.SchemaVersion);
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
            Assert.Equal(1, created.ReadMeta().SchemaVersion);
        }

        var v2 = new NoOpV002();
        var migrations = new ISaveMigration[] { new V001_Initial(), v2 };
        using (var first = SaveFile.Open(path, migrations))
        {
            Assert.Equal(1, v2.Calls);
            Assert.Equal(2, first.ReadMeta().SchemaVersion);
        }

        using var second = SaveFile.Open(path, migrations);
        Assert.Equal(1, v2.Calls);
        Assert.Equal(2, second.ReadMeta().SchemaVersion);
    }

    [Fact]
    public void NewerSchemaIsRefusedWithoutChangingTheSave()
    {
        Assert.Equal(1, SaveMigrations.CurrentVersion);
        var path = NewPath();
        using (var created = SaveFile.Create(path, SampleMeta()))
        {
            Assert.Equal(SampleMeta().CareerName, created.ReadMeta().CareerName);
        }

        SaveMeta upgradedMeta;
        using (var upgraded = SaveFile.Open(path, [new V001_Initial(), new NoOpV002()]))
        {
            upgradedMeta = upgraded.ReadMeta();
            Assert.Equal(2, upgradedMeta.SchemaVersion);
        }

        var exception = Assert.Throws<SaveSchemaTooNewException>(() => SaveFile.Open(path));
        Assert.Equal(2, exception.FileSchemaVersion);
        Assert.Equal(1, exception.SupportedSchemaVersion);
        Assert.Equal(Path.GetFullPath(path), exception.Path);

        using var again = SaveFile.Open(path, [new V001_Initial(), new NoOpV002()]);
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

        var migration = new CorruptThenFailV002();
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SaveFile.Open(path, [new V001_Initial(), migration]));
        Assert.Equal("migration failed", exception.Message);
        Assert.Equal(1, migration.RowsUpdated);

        using var opened = SaveFile.Open(path);
        Assert.Equal(written, opened.ReadMeta());
    }

    private string NewPath() => Path.Combine(_directory, $"{Guid.NewGuid():N}.paddock");

    private static SaveMeta SampleMeta() => new(
        careerName: "Zespół próbny",
        managerName: "Test Manager",
        playerTeamId: "team-1",
        currentGameDate: new DateOnly(1950, 1, 1),
        worldDataHash: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
        masterSeed: ulong.MaxValue);

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

    private sealed class NoOpV002 : ISaveMigration
    {
        public int Version => 2;

        public int Calls { get; private set; }

        public void Apply(SqliteConnection connection, SqliteTransaction transaction)
        {
            Calls++;
        }
    }

    private sealed class CorruptThenFailV002 : ISaveMigration
    {
        public int Version => 2;

        public int RowsUpdated { get; private set; }

        public void Apply(SqliteConnection connection, SqliteTransaction transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE meta SET career_name = 'corrupted' WHERE id = 1";
            RowsUpdated = command.ExecuteNonQuery();
            throw new InvalidOperationException("migration failed");
        }
    }
}
