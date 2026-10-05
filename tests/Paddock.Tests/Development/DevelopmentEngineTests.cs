using Paddock.Application.Development;
using Paddock.Domain.Cars;
using Paddock.Domain.Contracts;
using Paddock.Domain.Development;
using Paddock.Domain.Finance;
using Paddock.Domain.People;
using Paddock.Domain.Random;
using Paddock.Domain.Spy;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using static Paddock.Tests.Development.DevelopmentKit;

namespace Paddock.Tests.Development;

/// <summary>
/// Car development, path A (T42). Fixtures are SYNTHETIC; every number under test is an uncalibrated ESTIMATE, so the tests
/// pin directions and proportions, not magnitudes.
/// </summary>
public class DevelopmentEngineTests
{
    private const long Annual = 15_000_000;

    [Fact]
    public void GainHalvesWithHalfTheResources()
    {
        double Gain(long cost)
        {
            var kit = new DevelopmentKit(seed: 7);
            var share = DevelopmentMath.ExpectedShare(DevKind.Upgrade, cost, Annual, 1d);
            var before = kit.Level(Alfa, DevArea.Aero);
            kit.PutProject(Upgrade(Alfa, DevArea.Aero, cost, share, days: 10));
            kit.Live(10);
            Assert.Equal(ProjectStatus.Completed, kit.Project(project => project.Number == 1).Status);
            return kit.Level(Alfa, DevArea.Aero) - before;
        }

        var full = Gain(1_000_000);
        var half = Gain(500_000);
        Assert.True(full > 0.5);
        Assert.InRange(half / full, 0.48, 0.52);
        Assert.Equal(DevelopmentMath.ExpectedShare(DevKind.Upgrade, 1_000_000, Annual, 1d), 2 * DevelopmentMath.ExpectedShare(DevKind.Upgrade, 500_000, Annual, 1d), 9);
    }

    [Fact]
    public void GainShrinksAsTheCarNearsItsCeilingAndNeverPassesIt()
    {
        double Gain(double fractionOfCeiling)
        {
            var kit = new DevelopmentKit(seed: 7);
            var car = kit.AlfaCars[0];
            var full = ConceptMapping.Effects(car.Concept, car.ConceptCeiling).Full;
            foreach (var each in kit.AlfaCars)
            {
                kit.ReplaceCar(each.WithDesign(each.Season, each.Concept, full.Scale(fractionOfCeiling), each.ConceptCeiling, 100, each.TyreWearMultiplier, each.SupplierChangeCost));
            }

            var before = kit.Level(Alfa, DevArea.Aero);
            kit.PutProject(Upgrade(Alfa, DevArea.Aero, 1_000_000, 0.5, days: 10));
            kit.Live(10);
            var after = kit.Level(Alfa, DevArea.Aero);
            Assert.True(after <= DevelopmentMath.LevelOf(full, DevArea.Aero) + 0.001);
            return after - before;
        }

        var far = Gain(0.6);
        var near = Gain(0.95);
        var nearer = Gain(0.99);
        Assert.True(far > near);
        Assert.True(near > nearer);
        Assert.True(nearer >= 0);
    }

    [Fact]
    public void DuplicateKeyRolesDoNotFillExtraChairs()
    {
        Engineer Chair(string id) => new(id, StaffRole.TechnicalDirector, new Dictionary<string, int>(), 0.5, 0.5);
        var one = EngineeringCapacity.Derive([Chair("a")], 1955, keyChairsInEra: 4, 0, Annual);
        var four = EngineeringCapacity.Derive([Chair("a"), Chair("b"), Chair("c"), Chair("d")], 1955, keyChairsInEra: 4, 0, Annual);
        Assert.Equal(one.Headcount, four.Headcount);
    }

