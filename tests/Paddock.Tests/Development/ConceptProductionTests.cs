using Microsoft.Data.Sqlite;
using Paddock.Application.Access;
using Paddock.Application.Commands;
using Paddock.Application.Development;
using Paddock.Application.Inbox;
using Paddock.Domain.Cars;
using Paddock.Domain.Development;
using Paddock.Domain.Finance;
using Paddock.Domain.Inbox;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;
using Paddock.Tests.Persistence;
using static Paddock.Tests.Development.DevelopmentKit;
using AccessManagerId = Paddock.Application.Access.ManagerId;

namespace Paddock.Tests.Development;

/// <summary>
/// T42c: a ready concept goes live only after the principal commits it and production has taken its time. Fixtures are
/// SYNTHETIC; every duration and cost under test is an ESTIMATE, so the tests pin order and direction, not magnitudes.
/// </summary>
public class ConceptProductionTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-production-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static CommitConceptCommand Commit(string project = "dev:1", string org = "alfa", Paddock.Application.Managers.ManagerId? manager = null, DateOnly? on = null) =>
        new() { ManagerId = manager ?? Anna, IssuedOn = on ?? Day(Opening), OrganizationId = org, ProjectId = project };

    private static DeployConceptCommand Timing(string timing, int races = 0) =>
        new() { ManagerId = Anna, IssuedOn = Day(Opening), OrganizationId = "alfa", ProjectId = "dev:1", Timing = timing, Races = races };

    private static string? Key(CommandResult result) => (result as CommandResult.Rejected)?.Reason.Key;

    /// <summary>A kit whose only concept finished on day five and now waits with the default timing (next season).</summary>
    private static DevelopmentKit WithReadyConcept(int seed = 3, INextRaceSource? races = null)
    {
        var kit = new DevelopmentKit(seed: (ulong)seed, races: races);
        kit.PutProject(Concept(Alfa, 1_000_000, 0.5, days: 5));
        kit.Live(5);
        Assert.Equal(ProjectStatus.Ready, kit.Project(project => project.Number == 1).Status);
        return kit;
    }

    private static double Strength(DevelopmentKit kit) => kit.AlfaCars[0].Levels.Downforce + kit.AlfaCars[0].Levels.Braking;

    [Fact]
    public void ACommittedConceptIsNotLiveBeforeTheFinishDateAndGoesLiveAfterIt()
    {
        var kit = WithReadyConcept();
        var before = Strength(kit);
        var committedOn = kit.Today;
        Assert.IsType<CommandResult.Accepted>(kit.Submit(Commit()));
        var project = kit.Project(p => p.Number == 1);
        Assert.Equal(ProjectStatus.InProduction, project.Status);
        var ends = project.ProductionEnds!.Value;
        Assert.True(ends > committedOn);

        while (kit.Today <= ends)
        {
            Assert.Equal(ProjectStatus.InProduction, kit.Project(p => p.Number == 1).Status);
            Assert.Equal(before, Strength(kit), 3);
            kit.Live(1);
        }

        Assert.Equal(ends.AddDays(1), kit.Today);
        Assert.Equal(ProjectStatus.InProduction, kit.Project(p => p.Number == 1).Status);
        kit.Live(1);
        Assert.Equal(ProjectStatus.Deployed, kit.Project(p => p.Number == 1).Status);
        Assert.True(Strength(kit) > before);
    }

    [Fact]
    public void AConceptCommittedLateInTheSeasonSurvivesTheSeasonChangeAndGoesLiveAfterIt()
    {
        var kit = new DevelopmentKit(seed: 3);
        kit.PutProject(Concept(Alfa, 1_000_000, 0.5, days: 5));
        kit.Live(360);
        Assert.IsType<CommandResult.Accepted>(kit.Submit(Commit(on: Day(kit.Today))));
        var ends = kit.Project(p => p.Number == 1).ProductionEnds!.Value;
        Assert.Equal(1956, ends.Year);
        var before = Strength(kit);

        kit.Live(kit.Today.DaysUntil(GameDate.SeasonStart(1956)) + 1);
        Assert.Equal(1956, kit.Today.Year);
        Assert.Equal(ProjectStatus.InProduction, kit.Project(p => p.Number == 1).Status);

        DevelopmentEngineTests.LiveThroughProduction(kit);
        Assert.Equal(ProjectStatus.Deployed, kit.Project(p => p.Number == 1).Status);
        Assert.True(Strength(kit) > before);
    }

    [Fact]
    public void ProductionCostIsPostedToTheLedgerOnTheCommitDay()
    {
        var kit = WithReadyConcept();
        var balance = kit.Finance.BalanceOf(Alfa);
        var entries = kit.Finance.EntriesOf(Alfa).Count;
        Assert.IsType<CommandResult.Accepted>(kit.Submit(Commit()));
        var project = kit.Project(p => p.Number == 1);
        Assert.True(project.ProductionCostCents > 0);
        Assert.Equal(balance - project.ProductionCostCents, kit.Finance.BalanceOf(Alfa));
        var entry = kit.Finance.EntriesOf(Alfa).Skip(entries).Single();
        Assert.Equal(LedgerCategories.Development, entry.Category);
        Assert.Equal(DevelopmentKeys.LedgerProduction, entry.ReasonKey);
        Assert.Equal(-project.ProductionCostCents, entry.AmountCents);

        kit.Live(400);
        Assert.Single(kit.Finance.EntriesOf(Alfa), e => e.ReasonKey == DevelopmentKeys.LedgerProduction);
    }

    [Fact]
    public void AHeldConceptCanBeCommittedLater()
    {
        var kit = WithReadyConcept();
        Assert.IsType<CommandResult.Accepted>(kit.Submit(Timing("Hold")));
        kit.Live(365);
        Assert.Equal(1956, kit.Today.Year);
        Assert.Equal(ProjectStatus.Ready, kit.Project(p => p.Number == 1).Status);

        Assert.IsType<CommandResult.Accepted>(kit.Submit(Commit(on: Day(kit.Today))));
        Assert.Equal(ProjectStatus.InProduction, kit.Project(p => p.Number == 1).Status);
        Assert.Equal(ConceptTiming.Hold, kit.Project(p => p.Number == 1).Timing);
        DevelopmentEngineTests.LiveThroughProduction(kit);
        Assert.Equal(ProjectStatus.Deployed, kit.Project(p => p.Number == 1).Status);
    }

    [Fact]
    public void TheOldTimingsStillWorkAndWhenReadyNowMeansCommitAsSoonAsReady()
    {
        var auto = new DevelopmentKit(seed: 3);
        auto.PutProject(Concept(Alfa, 1_000_000, 0.5, days: 5) with { Timing = ConceptTiming.WhenReady });
        auto.Live(5);
        Assert.Equal(ProjectStatus.InProduction, auto.Project(p => p.Number == 1).Status);
        DevelopmentEngineTests.LiveThroughProduction(auto);
        Assert.Equal(ProjectStatus.Deployed, auto.Project(p => p.Number == 1).Status);

        var seasonal = WithReadyConcept();
        seasonal.Live(365);
        Assert.Equal(ProjectStatus.Deployed, seasonal.Project(p => p.Number == 1).Status);

        var held = WithReadyConcept();
        Assert.IsType<CommandResult.Accepted>(held.Submit(Timing("Hold")));
        held.Live(365);
        Assert.Equal(ProjectStatus.Ready, held.Project(p => p.Number == 1).Status);

        var commandWhenReady = WithReadyConcept();
        Assert.IsType<CommandResult.Accepted>(commandWhenReady.Submit(Timing("WhenReady")));
        Assert.Equal(ProjectStatus.InProduction, commandWhenReady.Project(p => p.Number == 1).Status);
    }

    [Fact]
    public void CommittingIsCheckedForOwnerKindAndStatus()
    {
        var kit = new DevelopmentKit(seed: 3);
        kit.PutProject(Concept(Alfa, 1_000_000, 0.5, days: 60));
        kit.PutProject(Upgrade(Alfa, DevArea.Aero, 1_000_000, 0.2, days: 40));
        Assert.Equal(DevelopmentKeys.NotReady, Key(kit.Submit(Commit("dev:1"))));
        Assert.Equal(DevelopmentKeys.NotConcept, Key(kit.Submit(Commit("dev:2"))));
        Assert.Equal(DevelopmentKeys.UnknownProject, Key(kit.Submit(Commit("dev:9"))));
        Assert.Equal(DevelopmentKeys.NoControl, Key(kit.Submit(Commit("dev:1", manager: Bram))));
        Assert.Equal(DevelopmentKeys.UnknownProject, Key(kit.Submit(Commit("dev:1", "beta", Bram))));
        Assert.Equal(DevelopmentKeys.NoControl, Key(kit.Submit(Commit("dev:1", "nobody"))));

        var ready = WithReadyConcept();
        Assert.IsType<CommandResult.Accepted>(ready.Submit(Commit()));
        Assert.Equal(DevelopmentKeys.NotReady, Key(ready.Submit(Commit())));
        Assert.Equal(DevelopmentKeys.NotDeployable, Key(ready.Submit(Timing("WhenReady"))));
    }

    [Fact]
    public void CommitCommandRoundTripsThroughTheCodec()
    {
        var command = Commit("dev:4") with { SubmissionNumber = 7 };
        var encoded = CommandCodec.Production.Encode(command);
        Assert.Equal(command, CommandCodec.Production.Decode(encoded.Tag, encoded.Text, command.ManagerId, command.SubmissionNumber, command.IssuedOn));
    }

    [Fact]
    public void ProductionTimeGrowsWithTheEraAndShrinksWithHeadcountButQualityStaysPut()
    {
        Assert.True(DevelopmentMath.ConceptProductionDays(1955) < DevelopmentMath.ConceptProductionDays(1990));
        Assert.True(DevelopmentMath.ConceptProductionDays(1990) < DevelopmentMath.ConceptProductionDays(2025));
        Assert.True(DevelopmentMath.ConceptProductionDays(1955) >= 1);

        var capacity = new EngineeringCapacity(Headcount: 40, Quality: 0.6, ReferenceHeadcount: 40);
        var bigger = capacity.WithExtraHeadcount(120);
        var days = DevelopmentMath.ConceptProductionDays(1955);
        Assert.True(bigger.DurationDays(days) < capacity.DurationDays(days));
        Assert.Equal(capacity.Quality, bigger.Quality);
    }

    [Fact]
    public void ACommittedRunIsDeterministic()
    {
        string Run()
        {
            var kit = WithReadyConcept(seed: 11);
            Assert.IsType<CommandResult.Accepted>(kit.Submit(Commit()));
            kit.Live(200);
            return kit.World.StateHash();
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void ProductionAddsNoRandomnessSoAnotherTeamsDrawsStayWhereTheyWere()
    {
        // Production draws nothing: the outcome of the concept is fixed when development ends. Same seed, same hash for the
        // rival's projects whether or not the player commits.
        DevelopmentKit Run(bool commit)
        {
            var kit = WithReadyConcept(seed: 5);
            if (commit)
            {
                Assert.IsType<CommandResult.Accepted>(kit.Submit(Commit()));
            }

            kit.Live(100);
            return kit;
        }

        var committed = Run(true);
        var waiting = Run(false);
        static IEnumerable<DevProject> Own(DevelopmentKit kit) => kit.Section.ProjectsOf(Beta).Select(project => project with { Number = 0 });
        Assert.Equal(Own(waiting), Own(committed));
        Assert.Equal(waiting.Cars.Of(Beta).Select(car => (car.Id, car.Levels, car.Understanding)), committed.Cars.Of(Beta).Select(car => (car.Id, car.Levels, car.Understanding)));
    }

    [Fact]
    public void TheViewShowsProductionTimeCostAndTheNextRaceForOwnTeamOnly()
    {
        var calendar = new FakeCalendar(new GameDate(1955, 1, 20), new GameDate(1955, 2, 20), new GameDate(1955, 4, 20));
        var kit = WithReadyConcept(races: calendar);
        var query = new DevelopmentQuery(kit.Book, kit.Environment);
        var before = kit.World.StateHash();
        var view = query.View(AccessContext.ForManager(new AccessManagerId(Anna.Value))).Own[0];
        Assert.Equal(before, kit.World.StateHash());

        var concept = view.Projects.Single(p => p.ProjectId == "dev:1");
        Assert.Equal(nameof(ProjectStatus.Ready), concept.Status);
        var plan = ConceptProduction.Plan(kit.World, kit.Finance, Alfa, kit.Project(p => p.Number == 1), kit.Today);
        Assert.Equal(plan.Days, concept.ProductionDays);
        Assert.Equal(plan.CostCents, concept.ProductionCostCents);
        Assert.Null(concept.ProductionEnds);
        Assert.Equal(kit.Today.DaysUntil(new GameDate(1955, 1, 20)), view.DaysToNextRace);
        var firstAfter = calendar.NextRaceOnOrAfter(Alfa, kit.Today.AddDays(plan.Days + 1))!.Value;
        Assert.Equal(new DateOnly(firstAfter.Year, firstAfter.Month, firstAfter.Day), concept.GoesLiveOn);
        Assert.True(view.OngoingGain.High >= view.OngoingGain.Low);

        Assert.IsType<CommandResult.Accepted>(kit.Submit(Commit()));
        var committed = query.View(AccessContext.ForManager(new AccessManagerId(Anna.Value))).Own[0].Projects.Single(p => p.ProjectId == "dev:1");
        Assert.Equal(nameof(ProjectStatus.InProduction), committed.Status);
        var ends = kit.Project(p => p.Number == 1).ProductionEnds!.Value;
        Assert.Equal(new DateOnly(ends.Year, ends.Month, ends.Day), committed.ProductionEnds);
        Assert.Equal(kit.Today.DaysUntil(ends), committed.ProductionDays);

        var rival = query.View(AccessContext.ForAi(new AccessManagerId(Bram.Value)));
        Assert.Equal([Beta.Value], rival.Own.Select(own => own.OrganizationId));
        Assert.DoesNotContain(rival.Own[0].Projects, p => p.ProjectId == "dev:1");
    }

    [Fact]
    public void AViewWithoutACalendarSaysNothingAboutRaces()
    {
        var kit = WithReadyConcept();
        var view = new DevelopmentQuery(kit.Book, kit.Environment).View(AccessContext.ForManager(new AccessManagerId(Anna.Value))).Own[0];
        Assert.Null(view.DaysToNextRace);
        Assert.Null(view.Projects.Single(p => p.ProjectId == "dev:1").GoesLiveOn);
        Assert.NotNull(view.Projects.Single(p => p.ProjectId == "dev:1").ProductionDays);
    }

    [Fact]
    public void ABecomingReadyConceptAsksThePrincipalAndCommitOrWaitAreCarriedOut()
    {
        DevelopmentKit Asked(out InboxItem item)
        {
            var kit = new DevelopmentKit(seed: 3);
            kit.PutProject(Concept(Alfa, 1_000_000, 0.5, days: 5));
            kit.LiveWithInbox(5);
            item = kit.Inbox.Section.ItemsOf(Anna.Value).Single(entry => entry.Kind == DevelopmentKeys.ConceptInboxKind);
            return kit;
        }

        var commit = Asked(out var item);
        Assert.True(item.NeedsDecision);
        Assert.Equal("dev:1", item.Arguments[DevelopmentKeys.ProjectArgument]);
        Assert.Equal(DevelopmentKeys.OptionWait, item.Draft.DefaultOptionId);
        Assert.Equal([DevelopmentKeys.OptionCommit, DevelopmentKeys.OptionWait], item.Draft.Options!.Select(option => option.Id));
        Assert.Empty(commit.Inbox.Section.ItemsOf(Bram.Value));
        Assert.IsType<CommandResult.Accepted>(commit.Submit(new ResolveInboxItemCommand { ManagerId = Anna, IssuedOn = Day(commit.Today), ItemId = item.Id, OptionId = DevelopmentKeys.OptionCommit }));
        Assert.Equal(ProjectStatus.InProduction, commit.Project(p => p.Number == 1).Status);

        var wait = Asked(out var waitItem);
        Assert.IsType<CommandResult.Accepted>(wait.Submit(new ResolveInboxItemCommand { ManagerId = Anna, IssuedOn = Day(wait.Today), ItemId = waitItem.Id, OptionId = DevelopmentKeys.OptionWait }));
        Assert.Equal(ProjectStatus.Ready, wait.Project(p => p.Number == 1).Status);

        var stale = Asked(out var staleItem);
        Assert.IsType<CommandResult.Accepted>(stale.Submit(Commit(on: Day(stale.Today))));
        Assert.Equal(DevelopmentKeys.NotReady, Key(stale.Submit(new ResolveInboxItemCommand { ManagerId = Anna, IssuedOn = Day(stale.Today), ItemId = staleItem.Id, OptionId = DevelopmentKeys.OptionCommit })));
    }

    [Fact]
    public void AConceptThatCommitsItselfDoesNotAskAndAnAnsweredDayStaysQuiet()
    {
        var kit = new DevelopmentKit(seed: 3);
        kit.PutProject(Concept(Alfa, 1_000_000, 0.5, days: 5) with { Timing = ConceptTiming.WhenReady });
        kit.LiveWithInbox(5);
        Assert.Equal(ProjectStatus.InProduction, kit.Project(p => p.Number == 1).Status);
        Assert.DoesNotContain(kit.Inbox.Section.ItemsOf(Anna.Value), entry => entry.Kind == DevelopmentKeys.ConceptInboxKind);
        kit.LiveWithInbox(3);
        Assert.DoesNotContain(kit.Inbox.Section.ItemsOf(Anna.Value), entry => entry.Kind == DevelopmentKeys.ConceptInboxKind);
    }

    [Fact]
    public void AnInProductionConceptAndAHeldTimingRoundTripThroughTheSaveWithTheSameHash()
    {
        var kit = WithReadyConcept();
        Assert.IsType<CommandResult.Accepted>(kit.Submit(Timing("Hold")));
        var second = DevProjectIds.Format(kit.Section.NextProject);
        kit.PutProject(Concept(Alfa, 1_000_000, 0.5, days: 5));
        kit.Live(5);
        Assert.IsType<CommandResult.Accepted>(kit.Submit(new CommitConceptCommand { ManagerId = Anna, IssuedOn = Day(kit.Today), OrganizationId = "alfa", ProjectId = second }));
        Assert.Contains(kit.Section.Projects, p => p.IsInProduction && p.ProductionEnds is not null && p.ProductionCostCents > 0);
        Assert.Contains(kit.Section.Projects, p => p.Timing == ConceptTiming.Hold);

        using var file = SaveFile.Create(Path.Combine(_directory, "production.paddock"), WorldFixtures.Meta());
        var repository = new WorldRepository(file);
        repository.SaveWorld(kit.World, kit.Today);
        var loaded = repository.LoadWorld();
        Assert.Equal(kit.World.StateHash(), loaded.StateHash());
        Assert.Equal(kit.Section.Projects, loaded.Section<DevelopmentSection>(DevelopmentSection.SectionName)!.Projects);
    }

    [Fact]
    public void TheHashSeesProductionFieldsAndAnOldSectionKeepsItsHash()
    {
        var kit = WithReadyConcept();
        var section = kit.Section;
        var project = section.Projects[0];
        Assert.Equal(0, project.ProductionCostCents);
        string Hash(DevelopmentSection changed) => kit.World.WithSection(changed).StateHash();
        var baseline = Hash(section);
        var committed = project with { Status = ProjectStatus.InProduction, ProductionEnds = new GameDate(1955, 6, 1), ProductionCostCents = 10 };
        var hashes = new[]
        {
            Hash(section.ReplaceProject(committed)),
            Hash(section.ReplaceProject(committed with { ProductionEnds = new GameDate(1955, 6, 2) })),
            Hash(section.ReplaceProject(committed with { ProductionCostCents = 11 })),
        };
        Assert.All(hashes, hash => Assert.NotEqual(baseline, hash));
        Assert.Equal(3, hashes.Distinct().Count());
        Assert.Throws<InvalidOperationException>(() => DevelopmentSection.Restore(
            section.NextProject,
            section.Plans,
            section.Accounts,
            [project with { Status = ProjectStatus.InProduction }]));
    }

    [Fact]
    public void ASaveFromBeforeTheProductionMigrationKeepsItsProjectsAndAcceptsTheNewValues()
    {
        var path = Path.Combine(_directory, "v16.paddock");
        var before = SaveMigrations.Production.TakeWhile(migration => migration is not V017_ConceptProduction).ToArray();
        using (var created = SaveFile.Create(path, WorldFixtures.Meta(), before))
        {
            Exec(created, "INSERT INTO development_counter (id, next_project) VALUES (1, 2)");
            Exec(
                created,
                "INSERT INTO development_projects (number, organization_id, kind, area, engineer, started, duration_days, progress_days, cost, posted, share_milli, "
                + "risk_milli, outcome_milli, status, timing, timing_races, races_waited, closed_on) "
                + "VALUES (1, 'alfa', 'Concept', NULL, 'person:a-td', '1955-01-01', 5, 5, 1000, 1000, 500, 0, 500, 'Ready', 'NextSeason', 0, 0, NULL)");
        }

        using var opened = SaveFile.Open(path);
        Assert.Equal(SaveMigrations.CurrentVersion, opened.ReadMeta().SchemaVersion);
        var store = new DevelopmentSectionStore();
        var section = (DevelopmentSection)store.Load(opened.Connection, 1);
        var project = Assert.Single(section.Projects);
        Assert.Equal((ProjectStatus.Ready, ConceptTiming.NextSeason, null, 0L), (project.Status, project.Timing, project.ProductionEnds, project.ProductionCostCents));

        var moved = section.ReplaceProject(project with
        {
            Status = ProjectStatus.InProduction,
            Timing = ConceptTiming.Hold,
            ProductionEnds = new GameDate(1955, 3, 1),
            ProductionCostCents = 500,
        });
        using var transaction = opened.Connection.BeginTransaction();
        store.Replace(opened.Connection, transaction, moved);
        transaction.Commit();
        Assert.Equal(moved.Projects, ((DevelopmentSection)store.Load(opened.Connection, 1)).Projects);
    }

    [Fact]
    public void ARunWithNoCommitKeepsTheHashItHadBeforeThisChange()
    {
        // No concept in production anywhere: the canonical text has no "prod" group, so it is the text T42b wrote.
        var kit = new DevelopmentKit(seed: 12);
        kit.PutProject(Upgrade(Alfa, DevArea.Aero, 1_000_000, 0.2, days: 10));
        kit.Live(20);
        Assert.DoesNotContain(kit.Section.Projects, p => p.ProductionEnds is not null);
        Assert.Equal(kit.World.StateHash(), kit.World.WithSection(DevelopmentSection.Restore(kit.Section.NextProject, kit.Section.Plans, kit.Section.Accounts, kit.Section.Projects)).StateHash());
    }

    private static void Exec(SaveFile file, string sql)
    {
        using var command = file.Connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private sealed class FakeCalendar(params GameDate[] races) : INextRaceSource
    {
        public GameDate? NextRaceOnOrAfter(OrganizationId organization, GameDate day) =>
            races.Where(race => race >= day).OrderBy(race => race).Select(race => (GameDate?)race).FirstOrDefault();
    }
}
