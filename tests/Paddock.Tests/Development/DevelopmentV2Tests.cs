using System.Globalization;
using Paddock.Application.Access;
using Paddock.Application.Commands;
using Paddock.Application.Development;
using Paddock.Domain.Cars;
using Paddock.Domain.Development;
using Paddock.Persistence;
using Paddock.Simulation.Cars;
using Paddock.Simulation.Racing.Pace;
using Paddock.Tests.Persistence;
using Xunit.Abstractions;
using static Paddock.Tests.Development.DevelopmentKit;
using AccessManagerId = Paddock.Application.Access.ManagerId;

namespace Paddock.Tests.Development;

/// <summary>
/// Car development v2 (PP-066): automated by engineers, concepts that last, numbers the player can read. Fixtures are SYNTHETIC and every
/// constant under test is an ESTIMATE, so the tests pin direction and order, not magnitudes.
/// </summary>
public class DevelopmentV2Tests(ITestOutputHelper output) : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-dev2-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static DevelopmentKit ReadyRedesign(int seed = 3, int philosophy = -1000, int aero = 0, INextRaceSource? races = null)
    {
        var kit = new DevelopmentKit(seed: (ulong)seed, races: races);
        kit.PutProject(Redesign(Alfa, 1_000_000, days: 5, philosophy, aero));
        kit.Live(5);
        Assert.Equal(ProjectStatus.Ready, kit.Project(project => project.Number == 1).Status);
        return kit;
    }

    private static SetDevelopmentSplitCommand Split(int current, int account, int next) =>
        new() { ManagerId = Anna, IssuedOn = Day(Opening), OrganizationId = "alfa", CurrentPercent = current, AccountPercent = account, NextYearPercent = next, SubmissionNumber = 1 };

    private static CommitConceptCommand Commit(DevelopmentKit kit) =>
        new() { ManagerId = Anna, IssuedOn = Day(kit.Today), OrganizationId = "alfa", ProjectId = "dev:1" };

    [Fact]
    public void ANewConceptHasADrawnCeilingNearTheOneInTheCarAndEvolutionIsNarrowerThanRevolution()
    {
        var evolution = new List<double>();
        var revolution = new List<double>();
        var anchor = new DevelopmentKit().AlfaCars[0].ConceptCeiling;
        for (var seed = 1; seed <= 40; seed++)
        {
            evolution.Add(ReadyRedesign(seed, -1000).Project(p => p.Number == 1).CeilingMilli / 1000d);
            revolution.Add(ReadyRedesign(seed, 1000).Project(p => p.Number == 1).CeilingMilli / 1000d);
        }

        double Spread(List<double> values) => Math.Sqrt(values.Sum(v => Math.Pow(v - values.Average(), 2)) / values.Count);
        Assert.True(Spread(revolution) > Spread(evolution), "a revolution has a wider range of ceilings");
        Assert.True(revolution.Average() > evolution.Average(), "a revolution reaches higher on average");
        Assert.InRange(evolution.Average(), anchor - 2, anchor + 8);
        output.WriteLine($"anchor {anchor:F1}: evolution {evolution.Average():F1} +-{Spread(evolution):F1}, revolution {revolution.Average():F1} +-{Spread(revolution):F1}");
    }

    [Fact]
    public void ACommittedConceptReplacesTheConceptInTheCarAndResetsUnderstanding()
    {
        var kit = ReadyRedesign(philosophy: 1000, aero: 500);
        var project = kit.Project(p => p.Number == 1);
        var before = kit.AlfaCars[0];
        Assert.IsType<CommandResult.Accepted>(kit.Submit(Commit(kit)));
        var ends = kit.Project(p => p.Number == 1).ProductionEnds!.Value;
        while (kit.Today <= ends.AddDays(1))
        {
            kit.Live(1);
        }

        var after = kit.AlfaCars[0];
        Assert.Equal(ProjectStatus.Deployed, kit.Project(p => p.Number == 1).Status);
        Assert.Equal(1d, after.Concept.Philosophy);
        Assert.Equal(0.5d, after.Concept.Aero);
        Assert.Equal(project.CeilingMilli / 1000d, after.ConceptCeiling, 3);
        Assert.True(after.Understanding < before.Understanding);
        Assert.Equal(1955, kit.Section.AccountOf(Alfa).ConceptYear);
        Assert.Contains(kit.Section.NotesOf(Alfa), note => note.Source == UnderstandingSources.Concept && note.DeltaMilli < 0);
    }

    [Fact]
    public void ARevolutionStartsLowerThanAnEvolutionOfTheSameCeiling()
    {
        Assert.Equal(85, (int)Math.Round(DevelopmentMath.StartFraction(-1000) * 100));
        Assert.True(DevelopmentMath.StartFraction(1000) < DevelopmentMath.StartFraction(-1000));
        Assert.True(DevelopmentMath.CeilingSpread(1000, 10) > DevelopmentMath.CeilingSpread(-1000, 10));
        Assert.True(DevelopmentMath.CeilingShift(1000, 0.5) > DevelopmentMath.CeilingShift(-1000, 0.5));
        Assert.True(DevelopmentMath.ConceptRisk(10, 1955, 1000) > DevelopmentMath.ConceptRisk(10, 1955, -1000));
        var (straights, corners) = DevelopmentMath.AeroEffectPercent(500);
        Assert.True(straights < 0 && corners > 0);
    }

    [Fact]
    public void InnovationWidensTheOutcomeAndRaisesTheChanceOfABreakthroughButNotTheMean()
    {
        Assert.True(DevelopmentMath.BreakthroughChance(20) > DevelopmentMath.BreakthroughChance(10));
        Assert.True(DevelopmentMath.BreakthroughChance(10) > DevelopmentMath.BreakthroughChance(1));
        Assert.True(DevelopmentMath.Noise(1, 20) - DevelopmentMath.Noise(0, 20) > DevelopmentMath.Noise(1, 1) - DevelopmentMath.Noise(0, 1));
        Assert.Equal(1d, DevelopmentMath.Noise(0.5, 20), 9);
        Assert.Equal(1d, DevelopmentMath.Noise(0.5, 1), 9);
    }

    [Fact]
    public void ABreakthroughUpgradeGivesMoreThanTheFundingPromisedAndLiftsTheCeiling()
    {
        var lucky = 0;
        for (var seed = 1; seed <= 400 && lucky == 0; seed++)
        {
            var kit = new DevelopmentKit((ulong)seed);
            var ceiling = kit.AlfaCars[0].ConceptCeiling;
            kit.PutProject(Upgrade(Alfa, DevArea.Aero, 1_000_000, 0.4, days: 3));
            kit.Live(3);
            var project = kit.Project(p => p.Number == 1);
            if (project.IsBreakthrough)
            {
                lucky = seed;
                Assert.True(kit.AlfaCars[0].ConceptCeiling > ceiling);
                Assert.Contains(kit.Section.NotesOf(Alfa), note => note.Source == UnderstandingSources.Part);
            }
        }

        Assert.NotEqual(0, lucky);
    }

    [Fact]
    public void TheEnginesStartANewConceptByThemselvesFromTheSliderAndTheCharacter()
    {
        var kit = new DevelopmentKit(seed: 5);
        Assert.IsType<CommandResult.Accepted>(kit.Submit(Split(70, 0, 30)));
        Assert.IsType<CommandResult.Accepted>(kit.Submit(new SetNextConceptCommand { ManagerId = Anna, IssuedOn = Day(kit.Today), OrganizationId = "alfa", PhilosophyMilli = 1000, AeroMilli = -500, SubmissionNumber = 2 }));
        kit.Live(2);
        var concept = Assert.Single(kit.Section.ProjectsOf(Alfa), p => p.Kind == DevKind.Concept);
        Assert.True(concept.IsRedesign);
        Assert.Equal((1000, -500), (concept.PhilosophyMilli, concept.AeroMilli));
        Assert.Contains(kit.Section.ProjectsOf(Alfa), p => p.Kind == DevKind.Upgrade);

        Assert.IsType<CommandResult.Accepted>(kit.Submit(new SetNextConceptCommand { ManagerId = Anna, IssuedOn = Day(kit.Today), OrganizationId = "alfa", PhilosophyMilli = -1000, AeroMilli = 0, SubmissionNumber = 3 }));
        Assert.Equal((1000, -500), (kit.Project(p => p.Number == concept.Number).PhilosophyMilli, kit.Project(p => p.Number == concept.Number).AeroMilli));
    }

    [Fact]
    public void ASliderOfZeroStartsNoConceptAndAWaitingConceptBlocksAnother()
    {
        var none = new DevelopmentKit(seed: 5);
        none.SetPlan(Alfa, 100, 0, 0);
        none.Live(200);
        Assert.DoesNotContain(none.Section.ProjectsOf(Alfa), p => p.Kind == DevKind.Concept);

        var waiting = ReadyRedesign();
        waiting.SetPlan(Alfa, 80, 0, 20);
        waiting.Live(60);
        Assert.Single(waiting.Section.ProjectsOf(Alfa), p => p.Kind == DevKind.Concept);
    }

    [Fact]
    public void TheViewGivesRangesFromTheStaffAndOnlyBandsForTheTopThreeRivals()
    {
        var kit = new DevelopmentKit(seed: 2);
        var skilled = new DevelopmentKit(seed: 2, skill: 20);
        var clumsy = new DevelopmentKit(seed: 2, skill: 2);
        double Width(DevelopmentKit k)
        {
            var area = new DevelopmentQuery(k.Book, k.Environment).View(AccessContext.ForManager(new AccessManagerId(Anna.Value))).Own[0].Areas[0];
            return area.Own.High - area.Own.Low;
        }

        Assert.True(Width(skilled) < Width(clumsy), "better technical staff give a narrower range");
        var view = new DevelopmentQuery(kit.Book, kit.Environment).View(AccessContext.ForManager(new AccessManagerId(Anna.Value))).Own[0];
        Assert.Equal(DevelopmentQuery.Areas, view.Areas.Select(a => a.Area));
        var truthBeta = kit.Cars.Of(Beta)[0].Levels;
        var total = view.Areas.Single(a => a.Area == DevelopmentQuery.AreaTotal);
        var rival = Assert.Single(total.Rivals);
        Assert.Equal(Beta.Value, rival.OrganizationId);
        Assert.True(rival.Band.High - rival.Band.Low > total.Own.High - total.Own.Low, "a rival is a wider band than the own team");
        Assert.True(Math.Abs(((rival.Band.Low + rival.Band.High) / 2d) - DevelopmentMath.Overall(truthBeta)) > 0d, "the middle of a rival band is not its truth");
        Assert.Equal("Alfa 55", view.Concept.Name);
        Assert.DoesNotContain("dev:", view.Concept.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void RacesAndTestsAddUnderstandingAndTheViewSaysSo()
    {
        var kit = new DevelopmentKit(seed: 2);
        foreach (var car in kit.AlfaCars)
        {
            kit.ReplaceCar(car.WithDesign(car.Season, car.Concept, car.Levels, car.ConceptCeiling, 30, car.TyreWearMultiplier, car.SupplierChangeCost));
        }

        DevelopmentRaceHook.OnRaceFinished(kit.Book, kit.Environment, kit.Today, kit.AlfaCars.Select(car => (car.Id, 300)).ToArray());
        var view = new DevelopmentQuery(kit.Book, kit.Environment).View(AccessContext.ForManager(new AccessManagerId(Anna.Value))).Own[0];
        var note = Assert.Single(view.Understanding.Notes);
        Assert.Equal(UnderstandingSources.Race, note.Source);
        Assert.True(note.Points > 0);
    }

    [Fact]
    public void AnUnderstoodCarLosesNothingAndAnUnknownOneLosesAFewPoints()
    {
        var kit = new DevelopmentKit();
        var car = kit.AlfaCars[0];
        var limits = EraPerformanceLimits.EstimateFor(1955);
        var understood = CarPerformanceFor.Resolve(car.WithDesign(car.Season, car.Concept, car.Levels, car.ConceptCeiling, 100, car.TyreWearMultiplier, car.SupplierChangeCost), limits);
        var unknown = CarPerformanceFor.Resolve(car.WithDesign(car.Season, car.Concept, car.Levels, car.ConceptCeiling, 0, car.TyreWearMultiplier, car.SupplierChangeCost), limits);
        Assert.Equal(understood.Power, unknown.Power);
        Assert.Equal(CarEstimates.UnderstandingMaxLoss, understood.MechanicalGrip - unknown.MechanicalGrip, 6);
        Assert.Equal(CarEstimates.UnderstandingMaxLoss, understood.Reliability - unknown.Reliability, 6);
    }

    [Fact]
    public void TheEngineersTellTheHumanWhatArrivedAndAskWithNumbersButAnAiHearsNothing()
    {
        var kit = new DevelopmentKit(seed: 3);
        var ai = new Paddock.Application.Managers.ManagerId("ai:alfa");
        kit.Managers.Register(ai, Paddock.Application.Managers.ManagerKind.Ai, "AI alfa");
        kit.Control.Assign(ai, Alfa);
        kit.PutProject(Upgrade(Alfa, DevArea.Aero, 1_000_000, 0.4, days: 4) with { Engineer = "person:a-td" });
        kit.PutProject(Redesign(Alfa, 1_000_000, days: 6) with { Engineer = "person:a-td" });
        kit.PutProject(Upgrade(Beta, DevArea.Aero, 1_000_000, 0.4, days: 4));
        kit.LiveWithInbox(6);
        var items = kit.Inbox.Section.ItemsOf(Anna.Value);
        var part = Assert.Single(items, item => item.Kind == DevelopmentKeys.PartKind);
        Assert.StartsWith("development.inbox.part.", part.Draft.SubjectKey, StringComparison.Ordinal);
        Assert.Contains("–", part.Arguments["before"], StringComparison.Ordinal);
        Assert.Contains("–", part.Arguments["after"], StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(part.Arguments["rivals"]));
        var ask = Assert.Single(items, item => item.Kind == DevelopmentKeys.ConceptInboxKind);
        Assert.True(ask.NeedsDecision);
        Assert.StartsWith("development.inbox.newConcept.", ask.Draft.SubjectKey, StringComparison.Ordinal);
        Assert.Equal("Alfa 55", ask.Arguments["concept"]);
        foreach (var name in new[] { "ceiling", "ceilingNow", "gainLow", "gainHigh", "start", "levelNow", "days", "amount" })
        {
            Assert.True(ask.Arguments.ContainsKey(name), name);
        }

        Assert.Equal(DevelopmentKeys.OptionWait, ask.Draft.DefaultOptionId);
        Assert.Empty(kit.Inbox.Section.ItemsOf(ai.Value));
        Assert.Null(kit.Managers.Get(ai).BlockingItem);
    }

    [Fact]
    public void AFailedConceptIsNotSilent()
    {
        var kit = new DevelopmentKit(seed: 3);
        kit.PutProject(Redesign(Alfa, 1_000_000, days: 4, risk: 1000));
        kit.LiveWithInbox(4);
        Assert.Equal(ProjectStatus.Failed, kit.Project(p => p.Number == 1).Status);
        Assert.Contains(kit.Inbox.Section.ItemsOf(Anna.Value), item => item.Kind == DevelopmentKeys.ConceptFailedKind);
    }

    [Fact]
    public void TheNewFieldsRoundTripThroughTheSaveWithTheSameHash()
    {
        var kit = ReadyRedesign(philosophy: 1000, aero: 500);
        Assert.IsType<CommandResult.Accepted>(kit.Submit(new SetNextConceptCommand { ManagerId = Anna, IssuedOn = Day(kit.Today), OrganizationId = "alfa", PhilosophyMilli = 1000, AeroMilli = 500, SubmissionNumber = 2 }));
        foreach (var car in kit.AlfaCars)
        {
            kit.ReplaceCar(car.WithDesign(car.Season, car.Concept, car.Levels, car.ConceptCeiling, 30, car.TyreWearMultiplier, car.SupplierChangeCost));
        }

        DevelopmentRaceHook.OnRaceFinished(kit.Book, kit.Environment, kit.Today, kit.AlfaCars.Select(car => (car.Id, 200)).ToArray());
        using var file = SaveFile.Create(Path.Combine(_directory, "v2.paddock"), WorldFixtures.Meta());
        var repository = new WorldRepository(file);
        repository.SaveWorld(kit.World, kit.Today);
        var loaded = repository.LoadWorld();
        Assert.Equal(kit.World.StateHash(), loaded.StateHash());
        var section = loaded.Section<DevelopmentSection>(DevelopmentSection.SectionName)!;
        Assert.Equal(kit.Section.Projects, section.Projects);
        Assert.Equal(kit.Section.Plans, section.Plans);
        Assert.Equal(kit.Section.Accounts, section.Accounts);
        Assert.Equal(kit.Section.Notes, section.Notes);
        Assert.NotEmpty(section.Notes);
    }

    [Fact]
    public void TheSameSeedGivesTheSameDevelopmentForThreeSeasons()
    {
        string Run()
        {
            var kit = new DevelopmentKit(seed: 9);
            Play(kit, Policy.SwitchRevolution, 3 * 365);
            return kit.World.StateHash();
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void TheAiAndTheHumanUseTheSameCommandsAndTheSameAutomatedPath()
    {
        // Two teams of equal staff and equal cars: the human commits through the dispatcher as Anna, the AI manager as Bram.
        var kit = new DevelopmentKit(seed: 4);
        Assert.IsType<CommandResult.Accepted>(kit.Submit(new SetNextConceptCommand { ManagerId = Bram, IssuedOn = Day(kit.Today), OrganizationId = "beta", PhilosophyMilli = 1000, AeroMilli = 0, SubmissionNumber = 2 }));
        Assert.IsType<CommandResult.Accepted>(kit.Submit(new SetNextConceptCommand { ManagerId = Anna, IssuedOn = Day(kit.Today), OrganizationId = "alfa", PhilosophyMilli = 1000, AeroMilli = 0, SubmissionNumber = 3 }));
        kit.Live(200);
        var a = kit.Section.ProjectsOf(Alfa).Where(p => p.Kind == DevKind.Concept).ToArray();
        var b = kit.Section.ProjectsOf(Beta).Where(p => p.Kind == DevKind.Concept).ToArray();
        Assert.Equal(a.Length, b.Length);
        Assert.Equal(a.Select(p => (p.Status, p.PhilosophyMilli, p.DurationDays)), b.Select(p => (p.Status, p.PhilosophyMilli, p.DurationDays)));
    }

    // ------------------------------------------------------------------ balance: keep one concept or switch (PP-066)

    private enum Policy
    {
        Keep,
        SwitchEvolution,
        SwitchRevolution,
    }

    /// <summary>
    /// Plays a team on a policy and returns the mean strength of its car, as the race reads it (understanding included), sampled weekly.
    /// "Keep" puts every person on the car that races. A switcher gives 30 percent to the next concept and introduces every finished concept
    /// at once, which is the automated path an AI follows too.
    /// </summary>
    private static (double Mean, double[] Seasons) Play(DevelopmentKit kit, Policy policy, int days)
    {
        var limits = EraPerformanceLimits.EstimateFor(1955);
        var number = 1L;
        switch (policy)
        {
            case Policy.Keep:
                kit.SetPlan(Alfa, 100, 0, 0);
                break;
            default:
                kit.SetPlan(Alfa, 70, 0, 30);
                var plan = kit.Section.PlanOf(Alfa)! with { NextPhilosophyMilli = policy == Policy.SwitchRevolution ? 1000 : -1000 };
                kit.PutPlan(plan);
                break;
        }

        var samples = new List<double>();
        var perSeason = new double[(days + 364) / 365];
        var counts = new int[perSeason.Length];
        for (var day = 0; day < days; day++)
        {
            kit.Live(1);
            if (policy != Policy.Keep && kit.Section.Projects.FirstOrDefault(p => p.Organization == Alfa && p.Kind == DevKind.Concept && p.Status == ProjectStatus.Ready) is { } ready)
            {
                kit.Submit(new CommitConceptCommand { ManagerId = Anna, IssuedOn = Day(kit.Today), OrganizationId = "alfa", ProjectId = ready.Id, SubmissionNumber = number++ });
            }

            var doy = kit.Today.DayOfYear;
            if (doy is > 60 and < 300 && doy % 21 == 0)
            {
                DevelopmentRaceHook.OnRaceFinished(kit.Book, kit.Environment, kit.Today, kit.AlfaCars.Select(car => (car.Id, 300)).ToArray());
            }

            if (day % 7 == 0)
            {
                var performance = CarPerformanceFor.Resolve(kit.AlfaCars[0], limits);
                var strength = (performance.Power + performance.Downforce + performance.MechanicalGrip + performance.Braking + performance.Reliability) / 5d;
                samples.Add(strength);
                perSeason[day / 365] += strength;
                counts[day / 365]++;
            }
        }

        return (samples.Average(), perSeason.Select((sum, i) => sum / counts[i]).ToArray());
    }

    private static DevelopmentKit Team(ulong seed, int skill, double strength)
    {
        var kit = new DevelopmentKit(seed, betaToo: false, skill: skill);
        foreach (var car in kit.AlfaCars)
        {
            var levels = PerformanceLevels.Of(strength, strength, strength, strength, strength);
            kit.ReplaceCar(car.WithDesign(car.Season, car.Concept, levels, strength + CarEstimates.InitialHeadroom, 100, car.TyreWearMultiplier, car.SupplierChangeCost));
        }

        return kit;
    }

    [Fact]
    public void KeepingOneConceptAndSwitchingConceptsEachBeatTheOtherSomewhereOverThreeSeasons()
    {
        var keepWins = 0;
        var switchWins = 0;
        var cells = 0;
        var table = new List<string>();
        foreach (var skill in new[] { 6, 12, 18 })
        {
            foreach (var strength in new[] { 40d, 60d, 75d })
            {
                var sums = new Dictionary<Policy, double>();
                var last = new Dictionary<Policy, double>();
                foreach (var policy in Enum.GetValues<Policy>())
                {
                    double total = 0;
                    double final = 0;
                    for (ulong seed = 1; seed <= 4; seed++)
                    {
                        var (mean, seasons) = Play(Team(seed, skill, strength), policy, 3 * 365);
                        total += mean;
                        final += seasons[^1];
                    }

                    sums[policy] = total / 4;
                    last[policy] = final / 4;
                }

                table.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"skill {skill,2} strength {strength,3}: keep {sums[Policy.Keep]:F1} (year3 {last[Policy.Keep]:F1}), evolve+switch {sums[Policy.SwitchEvolution]:F1} (year3 {last[Policy.SwitchEvolution]:F1}), revolve+switch {sums[Policy.SwitchRevolution]:F1} (year3 {last[Policy.SwitchRevolution]:F1})"));
                var bestSwitch = Math.Max(sums[Policy.SwitchEvolution], sums[Policy.SwitchRevolution]);
                cells++;
                if (sums[Policy.Keep] > bestSwitch)
                {
                    keepWins++;
                }
                else
                {
                    switchWins++;
                }
            }
        }

        foreach (var line in table)
        {
            output.WriteLine(line);
        }

        output.WriteLine($"keep wins {keepWins} of {cells}, switching wins {switchWins} of {cells}");
        Assert.True(keepWins > 0, "keeping one concept must win somewhere");
        Assert.True(switchWins > 0, "switching must win somewhere");
    }
}