    [Fact]
    public void ARenewalDoesNotResetEngineerAdaptation()
    {
        var kit = new DevelopmentKit();
        var engineer = kit.World.Contracts.First(contract => contract.Role.IsStaff && contract.OrganizationId == Alfa);
        var (withPrior, _) = kit.World.AddContract(new ContractSpec(
            engineer.PersonId,
            Alfa,
            engineer.Role,
            new GameDate(1948, 1, 1),
            new GameDate(1954, 12, 31),
            engineer.Salary,
            true,
            null,
            null));
        var on = new GameDate(1955, 6, 1);
        var seated = Assert.Single(EngineerRoster.Of(withPrior, Alfa, on), person => person.Id == engineer.PersonId.Value);
        Assert.Equal(1d, seated.Adaptation);
        var reset = EngineerRoster.Of(kit.World, Alfa, on).Single(person => person.Id == engineer.PersonId.Value);
        Assert.True(reset.Adaptation < 1d);
    }

    [Fact]
    public void FiftyExtraEngineersShortenAProjectButDoNotRaiseQuality()
    {
        var kit = new DevelopmentKit();
        var roster = EngineerRoster.Of(kit.World, Alfa, Opening);
        var capacity = EngineeringCapacity.Derive(roster, 1955, EngineerRoster.ChairsIn(1955), 0, Annual);
        var bigger = capacity.WithExtraHeadcount(50);
        Assert.True(bigger.DurationDays(DevelopmentEstimates.UpgradeBaseDays) < capacity.DurationDays(DevelopmentEstimates.UpgradeBaseDays));
        Assert.Equal(capacity.Quality, bigger.Quality);
        Assert.Equal(
            DevelopmentMath.ExpectedShare(DevKind.Upgrade, 1_000_000, Annual, capacity.Quality),
            DevelopmentMath.ExpectedShare(DevKind.Upgrade, 1_000_000, Annual, bigger.Quality));
        Assert.True(bigger.Slots >= capacity.Slots);

        var weak = new DevelopmentKit(skill: 3);
        var weakCapacity = EngineeringCapacity.Derive(EngineerRoster.Of(weak.World, Alfa, Opening), 1955, 3, 0, Annual);
        Assert.True(weakCapacity.Quality < capacity.Quality);
    }

    [Fact]
    public void EveryEngineerChoiceHasATraceAndEnablingTheSinkDoesNotChangeTheHash()
    {
        var sink = new MemorySink();
        var traced = new DevelopmentKit(seed: 11, sink: sink);
        var silent = new DevelopmentKit(seed: 11);
        traced.Live(120);
        silent.Live(120);

        Assert.Equal(silent.World.StateHash(), traced.World.StateHash());
        var projects = traced.Section.Projects.OrderBy(project => project.Number).ToArray();
        Assert.NotEmpty(projects);
        Assert.Equal(projects.Length, sink.Traces.Count);
        Assert.Equal(
            projects.Select(project => project.Engineer).OrderBy(id => id, StringComparer.Ordinal),
            sink.Traces.Select(trace => trace.Who).OrderBy(id => id, StringComparer.Ordinal));
        foreach (var trace in sink.Traces)
        {
            Assert.Equal("development.choose", trace.Trigger);
            Assert.True(trace.Options.Count >= 3);
            Assert.Contains(trace.Options, option => option.Id == trace.ChosenOptionId);
            Assert.All(trace.Options, option => Assert.NotEmpty(option.Factors));
            Assert.Equal(trace.Options.Max(option => option.Utility), trace.Options.First(option => option.Id == trace.ChosenOptionId).Utility, 6);
        }
    }

    [Fact]
    public void ATieBetweenEquallyGoodProposalsIsBrokenByAiDecisionsAndIsRepeatable()
    {
        Engineer Same(string id) => new(id, StaffRole.TechnicalDirector, new Dictionary<string, int>(), 0.5, 0.5);
        var kit = new DevelopmentKit();
        var plan = DevelopmentPlan.Default(Alfa) with { CurrentPercent = 100, AccountPercent = 0, NextYearPercent = 0 };
        string Pick(ulong seed) => EngineerChoice.Choose(
            Alfa,
            [Same("person:x"), Same("person:y")],
            plan,
            kit.AlfaCars,
            DevelopmentAccount.Empty(Alfa),
            [],
            Opening,
            seed,
            0)!.Chosen.Engineer.Id;

        var picks = Enumerable.Range(1, 40).Select(seed => Pick((ulong)seed)).ToArray();
        Assert.Equal(picks, Enumerable.Range(1, 40).Select(seed => Pick((ulong)seed)));
        Assert.Contains("person:x", picks);
        Assert.Contains("person:y", picks);
        var decision = EngineerChoice.Choose(Alfa, [Same("person:x"), Same("person:y")], plan, kit.AlfaCars, DevelopmentAccount.Empty(Alfa), [], Opening, 1, 0)!;
        Assert.True(decision.TieBroken);
        Assert.Contains(RngStreamName.AiDecisions, RngStreamName.All);
    }

