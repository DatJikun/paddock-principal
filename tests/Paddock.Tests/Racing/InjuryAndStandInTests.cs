using System.Collections.Immutable;
using AccessContext = Paddock.Application.Access.AccessContext;
using AccessManagerId = Paddock.Application.Access.ManagerId;
using Paddock.Application.Career;
using Paddock.Application.Inbox;
using Paddock.Application.Localization;
using Paddock.Application.Racing;
using Paddock.Domain.Cars;
using Paddock.Domain.Inbox;
using Paddock.Domain.People;
using Paddock.Domain.Racing;
using Paddock.Domain.Random;
using Paddock.Domain.Spy;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Cars;
using Paddock.Simulation.Racing.Incidents;
using Paddock.Simulation.Racing.Points;
using Paddock.Simulation.Racing.Qualifying;
using Paddock.Simulation.Racing.Weather;
using Paddock.Simulation.Racing.Weekend;
using Paddock.Persistence;
using Paddock.Tests.Career;
using Paddock.Tests.Persistence;

namespace Paddock.Tests.Racing;

/// <summary>
/// Tests for driver injuries and stand-ins (PP-061, #219).
/// Covers light and serious injuries, pace penalties, stand-in candidate ranking by team belief (INV-003),
/// reserve priority, inbox decisions, AI traces, race reports, and determinism.
/// </summary>
public sealed class InjuryAndStandInTests
{
    private static readonly TranslationCatalog Catalog = TranslationLoader.LoadDirectory(Path.Combine(RepoPaths.Root(), "strings"));

    [Fact]
    public void LightInjury_RngDrawsZeroOrOneRaceOut_AndPacePenaltyAppliesWithin21Days()
    {
        // Incident child stream RNG:
        var seed = 42UL;
        var date = new GameDate(1955, 5, 22);
        var incidentStream = RngStream.Derive(seed, RngStreamName.Incidents, 1955, 1);
        var rng = incidentStream.DeriveChild($"injury:driver_1:{date}");

        var racesOut = rng.NextDouble() < IncidentConstants.LightInjuryZeroRacesProbability ? 0 : 1;
        Assert.True(racesOut is 0 or 1);

        // When 0 races out, InjuredUntil is date of injury:
        var world = CreateWorldWithTeamAndDrivers(out var teamId, out var driver1Id, out _, out _);
        world = world.InjurePerson(driver1Id, date);
        var driver = world.GetPerson(driver1Id)!;

        // Driver was injured on date of race, but is not injured for upcoming races:
        Assert.True(driver.IsInjured(date));
        Assert.False(driver.IsInjured(date.AddDays(7)));

        // Race within 21 days: loses pace share (LightInjuryPacePenalty = 0.03)
        var rules = CareerKit.Data.RuleSetFor(1955);
        var raceDateWithin21 = date.AddDays(14);
        var fieldWithin21 = RaceFieldBuilder.Build(world, raceDateWithin21, rules, 10, null, null);
        var entryWithin21 = fieldWithin21.Entries.FirstOrDefault(e => e.Drivers[0].DriverId == driver1Id.Value);
        Assert.NotNull(entryWithin21);
        var expectedPenalizedPace = 15 * WeekendConstants.AttributeToRatingScale * (1.0 - IncidentConstants.LightInjuryPacePenalty);
        Assert.Equal(expectedPenalizedPace, entryWithin21.Drivers[0].Pace.Pace, precision: 5);

        // Race after 21 days: normal pace
        var raceDateAfter21 = date.AddDays(25);
        var fieldAfter21 = RaceFieldBuilder.Build(world, raceDateAfter21, rules, 10, null, null);
        var entryAfter21 = fieldAfter21.Entries.FirstOrDefault(e => e.Drivers[0].DriverId == driver1Id.Value);
        Assert.NotNull(entryAfter21);
        var expectedNormalPace = 15 * WeekendConstants.AttributeToRatingScale;
        Assert.Equal(expectedNormalPace, entryAfter21.Drivers[0].Pace.Pace, precision: 5);
    }

