using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Managers;
using Paddock.Application.Principals;
using Paddock.Domain.Principals;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;
using Paddock.Simulation.Codec;
using Paddock.Tests.Persistence;

namespace Paddock.Tests.Principals;

public class PrincipalsSectionTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-principals-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static AiPrincipalRecord Record(OrganizationId organization, string archetype = "Builder", PersonId? person = null, int sacrificed = 0) =>
        new(
            organization,
            archetype,
            person,
            new GameDate(1955, 1, 1),
            new GameDate(1955, 2, 1),
            new GameDate(1955, 3, 1),
            sacrificed,
            1955,
            "ChiefDesigner,TechnicalDirector");

    private static long Count(SaveFile file, string table)
    {
        using var command = file.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM " + table;
        return (long)command.ExecuteScalar()!;
    }

    [Fact]
    public void TheHashSeesEveryFieldOfTheSection()
    {
        var world = WorldFixtures.Small();
        var team = world.Organizations[0].Id;
        var person = PersonId.Real("fixture_principal");
        var baseline = world.WithSection(PrincipalsSection.Empty.With(Record(team, person: person))).StateHash();
        var record = Record(team, person: person);

        AiPrincipalRecord[] changes =
        [
            record with { Archetype = "Survivor" },
            record with { Person = null },
            record with { AssignedOn = new GameDate(1955, 1, 2) },
            record with { LastReview = null },
            record with { NextReview = new GameDate(1955, 3, 2) },
            record with { SacrificedSeason = 1955 },
            record with { ScoutSeason = 1954 },
            record with { StaffRoles = "ChiefDesigner" },
        ];
        var hashes = changes.Select(changed => world.WithSection(PrincipalsSection.Empty.With(changed)).StateHash()).Append(baseline).ToArray();
        Assert.Equal(hashes.Length, hashes.Distinct().Count());
    }

    [Fact]
    public void ARecordIsCheckedAndTwoRecordsOfOneTeamAreRefused()
    {
        var team = OrganizationId.Real("t");
        Assert.Throws<ArgumentException>(() => PrincipalsSection.Empty.With(Record(team) with { SacrificedSeason = -1 }));
        Assert.Throws<ArgumentException>(() => PrincipalsSection.Empty.With(Record(team) with { NextReview = new GameDate(1955, 1, 1) }));
        Assert.Throws<InvalidOperationException>(() => PrincipalsSection.Restore([Record(team), Record(team)]));
        Assert.Same(PrincipalsSection.Empty, PrincipalsSection.Empty.Without(team));
        Assert.True(PrincipalsSection.Empty.With(Record(team)).Without(team).IsEmpty);
    }

    [Fact]
    public void ASectionRoundTripsThroughTheSaveWithTheSameHash()
    {
        var world = WorldFixtures.Small();
        var first = world.Organizations[0].Id;
        var second = world.Organizations[1].Id;
        var section = PrincipalsSection.Empty
            .With(Record(first, "Contender", PersonId.Real("fixture_principal")))
            .With(Record(second, "Survivor", PersonId.Generated(7), sacrificed: 1955));
        world = world.WithSection(section);

        using var file = SaveFile.Create(Path.Combine(_directory, "principals.paddock"), WorldFixtures.Meta());
        var repository = new WorldRepository(file);
        repository.SaveWorld(world, WorldFixtures.Opening);
        var loaded = repository.LoadWorld();

        Assert.Equal(world.StateHash(), loaded.StateHash());
        Assert.Equal(section.Records, loaded.Section<PrincipalsSection>(PrincipalsSection.SectionName)!.Records);

        repository.SaveWorld(world.WithoutSection(PrincipalsSection.SectionName), WorldFixtures.Opening);
        Assert.Null(repository.LoadWorld().Section<PrincipalsSection>(PrincipalsSection.SectionName));
        Assert.Equal(0L, Count(file, "ai_principals"));
    }

    [Fact]
    public void ASaveFromBeforeThePrincipalsMigrationLoadsWithNoSectionAndTheSameHash()
    {
        var path = Path.Combine(_directory, "old.paddock");
        var before = SaveMigrations.Production.TakeWhile(migration => migration is not V018_PrincipalsSection).ToArray();
        var world = WorldFixtures.Small();
        using (var created = SaveFile.Create(path, WorldFixtures.Meta(), before))
        {
            Assert.Equal(before.Length, created.ReadMeta().SchemaVersion);
        }

        using var opened = SaveFile.Open(path);
        Assert.Equal(SaveMigrations.CurrentVersion, opened.ReadMeta().SchemaVersion);
        var repository = new WorldRepository(opened);
        repository.SaveWorld(world, WorldFixtures.Opening);
        Assert.Null(repository.LoadWorld().Section<PrincipalsSection>(PrincipalsSection.SectionName));
        Assert.Equal(world.StateHash(), repository.LoadWorld().StateHash());
        Assert.Equal(0L, Count(opened, "ai_principals"));
    }

    [Fact]
    public void ACareerThatRanThePrincipalsSavesAndLoadsWithTheSameHash()
    {
        var kit = new PrincipalKit(new PrincipalKitOptions { Pool = false });
        kit.RunDays(120);
        var world = kit.Session.World;
        Assert.False(world.Section<PrincipalsSection>(PrincipalsSection.SectionName)!.IsEmpty);

        using var file = SaveFile.Create(Path.Combine(_directory, "career.paddock"), WorldFixtures.Meta());
        var repository = new WorldRepository(file);
        repository.SaveWorld(world, world.CurrentDate);
        Assert.Equal(world.StateHash(), repository.LoadWorld().StateHash());
    }

    // ---------------------------------------------------------------- the command

    [Fact]
    public void TheReviewCommandRoundTripsThroughTheCommandCodec()
    {
        var command = new RecordPrincipalReviewCommand
        {
            ManagerId = new ManagerId("ai:pt_alfa"),
            IssuedOn = new DateOnly(1956, 1, 6),
            OrganizationId = "pt_alfa",
            Archetype = "Opportunist",
            Person = "gen:4",
            NextReview = new DateOnly(1956, 2, 5),
            SacrificedSeason = 1956,
            ScoutSeason = 1956,
            StaffRoles = "ChiefDesigner,TechnicalDirector",
        }.WithSubmissionNumber(5);
        var codec = CommandCodec.Production;
        var encoded = codec.Encode(command);
        Assert.Equal("principals.review/1", encoded.Tag);
        Assert.Equal(command, codec.Decode(encoded.Tag, encoded.Text, command.ManagerId, command.SubmissionNumber, command.IssuedOn));
    }

    [Fact]
    public void AReviewSavedBeforeFollowUpsWereStoredDecodesAsUnknown()
    {
        var body = FlatJson.Write(
            ("organization", "pt_alfa"),
            ("archetype", "Opportunist"),
            ("person", "gen:4"),
            ("nextReview", "1956-02-05"),
            ("sacrificed", 1956),
            ("scouted", 1956),
            ("roles", "ChiefDesigner,TechnicalDirector"));
        var command = (RecordPrincipalReviewCommand)CommandCodec.Production.Decode(
            "principals.review/1",
            body,
            new ManagerId("ai:pt_alfa"),
            5,
            new DateOnly(1956, 1, 6));

        Assert.Equal(-1, command.FollowUps);
    }

    private sealed class Rig
    {
        public Rig(bool humanRuns)
        {
            var world = WorldFixtures.Small();
            Team = world.Organizations.First(item => item.Kind == OrganizationKind.Team).Id;
            Managers = new ManagerRegistry();
            Control = new ControlTable();
            Ai = new ManagerId("ai:" + Team.Value);
            Managers.Register(Ai, ManagerKind.Ai, "AI");
            Control.Assign(Ai, Team);
            if (humanRuns)
            {
                var human = new ManagerId("human:anna");
                Managers.Register(human, ManagerKind.Human, "Anna");
                Control.Assign(human, Team);
            }

            var box = world;
            Book = new PrincipalsBook(() => box, next => box = next);
            Handler = new RecordPrincipalReviewHandler(Book, Control);
            Context = new CommandContext(new Paddock.Application.World.StubWorldState(new DateOnly(1955, 1, 1)), Managers);
        }

        public OrganizationId Team { get; }

        public ManagerRegistry Managers { get; }

        public ControlTable Control { get; }

        public ManagerId Ai { get; }

        public PrincipalsBook Book { get; }

        public RecordPrincipalReviewHandler Handler { get; }

        public CommandContext Context { get; }

        public RecordPrincipalReviewCommand Command(Func<RecordPrincipalReviewCommand, RecordPrincipalReviewCommand>? change = null)
        {
            var command = new RecordPrincipalReviewCommand
            {
                ManagerId = Ai,
                IssuedOn = new DateOnly(1955, 1, 1),
                OrganizationId = Team.Value,
                Archetype = "Builder",
                NextReview = new DateOnly(1955, 2, 1),
            };
            return change is null ? command : change(command);
        }
    }

    [Fact]
    public void TheCommandIsAcceptedForAnAiTeamAndWritesTheRecord()
    {
        var rig = new Rig(humanRuns: false);
        var command = rig.Command(c => c with { Person = "fixture_principal", StaffRoles = "ChiefDesigner" });
        Assert.Null(rig.Handler.Validate(command, rig.Context));
        rig.Handler.Execute(command, rig.Context);
        var record = rig.Book.Section.Of(rig.Team)!;
        Assert.Equal("Builder", record.Archetype);
        Assert.Equal(new GameDate(1955, 1, 1), record.AssignedOn);
        Assert.Equal(new GameDate(1955, 2, 1), record.NextReview);

        // The same archetype for the same person keeps the day it was assigned.
        var later = command with { IssuedOn = new DateOnly(1955, 2, 1), NextReview = new DateOnly(1955, 3, 1) };
        rig.Handler.Execute(later, rig.Context);
        Assert.Equal(new GameDate(1955, 1, 1), rig.Book.Section.Of(rig.Team)!.AssignedOn);
        rig.Handler.Execute(later with { Archetype = "Survivor" }, rig.Context);
        Assert.Equal(new GameDate(1955, 2, 1), rig.Book.Section.Of(rig.Team)!.AssignedOn);
    }

    [Theory]
    [InlineData("archetype", PrincipalKeys.BadArchetype)]
    [InlineData("person", PrincipalKeys.BadPerson)]
    [InlineData("date", PrincipalKeys.BadDate)]
    [InlineData("season", PrincipalKeys.BadSeason)]
    [InlineData("roles", PrincipalKeys.BadRoles)]
    [InlineData("organization", PrincipalKeys.UnknownOrganization)]
    public void ABadCommandIsRefusedWithAKeyAndChangesNothing(string what, string key)
    {
        var rig = new Rig(humanRuns: false);
        var command = what switch
        {
            "archetype" => rig.Command(c => c with { Archetype = "Maverick" }),
            "person" => rig.Command(c => c with { Person = "gen:0" }),
            "date" => rig.Command(c => c with { NextReview = new DateOnly(1954, 1, 1) }),
            "season" => rig.Command(c => c with { ScoutSeason = -1 }),
            "roles" => rig.Command(c => c with { StaffRoles = "TechnicalDirector,ChiefDesigner" }),
            _ => rig.Command(c => c with { OrganizationId = "nowhere" }),
        };
        Assert.Equal(key, rig.Handler.Validate(command, rig.Context)!.Key);
        Assert.True(rig.Book.Section.IsEmpty);
    }

    [Fact]
    public void OnlyAnAiManagerWhoRunsTheTeamAndWhereNoHumanDoesMayRecordAReview()
    {
        var humanRun = new Rig(humanRuns: true);
        Assert.Equal(PrincipalKeys.HumanTeam, humanRun.Handler.Validate(humanRun.Command(), humanRun.Context)!.Key);

        var rig = new Rig(humanRuns: false);
        var human = new ManagerId("human:bram");
        rig.Managers.Register(human, ManagerKind.Human, "Bram");
        Assert.Equal(PrincipalKeys.NotAnAiManager, rig.Handler.Validate(rig.Command(c => c with { ManagerId = human }), rig.Context)!.Key);

        var stranger = new ManagerId("ai:other");
        rig.Managers.Register(stranger, ManagerKind.Ai, "Other");
        Assert.Equal(PrincipalKeys.NotInControl, rig.Handler.Validate(rig.Command(c => c with { ManagerId = stranger }), rig.Context)!.Key);
    }

    [Fact]
    public void ThePrincipalOrganizationsMapAnAiManagerToItsTeamAndLeaveOthersToTheHost()
    {
        Assert.Equal(OrganizationId.Real("pt_alfa"), new PrincipalOrganizations().OrganizationOf("ai:pt_alfa"));
        Assert.Null(new PrincipalOrganizations().OrganizationOf("human:anna"));
        Assert.Equal(OrganizationId.Real("pt_alfa"), PrincipalKeys.ManagerOf(OrganizationId.Real("pt_alfa")).Value is "ai:pt_alfa" ? new PrincipalOrganizations().OrganizationOf("ai:pt_alfa") : null);
        Assert.True(PrincipalKeys.IsPrincipalManager(new ManagerId("ai:pt_alfa")));
        Assert.False(PrincipalKeys.IsPrincipalManager(new ManagerId("ai:")));
    }
}