    [Fact]
    public void ASkilledEngineerOutscoresAWeakOneInTheirOwnArea()
    {
        var kit = new DevelopmentKit();
        var plan = DevelopmentPlan.Default(Alfa) with { CurrentPercent = 100, AccountPercent = 0, NextYearPercent = 0 };
        var strong = new Engineer("person:strong", StaffRole.HeadOfAerodynamics, new Dictionary<string, int> { ["aerodynamics"] = 20 }, 0.5, 0.5);
        var weak = new Engineer("person:weak", StaffRole.HeadOfAerodynamics, new Dictionary<string, int> { ["aerodynamics"] = 2 }, 0.5, 0.5);
        var decision = EngineerChoice.Choose(Alfa, [weak, strong], plan, kit.AlfaCars, DevelopmentAccount.Empty(Alfa), [], Opening, 1, 0)!;
        Assert.Equal("person:strong", decision.Chosen.Engineer.Id);
        Assert.Equal(DevArea.Aero, decision.Chosen.Area);
    }

    [Fact]
    public void ThePlanSteersWhatTheEngineersChoose()
    {
        var kit = new DevelopmentKit();
        var engineers = EngineerRoster.Of(kit.World, Alfa, Opening);
        DevKind First(int current, int account, int next)
        {
            var plan = DevelopmentPlan.Default(Alfa) with { CurrentPercent = current, AccountPercent = account, NextYearPercent = next };
            return EngineerChoice.Choose(Alfa, engineers, plan, kit.AlfaCars, DevelopmentAccount.Empty(Alfa), [], Opening, 1, 0)!.Chosen.Kind;
        }

        Assert.Equal(DevKind.Upgrade, First(90, 5, 5));
        Assert.Equal(DevKind.Research, First(5, 90, 5));
        Assert.Equal(DevKind.Concept, First(5, 5, 90));
    }

    [Fact]
    public void SwitchingTheSplitMovesNextYearsStartAndTheSeasonsLevelInAFixedSeedAbTest()
    {
        double jumpFocusedOnNextYear = 0;
        double jumpFocusedOnCurrent = 0;
        double seasonFocusedOnNextYear = 0;
        double seasonFocusedOnCurrent = 0;
        for (ulong seed = 1; seed <= 6; seed++)
        {
            var (beforeA, afterA) = Run(seed, 20, 10, 70);
            var (beforeB, afterB) = Run(seed, 70, 10, 20);
            jumpFocusedOnNextYear += afterA - beforeA;
            jumpFocusedOnCurrent += afterB - beforeB;
            seasonFocusedOnNextYear += beforeA;
            seasonFocusedOnCurrent += beforeB;
        }

        Assert.True(jumpFocusedOnNextYear > jumpFocusedOnCurrent, "more next-year funding gives a bigger start of the new season");
        Assert.True(seasonFocusedOnCurrent > seasonFocusedOnNextYear, "more current-car funding gives a stronger car in the same season");

        static (double Before, double After) Run(ulong seed, int current, int account, int next)
        {
            var kit = new DevelopmentKit(seed);
            kit.SetPlan(Alfa, current, account, next);
            kit.Live(365);
            var before = Sum(kit);
            Assert.Equal(1955, kit.AlfaCars[0].Season);
            kit.Live(1);
            Assert.Equal(1956, kit.AlfaCars[0].Season);
            return (before, Sum(kit));
        }

        static double Sum(DevelopmentKit kit)
        {
            var levels = kit.AlfaCars[0].Levels;
            return levels.Downforce + levels.MechanicalGrip + levels.Braking + levels.Reliability;
        }
    }