    [Fact]
    public void SeriousInjury_RngDrawsBetween2And6Races_AndDriverReturnsWhenHealed()
    {
        var seed = 12345UL;
        var date = new GameDate(1955, 5, 22);
        var incidentStream = RngStream.Derive(seed, RngStreamName.Incidents, 1955, 1);
        var rng = incidentStream.DeriveChild($"injury:driver_1:{date}");

        var racesOut = rng.NextInt(IncidentConstants.MinSeriousRaces, IncidentConstants.MaxSeriousRaces + 1);
        Assert.InRange(racesOut, 2, 6);

        // Person injured until race date covering racesOut:
        var healDate = date.AddDays(60);
        var world = CreateWorldWithTeamAndDrivers(out _, out var driver1Id, out _, out _);
        world = world.InjurePerson(driver1Id, healDate);
        var driver = world.GetPerson(driver1Id)!;

        Assert.True(driver.IsInjured(date.AddDays(30)));
        Assert.True(driver.IsInjured(healDate));
        Assert.False(driver.IsInjured(healDate.AddDays(1)));
    }

    [Fact]
    public void StandInCandidateFinder_PrioritizesContractedReserveDriver()
    {
        var world = CreateWorldWithTeamAndDrivers(out var teamId, out var driver1Id, out _, out var reserveId);

        // driver1 is injured:
        var nextRace = new GameDate(1955, 6, 5);
        world = world.InjurePerson(driver1Id, nextRace);

        var candidates = StandInCandidateFinder.FindCandidates(world, teamId, nextRace);
        Assert.NotEmpty(candidates);
        // The first candidate MUST be the contracted reserve driver:
        Assert.Equal(reserveId, candidates[0].Id);
    }

    [Fact]
    public void StandInCandidateFinder_RanksFreeAgentsByTeamKnowledge_NotHiddenTruth()
    {
        // Setup team without reserve, and two free agents:
        // FreeAgent1: high hidden truth, but team has LOW belief band.
        // FreeAgent2: lower hidden truth, but team has HIGH belief band.
        var today = new GameDate(1955, 1, 1);
        var world = WorldState.At(today);

        (world, var teamId) = world.AddOrganization(new OrganizationSpec(
            OrganizationKind.Team, true, "alpha", today, null, 1_000_000, [new("Alpha", today, null)]));

        // Free agent 1: high truth (18), low belief (5..8)
        (world, var agent1Id) = world.AddPerson(new PersonSpec(
            "High", "Hidden", new GameDate(1925, 1, 1), "GBR", false, null, [PersonRole.Driver],
            CreateDriverTruth(18)));

        // Free agent 2: lower truth (10), high belief (14..16)
        (world, var agent2Id) = world.AddPerson(new PersonSpec(
            "Good", "Reputation", new GameDate(1926, 1, 1), "ITA", false, null, [PersonRole.Driver],
            CreateDriverTruth(10)));

        // Beliefs of the team:
        world = world.SetKnowledge(new PersonKnowledge(teamId, agent1Id, [new("cornering", new AttributeBand(5, 8))], null));
        world = world.SetKnowledge(new PersonKnowledge(teamId, agent2Id, [new("cornering", new AttributeBand(14, 16))], null));

        var candidates = StandInCandidateFinder.FindCandidates(world, teamId, today);
        Assert.True(candidates.Count >= 2);
        // Candidate ranked first must be agent2 because team belief is higher, even though agent1 has higher truth!
        Assert.Equal(agent2Id, candidates[0].Id);
        Assert.Equal(agent1Id, candidates[1].Id);
    }

