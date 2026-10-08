using Paddock.Domain.Pool;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;

namespace Paddock.Tests.Persistence;

/// <summary>V029 gives pool members an academy (#268): a junior season paid for by a team means the team had recruited him.</summary>
public sealed class AcademyMigrationTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-v29a-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void AV28SaveKeepsItsPoolAndReadsAPaidJuniorAsRecruitedByThePayer()
    {
        var path = Path.Combine(_directory, "v28.paddock");
        using (var created = SaveFile.Create(path, WorldFixtures.Meta(), [.. SaveMigrations.Production.Take(28)]))
        {
            Exec(created, """
                INSERT INTO pool_counter (id, next_handle) VALUES (1, 4);
                INSERT INTO pool_members (person_id, handle, entered, funder_id, programme, funding_season)
                VALUES ('d_free', 1, '1955-01-01', NULL, NULL, NULL),
                       ('d_paid', 2, '1955-01-01', 'alpha', 'CheapSlow', 1955);
                INSERT INTO pool_lapsed (person_id, lapsed_on) VALUES ('d_gone', '1954-01-01');
                """);
        }

        using var opened = SaveFile.Open(path);
        Assert.Equal(SaveMigrations.CurrentVersion, opened.ReadMeta().SchemaVersion);
        Assert.True(Scalar(opened, "SELECT academy_id FROM pool_members WHERE person_id = 'd_free'") is DBNull);
        Assert.Equal("alpha", (string)Scalar(opened, "SELECT academy_id FROM pool_members WHERE person_id = 'd_paid'"));
        Assert.True(Scalar(opened, "SELECT academy_id FROM pool_lapsed WHERE person_id = 'd_gone'") is DBNull);
    }

    [Fact]
    public void AnAcademyAndALapsedJuniorsAcademyRoundTripWithTheSameHash()
    {
        var people = new[] { PersonId.Real("d_a"), PersonId.Real("d_b"), PersonId.Real("d_c") };
        var section = TalentPoolSection.Empty
            .EnterAll(people, new GameDate(1955, 1, 1))
            .Recruit(people[0], OrganizationId.Real("alpha"))
            .Fund(people[0], new JuniorFunding(OrganizationId.Real("alpha"), JuniorProgramme.ExpensiveFast, 1955))
            .Recruit(people[1], OrganizationId.Real("bravo"))
            .Lapse(people[1], new GameDate(1961, 1, 1));
        var world = WorldFixtures.Small().WithSection(section);

        using var file = SaveFile.Create(Path.Combine(_directory, "academy.paddock"), WorldFixtures.Meta());
        var repository = new WorldRepository(file);
        repository.SaveWorld(world, WorldFixtures.Opening);
        var loaded = repository.LoadWorld();

        Assert.Equal(world.StateHash(), loaded.StateHash());
        var pool = loaded.Section<TalentPoolSection>(TalentPoolSection.SectionName)!;
        Assert.Equal(OrganizationId.Real("alpha"), pool.Find(people[0])!.Academy);
        Assert.Equal(JuniorProgramme.ExpensiveFast, pool.Find(people[0])!.Funding!.Programme);
        Assert.Null(pool.Find(people[2])!.Academy);
        Assert.Equal(OrganizationId.Real("bravo"), pool.Lapsed.Single().Academy);
    }

    private static void Exec(SaveFile file, string sql)
    {
        using var command = file.Connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static object Scalar(SaveFile file, string sql)
    {
        using var command = file.Connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar()!;
    }
}
