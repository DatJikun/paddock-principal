using Paddock.Domain.Objectives;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;
using Paddock.Simulation.Objectives;
using Paddock.Simulation.Time;
using Paddock.Tests.Persistence;

namespace Paddock.Tests.Objectives;

/// <summary>
/// Objectives: the closed predicate set over registered facts, the day handler that emits met/failed,
/// and the pure forecast. The organizations, numbers and dates are SYNTHETIC fixtures, not historical or calibrated values.
/// </summary>
public class ObjectiveTests
{
    private static readonly OrganizationId Team = OrganizationId.Real("team_alpha");

    private static readonly OrganizationId Other = OrganizationId.Real("team_bravo");

    private static readonly OrganizationId Board = OrganizationId.Real("board_alpha");

    private static readonly GameDate Start = new(1955, 1, 1);

    private static readonly GameDate Deadline = new(1955, 12, 31);

    // --- Predicates over registered facts ---

    [Fact]
    public void EachPredicateReadsItsFactForTheOwner()
    {
        var facts = Facts(position: 3, podiums: 4, points: 12.5m, cash: 1_000, nationalities: ["GBR"], countries: ["ITA"]);

        Assert.True(new ChampionshipPositionAtMost(3).Evaluate(Team, facts));
        Assert.False(new ChampionshipPositionAtMost(2).Evaluate(Team, facts));
        Assert.True(new PodiumsAtLeast(4).Evaluate(Team, facts));
        Assert.False(new PodiumsAtLeast(5).Evaluate(Team, facts));
        Assert.True(new PointsAtLeast(12.5m).Evaluate(Team, facts));
        Assert.False(new PointsAtLeast(12.6m).Evaluate(Team, facts));
        Assert.True(new CashAtLeast(1_000).Evaluate(Team, facts));
        Assert.False(new CashAtLeast(1_001).Evaluate(Team, facts));
        Assert.True(new DriverNationalityInLineup("GBR").Evaluate(Team, facts));
        Assert.False(new DriverNationalityInLineup("FRA").Evaluate(Team, facts));
        Assert.True(new PersonFromCountryInLineup("ITA").Evaluate(Team, facts));
        Assert.False(new PersonFromCountryInLineup("GBR").Evaluate(Team, facts));
    }

    [Fact]
    public void AFactNobodySuppliedIsUnknownNotFalse()
    {
        var facts = new ObjectiveFactRegistry();

        Assert.Null(new ChampionshipPositionAtMost(3).Evaluate(Team, facts));
        Assert.Null(new DriverNationalityInLineup("GBR").Evaluate(Team, facts));
    }

    [Fact]
    public void FactsAreAboutOneOrganizationAndOtherSystemsRegisterThemOnce()
    {
        var registry = new ObjectiveFactRegistry();
        registry.RegisterNumber(ObjectiveFactKeys.Cash, owner => owner == Team ? 50m : null);

        Assert.Equal(50m, registry.Number(Team, ObjectiveFactKeys.Cash));
        Assert.Null(registry.Number(Other, ObjectiveFactKeys.Cash));
        Assert.Throws<InvalidOperationException>(() => registry.RegisterNumber(ObjectiveFactKeys.Cash, _ => 1m));
        Assert.Throws<InvalidOperationException>(() => registry.RegisterFlag(ObjectiveFactKeys.Cash, (_, _) => true));
    }