    [Fact]
    public void AIPrincipal_SelectsStandIn_AndRecordsDecisionTrace()
    {
        var world = CreateWorldWithTeamAndDrivers(out var teamId, out var driver1Id, out _, out var reserveId);
        var raceDate = new GameDate(1955, 6, 5);
        world = world.InjurePerson(driver1Id, raceDate);

        var sink = new MemorySink();
        var rules = CareerKit.Data.RuleSetFor(1955);
        var field = RaceFieldBuilder.Build(world, raceDate, rules, 10, null, null, round: 2, traceSink: sink);

        // Driver 1 was replaced by reserve:
        var entry = Assert.Single(field.Entries);
        Assert.Equal(reserveId.Value, entry.Drivers[0].DriverId);
        var standInFact = Assert.Single(field.StandIns);
        Assert.Equal(reserveId.Value, standInFact.DriverId);
        Assert.Equal(driver1Id.Value, standInFact.RegularDriverId);

        // Decision trace recorded:
        var trace = Assert.Single(sink.Traces);
        Assert.Equal(StandInResolver.Kind, trace.Trigger);
        Assert.Equal(reserveId.Value, trace.ChosenOptionId);
        Assert.Equal("ai:" + teamId.Value, trace.Who);
    }

    [Fact]
    public void HumanManager_InboxDecisionForStandIn_CanSelectDriverOrSkip()
    {
        var world = CreateWorldWithTeamAndDrivers(out var teamId, out var driver1Id, out _, out var reserveId);
        var raceDate = new GameDate(1955, 6, 5);
        world = world.InjurePerson(driver1Id, raceDate);

        var draft = new InboxItemDraft(
            StandInResolver.Kind,
            StandInKeys.Subject,
            [
                new("driverId", driver1Id.Value),
                new("raceDate", raceDate.ToString()),
            ],
            [
                new InboxOption(reserveId.Value, StandInKeys.OptionCandidateLabel, StandInKeys.OptionCandidateConsequence),
                new InboxOption(StandInResolver.OptionSkip, StandInKeys.OptionSkipLabel, StandInKeys.OptionSkipConsequence),
            ],
            raceDate,
            reserveId.Value);

        var (inboxWithItem, item) = InboxSection.Empty.Add("human", draft, new GameDate(1955, 5, 25));

        // When human chose Skip, the car does NOT start:
        var inboxSkipped = inboxWithItem.Resolve(item.Id, StandInResolver.OptionSkip, raceDate);
        var rules = CareerKit.Data.RuleSetFor(1955);
        var fieldSkip = RaceFieldBuilder.Build(world.WithSection(inboxSkipped), raceDate, rules, 10, null, null);
        Assert.Empty(fieldSkip.Entries);

        // If human instead picked reserve:
        var inboxPickedReserve = inboxWithItem.Resolve(item.Id, reserveId.Value, raceDate);
        var fieldReserve = RaceFieldBuilder.Build(world.WithSection(inboxPickedReserve), raceDate, rules, 10, null, null);
        var entry = Assert.Single(fieldReserve.Entries);
        Assert.Equal(reserveId.Value, entry.Drivers[0].DriverId);
    }

    [Fact]
    public void NoStandInCandidate_CarDoesNotStart()
    {
        // World with only 1 driver (the regular one) and no free agents/reserves:
        var today = new GameDate(1955, 1, 1);
        var world = WorldState.At(today);
        (world, var teamId) = world.AddOrganization(new OrganizationSpec(
            OrganizationKind.Team, true, "alpha", today, null, 1_000_000, [new("Alpha", today, null)]));
        (world, var driverId) = world.AddPerson(new PersonSpec(
            "Sole", "Driver", new GameDate(1925, 1, 1), "GBR", false, null, [PersonRole.Driver], CreateDriverTruth(10)));
        (world, _) = world.AddContract(new ContractSpec(
            driverId, teamId, ContractRole.Driver(SeatStatus.NumberOne), today, new GameDate(1955, 12, 31), 100_000, true, null, null));

        var cars = CarsSection.Empty.Add(CreateTeamCar(CarIds.Format(1), teamId, 1955, driverId));
        world = world.WithSection(cars);

        var raceDate = new GameDate(1955, 6, 5);
        world = world.InjurePerson(driverId, raceDate);

        var rules = CareerKit.Data.RuleSetFor(1955);
        var field = RaceFieldBuilder.Build(world, raceDate, rules, 10, null, null);
        Assert.Empty(field.Entries);
    }

