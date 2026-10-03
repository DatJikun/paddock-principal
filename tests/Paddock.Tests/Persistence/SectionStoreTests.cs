using Microsoft.Data.Sqlite;
using Paddock.Domain.Inbox;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;

namespace Paddock.Tests.Persistence;

/// <summary>
/// World sections are saved in the same transaction as the world, through <see cref="ISectionStore"/>.
/// The inbox is the example section. The inbox items here are synthetic fixtures.
/// </summary>
public class SectionStoreTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-sections-").FullName;

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void AnInboxRoundTripsThroughTheSaveWithTheSameHash()
    {
        var world = WorldWithInbox();
        using var file = NewSave();
        var repository = new WorldRepository(file);

        repository.SaveWorld(world, WorldFixtures.Opening);
        var loaded = repository.LoadWorld();

        Assert.Equal(world.StateHash(), loaded.StateHash());
        var inbox = loaded.Section<InboxSection>(InboxSection.SectionName)!;
        Assert.Equal(4, inbox.NextNumber);
        Assert.Equal(
            [InboxStatus.Resolved, InboxStatus.Open, InboxStatus.Expired],
            inbox.Items.Select(item => item.Status));
        var offer = inbox.Find("inb:2")!;
        Assert.Equal(["accept", "decline"], offer.Options.Select(option => option.Id));
        Assert.Equal("decline", offer.DefaultOptionId);
        Assert.Equal(new GameDate(1955, 1, 10), offer.ValidUntil);
        Assert.Equal("Fixture Motors", offer.Arguments["who"]);
    }

    [Fact]
    public void SaveAllAndLoadAllCarryTheInboxToo()
    {
        var world = WorldWithInbox();
        using var file = NewSave();
        var repository = new WorldRepository(file);

        repository.SaveAll(new WorldSnapshot(world, [], 1, 1, [], [], 1), WorldFixtures.Opening);

        Assert.Equal(world.StateHash(), repository.LoadAll().World.StateHash());
    }

    [Fact]
    public void AWorldWithNoSectionsLoadsWithNoneAndTheSameHash()
    {
        var world = WorldFixtures.Small();
        using var file = NewSave();
        var repository = new WorldRepository(file);

        repository.SaveWorld(world, WorldFixtures.Opening);
        var loaded = repository.LoadWorld();

        Assert.Empty(loaded.Sections);
        Assert.Equal(world.StateHash(), loaded.StateHash());
        Assert.Equal(0L, Count(file, "world_sections"));
    }

    [Fact]
    public void SavingAgainReplacesTheInboxAndDroppingItClearsItsRows()
    {
        var world = WorldWithInbox();
        using var file = NewSave();
        var repository = new WorldRepository(file);
        repository.SaveWorld(world, WorldFixtures.Opening);
        Assert.Equal(3L, Count(file, "inbox_items"));
        Assert.Equal(4L, Count(file, "inbox_options"));

        var smaller = world.WithSection(
            InboxSection.Empty.Add("mgr-anna", Draft(), WorldFixtures.Opening).Section);
        repository.SaveWorld(smaller, WorldFixtures.Opening);
        Assert.Equal(1L, Count(file, "inbox_items"));
        Assert.Equal(smaller.StateHash(), repository.LoadWorld().StateHash());

        repository.SaveWorld(world.WithoutSection(InboxSection.SectionName), WorldFixtures.Opening);
        Assert.Equal(0L, Count(file, "inbox_items"));
        Assert.Equal(0L, Count(file, "inbox_arguments"));
        Assert.Equal(0L, Count(file, "inbox_options"));
        Assert.Equal(0L, Count(file, "inbox_counter"));
        Assert.Equal(0L, Count(file, "world_sections"));
        Assert.Empty(repository.LoadWorld().Sections);
    }

    [Fact]
    public void ASaveSurvivesCloseAndReopen()
    {
        var path = NewPath();
        var world = WorldWithInbox();
        using (var file = SaveFile.Create(path, WorldFixtures.Meta()))
        {
            new WorldRepository(file).SaveWorld(world, WorldFixtures.Opening);
        }

        using var reopened = SaveFile.Open(path);
        Assert.Equal(world.StateHash(), new WorldRepository(reopened).LoadWorld().StateHash());
    }

    [Fact]
    public void ASectionWithoutAStoreIsRefusedAndTheEarlierSaveIsUntouched()
    {
        var world = WorldFixtures.Small();
        using var file = NewSave();
        var repository = new WorldRepository(file);
        repository.SaveWorld(world, WorldFixtures.Opening);

        var withUnknown = world.WithSection(new UnstoredSection());
        var refusal = Assert.Throws<InvalidOperationException>(() => repository.SaveWorld(withUnknown, WorldFixtures.Opening));

        Assert.Contains("unstored", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(world.StateHash(), repository.LoadWorld().StateHash());
        Assert.Equal(0L, Count(file, "world_sections"));
    }

    [Fact]
    public void ASaveWithASectionIsRefusedByABuildThatHasNoStoreForIt()
    {
        var path = NewPath();
        using (var file = SaveFile.Create(path, WorldFixtures.Meta()))
        {
            new WorldRepository(file).SaveWorld(WorldWithInbox(), WorldFixtures.Opening);
        }

        using var reopened = SaveFile.Open(path);
        var withoutStores = new WorldRepository(reopened, []);

        var failure = Assert.Throws<InvalidDataException>(() => withoutStores.LoadWorld());
        Assert.Contains("inbox", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASectionStoredWithANewerSchemaThanThisBuildWritesIsRefused()
    {
        using var file = NewSave();
        var repository = new WorldRepository(file);
        repository.SaveWorld(WorldWithInbox(), WorldFixtures.Opening);
        Exec(file, "UPDATE world_sections SET schema_version = 99 WHERE name = 'inbox'");

        var failure = Assert.Throws<InvalidDataException>(() => repository.LoadWorld());

        Assert.Contains("newer", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AStoreThatWritesADifferentSchemaVersionThanTheSectionIsRefused()
    {
        using var file = NewSave();
        var repository = new WorldRepository(file, [new VersionTwoInboxStore()]);

        Assert.Throws<InvalidOperationException>(() => repository.SaveWorld(WorldWithInbox(), WorldFixtures.Opening));
    }

    [Fact]
    public void TwoStoresForOneSectionAreRefused()
    {
        using var file = NewSave();

        Assert.Throws<ArgumentException>(() => new WorldRepository(file, [new InboxSectionStore(), new InboxSectionStore()]));
    }

    [Fact]
    public void RowsThatDoNotFormAValidInboxFailTheLoadAsInvalidData()
    {
        using var file = NewSave();
        var repository = new WorldRepository(file);
        repository.SaveWorld(WorldWithInbox(), WorldFixtures.Opening);

        // A resolved item with no chosen option is not something the domain would have produced.
        Exec(file, "UPDATE inbox_items SET chosen_option = NULL WHERE number = 1");
        Assert.Throws<InvalidDataException>(() => repository.LoadWorld());

        Exec(file, "UPDATE inbox_items SET chosen_option = 'accept' WHERE number = 1");
        Exec(file, "UPDATE inbox_counter SET next_number = 2");
        Assert.Throws<InvalidDataException>(() => repository.LoadWorld());
    }

    [Fact]
    public void AV003SaveMigratesToAnEmptyInboxRegistryAndKeepsItsHash()
    {
        var path = NewPath();
        using (SaveFile.Create(path, WorldFixtures.Meta(), [new V001_Initial(), new V002_CareerConfig(), new V003_WorldEntities()]))
        {
        }

        using var opened = SaveFile.Open(path);
        Assert.Equal(SaveMigrations.CurrentVersion, opened.ReadMeta().SchemaVersion);
        Assert.Equal(0L, Count(opened, "world_sections"));
        Assert.Equal(0L, Count(opened, "inbox_items"));
        var repository = new WorldRepository(opened);
        Assert.False(repository.HasWorld);
        var world = WorldFixtures.Small();
        repository.SaveWorld(world, WorldFixtures.Opening);
        Assert.Equal(world.StateHash(), repository.LoadWorld().StateHash());
    }

    private static InboxItemDraft Draft() => new(
        "test.offer",
        "inbox.test.offer.subject",
        [new("amount", "100"), new("who", "Fixture Motors")],
        [
            new InboxOption("accept", "inbox.test.offer.accept", "inbox.test.offer.acceptConsequence"),
            new InboxOption("decline", "inbox.test.offer.decline", "inbox.test.offer.declineConsequence"),
        ],
        new GameDate(1955, 1, 10),
        "decline");

    /// <summary>Three items: one resolved, one open offer that lapses, one lapsed information item. SYNTHETIC.</summary>
    private static WorldState WorldWithInbox()
    {
        var day = WorldFixtures.Opening;
        var section = InboxSection.Empty;
        (section, _) = section.Add("mgr-anna", new InboxItemDraft(
            "test.offer",
            "inbox.test.offer.subject",
            [new("amount", "5")],
            [new InboxOption("accept", "k.a", "k.ac"), new InboxOption("decline", "k.d", "k.dc")],
            null,
            null), day);
        (section, _) = section.Add("mgr-bram", Draft(), day);
        (section, _) = section.Add("mgr-anna", new InboxItemDraft("test.note", "inbox.test.note.subject", null, null, day, null), day);
        section = section.Resolve("inb:1", "accept", day);
        section = section.Expire("inb:3", day.AddDays(1), applyDefault: false);
        return WorldFixtures.Small().WithSection(section);
    }

    private SaveFile NewSave() => SaveFile.Create(NewPath(), WorldFixtures.Meta());

    private string NewPath() => Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".paddock");

    private static long Count(SaveFile file, string table)
    {
        using var command = file.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM " + table;
        return (long)command.ExecuteScalar()!;
    }

    private static void Exec(SaveFile file, string sql)
    {
        using var command = file.Connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private sealed class UnstoredSection : IWorldSection
    {
        public string Name => "unstored";

        public int SchemaVersion => 1;

        public void WriteCanonical(CanonicalWriter writer)
        {
        }
    }

    private sealed class VersionTwoInboxStore : ISectionStore
    {
        public string SectionName => InboxSection.SectionName;

        public int SchemaVersion => 2;

        public void Replace(SqliteConnection connection, SqliteTransaction transaction, IWorldSection? section) =>
            throw new InvalidOperationException("Should not be reached.");

        public IWorldSection Load(SqliteConnection connection, int storedSchemaVersion) =>
            throw new InvalidOperationException("Should not be reached.");
    }
}