    [Fact]
    public void ADeploymentMomentChangesWhenAFinishedConceptHitsTheCarAndResetsUnderstanding()
    {
        DevelopmentKit Finished()
        {
            var kit = new DevelopmentKit(seed: 3);
            kit.PutProject(Concept(Alfa, 1_000_000, 0.5, days: 5));
            kit.Live(5);
            Assert.Equal(ProjectStatus.Ready, kit.Project(project => project.Number == 1).Status);
            return kit;
        }

        var now = Finished();
        var later = Finished();
        var sum = (DevelopmentKit kit) => kit.AlfaCars[0].Levels.Downforce + kit.AlfaCars[0].Levels.Braking;
        var before = sum(now);
        Assert.True(now.AlfaCars[0].Understanding > CarEstimates.NewConceptUnderstanding);

        Assert.IsType<Paddock.Application.Commands.CommandResult.Accepted>(now.Submit(Deploy("dev:1", "WhenReady", 0)));
        Assert.Equal(ProjectStatus.InProduction, now.Project(project => project.Number == 1).Status);
        Assert.Equal(before, sum(now), 3);
        LiveThroughProduction(now);
        Assert.Equal(ProjectStatus.Deployed, now.Project(project => project.Number == 1).Status);
        Assert.True(sum(now) > before);
        Assert.True(now.AlfaCars[0].Understanding <= CarEstimates.NewConceptUnderstanding + 1);

        Assert.IsType<Paddock.Application.Commands.CommandResult.Accepted>(later.Submit(Deploy("dev:1", "NextSeason", 0)));
        Assert.Equal(ProjectStatus.Ready, later.Project(project => project.Number == 1).Status);
        Assert.Equal(before, sum(later), 3);
        Assert.True(later.AlfaCars[0].Understanding > CarEstimates.NewConceptUnderstanding);
    }

    private static DevelopmentKit ReadyConceptAcrossRollover(string timing)
    {
        var kit = new DevelopmentKit(seed: 3);
        kit.PutProject(Concept(Alfa, 1_000_000, 0.5, days: 5));
        kit.Live(5);
        Assert.IsType<Paddock.Application.Commands.CommandResult.Accepted>(kit.Submit(Deploy("dev:1", timing, 0)));
        kit.Live(365);
        Assert.Equal(1956, kit.Today.Year);
        return kit;
    }

    [Fact]
    public void AHeldConceptStaysReadyAcrossARolloverWhileANextSeasonOneIsConsumed()
    {
        var held = ReadyConceptAcrossRollover("Hold");
        Assert.Equal(ProjectStatus.Ready, held.Project(project => project.Number == 1).Status);

        var consumed = ReadyConceptAcrossRollover("NextSeason");
        Assert.Equal(ProjectStatus.Deployed, consumed.Project(project => project.Number == 1).Status);
        Assert.True(consumed.AlfaCars[0].Levels.Downforce > held.AlfaCars[0].Levels.Downforce);
    }

    [Fact]
    public void AHeldConceptCanBeDeployedLaterAndTheAccountDecaysMeanwhile()
    {
        var kit = ReadyConceptAcrossRollover("Hold");
        var downforce = kit.AlfaCars[0].Levels.Downforce;
        var stock = kit.Section.AccountOf(Alfa).StockMilli;
        kit.Live(30);
        Assert.True(kit.Section.AccountOf(Alfa).StockMilli <= stock);

        Assert.IsType<Paddock.Application.Commands.CommandResult.Accepted>(kit.Submit(Deploy("dev:1", "WhenReady", 0)));
        Assert.Equal(ProjectStatus.InProduction, kit.Project(project => project.Number == 1).Status);
        LiveThroughProduction(kit);
        Assert.Equal(ProjectStatus.Deployed, kit.Project(project => project.Number == 1).Status);
        Assert.True(kit.AlfaCars[0].Levels.Downforce > downforce);
        Assert.True(kit.AlfaCars[0].Understanding <= CarEstimates.NewConceptUnderstanding + 1);
    }

