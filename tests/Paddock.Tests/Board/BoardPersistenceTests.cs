using Microsoft.Data.Sqlite;
using Paddock.Application.Board;
using Paddock.Application.Commands;
using Paddock.Domain.Board;
using Paddock.Domain.Objectives;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;
using Paddock.Tests.Persistence;
using static Paddock.Tests.Board.BoardKit;

namespace Paddock.Tests.Board;

/// <summary>
/// The board and objectives sections in the save, the command codecs, and the canonical hash. Fixtures are SYNTHETIC (see
/// <see cref="BoardKit"/>).
/// </summary>
public class BoardPersistenceTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-board-").FullName;

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private static Lab BusyLab()
    {
        // A season with a dismissal, an offer and settled objectives: the board section carries a principal of each kind,
        // reputations with history, an unemployed manager and objectives in every state.
        var lab = new Lab(42UL, free: 5);
        lab.Appoint(Pam, T3);
        lab.Appoint(Quinn, T4, founder: true);
        lab.Facts.Position(T3, 5);
        lab.Facts.Position(T1, 1);
        lab.Facts.Cash(T2, -100);
        lab.Facts.Position(T2, 2);
        lab.AdvanceTo(new GameDate(1956, 6, 8));
        lab.Advance(BoardEstimates.GuaranteeWindowDays + 1);
        return lab;
    }

    [Fact]
    public void TheBoardAndItsObjectivesRoundTripThroughTheSaveWithTheSameHash()
    {
        var lab = BusyLab();
        var world = lab.Inbox.Into(lab.Board.Into());
        Assert.NotNull(lab.Section.UnemployedManager(Pam.Value));
        Assert.NotEmpty(lab.Section.Changes);
        Assert.Contains(lab.Board.Objectives.Objectives, objective => objective.Status == ObjectiveStatus.Failed);
        Assert.Contains(lab.Board.Objectives.Objectives, objective => objective.Status == ObjectiveStatus.Open);
        using var file = SaveFile.Create(Path.Combine(_directory, "board.paddock"), WorldFixtures.Meta());
        var repository = new WorldRepository(file);

        repository.SaveWorld(world, lab.Today);
        var loaded = repository.LoadWorld();

        Assert.Equal(world.StateHash(), loaded.StateHash());
        var board = loaded.Section<BoardSection>(BoardSection.SectionName)!;
        Assert.Equal(lab.Section.Boards, board.Boards);
        Assert.Equal(lab.Section.Changes, board.Changes);
        Assert.Equal(lab.Section.Unemployed, board.Unemployed);
        Assert.Equal(lab.Section.Reputations, board.Reputations);
        var objectives = loaded.Section<ObjectivesSection>(ObjectivesSection.SectionName)!;
        Assert.Equal(lab.Board.Objectives.NextNumber, objectives.NextNumber);
        Assert.Equal(
            lab.Board.Objectives.Objectives.Select(Describe),
            objectives.Objectives.Select(Describe));
    }

    private static string Describe(Objective objective) =>
        string.Join(
            "|",
            objective.Id,
            objective.Owner,
            objective.Grantor,
            objective.KindKey,
            objective.ReasonKey,
            objective.Predicate.Name,
            objective.Predicate.Parameter,
            objective.Baseline,
            objective.Created,
            objective.Deadline,
            objective.EffectOnMet.Key,
            string.Join(",", objective.EffectOnMet.Arguments.Select(a => a.Key + "=" + a.Value)),
            objective.EffectOnFailed.Key,
            string.Join(",", objective.EffectOnFailed.Arguments.Select(a => a.Key + "=" + a.Value)),
            objective.Status,
            objective.SettledOn);

    [Fact]
    public void SavingAgainReplacesTheRowsAndDroppingTheSectionsClearsThem()
    {
        var lab = BusyLab();
        using var file = SaveFile.Create(Path.Combine(_directory, "again.paddock"), WorldFixtures.Meta());
        var repository = new WorldRepository(file);
        repository.SaveWorld(lab.Board.Into(), lab.Today);
        Assert.True(Count(file, "boards") >= 5);
        Assert.Equal(1L, Count(file, "board_unemployed"));
        Assert.True(Count(file, "objectives") >= 10);

        lab.Advance(1);
        repository.SaveWorld(lab.Board.Into(), lab.Today);
        Assert.Equal(lab.Board.Into().StateHash(), repository.LoadWorld().StateHash());

        repository.SaveWorld(lab.World, lab.Today);
        foreach (var table in new[] { "boards", "board_reputations", "board_reputation_changes", "board_unemployed", "objectives", "objective_effect_arguments", "objectives_state" })
        {
            Assert.Equal(0L, Count(file, table));
        }

        Assert.Empty(repository.LoadWorld().Sections);
    }

    [Fact]
    public void ASaveFromBeforeTheBoardMigrationLoadsWithNoSectionsAndTheSameHash()
    {
        var path = Path.Combine(_directory, "old.paddock");
        var before = SaveMigrations.Production.TakeWhile(migration => migration is not V013_BoardSection).ToArray();
        var world = WorldFixtures.Small();
        using (var created = SaveFile.Create(path, WorldFixtures.Meta(), before))
        {
            Assert.Equal(before.Length, created.ReadMeta().SchemaVersion);
        }

        using var opened = SaveFile.Open(path);
        Assert.Equal(SaveMigrations.CurrentVersion, opened.ReadMeta().SchemaVersion);
        var repository = new WorldRepository(opened);
        repository.SaveWorld(world, WorldFixtures.Opening);

        Assert.Empty(repository.LoadWorld().Sections);
        Assert.Equal(world.StateHash(), repository.LoadWorld().StateHash());
        Assert.Equal(0L, Count(opened, "boards"));
    }

    [Fact]
    public void TheHashSeesEveryFieldOfTheBoardSection()
    {
        string Hash(Func<BoardRecord, BoardRecord> change)
        {
            var record = new BoardRecord(T1, 45, 600, 0, 3, "balanced", new PrincipalRecord(PrincipalKind.Ai, "p", Start, Start, false), null, null, null);
            return WorldState.At(Start).WithSection(BoardSection.Empty.WithBoard(change(record))).StateHash();
        }

        var baseline = Hash(record => record);

        Assert.Equal(baseline, Hash(record => record));
        Assert.NotEqual(baseline, Hash(record => record with { Patience = 46 }));
        Assert.NotEqual(baseline, Hash(record => record with { ConfidenceTenths = 601 }));
        Assert.NotEqual(baseline, Hash(record => record with { LowStreak = 1 }));
        Assert.NotEqual(baseline, Hash(record => record with { ExpectedPosition = 4 }));
        Assert.NotEqual(baseline, Hash(record => record with { Archetype = "aggressive" }));
        Assert.NotEqual(baseline, Hash(record => record with { LastReview = Start }));
        Assert.NotEqual(baseline, Hash(record => record with { LastTargetTenths = 300 }));
        Assert.NotEqual(baseline, Hash(record => record with { LastCash = -1 }));
        Assert.NotEqual(baseline, Hash(record => record with { Principal = null }));
        Assert.NotEqual(baseline, Hash(record => record with { Principal = record.Principal! with { Kind = PrincipalKind.Human } }));
        Assert.NotEqual(baseline, Hash(record => record with { Principal = record.Principal! with { Founder = true } }));
        Assert.NotEqual(baseline, Hash(record => record with { Principal = record.Principal! with { ProtectedUntil = Start.AddDays(1) } }));
    }

    [Fact]
    public void AWorldThatNeverUsedTheBoardKeepsItsHash()
    {
        var lab = new Lab(noRaces: true);
        var before = lab.Inbox.Into(lab.Board.Into()).StateHash();

        Assert.Empty(lab.Board.Into().Sections);
        Assert.Equal(lab.Contracts.Into().StateHash(), before);
    }

    [Fact]
    public void TheThreeBoardCommandsRoundTripThroughTheCommandLog()
    {
        var day = new DateOnly(1956, 6, 8);
        ICommand[] commands =
        [
            new AcceptJobOfferCommand { ManagerId = Pam, IssuedOn = day, ItemId = "inb:7", SubmissionNumber = 1 },
            new DeclineJobOfferCommand { ManagerId = Pam, IssuedOn = day, ItemId = "inb:8", SubmissionNumber = 2 },
            new ResignFromTeamCommand { ManagerId = Quinn, IssuedOn = day, SubmissionNumber = 3 },
        ];

        foreach (var command in commands)
        {
            var encoded = CommandCodec.Production.Encode(command);
            var decoded = CommandCodec.Production.Decode(encoded.Tag, encoded.Text, command.ManagerId, command.SubmissionNumber, command.IssuedOn);
            Assert.Equal(command, decoded);
            Assert.Equal(command.GetType(), decoded.GetType());
        }
    }

    [Fact]
    public void ReplayingTheSameCommandsOnTheSameSeedGivesTheSameState()
    {
        static string Run()
        {
            var lab = new Lab(77UL, free: 5);
            lab.Dismissed(Pam, 900, T3);
            lab.Advance(BoardEstimates.FirstOfferDelayDays + 1);
            var offer = lab.OpenOffers(Pam).Single();
            lab.Submit(new AcceptJobOfferCommand { ManagerId = Pam, IssuedOn = Date(lab.Today), ItemId = offer.Id });
            lab.Advance(40);
            lab.Submit(new ResignFromTeamCommand { ManagerId = Pam, IssuedOn = Date(lab.Today) });
            lab.Advance(20);
            return lab.Hash();
        }

        Assert.Equal(Run(), Run());
    }

    private static long Count(SaveFile file, string table)
    {
        using var command = file.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM " + table;
        return (long)command.ExecuteScalar()!;
    }
}
