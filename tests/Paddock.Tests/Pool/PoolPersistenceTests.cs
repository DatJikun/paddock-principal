using Paddock.Domain.Pool;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;
using Paddock.Tests.Persistence;

namespace Paddock.Tests.Pool;

/// <summary>The talent-pool section in the save (V008). The world is a synthetic fixture lived through the real pool handler.</summary>
public class PoolPersistenceTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-pool-").FullName;

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void ALivedPoolRoundTripsWithTheSameHashAndTheSameSection()
    {
        var world = LivedWorld();
        using var file = NewSave();
        var repository = new WorldRepository(file);

        repository.SaveWorld(world, WorldFixtures.Opening);
        var loaded = repository.LoadWorld();

        Assert.Equal(world.StateHash(), loaded.StateHash());
        var saved = world.Section<TalentPoolSection>(TalentPoolSection.SectionName)!;
        var restored = loaded.Section<TalentPoolSection>(TalentPoolSection.SectionName)!;
        Assert.Equal(Canonical(saved), Canonical(restored));
        Assert.NotEmpty(saved.Members);
        Assert.NotEmpty(saved.Lapsed);
        Assert.NotEmpty(saved.Focuses);
        Assert.NotEmpty(saved.Observations);
        Assert.Contains(saved.Members, member => member.Funding is not null);
        Assert.NotEmpty(loaded.Knowledge);
    }

    [Fact]
    public void SavingAgainReplacesTheRowsAndDroppingTheSectionClearsThem()
    {
        var world = LivedWorld();
        using var file = NewSave();
        var repository = new WorldRepository(file);
        repository.SaveWorld(world, WorldFixtures.Opening);
        var members = Count(file, "pool_members");
        Assert.True(members > 0);

        var pool = world.Section<TalentPoolSection>(TalentPoolSection.SectionName)!;
        var smaller = world.WithSection(pool.Leave(pool.Members[0].Id));
        repository.SaveWorld(smaller, WorldFixtures.Opening);
        Assert.Equal(members - 1, Count(file, "pool_members"));
        Assert.Equal(smaller.StateHash(), repository.LoadWorld().StateHash());

        repository.SaveWorld(world.WithoutSection(TalentPoolSection.SectionName), WorldFixtures.Opening);
        foreach (var table in new[] { "pool_counter", "pool_members", "pool_lapsed", "pool_focus", "pool_observations" })
        {
            Assert.Equal(0L, Count(file, table));
        }
    }

    [Fact]
    public void TheHandleCounterSurvivesTheSaveSoNoHandleIsReused()
    {
        var world = LivedWorld();
        using var file = NewSave();
        var repository = new WorldRepository(file);
        repository.SaveWorld(world, WorldFixtures.Opening);

        var before = world.Section<TalentPoolSection>(TalentPoolSection.SectionName)!;
        var after = repository.LoadWorld().Section<TalentPoolSection>(TalentPoolSection.SectionName)!;

        Assert.Equal(before.NextHandle, after.NextHandle);
        Assert.True(after.NextHandle > after.Members.Max(member => member.Handle));
    }

    [Fact]
    public void RowsThatDoNotFormAValidPoolFailTheLoadAsInvalidData()
    {
        var world = LivedWorld();
        using var file = NewSave();
        var repository = new WorldRepository(file);
        repository.SaveWorld(world, WorldFixtures.Opening);

        Exec(file, "UPDATE pool_counter SET next_handle = 1");
        Assert.Throws<InvalidDataException>(() => repository.LoadWorld());
        repository.SaveWorld(world, WorldFixtures.Opening);

        Exec(file, "INSERT INTO pool_observations (organization_id, person_id, milli_points) VALUES ('alpha', 'nobody', 5)");
        Assert.Throws<InvalidDataException>(() => repository.LoadWorld());
    }

    [Fact]
    public void ASectionStoredWithANewerSchemaThanThisBuildWritesIsRefused()
    {
        var world = LivedWorld();
        using var file = NewSave();
        var repository = new WorldRepository(file);
        repository.SaveWorld(world, WorldFixtures.Opening);
        Exec(file, "UPDATE world_sections SET schema_version = 99 WHERE name = 'talent-pool'");

        Assert.Throws<InvalidDataException>(() => repository.LoadWorld());
    }

    [Fact]
    public void AnOlderSaveMigratesToNoPoolAndKeepsItsHash()
    {
        var path = Path.Combine(_directory, "old.paddock");
        using (SaveFile.Create(path, WorldFixtures.Meta(), [new V001_Initial(), new V002_CareerConfig(), new V003_WorldEntities(), new V004_WorldSections(), new V005_InboxSection(), new V006_PersonRetirement(), new V007_CareerRun()]))
        {
        }

        using var opened = SaveFile.Open(path);
        Assert.Equal(SaveMigrations.CurrentVersion, opened.ReadMeta().SchemaVersion);
        Assert.Equal(0L, Count(opened, "pool_members"));
        var world = WorldFixtures.Small();
        var repository = new WorldRepository(opened);
        repository.SaveWorld(world, WorldFixtures.Opening);
        Assert.Equal(world.StateHash(), repository.LoadWorld().StateHash());
        Assert.Null(repository.LoadWorld().Section(TalentPoolSection.SectionName));
    }

    /// <summary>Six years of a small pool with fillers, a funded season, two scouting focuses and one real driver who lapses.</summary>
    private static WorldState LivedWorld()
    {
        var world = PoolKit.EmptyWorld();
        var ids = new List<PersonId>();
        for (var i = 0; i < 5; i++)
        {
            (world, var id) = PoolKit.Add(world, PoolKit.RealDriver("d_p" + i, 1930 + i, current: 6 + i, extra: 5));
            ids.Add(id);
        }

        var run = new PoolKit.Run(world, 21, ids, [], new Paddock.Simulation.Pool.TalentPoolOptions { TargetSize = 8 });
        run.SetFocus(PoolKit.Alpha, ScoutFocusKind.Pool);
        run.SetFocus(PoolKit.Bravo, ScoutFocusKind.Person, ids[4]);
        run.World = run.World.WithSection(run.Pool.Recruit(ids[3], PoolKit.Bravo).Fund(ids[3], new JuniorFunding(PoolKit.Bravo, JuniorProgramme.CheapSlow, 1950)));
        run.LiveUntil(new GameDate(1953, 1, 2));
        run.LiveUntil(new GameDate(1957, 1, 2));
        // A season that is funded now and has not acted yet is part of the saved state too.
        var member = run.Pool.Members.First(m => m.Funding is null);
        run.World = run.World.WithSection(run.Pool.Recruit(member.Id, PoolKit.Alpha).Fund(member.Id, new JuniorFunding(PoolKit.Alpha, JuniorProgramme.ExpensiveFast, 1957)));
        run.World = run.World.WithSection(run.Pool.SetFocus(new ScoutFocus(PoolKit.Bravo, ScoutFocusKind.Pool, null)));
        return run.World.WithDate(WorldFixtures.Opening);
    }

    private static string Canonical(TalentPoolSection section)
    {
        var writer = new CanonicalWriter();
        section.WriteCanonical(writer);
        return writer.ToString();
    }

    private SaveFile NewSave() => SaveFile.Create(Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".paddock"), WorldFixtures.Meta());

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
}