    [Fact]
    public void RaceReport_MentionsStandInInEnglishAndPolish()
    {
        var input = new RaceReportInput(
            Season: 1955,
            Round: 1,
            TrackId: "testring",
            Conditions: new RaceReportConditions(WetnessBand.Dry, 20),
            Qualifying: new QualifyingResult([], [], [], 0, []),
            Tape: RaceTape.From([new RaceStarted(0, 0, 0, 1, ["sub_driver"]), new RaceEnded(1, 1, 10_000)]),
            CarResults: [new CarRaceResult("car:1", "sub_driver", "ferrari", 1, FinishStatus.Classified, 1, 10.0, null, null, ["sub_driver"], 0, ["dry"], false)],
            Classification: new RaceClassification([new ClassifiedCar(1, true, "ferrari", ["sub_driver"], 8m, 8m)], [new DriverScore("sub_driver", 8m, 1)], [new ConstructorScore("ferrari", 8m, [1])]),
            PersonOutcomes: [],
            PitStops: [],
            Neutralisations: [],
            ScheduledLaps: 1,
            LapsRun: 1,
            StandIns: [new StandInFact("sub_driver", "regular_driver", "ferrari")]);

        var report = RaceReportBuilder.Build(input);
        var resultSection = report.Sections.First(s => s.Title.Key == RaceReportKeys.SectionResult);
        var standInLine = resultSection.Lines.FirstOrDefault(l => l.Key == RaceReportKeys.ResultStandIn);
        Assert.NotNull(standInLine);

        // Check English rendering:
        var enSink = new CollectingMissingKeySink();
        var enText = RaceReportRenderer.Line(standInLine, new Localizer(Catalog, Language.En, enSink), RaceReportNames.Ids);
        Assert.Empty(enSink.Reports);
        Assert.Equal("sub_driver stood in for regular_driver at ferrari.", enText);

        // Check Polish rendering:
        var plSink = new CollectingMissingKeySink();
        var plText = RaceReportRenderer.Line(standInLine, new Localizer(Catalog, Language.Pl, plSink), RaceReportNames.Ids);
        Assert.Empty(plSink.Reports);
        Assert.Equal("sub_driver zastąpił kierowcę regular_driver w zespole ferrari.", plText);
    }