    [Fact]
    public void HoldingIsDeterministic()
    {
        Assert.Equal(ReadyConceptAcrossRollover("Hold").World.StateHash(), ReadyConceptAcrossRollover("Hold").World.StateHash());
    }

    [Fact]
    public void AConceptAfterNRacesWaitsForTheRacesAndThenDeploys()
    {
        var kit = new DevelopmentKit(seed: 3);
        kit.PutProject(Concept(Alfa, 1_000_000, 0.5, days: 5));
        kit.Live(5);
        Assert.IsType<Paddock.Application.Commands.CommandResult.Accepted>(kit.Submit(Deploy("dev:1", "AfterRaces", 2)));
        var runs = kit.AlfaCars.Select(car => (car.Id, 300)).ToArray();

        DevelopmentRaceHook.OnRaceFinished(kit.Book, kit.Environment, kit.Today, runs);
        kit.Live(1);
        Assert.Equal(ProjectStatus.Ready, kit.Project(project => project.Number == 1).Status);

        DevelopmentRaceHook.OnRaceFinished(kit.Book, kit.Environment, kit.Today, runs);
        kit.Live(1);
        Assert.Equal(ProjectStatus.InProduction, kit.Project(project => project.Number == 1).Status);
        LiveThroughProduction(kit);
        Assert.Equal(ProjectStatus.Deployed, kit.Project(project => project.Number == 1).Status);
    }

    /// <summary>Lives until the day after the last production day, when a committed concept goes live (T42c).</summary>
    internal static void LiveThroughProduction(DevelopmentKit kit)
    {
        var ends = kit.Section.Projects.Where(project => project.IsInProduction).Max(project => project.ProductionEnds!.Value);
        kit.Live(Math.Max(1, kit.Today.DaysUntil(ends) + 2));
    }

    [Fact]
    public void ACutProjectGivesAProportionalGainAndCost()
    {
        const long cost = 1_000_000;
        var share = 0.2;
        var fullKit = new DevelopmentKit(seed: 5);
        var before = fullKit.Level(Alfa, DevArea.Chassis);
        fullKit.PutProject(Upgrade(Alfa, DevArea.Chassis, cost, share, days: 20));
        fullKit.Live(20);
        var fullGain = fullKit.Level(Alfa, DevArea.Chassis) - before;

        var cutKit = new DevelopmentKit(seed: 5);
        cutKit.PutProject(Upgrade(Alfa, DevArea.Chassis, cost, share, days: 20));
        cutKit.Live(10);
        var result = cutKit.Submit(new CutProjectCommand { ManagerId = Anna, IssuedOn = Day(cutKit.Today), OrganizationId = Alfa.Value, ProjectId = "dev:1" });
        Assert.IsType<Paddock.Application.Commands.CommandResult.Accepted>(result);
        var cutGain = cutKit.Level(Alfa, DevArea.Chassis) - before;

        Assert.Equal(ProjectStatus.Cut, cutKit.Project(project => project.Number == 1).Status);
        Assert.InRange(cutGain / fullGain, 0.45, 0.55);
        var spent = -cutKit.Finance.EntriesOf(Alfa).Where(entry => entry.Category == LedgerCategories.Development && entry.Counterparty == "dev:1").Sum(entry => entry.AmountCents);
        Assert.InRange(spent, 499_000, 501_000);
        Assert.Equal(spent, cutKit.Project(project => project.Number == 1).PostedCents);

        Assert.IsType<Paddock.Application.Commands.CommandResult.Rejected>(cutKit.Submit(new CutProjectCommand { ManagerId = Anna, IssuedOn = Day(cutKit.Today), OrganizationId = Alfa.Value, ProjectId = "dev:1" }));
    }

