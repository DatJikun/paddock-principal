using System.Reflection;
using Paddock.Application.Access;
using Paddock.Application.Commands;
using Paddock.Application.Development;
using Paddock.Application.Inbox;
using Paddock.Domain.Cars;
using Paddock.Domain.Development;
using Paddock.Domain.Inbox;
using Paddock.Domain.World;
using Paddock.Persistence;
using Paddock.Tests.Persistence;
using static Paddock.Tests.Development.DevelopmentKit;
using AccessManagerId = Paddock.Application.Access.ManagerId;

namespace Paddock.Tests.Development;

/// <summary>Commands, the engineers' inbox reply, the manager view, the codec and the save of car development. SYNTHETIC fixtures.</summary>
public class DevelopmentCommandTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-development-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static SetDevelopmentSplitCommand Split(int current, int account, int next, string organization = "alfa", Paddock.Application.Managers.ManagerId? manager = null) =>
        new()
        {
            ManagerId = manager ?? Anna,
            IssuedOn = Day(Opening),
            OrganizationId = organization,
            CurrentPercent = current,
            AccountPercent = account,
            NextYearPercent = next,
            AeroPriority = 8,
            ChassisPriority = 5,
            ReliabilityPriority = 2,
            TyresPriority = 5,
        };

    private static string? Key(CommandResult result) => (result as CommandResult.Rejected)?.Reason.Key;

    [Fact]
    public void SettingTheSplitStoresThePlanAndRefusesBadInput()
    {
        var kit = new DevelopmentKit();
        Assert.IsType<CommandResult.Accepted>(kit.Submit(Split(30, 20, 50)));
        var plan = kit.Section.PlanOf(Alfa)!;
        Assert.Equal((30, 20, 50), (plan.CurrentPercent, plan.AccountPercent, plan.NextYearPercent));
        Assert.Equal(8, plan.AeroPriority);
        Assert.Equal(2, plan.ReliabilityPriority);
        Assert.Null(kit.Section.PlanOf(Beta));

        Assert.Equal(DevelopmentKeys.BadSplit, Key(kit.Submit(Split(50, 30, 30))));
        Assert.Equal(DevelopmentKeys.BadSplit, Key(kit.Submit(Split(110, -10, 0))));
        Assert.Equal(DevelopmentKeys.NoControl, Key(kit.Submit(Split(40, 30, 30, "beta"))));
        Assert.Equal(DevelopmentKeys.NoControl, Key(kit.Submit(Split(40, 30, 30, "nobody"))));
        Assert.Equal(DevelopmentKeys.BadPriority, Key(kit.Submit(Split(40, 30, 30) with { AeroPriority = 11 })));
        Assert.Equal((30, 20, 50), (kit.Section.PlanOf(Alfa)!.CurrentPercent, kit.Section.PlanOf(Alfa)!.AccountPercent, kit.Section.PlanOf(Alfa)!.NextYearPercent));
    }

    [Fact]
    public void ChangingTheSplitAsksNothingBecauseUpgradesAreNotMicromanaged()
    {
        var kit = new DevelopmentKit(seed: 6);
        kit.PutProject(Upgrade(Alfa, DevArea.Aero, 1_000_000, 0.2, days: 14));
        kit.Live(10);
        Assert.IsType<CommandResult.Accepted>(kit.Submit(Split(20, 0, 80)));
        Assert.Empty(kit.Inbox.Section.ItemsOf(Anna.Value));
        Assert.True(kit.Section.Find("dev:1")!.IsActive);
    }

    [Fact]
    public void TheNextConceptCharacterIsStoredValidatedAndRoundTripsThroughTheCodec()
    {
        var kit = new DevelopmentKit();
        var command = new SetNextConceptCommand { ManagerId = Anna, IssuedOn = Day(Opening), OrganizationId = "alfa", PhilosophyMilli = 1000, AeroMilli = -500, SubmissionNumber = 2 };
        Assert.IsType<CommandResult.Accepted>(kit.Submit(command));
        var plan = kit.Section.PlanOf(Alfa)!;
        Assert.Equal((1000, -500), (plan.NextPhilosophyMilli, plan.NextAeroMilli));
        Assert.Equal(DevelopmentKeys.BadCharacter, Key(kit.Submit(command with { PhilosophyMilli = 1001 })));
        Assert.Equal(DevelopmentKeys.NoControl, Key(kit.Submit(command with { OrganizationId = "beta" })));
        var encoded = CommandCodec.Production.Encode(command);
        Assert.Equal(command, CommandCodec.Production.Decode(encoded.Tag, encoded.Text, command.ManagerId, command.SubmissionNumber, command.IssuedOn));
    }

    [Fact]
    public void AnAiPrincipalChangingTheSplitGetsNoReplyThatWouldHoldTheClock()
    {
        var kit = new DevelopmentKit(seed: 6);
        var ai = new Paddock.Application.Managers.ManagerId("ai:alfa");
        kit.Managers.Register(ai, Paddock.Application.Managers.ManagerKind.Ai, "AI alfa");
        kit.Control.Assign(ai, Alfa);
        kit.PutProject(Upgrade(Alfa, DevArea.Aero, 1_000_000, 0.2, days: 14));
        kit.Live(10);
        Assert.IsType<CommandResult.Accepted>(kit.Submit(Split(20, 40, 40, manager: ai)));
        Assert.Empty(kit.Inbox.Section.ItemsOf(ai.Value));
        Assert.Null(kit.Managers.Get(ai).BlockingItem);
    }

    [Fact]
    public void DeployAndCutAreCheckedForOwnerKindAndStatus()
    {
        var kit = new DevelopmentKit(seed: 3);
        kit.PutProject(Upgrade(Alfa, DevArea.Aero, 1_000_000, 0.2, days: 40));
        kit.PutProject(Concept(Alfa, 1_000_000, 0.5, days: 60));
        DeployConceptCommand Deploy(string project, string timing, int races, Paddock.Application.Managers.ManagerId? manager = null, string org = "alfa") =>
            new() { ManagerId = manager ?? Anna, IssuedOn = Day(Opening), OrganizationId = org, ProjectId = project, Timing = timing, Races = races };

        Assert.Equal(DevelopmentKeys.NotConcept, Key(kit.Submit(Deploy("dev:1", "WhenReady", 0))));
        Assert.Equal(DevelopmentKeys.UnknownProject, Key(kit.Submit(Deploy("dev:99", "WhenReady", 0))));
        Assert.Equal(DevelopmentKeys.BadTiming, Key(kit.Submit(Deploy("dev:2", "Tomorrow", 0))));
        Assert.Equal(DevelopmentKeys.BadTiming, Key(kit.Submit(Deploy("dev:2", "AfterRaces", 0))));
        Assert.Equal(DevelopmentKeys.BadTiming, Key(kit.Submit(Deploy("dev:2", "AfterRaces", 31))));
        Assert.Equal(DevelopmentKeys.BadTiming, Key(kit.Submit(Deploy("dev:2", "WhenReady", 3))));
        Assert.Equal(DevelopmentKeys.NoControl, Key(kit.Submit(Deploy("dev:2", "WhenReady", 0, Bram))));
        Assert.Equal(DevelopmentKeys.UnknownProject, Key(kit.Submit(Deploy("dev:2", "WhenReady", 0, Bram, "beta"))));
        Assert.IsType<CommandResult.Accepted>(kit.Submit(Deploy("dev:2", "AfterRaces", 3)));
        Assert.Equal((ConceptTiming.AfterRaces, 3), (kit.Section.Find("dev:2")!.Timing, kit.Section.Find("dev:2")!.TimingRaces));

        Assert.Equal(DevelopmentKeys.UnknownProject, Key(kit.Submit(new CutProjectCommand { ManagerId = Bram, IssuedOn = Day(Opening), OrganizationId = "beta", ProjectId = "dev:1" })));
        Assert.Equal(DevelopmentKeys.NoControl, Key(kit.Submit(new CutProjectCommand { ManagerId = Bram, IssuedOn = Day(Opening), OrganizationId = "alfa", ProjectId = "dev:1" })));
    }

    [Fact]
    public void ARivalCannotSeeProjectsPlansOrCarsOfAnotherTeam()
    {
        var kit = new DevelopmentKit(seed: 2);
        kit.SetPlan(Alfa, 10, 10, 80);
        kit.Live(60);
        var query = new DevelopmentQuery(kit.Book, kit.Environment);

        var anna = query.View(AccessContext.ForManager(new AccessManagerId(Anna.Value)));
        var bram = query.View(AccessContext.ForAi(new AccessManagerId(Bram.Value)));
        Assert.Equal([Alfa.Value], anna.Own.Select(view => view.OrganizationId));
        Assert.Equal([Beta.Value], bram.Own.Select(view => view.OrganizationId));
        Assert.Equal(80, anna.Own[0].Next.SharePercent);
        var alfaProjects = kit.Section.ProjectsOf(Alfa).Select(project => project.Id).ToHashSet();
        Assert.NotEmpty(anna.Own[0].Projects);
        Assert.DoesNotContain(bram.Own[0].Projects, project => alfaProjects.Contains(project.ProjectId));
        Assert.Throws<InvalidOperationException>(() => query.View(AccessContext.Developer));

        Walk(typeof(DevelopmentOverview));
    }

    [Fact]
    public void TheViewShowsBandsAndAForecastWithoutChangingTheWorldOrDrawingRng()
    {
        var kit = new DevelopmentKit(seed: 2);
        kit.Live(30);
        var before = kit.World.StateHash();
        var query = new DevelopmentQuery(kit.Book, kit.Environment);
        var view = query.View(AccessContext.ForManager(new AccessManagerId(Anna.Value))).Own[0];

        Assert.Equal(before, kit.World.StateHash());
        var again = query.View(AccessContext.ForManager(new AccessManagerId(Anna.Value))).Own[0];
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(view), System.Text.Json.JsonSerializer.Serialize(again));
        Assert.Equal(view.Projects, again.Projects);
        Assert.All(view.Projects, project => Assert.True(project.ExpectedGain.High >= project.ExpectedGain.Low));
        var truth = kit.AlfaCars[0].Levels;
        var aero = view.Areas.Single(area => area.Area == DevelopmentQuery.AreaAero);
        Assert.True(aero.Own.Low <= truth.Downforce + 1 && aero.Own.High >= truth.Downforce - 1);
        Assert.True(aero.Own.High > aero.Own.Low);
        Assert.Equal(DevelopmentEstimates.TopRivals, aero.Rivals.Count);
        Assert.All(aero.Rivals, rival => Assert.True(rival.Band.High > rival.Band.Low));
        Assert.True(view.Headcount > 0);
    }

    [Fact]
    public void CommandsRoundTripThroughTheCodec()
    {
        ICommand[] commands =
        [
            Split(30, 20, 50) with { SubmissionNumber = 3 },
            new DeployConceptCommand { ManagerId = Anna, IssuedOn = Day(Opening), OrganizationId = "alfa", ProjectId = "dev:4", Timing = "AfterRaces", Races = 5, SubmissionNumber = 4 },
            new CutProjectCommand { ManagerId = Anna, IssuedOn = Day(Opening), OrganizationId = "alfa", ProjectId = "dev:4", SubmissionNumber = 5 },
        ];
        foreach (var command in commands)
        {
            var encoded = CommandCodec.Production.Encode(command);
            var decoded = CommandCodec.Production.Decode(encoded.Tag, encoded.Text, command.ManagerId, command.SubmissionNumber, ((dynamic)command).IssuedOn);
            Assert.Equal(command, decoded);
        }
    }

    [Fact]
    public void ADevelopmentSectionRoundTripsThroughTheSaveWithTheSameHash()
    {
        var kit = new DevelopmentKit(seed: 12);
        kit.SetPlan(Alfa, 30, 30, 40);
        kit.PutProject(Concept(Alfa, 1_000_000, 0.4, days: 5));
        kit.Live(150);
        Assert.NotEmpty(kit.Section.Projects);
        Assert.Contains(kit.Section.Projects, project => project.Status == ProjectStatus.Completed);
        using var file = SaveFile.Create(Path.Combine(_directory, "development.paddock"), WorldFixtures.Meta());
        var repository = new WorldRepository(file);
        repository.SaveWorld(kit.World, kit.Today);
        var loaded = repository.LoadWorld();

        Assert.Equal(kit.World.StateHash(), loaded.StateHash());
        var restored = loaded.Section<DevelopmentSection>(DevelopmentSection.SectionName)!;
        Assert.Equal(kit.Section.Projects, restored.Projects);
        Assert.Equal(kit.Section.Plans, restored.Plans);
        Assert.Equal(kit.Section.Accounts, restored.Accounts);
        Assert.Equal(kit.Section.NextProject, restored.NextProject);

        repository.SaveWorld(kit.World.WithoutSection(DevelopmentSection.SectionName), kit.Today);
        Assert.Null(repository.LoadWorld().Section<DevelopmentSection>(DevelopmentSection.SectionName));
        Assert.Equal(0L, Count(file, "development_projects"));
        Assert.Equal(0L, Count(file, "development_counter"));
    }

    [Fact]
    public void ASaveFromBeforeTheDevelopmentMigrationLoadsWithNoSectionAndTheSameHash()
    {
        var path = Path.Combine(_directory, "old.paddock");
        var before = SaveMigrations.Production.TakeWhile(migration => migration is not V016_DevelopmentSection).ToArray();
        var world = WorldFixtures.Small();
        using (var created = SaveFile.Create(path, WorldFixtures.Meta(), before))
        {
            Assert.Equal(before.Length, created.ReadMeta().SchemaVersion);
        }

        using var opened = SaveFile.Open(path);
        Assert.Equal(SaveMigrations.CurrentVersion, opened.ReadMeta().SchemaVersion);
        var repository = new WorldRepository(opened);
        repository.SaveWorld(world, WorldFixtures.Opening);
        Assert.Null(repository.LoadWorld().Section<DevelopmentSection>(DevelopmentSection.SectionName));
        Assert.Equal(world.StateHash(), repository.LoadWorld().StateHash());
        Assert.Equal(0L, Count(opened, "development_plans"));
    }

    [Fact]
    public void TheHashSeesEveryFieldOfTheSection()
    {
        var kit = new DevelopmentKit(seed: 12);
        kit.PutProject(Concept(Alfa, 1_000_000, 0.4, days: 5));
        kit.SetPlan(Alfa, 30, 30, 40);
        kit.PutAccount(new DevelopmentAccount(Alfa, 1000, 10, 1955));
        var section = kit.Section;
        string Hash(DevelopmentSection changed) => kit.World.WithSection(changed).StateHash();
        var baseline = Hash(section);
        var project = section.Projects[0];
        var plan = section.PlanOf(Alfa)!;
        var account = section.AccountOf(Alfa);
        var changes = new[]
        {
            Hash(section.ReplaceProject(project with { ProgressDays = 1 })),
            Hash(section.ReplaceProject(project with { PostedCents = 1 })),
            Hash(section.ReplaceProject(project with { Timing = ConceptTiming.WhenReady })),
            Hash(section.ReplaceProject(project with { Status = ProjectStatus.Ready })),
            Hash(section.ReplaceProject(project with { OutcomeMilli = 5 })),
            Hash(section.ReplaceProject(project with { Engineer = "someone" })),
            Hash(section.SetPlan(plan with { AeroPriority = 0 })),
            Hash(section.SetPlan(plan with { SpentNextYearCents = 7 })),
            Hash(section.SetAccount(account with { StockMilli = 2000 })),
            Hash(section.SetAccount(account with { NextYearShareMilli = 11 })),
            Hash(section.SetAccount(account with { RulesYear = 1956 })),
        };
        Assert.All(changes, changed => Assert.NotEqual(baseline, changed));
        Assert.Equal(changes.Length, changes.Distinct().Count());
    }

    [Fact]
    public void TheSectionRefusesNumbersOutOfOrderAndPlansOutOfRange()
    {
        var section = DevelopmentSection.Empty;
        var project = Upgrade(Alfa, DevArea.Aero, 1, 0.1, 5);
        Assert.Throws<InvalidOperationException>(() => section.AddProject(project with { Number = 2 }));
        var added = section.AddProject(project);
        Assert.Equal(2, added.NextProject);
        Assert.Throws<InvalidOperationException>(() => added.AddProject(project));
        Assert.Throws<InvalidOperationException>(() => added.SetPlan(DevelopmentPlan.Default(Alfa) with { CurrentPercent = 99 }));
        Assert.Throws<InvalidOperationException>(() => DevelopmentSection.Restore(1, [], [], [project]));
        Assert.True(DevelopmentSection.Empty.IsEmpty);
        Assert.False(DevProjectIds.TryParse("dev:01", out _));
        Assert.True(DevProjectIds.TryParse("dev:7", out var number) && number == 7);
    }

    private static long Count(SaveFile file, string table)
    {
        using var command = file.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM " + table;
        return (long)command.ExecuteScalar()!;
    }

    private static void Walk(Type type)
    {
        var forbidden = new HashSet<Type>
        {
            typeof(TeamCar),
            typeof(CarsSection),
            typeof(PerformanceLevels),
            typeof(DevelopmentSection),
            typeof(DevProject),
            typeof(DevelopmentPlan),
            typeof(DevelopmentAccount),
            typeof(Engineer),
        };
        var seen = new HashSet<Type>();
        void Visit(Type current)
        {
            if (!seen.Add(current))
            {
                return;
            }

            Assert.DoesNotContain(current, forbidden);
            if (current.IsGenericType)
            {
                foreach (var argument in current.GetGenericArguments())
                {
                    Visit(argument);
                }
            }

            if (current.HasElementType)
            {
                Visit(current.GetElementType()!);
            }

            if (current.Namespace is { } ns && ns.StartsWith("Paddock.", StringComparison.Ordinal))
            {
                foreach (var property in current.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    Visit(property.PropertyType);
                }
            }
        }

        Visit(type);
    }
}
