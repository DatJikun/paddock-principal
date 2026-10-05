using Microsoft.Data.Sqlite;
using Paddock.Domain.Time;
using Paddock.Persistence;

namespace Paddock.Tests.Persistence;

/// <summary>V024 adds <c>injured_until</c> to <c>persons</c> without losing rows (PP-061, #219).</summary>
public sealed class PersonInjuredUntilMigrationTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-v24-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void AV023SaveKeepsItsPersonsAndAcceptsInjuredUntil()
    {
        var path = Path.Combine(_directory, "v23.paddock");
        using (var created = SaveFile.Create(path, WorldFixtures.Meta(), [.. SaveMigrations.Production.Take(23)]))
        {
            Assert.Equal(23, created.ReadMeta().SchemaVersion);
            Exec(created, """
                INSERT INTO persons (id, is_real, given_name, family_name, birth_date, nationality)
                VALUES ('fangio', 1, 'Juan', 'Fangio', '1911-06-24', 'ARG');
                """);
            Assert.Throws<SqliteException>(() =>
                Exec(created, "UPDATE persons SET injured_until = '1955-06-01' WHERE id = 'fangio'"));
        }

        using var opened = SaveFile.Open(path);
        Assert.Equal(SaveMigrations.CurrentVersion, opened.ReadMeta().SchemaVersion);
        Assert.Equal(1L, Scalar(opened, "SELECT COUNT(*) FROM persons WHERE id = 'fangio'"));
        Assert.True(Scalar(opened, "SELECT injured_until FROM persons WHERE id = 'fangio'") is DBNull);

        Exec(opened, "UPDATE persons SET injured_until = '1955-06-01' WHERE id = 'fangio'");
        Assert.Equal("1955-06-01", (string)Scalar(opened, "SELECT injured_until FROM persons WHERE id = 'fangio'"));

        Exec(opened, "UPDATE persons SET injured_until = NULL WHERE id = 'fangio'");
        Assert.True(Scalar(opened, "SELECT injured_until FROM persons WHERE id = 'fangio'") is DBNull);

        Assert.Throws<SqliteException>(() =>
            Exec(opened, "UPDATE persons SET injured_until = '1955-6-1' WHERE id = 'fangio'"));
        Assert.Throws<SqliteException>(() =>
            Exec(opened, "UPDATE persons SET injured_until = 'not-a-date-123' WHERE id = 'fangio'"));
    }

    [Fact]
    public void WorldRepositoryRoundTripsInjuredPerson()
    {
        var path = Path.Combine(_directory, "roundtrip.paddock");
        using var save = SaveFile.Create(path, WorldFixtures.Meta());
        var repo = new WorldRepository(save);

        var world = WorldFixtures.Small();
        var person = world.Persons.First();
        var injuredWorld = world.InjurePerson(person.Id, new GameDate(1955, 6, 1));

        repo.SaveWorld(injuredWorld, WorldFixtures.Opening);
        var loaded = repo.LoadWorld();

        var loadedPerson = loaded.GetPerson(person.Id);
        Assert.NotNull(loadedPerson);
        Assert.Equal(new GameDate(1955, 6, 1), loadedPerson.InjuredUntil);
        Assert.True(loadedPerson.IsInjured(new GameDate(1955, 5, 1)));
        Assert.True(loadedPerson.IsInjured(new GameDate(1955, 6, 1)));
        Assert.False(loadedPerson.IsInjured(new GameDate(1955, 6, 2)));
    }

    [Fact]
    public void WorldWithoutInjuriesPreservesStateHashWithoutInjuredLine()
    {
        var world = WorldFixtures.Small();
        Assert.All(world.Persons, p => Assert.Null(p.InjuredUntil));

        var person = world.Persons.First();
        var injuredWorld = world.InjurePerson(person.Id, new GameDate(1955, 6, 1));
        Assert.NotEqual(world.StateHash(), injuredWorld.StateHash());

        var clearedWorld = injuredWorld.ClearPersonInjury(person.Id);
        Assert.Equal(world.StateHash(), clearedWorld.StateHash());
    }

    private static object Scalar(SaveFile save, string sql)
    {
        using var command = save.Connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar()!;
    }

    private static void Exec(SaveFile save, string sql)
    {
        using var command = save.Connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