    [Fact]
    public void SpendingPostsThroughTheLedgerAndMatchesTheProjects()
    {
        var kit = new DevelopmentKit(seed: 9);
        var opening = kit.Finance.BalanceOf(Alfa);
        kit.Live(90);
        var posted = kit.Finance.EntriesOf(Alfa).Where(entry => entry.Category == LedgerCategories.Development).ToArray();
        Assert.NotEmpty(posted);
        Assert.All(posted, entry => Assert.True(entry.AmountCents < 0));
        Assert.All(posted, entry => Assert.Equal(DevelopmentKeys.LedgerSpend, entry.ReasonKey));
        Assert.Equal(-posted.Sum(entry => entry.AmountCents), kit.Section.ProjectsOf(Alfa).Sum(project => project.PostedCents));
        Assert.True(kit.Finance.BalanceOf(Alfa) < opening);
        Assert.All(kit.Section.ProjectsOf(Alfa), project => Assert.True(project.PostedCents <= project.CostCents));
    }

    [Fact]
    public void ARegulationChangeDevaluesTheAccountInProportionToTheChangedDimensions()
    {
        int Stock(int changed)
        {
            var rules = new FakeRules()
                .Set(1955, FakeRules.WithFingerprint(0, 20))
                .Set(1956, FakeRules.WithFingerprint(changed, 20));
            var kit = new DevelopmentKit(seed: 4, rules: rules);
            kit.SetPlan(Alfa, 100, 0, 0);
            kit.PutAccount(new DevelopmentAccount(Alfa, 50_000, 0, 1955));
            kit.Live(366);
            Assert.Equal(1956, kit.Section.AccountOf(Alfa).RulesYear);
            return kit.Section.AccountOf(Alfa).StockMilli;
        }

        var none = Stock(0);
        var few = Stock(2);
        var more = Stock(4);
        Assert.True(none > 0);
        Assert.InRange((double)few / none, 0.73, 0.77);
        Assert.InRange((double)more / none, 0.48, 0.52);
        Assert.Equal(25_000, DevelopmentMath.StockAfterRuleChange(50_000, 2, 10));
        Assert.Equal(50_000, DevelopmentMath.StockAfterRuleChange(50_000, 0, 10));
        Assert.Equal(0, DevelopmentMath.StockAfterRuleChange(50_000, 10, 10));
    }

    [Fact]
    public void TheDayHandlerDevaluesTheAccountWhenTheSeasonChangedEventWasEmitted()
    {
        int Stock(int changed)
        {
            var rules = new FakeRules()
                .Set(1955, FakeRules.WithFingerprint(0, 20))
                .Set(1956, FakeRules.WithFingerprint(changed, 20));
            var kit = new DevelopmentKit(seed: 4, rules: rules);
            kit.SetPlan(Alfa, 100, 0, 0);
            kit.PutAccount(new DevelopmentAccount(Alfa, 50_000, 0, 1955));
            kit.LiveWithInbox(366);
            Assert.Equal(1956, kit.Section.AccountOf(Alfa).RulesYear);
            return kit.Section.AccountOf(Alfa).StockMilli;
        }

        var none = Stock(0);
        var more = Stock(4);
        Assert.True(none > 0);
        Assert.InRange((double)more / none, 0.48, 0.52);
    }

    [Fact]
    public void TheDayWalksOpenProjectsInNumberOrderAndDropsThemWhenTheyClose()
    {
        var kit = new DevelopmentKit(seed: 7);
        kit.PutProject(Upgrade(Alfa, DevArea.Aero, 1_000_000, 0.5, days: 10));
        AssertSameOpen(kit);
        kit.Live(10);
        Assert.Equal(ProjectStatus.Completed, kit.Project(project => project.Number == 1).Status);
        Assert.DoesNotContain(kit.Section.OpenOf(Alfa), project => project.Number == 1);
        Assert.Contains(kit.Section.ProjectsOf(Alfa), project => project.Number == 1);
        AssertSameOpen(kit);

        static void AssertSameOpen(DevelopmentKit kit)
        {
            Assert.Equal(
                kit.Section.ProjectsOf(Alfa).Where(project => project.IsOpen).Select(project => project.Number),
                kit.Section.OpenOf(Alfa).Select(project => project.Number));
        }
    }