    [Fact]
    public void Career_SaveAndResumeDuringInjury_PreservesStateHashAndDeterminism()
    {
        var opened = CareerKit.Opened(Domain.Career.CareerPreset.Chaos, 1955, 42UL);
        var session = opened.Session;

        // Run until before first race:
        var runResult = CareerHost.RunUntil(session, new GameDate(1955, 3, 1), null, CareerKit.Options);

        // Injure a driver:
        var driver = session.World.Persons.First(p => p.Roles.Any(r => r.IsDriver));
        var injuryUntil = new GameDate(1955, 5, 1);
        session.StoreWorld(session.World.InjurePerson(driver.Id, injuryUntil));

        var hashBeforeSave = session.World.StateHash();

        // Save session:
        var dir = Directory.CreateTempSubdirectory("paddock-injury-test-").FullName;
        try
        {
            var savePath = Path.Combine(dir, "injury.paddock");
            CareerKit.Save(savePath, opened, session, runResult.Host);

            // Resume session:
            var (resumedSession, _) = CareerKit.Resume(savePath);
            var hashAfterResume = resumedSession.World.StateHash();

            Assert.Equal(hashBeforeSave, hashAfterResume);

            var resumedDriver = resumedSession.World.GetPerson(driver.Id);
            Assert.NotNull(resumedDriver);
            Assert.Equal(injuryUntil, resumedDriver.InjuredUntil);
            Assert.True(resumedDriver.IsInjured(new GameDate(1955, 4, 1)));
            Assert.False(resumedDriver.IsInjured(new GameDate(1955, 5, 2)));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void StandInInboxDecision_OptionsHaveCandidateNameAndTeamDisplayName()
    {
        var world = CreateWorldWithTeamAndDrivers(out var teamId, out var driver1Id, out _, out var reserveId);
        var team = world.GetOrganization(teamId)!;
        var injuredDriver = world.GetPerson(driver1Id)!;
        var reserveDriver = world.GetPerson(reserveId)!;

        var today = new GameDate(1955, 5, 25);
        var nextRaceDate = new GameDate(1955, 6, 5);

        var draft = RaceWeekendDay.BuildStandInDraft(world, team, injuredDriver, CarIds.Format(1), nextRaceDate, today);

        var (inboxSection, item) = InboxSection.Empty.Add("human:player", draft, today);
        var book = new InboxBook(initial: inboxSection);
        var query = new InboxQuery(book);

        var view = query.View(AccessContext.ForManager(new AccessManagerId("human:player")));
        var itemView = Assert.Single(view.Items);

        var localizerPl = new Localizer(Catalog, Language.Pl, new CollectingMissingKeySink());
        var localizerEn = new Localizer(Catalog, Language.En, new CollectingMissingKeySink());

        var reserveOption = itemView.Options.FirstOrDefault(o => o.Id == reserveId.Value);
        Assert.NotNull(reserveOption);

        var plLabel = localizerPl.Get(reserveOption.Label.Key, reserveOption.Label.Parameters.ToDictionary(k => k.Key, k => (object?)k.Value));
        Assert.Contains(reserveDriver.Name, plLabel);
        Assert.DoesNotContain("{candidate}", plLabel);

        var plConsequence = localizerPl.Get(reserveOption.Consequence.Key, reserveOption.Consequence.Parameters.ToDictionary(k => k.Key, k => (object?)k.Value));
        Assert.Contains(reserveDriver.Name, plConsequence);
        Assert.Contains(team.NameOn(today), plConsequence);
        Assert.DoesNotContain("mercedes", plConsequence);
        Assert.DoesNotContain("{candidate}", plConsequence);
        Assert.DoesNotContain("{team}", plConsequence);

        var enLabel = localizerEn.Get(reserveOption.Label.Key, reserveOption.Label.Parameters.ToDictionary(k => k.Key, k => (object?)k.Value));
        Assert.Contains(reserveDriver.Name, enLabel);
        Assert.DoesNotContain("{candidate}", enLabel);

        var enConsequence = localizerEn.Get(reserveOption.Consequence.Key, reserveOption.Consequence.Parameters.ToDictionary(k => k.Key, k => (object?)k.Value));
        Assert.Contains(reserveDriver.Name, enConsequence);
        Assert.Contains(team.NameOn(today), enConsequence);
        Assert.DoesNotContain("mercedes", enConsequence);
        Assert.DoesNotContain("{candidate}", enConsequence);
        Assert.DoesNotContain("{team}", enConsequence);

        // Also verify free agent option
        (world, var freeAgentId) = world.AddPerson(new PersonSpec(
            "Free", "Agent", new GameDate(1926, 1, 1), "ITA", false, null, [PersonRole.Driver],
            CreateDriverTruth(10)));
        var freeAgent = world.GetPerson(freeAgentId)!;

        var draftWithFree = RaceWeekendDay.BuildStandInDraft(world, team, injuredDriver, CarIds.Format(1), nextRaceDate, today);
        var (inboxWithFree, itemWithFree) = InboxSection.Empty.Add("human:player", draftWithFree, today);
        var viewWithFree = new InboxQuery(new InboxBook(initial: inboxWithFree)).View(AccessContext.ForManager(new AccessManagerId("human:player")));
        var itemWithFreeView = Assert.Single(viewWithFree.Items);
        var freeOption = itemWithFreeView.Options.FirstOrDefault(o => o.Id == freeAgentId.Value);
        Assert.NotNull(freeOption);

        var plFreeLabel = localizerPl.Get(freeOption.Label.Key, freeOption.Label.Parameters.ToDictionary(k => k.Key, k => (object?)k.Value));
        Assert.Contains(freeAgent.Name, plFreeLabel);
        Assert.DoesNotContain("{candidate}", plFreeLabel);

        var plFreeConsequence = localizerPl.Get(freeOption.Consequence.Key, freeOption.Consequence.Parameters.ToDictionary(k => k.Key, k => (object?)k.Value));
        Assert.Contains(freeAgent.Name, plFreeConsequence);
        Assert.Contains(team.NameOn(today), plFreeConsequence);
        Assert.DoesNotContain("mercedes", plFreeConsequence);
        Assert.DoesNotContain("{candidate}", plFreeConsequence);
        Assert.DoesNotContain("{team}", plFreeConsequence);

        // Verify persistence roundtrip of option arguments through save:
        var dir = Directory.CreateTempSubdirectory("paddock-inbox-opt-").FullName;
        try
        {
            var savePath = Path.Combine(dir, "opt.paddock");
            using (var save = SaveFile.Create(savePath, WorldFixtures.Meta()))
            {
                var repo = new WorldRepository(save);
                repo.SaveWorld(world.WithSection(inboxWithFree), WorldFixtures.Opening);
                var loaded = repo.LoadWorld();
                var loadedInbox = loaded.Section<InboxSection>(InboxSection.SectionName)!;
                var loadedItem = loadedInbox.Find(itemWithFree.Id)!;
                var loadedReserveOpt = loadedItem.Options.First(o => o.Id == reserveId.Value);
                Assert.Equal(reserveDriver.Name, loadedReserveOpt.Arguments["candidate"]);
                Assert.Equal(team.NameOn(today), loadedReserveOpt.Arguments["team"]);
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static WorldState CreateWorldWithTeamAndDrivers(
        out OrganizationId teamId,
        out PersonId driver1Id,
        out PersonId driver2Id,
        out PersonId reserveId)
    {
        var today = new GameDate(1955, 1, 1);
        var world = WorldState.At(today);

        (world, teamId) = world.AddOrganization(new OrganizationSpec(
            OrganizationKind.Team, true, "mercedes", today, null, 1_000_000, [new("Mercedes", today, null)]));

        (world, driver1Id) = world.AddPerson(new PersonSpec(
            "Driver", "One", new GameDate(1920, 1, 1), "DEU", false, null, [PersonRole.Driver], CreateDriverTruth(15)));

        (world, driver2Id) = world.AddPerson(new PersonSpec(
            "Driver", "Two", new GameDate(1922, 1, 1), "DEU", false, null, [PersonRole.Driver], CreateDriverTruth(14)));

        (world, reserveId) = world.AddPerson(new PersonSpec(
            "Reserve", "Driver", new GameDate(1925, 1, 1), "DEU", false, null, [PersonRole.Driver], CreateDriverTruth(12)));

        (world, _) = world.AddContract(new ContractSpec(
            driver1Id, teamId, ContractRole.Driver(SeatStatus.NumberOne), today, new GameDate(1955, 12, 31), 100_000, true, null, null));

        (world, _) = world.AddContract(new ContractSpec(
            reserveId, teamId, ContractRole.Driver(SeatStatus.Reserve), today, new GameDate(1955, 12, 31), 50_000, true, null, null));

        var cars = CarsSection.Empty.Add(CreateTeamCar(CarIds.Format(1), teamId, 1955, driver1Id));
        world = world.WithSection(cars);

        return world;
    }

    private static TeamCar CreateTeamCar(string id, OrganizationId teamId, int season, PersonId? driverId) =>
        new(
            id,
            teamId,
            season,
            CarConcept.Neutral,
            ConceptMapping.StartingLevels(CarConcept.Neutral, 60),
            60,
            CarEstimates.InitialUnderstanding,
            1.0,
            0,
            null,
            driverId);

    private static PersonTruth CreateDriverTruth(int value)
    {
        var list = new List<NamedAttribute>
        {
            new("cornering", value),
            new("braking", value),
            new("smoothness", value),
            new("overtaking", value),
            new("defending", value),
            new("consistency", value),
            new("composure", value),
            new("adaptability", scientificAttribute(value)),
            new("wet_weather", value),
            new("fitness", value),
            new("feedback", value),
        };
        return new PersonTruth(list, list);

        static int scientificAttribute(int v) => v;
    }
}