    [Fact]
    public void PredicatesRejectNonsenseTargets()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChampionshipPositionAtMost(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PodiumsAtLeast(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PointsAtLeast(0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CashAtLeast(-1));
        Assert.Throws<ArgumentException>(() => new DriverNationalityInLineup(" "));
        Assert.Throws<ArgumentException>(() => new PersonFromCountryInLineup(""));
    }

    [Fact]
    public void ADraftNeedsABaselineExactlyWhenThePredicateIsNumeric()
    {
        Assert.Throws<ArgumentException>(() => Draft(new PodiumsAtLeast(2), baseline: null));
        Assert.Throws<ArgumentException>(() => Draft(new DriverNationalityInLineup("GBR"), baseline: 1m));
        Assert.Throws<ArgumentException>(() => new ObjectiveDraft(
            default, Board, "k", "r", new DriverNationalityInLineup("GBR"), null, Deadline, Effect("m"), Effect("f")));
    }

    // --- The section: ids and hash ---

    [Fact]
    public void ObjectivesGetStableIdsAndAreHashedWithTheWorld()
    {
        var (section, first) = ObjectivesSection.Empty.Add(Draft(new PodiumsAtLeast(2), 0m), Start);
        (section, var second) = section.Add(Draft(new DriverNationalityInLineup("GBR"), null), Start);

        Assert.Equal(["obj:1", "obj:2"], new[] { first.Id, second.Id });
        Assert.Same(first, section.Find("obj:1"));
        Assert.Null(section.Find("obj:3"));
        Assert.Null(section.Find("obj:01"));

        var world = WorldState.At(Start);
        Assert.Equal(world.WithSection(section).StateHash(), WorldState.At(Start).WithSection(section).StateHash());
        Assert.NotEqual(world.StateHash(), world.WithSection(section).StateHash());
    }

    [Fact]
    public void TheHashSeesEveryFieldOfAnObjective()
    {
        string Hash(ObjectiveDraft draft) =>
            WorldState.At(Start).WithSection(ObjectivesSection.Empty.Add(draft, Start).Section).StateHash();

        var baseline = Hash(Draft(new PodiumsAtLeast(2), 0m));

        Assert.Equal(baseline, Hash(Draft(new PodiumsAtLeast(2), 0.0m)));
        Assert.NotEqual(baseline, Hash(Draft(new PodiumsAtLeast(3), 0m)));
        Assert.NotEqual(baseline, Hash(Draft(new PodiumsAtLeast(2), 1m)));
        Assert.NotEqual(baseline, Hash(Draft(new PointsAtLeast(2m), 0m)));
        Assert.NotEqual(baseline, Hash(Draft(new PodiumsAtLeast(2), 0m, owner: Other)));
        Assert.NotEqual(baseline, Hash(Draft(new PodiumsAtLeast(2), 0m, grantor: Other)));
        Assert.NotEqual(baseline, Hash(Draft(new PodiumsAtLeast(2), 0m, reason: "objective.test.other")));
        Assert.NotEqual(baseline, Hash(Draft(new PodiumsAtLeast(2), 0m, deadline: Deadline.AddDays(-1))));
        Assert.NotEqual(baseline, Hash(Draft(new PodiumsAtLeast(2), 0m, onMet: Effect("other"))));
        Assert.NotEqual(baseline, Hash(Draft(new PodiumsAtLeast(2), 0m, onFailed: new ObjectiveEffect("objective.test.failed", [new("size", "2")]))));
    }

    [Fact]
    public void ASettledObjectiveStaysAsARecordAndCannotBeSettledTwice()
    {
        var (section, objective) = ObjectivesSection.Empty.Add(Draft(new PodiumsAtLeast(2), 0m), Start);

        var settled = section.Settle(objective.Id, met: true, Deadline);

        Assert.Equal(ObjectiveStatus.Met, settled.Find(objective.Id)!.Status);
        Assert.Equal(Deadline, settled.Find(objective.Id)!.SettledOn);
        Assert.Equal(ObjectiveStatus.Open, section.Find(objective.Id)!.Status);
        Assert.Throws<InvalidOperationException>(() => settled.Settle(objective.Id, met: false, Deadline));
        Assert.Throws<InvalidOperationException>(() => section.Settle("obj:9", met: true, Deadline));
        Assert.NotEqual(
            WorldState.At(Start).WithSection(section).StateHash(),
            WorldState.At(Start).WithSection(settled).StateHash());
    }

    [Fact]
    public void AWorldHoldingObjectivesRoundTripsThroughTheSaveWithTheSameHash()
    {
        // The first system that grants objectives (T38 sponsors) added the store.
        var directory = Directory.CreateTempSubdirectory("paddock-objectives-").FullName;
        try
        {
            using var file = SaveFile.Create(Path.Combine(directory, "a.paddock"), WorldFixtures.Meta());
            var repository = new WorldRepository(file);
            var (section, _) = ObjectivesSection.Empty.Add(Draft(new PodiumsAtLeast(2), 0m), Start);
            (section, var second) = section.Add(Draft(new DriverNationalityInLineup("GBR"), null), Start);
            section = section.Settle(second.Id, true, Start);
            var world = WorldFixtures.Small().WithSection(section);

            repository.SaveWorld(world, WorldFixtures.Opening);
            var loaded = repository.LoadWorld();

            Assert.Equal(world.StateHash(), loaded.StateHash());
            Assert.Equal(2, loaded.Section<ObjectivesSection>(ObjectivesSection.SectionName)!.Objectives.Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // --- The day handler ---

    [Fact]
    public void AnObjectiveMetOnItsDeadlineEmitsMetAndAFailedOneEmitsFailed()
    {
        var (section, met) = ObjectivesSection.Empty.Add(Draft(new PodiumsAtLeast(2), 0m, deadline: new GameDate(1955, 6, 1)), Start);
        (section, var failed) = section.Add(Draft(new CashAtLeast(5_000), 100m, deadline: new GameDate(1955, 6, 1)), Start);
        var facts = Facts(podiums: 2, cash: 1_000);

        var events = LiveDay(section, facts, new GameDate(1955, 6, 1));

        Assert.Collection(
            events,
            first =>
            {
                Assert.Equal(ObjectiveEventTypes.Met, first.TypeId);
                var payload = Assert.IsType<ObjectiveOutcomePayload>(first.Payload);
                Assert.Equal(met.Id, payload.ObjectiveId);
                Assert.Equal(Team.Value, payload.OwnerId);
                Assert.Equal(Board.Value, payload.GrantorId);
                Assert.True(payload.Met);
                Assert.Equal(2m, payload.Value);
                Assert.Equal(new GameDate(1955, 6, 1), first.Date);
            },
            second =>
            {
                Assert.Equal(ObjectiveEventTypes.Failed, second.TypeId);
                var payload = Assert.IsType<ObjectiveOutcomePayload>(second.Payload);
                Assert.Equal(failed.Id, payload.ObjectiveId);
                Assert.False(payload.Met);
                Assert.Equal(1_000m, payload.Value);
            });
    }

    [Fact]
    public void NothingIsEmittedBeforeTheDeadline()
    {
        var (section, _) = ObjectivesSection.Empty.Add(Draft(new PodiumsAtLeast(2), 0m, deadline: new GameDate(1955, 6, 1)), Start);

        Assert.Empty(LiveDay(section, Facts(podiums: 9), new GameDate(1955, 5, 31)));
    }

    [Fact]
    public void AnUnknownFactAtTheDeadlineCountsAsNotMet()
    {
        var (section, _) = ObjectivesSection.Empty.Add(Draft(new PodiumsAtLeast(2), 0m, deadline: new GameDate(1955, 6, 1)), Start);

        var only = Assert.Single(LiveDay(section, new ObjectiveFactRegistry(), new GameDate(1955, 6, 1)));

        Assert.Equal(ObjectiveEventTypes.Failed, only.TypeId);
        Assert.Null(Assert.IsType<ObjectiveOutcomePayload>(only.Payload).Value);
    }

    [Fact]
    public void AnObjectiveStopsBeingDueOnceItsOutcomeIsApplied()
    {
        var (section, objective) = ObjectivesSection.Empty.Add(Draft(new PodiumsAtLeast(2), 0m, deadline: new GameDate(1955, 6, 1)), Start);
        var facts = Facts(podiums: 2);
        var handlers = new DayHandlerRegistry([new ObjectiveDayHandler(() => section, facts)]);
        var state = new WorldClockState(new GameDate(1955, 6, 1), 42UL);

        var day = WorldClock.AdvanceDay(state, handlers);
        Assert.Single(day.Events);
        section = ObjectiveOutcomes.Apply(section, day.Events);
        Assert.Equal(ObjectiveStatus.Met, section.Find(objective.Id)!.Status);

        Assert.Empty(WorldClock.AdvanceDay(day.State, handlers).Events);
        // Applying the same events again is harmless.
        Assert.Equal(
            WorldState.At(Start).WithSection(section).StateHash(),
            WorldState.At(Start).WithSection(ObjectiveOutcomes.Apply(section, day.Events)).StateHash());
    }

    [Fact]
    public void ADayThatWasMissedStillSettlesTheObjectiveWhenItIsLived()
    {
        var (section, _) = ObjectivesSection.Empty.Add(Draft(new PodiumsAtLeast(2), 0m, deadline: new GameDate(1955, 6, 1)), Start);

        var only = Assert.Single(LiveDay(section, Facts(podiums: 2), new GameDate(1955, 6, 9)));

        Assert.Equal(ObjectiveEventTypes.Met, only.TypeId);
    }

    [Fact]
    public void TheHandlerChangesNothingAndDrawsNoRandomNumbers()
    {
        var (section, _) = ObjectivesSection.Empty.Add(Draft(new PodiumsAtLeast(2), 0m, deadline: new GameDate(1955, 6, 1)), Start);
        var before = WorldState.At(Start).WithSection(section).StateHash();
        var handlers = new DayHandlerRegistry([new ObjectiveDayHandler(() => section, Facts(podiums: 2))]);

        var day = WorldClock.AdvanceDay(new WorldClockState(new GameDate(1955, 6, 1), 42UL), handlers);

        Assert.Equal(before, WorldState.At(Start).WithSection(section).StateHash());
        Assert.Empty(day.State.RngStates);
    }

    [Fact]
    public void TheSameDaysGiveTheSameEventsAndHash()
    {
        string Run()
        {
            var (section, _) = ObjectivesSection.Empty.Add(Draft(new PodiumsAtLeast(2), 0m, deadline: new GameDate(1955, 2, 1)), Start);
            (section, _) = section.Add(Draft(new DriverNationalityInLineup("GBR"), null, deadline: new GameDate(1955, 2, 3)), Start);
            var handlers = new DayHandlerRegistry([new ObjectiveDayHandler(() => section, Facts(podiums: 1, nationalities: ["GBR"]))]);
            var state = new WorldClockState(new GameDate(1955, 1, 30), 42UL);
            var log = new List<string>();
            for (var i = 0; i < 6; i++)
            {
                var day = WorldClock.AdvanceDay(state, handlers);
                state = day.State;
                log.AddRange(day.Events.Select(item => item.Id.Value + item.TypeId + item.Date));
                section = ObjectiveOutcomes.Apply(section, day.Events);
            }

            return string.Join('|', log) + WorldState.At(Start).WithSection(section).StateHash();
        }

        Assert.Equal(Run(), Run());
    }

    // --- Forecast ---

    [Fact]
    public void ForecastExtendsTheStraightLineFromTheBaselineToTheDeadline()
    {
        // 1 January to 31 December 1955 is 364 days; halfway is day 182.
        var objective = Objective(new PodiumsAtLeast(4), baseline: 0m);
        var halfway = Start.AddDays(182);

        var result = ObjectiveForecast.Project(objective, 2m, halfway);

        Assert.Equal(ForecastKind.OnTrack, result.Kind);
        Assert.Equal(4m, result.Projected);
        Assert.Equal(ForecastKind.OffTrack, ObjectiveForecast.Project(objective, 1m, halfway).Kind);
        Assert.Equal(2m, ObjectiveForecast.Project(objective, 1m, halfway).Projected);
    }

    [Fact]
    public void ForecastHandlesAPositionWhereLowerIsBetterAndNeverGoesBelowFirst()
    {
        var objective = Objective(new ChampionshipPositionAtMost(3), baseline: 10m);
        var halfway = Start.AddDays(182);

        var improving = ObjectiveForecast.Project(objective, 6m, halfway);
        Assert.Equal(2m, improving.Projected);
        Assert.Equal(ForecastKind.OnTrack, improving.Kind);

        var surging = ObjectiveForecast.Project(objective, 1m, halfway);
        Assert.Equal(1m, surging.Projected);

        var sliding = ObjectiveForecast.Project(objective, 12m, halfway);
        Assert.Equal(ForecastKind.OffTrack, sliding.Kind);
        Assert.Equal(14m, sliding.Projected);
    }

    [Fact]
    public void ForecastOnTheFirstDayIsTheCurrentValueAndAfterTheDeadlineItDoesNotExtrapolate()
    {
        var objective = Objective(new PointsAtLeast(10m), baseline: 0m);

        Assert.Equal(3m, ObjectiveForecast.Project(objective, 3m, Start).Projected);
        Assert.Equal(7m, ObjectiveForecast.Project(objective, 7m, Deadline.AddDays(30)).Projected);
        Assert.Equal(ForecastKind.OffTrack, ObjectiveForecast.Project(objective, 7m, Deadline.AddDays(30)).Kind);
    }

    [Fact]
    public void ForecastIsUnknownWithoutAFigureAndNotProjectableForALineupObjective()
    {
        var numeric = Objective(new CashAtLeast(100), baseline: 50m);
        var lineup = Objective(new DriverNationalityInLineup("GBR"), baseline: null);

        Assert.Equal(new ForecastResult(ForecastKind.Unknown, null), ObjectiveForecast.Project(numeric, null, Start.AddDays(10)));
        Assert.Equal(new ForecastResult(ForecastKind.NotProjectable, null), ObjectiveForecast.Project(lineup, null, Start.AddDays(10)));
    }

    [Fact]
    public void ForecastIsAPureFunction()
    {
        var objective = Objective(new PodiumsAtLeast(4), baseline: 0m);
        var day = Start.AddDays(100);

        Assert.Equal(ObjectiveForecast.Project(objective, 2m, day), ObjectiveForecast.Project(objective, 2m, day));
    }

    // --- Helpers ---

    private static ObjectiveEffect Effect(string name) => new("objective.test." + name);

    private static ObjectiveDraft Draft(
        ObjectivePredicate predicate,
        decimal? baseline,
        OrganizationId? owner = null,
        OrganizationId? grantor = null,
        string reason = "objective.test.reason",
        GameDate? deadline = null,
        ObjectiveEffect? onMet = null,
        ObjectiveEffect? onFailed = null) => new(
            owner ?? Team,
            grantor ?? Board,
            "objective.test.kind",
            reason,
            predicate,
            baseline,
            deadline ?? Deadline,
            onMet ?? Effect("met"),
            onFailed ?? Effect("failed"));

    private static Objective Objective(ObjectivePredicate predicate, decimal? baseline) =>
        ObjectivesSection.Empty.Add(Draft(predicate, baseline), Start).Objective;

    private static ObjectiveFactRegistry Facts(
        decimal? position = null,
        decimal? podiums = null,
        decimal? points = null,
        decimal? cash = null,
        string[]? nationalities = null,
        string[]? countries = null)
    {
        var registry = new ObjectiveFactRegistry();
        registry.RegisterNumber(ObjectiveFactKeys.ChampionshipPosition, owner => owner == Team ? position : null);
        registry.RegisterNumber(ObjectiveFactKeys.SeasonPodiums, owner => owner == Team ? podiums : null);
        registry.RegisterNumber(ObjectiveFactKeys.SeasonPoints, owner => owner == Team ? points : null);
        registry.RegisterNumber(ObjectiveFactKeys.Cash, owner => owner == Team ? cash : null);
        registry.RegisterFlag(ObjectiveFactKeys.LineupDriverNationality, (owner, value) => owner == Team ? nationalities?.Contains(value) ?? false : null);
        registry.RegisterFlag(ObjectiveFactKeys.LineupPersonCountry, (owner, value) => owner == Team ? countries?.Contains(value) ?? false : null);
        return registry;
    }

    private static IReadOnlyList<DomainEvent> LiveDay(ObjectivesSection section, IObjectiveFacts facts, GameDate day)
    {
        var handlers = new DayHandlerRegistry([new ObjectiveDayHandler(() => section, facts)]);
        return WorldClock.AdvanceDay(new WorldClockState(day, 42UL), handlers).Events;
    }
}