    [Fact]
    public void TheEraTestingRulesCapUnderstanding()
    {
        double Reach(string? testing)
        {
            var rules = new FakeRules().Set(1955, new DevelopmentEra(testing, null, new Dictionary<string, string>()));
            var kit = new DevelopmentKit(seed: 2, rules: rules);
            kit.SetPlan(Alfa, 0, 100, 0);
            foreach (var car in kit.AlfaCars)
            {
                kit.ReplaceCar(car.WithDesign(car.Season, car.Concept, car.Levels, car.ConceptCeiling, 30, car.TyreWearMultiplier, car.SupplierChangeCost));
            }

            kit.Live(300);
            return kit.AlfaCars[0].Understanding;
        }

        var banned = Reach("in_season_banned");
        var open = Reach("unrestricted");
        Assert.InRange(banned, 30.1, 60.001);
        Assert.True(open > banned);
        Assert.Equal(60, DevelopmentMath.UnderstandingCap("in_season_banned", null));
        Assert.True(DevelopmentMath.UnderstandingCap("limited_days", "fixed_limit") < DevelopmentMath.UnderstandingCap("limited_days", null));
    }

    [Fact]
    public void ARaceAddsUnderstandingFromRacesAndKilometres()
    {
        var kit = new DevelopmentKit();
        foreach (var car in kit.AlfaCars)
        {
            kit.ReplaceCar(car.WithDesign(car.Season, car.Concept, car.Levels, car.ConceptCeiling, 30, car.TyreWearMultiplier, car.SupplierChangeCost));
        }

        DevelopmentRaceHook.OnRaceFinished(kit.Book, kit.Environment, kit.Today, kit.AlfaCars.Select(car => (car.Id, 300)).ToArray());
        var expected = 30 + DevelopmentEstimates.UnderstandingPerRace + (DevelopmentEstimates.UnderstandingPerHundredKm * 3);
        Assert.Equal(expected, kit.AlfaCars[0].Understanding, 3);
        Assert.Equal(kit.AlfaCars[0].Understanding, kit.AlfaCars[1].Understanding);
    }

    [Fact]
    public void ResearchFillsTheAccountAndAnUpgradeDrawsOnItAndTheStockDecays()
    {
        var research = new DevelopmentKit(seed: 1);
        research.PutProject(Upgrade(Alfa, DevArea.Aero, 1_000_000, 0.3, days: 5) with { Kind = DevKind.Research, Area = null });
        research.SetPlan(Alfa, 100, 0, 0);
        research.Live(5);
        Assert.InRange(research.Section.AccountOf(Alfa).StockMilli, 22_000, 38_000);

        double Gain(int stock)
        {
            var kit = new DevelopmentKit(seed: 1);
            kit.SetPlan(Alfa, 100, 0, 0);
            kit.PutAccount(new DevelopmentAccount(Alfa, stock, 0, 1955));
            var before = kit.Level(Alfa, DevArea.Aero);
            kit.PutProject(Upgrade(Alfa, DevArea.Aero, 1_000_000, 0.2, days: 5));
            kit.Live(5);
            if (stock > 0)
            {
                Assert.True(kit.Section.AccountOf(Alfa).StockMilli < stock * 0.85);
            }

            return kit.Level(Alfa, DevArea.Aero) - before;
        }

        Assert.True(Gain(80_000) > Gain(0));
        Assert.True(DevelopmentMath.StockAfterDay(40_000) < 40_000);
    }

    [Fact]
    public void ATeamWithoutAnyManagerOrPlanStillDevelopsAndTheSameSeedRepeatsExactly()
    {
        string Hash(ulong seed)
        {
            var kit = new DevelopmentKit(seed);
            kit.Live(300);
            Assert.NotEmpty(kit.Section.ProjectsOf(Alfa));
            Assert.NotEmpty(kit.Section.ProjectsOf(Beta));
            return kit.World.StateHash();
        }

        Assert.Equal(Hash(5), Hash(5));
        Assert.NotEqual(Hash(5), Hash(6));
    }

    [Fact]
    public void AnotherTeamsProjectsNeverShiftThisTeamsOutcomes()
    {
        var alone = new DevelopmentKit(seed: 8, betaToo: false);
        var together = new DevelopmentKit(seed: 8, betaToo: true);
        alone.Live(250);
        together.Live(250);
        Assert.Equal(
            alone.Section.ProjectsOf(Alfa).Select(project => project with { Number = 0 }),
            together.Section.ProjectsOf(Alfa).Select(project => project with { Number = 0 }));
        Assert.Equal(
            alone.AlfaCars.Select(car => (car.Levels, car.Understanding)),
            together.AlfaCars.Select(car => (car.Levels, car.Understanding)));
        var key = DevelopmentEngine.OutcomeKey(together.Section.ProjectsOf(Alfa)[0]);
        var childOfAlfa = RngStream.Derive(8, RngStreamName.Development, 1955).DeriveChild(key).NextULong();
        Assert.Equal(childOfAlfa, RngStream.Derive(8, RngStreamName.Development, 1955).DeriveChild(key).NextULong());
        Assert.NotEqual(childOfAlfa, RngStream.Derive(8, RngStreamName.People, 1955).DeriveChild(key).NextULong());
        Assert.Contains(RngStreamName.Development, RngStreamName.All);
    }

    [Fact]
    public void RolloverCarriesNextYearsWorkBanksReadyConceptsAndResetsTheYearsSpending()
    {
        var kit = new DevelopmentKit(seed: 3);
        kit.SetPlan(Alfa, 100, 0, 0);
        kit.PutProject(Concept(Alfa, 1_000_000, 0.5, days: 5));
        kit.Live(5);
        var before = kit.AlfaCars[0].Levels;
        kit.Live(361);
        Assert.Equal(1956, kit.AlfaCars[0].Season);
        Assert.Equal(ProjectStatus.Deployed, kit.Project(project => project.Number == 1).Status);
        Assert.True(kit.AlfaCars[0].Levels.Downforce > before.Downforce);
        Assert.Equal(0, kit.Section.AccountOf(Alfa).NextYearShareMilli);
        Assert.True(kit.Section.PlanOf(Alfa)!.TotalSpentCents < 5_000_000);
    }

    [Fact]
    public void FormulasAreMonotoneAndStayInRange()
    {
        Assert.True(DevelopmentMath.ExpectedShare(DevKind.Concept, 1_000_000, Annual, 0.7) > DevelopmentMath.ExpectedShare(DevKind.Upgrade, 1_000_000, Annual, 0.7));
        Assert.Equal(DevelopmentEstimates.MaxShare, DevelopmentMath.ExpectedShare(DevKind.Concept, 100_000_000, Annual, 1d));
        Assert.Equal(0d, DevelopmentMath.ExpectedShare(DevKind.Upgrade, 1_000_000, 0, 1d));
        Assert.True(DevelopmentMath.Risk(DevKind.Concept, 10) > DevelopmentMath.Risk(DevKind.Upgrade, 10));
        Assert.True(DevelopmentMath.Risk(DevKind.Upgrade, 20) < DevelopmentMath.Risk(DevKind.Upgrade, 1));
        Assert.True(DevelopmentMath.Risk(DevKind.Upgrade, 20) >= DevelopmentEstimates.MinRisk);
        Assert.InRange(DevelopmentMath.Noise(0), 0.75, 1.25);
        Assert.InRange(DevelopmentMath.Noise(1), 0.75, 1.25);
        Assert.Equal(25, EngineeringCapacity.EraHeadcount(1950));
        Assert.Equal(1000, EngineeringCapacity.EraHeadcount(2030));
        Assert.True(EngineeringCapacity.EraHeadcount(1980) > EngineeringCapacity.EraHeadcount(1955));
        Assert.Equal(3, EngineerRoster.ChairsIn(1955));
        Assert.Equal(999, DevelopmentMath.NextYearShareAfter(990, 0.9));
        Assert.Equal(1000, DevelopmentMath.NextYearShareAfter(990, 1d));
        Assert.Equal(0, DevelopmentMath.StockAfterResearch(0, 0));
        Assert.Equal(100_000, DevelopmentMath.StockAfterResearch(100_000, 0.5));
    }

    private static DeployConceptCommand Deploy(string project, string timing, int races) =>
        new()
        {
            ManagerId = Anna,
            IssuedOn = Day(Opening),
            OrganizationId = Alfa.Value,
            ProjectId = project,
            Timing = timing,
            Races = races,
        };
}
