using Paddock.Application.Board;
using Paddock.Application.Commands;
using Paddock.Application.Inbox;
using Paddock.Domain.Board;
using Paddock.Domain.Inbox;
using Paddock.Domain.Principals;
using Paddock.Domain.Spy;
using Paddock.Domain.Objectives;
using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Objectives;
using static Paddock.Tests.Board.BoardKit;

namespace Paddock.Tests.Board;

/// <summary>
/// The rules of reputation, board confidence, objectives and dismissal. The fixtures are SYNTHETIC (see <see cref="BoardKit"/>) and
/// every number the tests expect is an uncalibrated ESTIMATE, written out so a change of an estimate shows up as a failing test.
/// </summary>
public class BoardRulesTests
{
    // --- The pure model ---

    [Fact]
    public void ANewPrincipalIsProtectedForTheFirstFullSeasonAndLongerWithReputation()
    {
        // Appointed on the first day of a season: that season is the first full one.
        Assert.Equal(new GameDate(1955, 12, 31), ReputationModel.ProtectedUntil(new GameDate(1955, 1, 1), 0));

        // Appointed in the spring: the first FULL season is the next one (a new manager in 1955 is not sacked in spring).
        Assert.Equal(new GameDate(1956, 12, 31), ReputationModel.ProtectedUntil(new GameDate(1955, 3, 20), 0));

        // 3 days of extra protection per reputation point: 30 points is 90 days, 100 points is 300.
        Assert.Equal(new GameDate(1955, 12, 31).AddDays(90), ReputationModel.ProtectedUntil(new GameDate(1955, 1, 1), 300));
        Assert.Equal(new GameDate(1955, 12, 31).AddDays(300), ReputationModel.ProtectedUntil(new GameDate(1955, 1, 1), 1000));
        Assert.True(
            ReputationModel.ProtectedUntil(new GameDate(1955, 1, 1), 800) > ReputationModel.ProtectedUntil(new GameDate(1955, 1, 1), 200));
    }

    [Fact]
    public void ConfidenceHeadsForATargetThatDependsOnThePositionAgainstTheExpectation()
    {
        Assert.Equal(550, ReputationModel.TargetConfidenceTenths(0.0));
        Assert.Equal(1000, ReputationModel.TargetConfidenceTenths(1.0));
        Assert.Equal(100, ReputationModel.TargetConfidenceTenths(-1.0));
        Assert.Equal(-0.5, ReputationModel.Margin(3, 5, 5));
        Assert.Equal(1.0, ReputationModel.Margin(5, 1, 5));
        Assert.Equal(0.0, ReputationModel.Margin(1, 1, 1));

        // One review closes a quarter of the gap: 600 towards 325 is 531.
        Assert.Equal(531, ReputationModel.ReviewConfidence(600, 325, 0));
        Assert.Equal(501, ReputationModel.ReviewConfidence(600, 325, 30));
        Assert.Equal(0, ReputationModel.ReviewConfidence(10, 0, 30));
    }

    [Fact]
    public void CashHurtsConfidenceOnlyWhenKnownAndBadOrFalling()
    {
        Assert.Equal(0, ReputationModel.CashPenaltyTenths(null, 100));
        Assert.Equal(BoardEstimates.NegativeCashPenaltyTenths, ReputationModel.CashPenaltyTenths(-1, 100));
        Assert.Equal(BoardEstimates.FallingCashPenaltyTenths, ReputationModel.CashPenaltyTenths(70, 100));
        Assert.Equal(0, ReputationModel.CashPenaltyTenths(80, 100));
        Assert.Equal(0, ReputationModel.CashPenaltyTenths(50, null));
    }

    [Fact]
    public void APatientBoardWaitsLongerAndHasALowerBar()
    {
        Assert.True(ReputationModel.DismissThresholdTenths(100) < ReputationModel.DismissThresholdTenths(50));
        Assert.True(ReputationModel.DismissThresholdTenths(50) < ReputationModel.DismissThresholdTenths(0));
        Assert.Equal(400, ReputationModel.DismissThresholdTenths(50));
        Assert.Equal(4, ReputationModel.ReviewsToDismiss(0));
        Assert.Equal(8, ReputationModel.ReviewsToDismiss(100));
        Assert.Equal(45, ReputationModel.PatienceFor(new GameDate(1950, 1, 1), new GameDate(1955, 1, 1)));
        Assert.Equal(80, ReputationModel.PatienceFor(new GameDate(1850, 1, 1), new GameDate(1955, 1, 1)));
    }

    [Fact]
    public void TheExpectedPositionBlendsLastSeasonWithTheBudgetRank()
    {
        Assert.Equal(5, ReputationModel.ExpectedPosition(null, 5, 5));
        Assert.Equal(3, ReputationModel.ExpectedPosition(1, 5, 5));
        Assert.Equal(1, ReputationModel.ExpectedPosition(1, 1, 5));
        Assert.Equal(5, ReputationModel.ExpectedPosition(9, 9, 5));
    }

    [Fact]
    public void SeasonReputationAddsTheMarginTheTitleDebtAndPeopleDevelopment()
    {
        // Margin (5-1)/4 = 1 gives the full 80 tenths, and the title adds 60.
        Assert.Equal(140, ReputationModel.SeasonReputationTenths(5, 1, 5, false, null));
        Assert.Equal(-40, ReputationModel.SeasonReputationTenths(3, 3, 5, true, null));
        Assert.Equal(-40, ReputationModel.SeasonReputationTenths(3, 3, 5, true, 0m));
        Assert.Equal(20, ReputationModel.SeasonReputationTenths(3, 3, 5, false, 2m));
        Assert.Equal(30, ReputationModel.SeasonReputationTenths(3, 3, 5, false, 9m));
        Assert.Equal(-80 * 3 / 4, ReputationModel.SeasonReputationTenths(1, 4, 5, false, null));
    }

    [Fact]
    public void ReputationShiftsPrestigeOnlyAwayFromTheMiddle()
    {
        Assert.Equal(0.0, ReputationModel.PrestigeShift(50.0));
        Assert.Equal(0.15, ReputationModel.PrestigeShift(100.0), 10);
        Assert.Equal(-0.15, ReputationModel.PrestigeShift(0.0), 10);
        Assert.True(ReputationModel.RequiredReputationTenths(1.0) > ReputationModel.RequiredReputationTenths(0.0));
        Assert.Equal(800, ReputationModel.RequiredReputationTenths(1.0));
    }

    // --- The board of each team and its objectives ---

    [Fact]
    public void EveryTeamGetsABoardWithItsPrincipalAndAPatienceFromItsAge()
    {
        var lab = new Lab(noRaces: true);

        lab.Advance(1);

        Assert.Equal(5, lab.Section.Boards.Count);
        foreach (var board in lab.Section.Boards)
        {
            Assert.Equal(45, board.Patience);
            Assert.NotNull(board.Principal);
        }

        var first = lab.Section.Board(T1)!;
        Assert.Equal(PrincipalKind.Ai, first.Principal!.Kind);
        Assert.Equal(Principal(1).Value, first.Principal.Subject);
        Assert.Equal(new GameDate(1950, 1, 1), first.Principal.ProtectedUntil);

        // Teams 4 and 5 had nobody, so free principals were hired for them.
        Assert.All(new[] { T4, T5 }, team => Assert.Equal(PrincipalKind.Ai, lab.Section.Board(team)!.Principal!.Kind));
    }

    [Fact]
    public void TheSeasonAndMultiYearObjectivesComeFromPublicFactsAndStateTheirStakes()
    {
        var history = new FakeHistory();
        history.Set(T5, 1954, 1);
        var lab = new Lab(history: history, noRaces: true);

        lab.Advance(1);

        // Budget rank alone for teams 1 to 4; for team 5 the last position (1) is blended in: round(0.6 * 1 + 0.4 * 5) = 3.
        Assert.Equal([1, 2, 3, 4, 3], Teams.Select(team => lab.Section.Board(team)!.ExpectedPosition));
        var season = lab.Board.Objectives.OwnedBy(T5).Single(objective => objective.KindKey == BoardKeys.ObjectiveSeason);
        Assert.Equal(3, ((ChampionshipPositionAtMost)season.Predicate).Position);
        Assert.Equal(new GameDate(1955, 12, 31), season.Deadline);
        Assert.Equal(T5, season.Owner);
        Assert.Equal(BoardKeys.EffectConfidenceUp, season.EffectOnMet.Key);
        Assert.Equal("15", season.EffectOnMet.Arguments["points"]);
        Assert.Equal(BoardKeys.EffectConfidenceDown, season.EffectOnFailed.Key);
        Assert.Equal("-150", season.EffectOnFailed.Arguments["tenths"]);

        var multi = lab.Board.Objectives.OwnedBy(T5).Single(objective => objective.KindKey == BoardKeys.ObjectiveMultiYear);
        Assert.Equal(1, ((ChampionshipPositionAtMost)multi.Predicate).Position);
        Assert.Equal(new GameDate(1957, 12, 31), multi.Deadline);
        Assert.Equal(10, lab.Board.Objectives.Objectives.Count);
    }

    [Fact]
    public void ObjectivesAreNotGrantedTwiceWhileTheyAreOpen()
    {
        var lab = new Lab(noRaces: true);

        lab.Advance(40);

        Assert.Equal(10, lab.Board.Objectives.Objectives.Count);
    }

    [Fact]
    public void SettledObjectivesMoveTheBoardsConfidenceByTheStatedAmount()
    {
        var lab = new Lab(noRaces: true);
        lab.Facts.Position(T1, 1);
        lab.Facts.Position(T3, 5);
        lab.AdvanceTo(new GameDate(1955, 12, 31));
        var before1 = lab.Section.Board(T1)!.ConfidenceTenths;
        var before3 = lab.Section.Board(T3)!.ConfidenceTenths;

        lab.Advance(1);

        Assert.Equal(Math.Min(1000, before1 + 150), lab.Section.Board(T1)!.ConfidenceTenths);
        Assert.Equal(before3 - 150, lab.Section.Board(T3)!.ConfidenceTenths);
        var met = lab.Board.Objectives.OwnedBy(T1).First(objective => objective.KindKey == BoardKeys.ObjectiveSeason);
        var failed = lab.Board.Objectives.OwnedBy(T3).First(objective => objective.KindKey == BoardKeys.ObjectiveSeason);
        Assert.Equal(ObjectiveStatus.Met, met.Status);
        Assert.Equal(ObjectiveStatus.Failed, failed.Status);
        Assert.Equal(5, lab.Events.Count(e => e.TypeId is ObjectiveEventTypes.Met or ObjectiveEventTypes.Failed && e.Date == new GameDate(1955, 12, 31)));

        // The next season asks again: new objectives come on 1 January, the settled ones stay as records.
        lab.Advance(2);
        Assert.Equal(2, lab.Board.Objectives.OwnedBy(T3).Count(objective => objective.KindKey == BoardKeys.ObjectiveSeason));
    }

    // --- Reviews ---

    [Fact]
    public void ARaceMovesConfidenceAQuarterOfTheWayToWhatThePositionDeserves()
    {
        var lab = new Lab();
        lab.Appoint(Pam, T3);
        lab.Facts.Position(T3, 5);

        lab.AdvanceTo(new GameDate(1955, 3, 2));

        // Expected 3 (budget rank), actual 5 of 5: margin -0.5, target 325, so 600 becomes 531.
        var board = lab.Section.Board(T3)!;
        Assert.Equal(531, board.ConfidenceTenths);
        Assert.Equal(325, board.LastTargetTenths);
        Assert.Equal(new GameDate(1955, 3, 1), board.LastReview);

        // Teams whose position nobody supplied are not judged.
        Assert.Equal(600, lab.Section.Board(T1)!.ConfidenceTenths);
    }

    [Fact]
    public void NegativeCashCostsConfidenceAtEveryReview()
    {
        var lab = new Lab();
        lab.Appoint(Pam, T3);
        lab.Facts.Position(T3, 3);
        lab.Facts.Cash(T3, -5);

        lab.AdvanceTo(new GameDate(1955, 3, 2));

        // At expectation the target is 550: 600 -> 588 (12.5 rounds away from zero to 13), minus 30.
        Assert.Equal(600 - 13 - 30, lab.Section.Board(T3)!.ConfidenceTenths);
    }

    [Fact]
    public void ANewPrincipalIsNotDismissedInTheFirstSeasonWhateverTheResults()
    {
        var lab = new Lab();
        lab.Appoint(Pam, T3);
        lab.Facts.Position(T3, 5);

        lab.AdvanceTo(new GameDate(1956, 1, 1));

        var board = lab.Section.Board(T3)!;
        Assert.Equal(PrincipalKind.Human, board.Principal!.Kind);
        Assert.Equal(Pam.Value, board.Principal.Subject);
        Assert.Equal(0, board.LowStreak);
        Assert.True(board.ConfidenceTenths < ReputationModel.DismissThresholdTenths(board.Patience));
        Assert.Empty(lab.EventsOf(BoardEventTypes.ManagerDismissed));
        Assert.Equal(new GameDate(1955, 12, 31).AddDays(90), board.Principal.ProtectedUntil);
    }

    [Fact]
    public void ABoardDismissesAnUnprotectedAiPrincipalAfterEnoughBadReviewsAndReplacesThem()
    {
        var lab = new Lab(free: 5);
        lab.Facts.Position(T2, 5);

        lab.AdvanceTo(new GameDate(1955, 6, 1));

        // Expected 2, actual 5: target 213. Confidence 600, 503, 430, 376, 335, 304, 281, 264; the bar is 410 and five
        // reviews below it in a row dismiss, so the seventh race (24 May) does it.
        var dismissed = Assert.Single(lab.EventsOf(BoardEventTypes.ManagerDismissed));
        Assert.Equal(new GameDate(1955, 5, 24), dismissed.Date);
        var payload = Assert.IsType<BoardFactPayload>(dismissed.Payload);
        Assert.Equal(T2.Value, payload.Organization);
        Assert.Equal(Principal(2).Value, payload.Subject);
        Assert.Equal("Ai", payload.Kind);

        var board = lab.Section.Board(T2)!;
        Assert.Equal(PrincipalKind.Ai, board.Principal!.Kind);
        Assert.NotEqual(Principal(2).Value, board.Principal.Subject);
        Assert.Equal(new GameDate(1955, 5, 24), board.Principal.Since);
        Assert.Equal(BoardEstimates.InitialConfidenceTenths, board.ConfidenceTenths);
        Assert.Equal(0, board.LowStreak);

        // The old principal's contract ended the day before; the new one has one that started today.
        var old = lab.World.Contracts.Single(contract => contract.PersonId == Principal(2));
        Assert.Equal(new GameDate(1955, 5, 23), old.End);
        var hired = lab.World.Contracts.Single(contract => contract.OrganizationId == T2 && contract.Start == new GameDate(1955, 5, 24));
        Assert.Equal(board.Principal.Subject, hired.PersonId.Value);
        Assert.Equal(StaffRole.TeamPrincipal, hired.Role.StaffRole);

        // The team visibly changes direction: the archetype was rerolled to a different one.
        Assert.NotEqual(BoardEngine.DefaultArchetype, board.Archetype);

        // The dismissed principal's reputation took the blow; the new one is protected like any new principal.
        Assert.Equal(300 - BoardEstimates.DismissalTenths, lab.Section.ReputationTenths(Principal(2).Value));
        Assert.True(board.Principal.ProtectedUntil >= new GameDate(1956, 12, 31));
        Assert.Single(lab.EventsOf(BoardEventTypes.ManagerAppointed), appointed =>
            appointed.Payload is BoardFactPayload { Organization: "fixture_team_2", Archetype: not "" } && appointed.Date == new GameDate(1955, 5, 24));
    }

    [Fact]
    public void TheFounderOfATeamIsNeverDismissed()
    {
        var lab = new Lab();
        lab.Appoint(Pam, T3, founder: true);
        lab.Facts.AllPositions(5);

        lab.AdvanceTo(new GameDate(1958, 1, 1));

        var board = lab.Section.Board(T3)!;
        Assert.Equal(Pam.Value, board.Principal!.Subject);
        Assert.True(board.Principal.Founder);
        Assert.DoesNotContain(lab.Events, e => e.TypeId == BoardEventTypes.ManagerDismissed && e.Payload is BoardFactPayload { Subject: "mgr-pam" });
        Assert.True(board.ConfidenceTenths < ReputationModel.DismissThresholdTenths(board.Patience));
        Assert.Equal(0, board.LowStreak);
    }

    [Fact]
    public void ThePrincipalOfAVacantTeamIsHiredFromTheFreeOnesWhenOneAppears()
    {
        var lab = new Lab(free: 0, noRaces: true);

        lab.Advance(1);

        Assert.Null(lab.Section.Board(T4)!.Principal);
        Assert.Null(lab.Section.Board(T5)!.Principal);
        Assert.Empty(lab.EventsOf(BoardEventTypes.ManagerAppointed));
    }

    // --- Reputation at a season end ---

    [Fact]
    public void ASeasonEndChangesThePrincipalsReputationAndKeepsTheHistory()
    {
        var lab = new Lab(noRaces: true);
        lab.Facts.Position(T1, 1);
        lab.Facts.Position(T2, 2);
        lab.Facts.Cash(T2, -1);
        lab.Facts.Position(T3, 3);
        lab.Facts.People(T3, 2m);

        lab.AdvanceTo(new GameDate(1956, 1, 1));

        // Expected 1 and champion: title bonus 60. Expected 2, met, in debt: -40. Expected 3, met, people development 2: +20.
        Assert.Equal(300 + 60, lab.Section.ReputationTenths(Principal(1).Value));
        Assert.Equal(300 - 40, lab.Section.ReputationTenths(Principal(2).Value));
        Assert.Equal(300 + 20, lab.Section.ReputationTenths(Principal(3).Value));
        var line = Assert.Single(lab.Section.ChangesOf(Principal(1).Value));
        Assert.Equal(new GameDate(1955, 12, 31), line.On);
        Assert.Equal(60, line.DeltaTenths);
        Assert.Equal(360, line.ResultTenths);
        Assert.Equal(BoardKeys.ReputationSeason, line.ReasonKey);

        // Teams without a standing are not judged.
        Assert.Empty(lab.Section.ChangesOf(Principal(4).Value));
    }

    [Fact]
    public void ReputationNeverLeavesTheScale()
    {
        var section = BoardSection.Empty;
        section = section.WithReputationChange("m", Start, 5_000, "k");
        Assert.Equal(1000, section.ReputationTenths("m"));
        section = section.WithReputationChange("m", Start, -5_000, "k");
        Assert.Equal(0, section.ReputationTenths("m"));
        Assert.Equal(BoardEstimates.InitialReputationTenths, section.ReputationTenths("unknown"));
    }

    [Fact]
    public void SeasonTargetRewardsAndPenaltiesRiseFromSafeToAmbitious()
    {
        Assert.True(SeasonTarget.RewardTenths(SeasonAmbition.Safe) < SeasonTarget.RewardTenths(SeasonAmbition.Expected));
        Assert.True(SeasonTarget.RewardTenths(SeasonAmbition.Expected) < SeasonTarget.RewardTenths(SeasonAmbition.Ambitious));
        Assert.True(SeasonTarget.PenaltyTenths(SeasonAmbition.Safe) < SeasonTarget.PenaltyTenths(SeasonAmbition.Expected));
        Assert.True(SeasonTarget.PenaltyTenths(SeasonAmbition.Expected) < SeasonTarget.PenaltyTenths(SeasonAmbition.Ambitious));
        Assert.True(SeasonTarget.Position(3, 5, SeasonAmbition.Safe) > SeasonTarget.Position(3, 5, SeasonAmbition.Expected));
        Assert.True(SeasonTarget.Position(3, 5, SeasonAmbition.Expected) > SeasonTarget.Position(3, 5, SeasonAmbition.Ambitious));
        Assert.Equal(SeasonAmbition.Ambitious, SeasonTarget.ForArchetype("Contender"));
        Assert.Equal(SeasonAmbition.Ambitious, SeasonTarget.ForArchetype("Pretender"));
        Assert.Equal(SeasonAmbition.Safe, SeasonTarget.ForArchetype("Survivor"));
        Assert.Equal(SeasonAmbition.Expected, SeasonTarget.ForArchetype("Builder"));
        Assert.Equal(SeasonAmbition.Expected, SeasonTarget.ForArchetype(null));
    }

    [Fact]
    public void ATeamExpectedToLeadGetsThreeDifferentTargets_AndNeverTwoIdenticalOnes()
    {
        // #264: expected P1 used to give safe P3, expected P1 and ambitious P1 (clamped), two of them identical.
        Assert.Equal(2, SeasonTarget.Position(1, 10, SeasonAmbition.Safe));
        Assert.Equal(1, SeasonTarget.Position(1, 10, SeasonAmbition.Expected));
        Assert.Equal(1, SeasonTarget.Position(1, 10, SeasonAmbition.Ambitious));
        Assert.Equal(0, SeasonTarget.WinsRequired(1, 10, 7, SeasonAmbition.Safe));
        Assert.Equal(0, SeasonTarget.WinsRequired(1, 10, 7, SeasonAmbition.Expected));
        var wins = SeasonTarget.WinsRequired(1, 10, 7, SeasonAmbition.Ambitious);
        Assert.InRange(wins, 1, 7);
        Assert.Equal(3, wins);

        // A team that can still aim higher than expected needs no wins condition.
        Assert.Equal(0, SeasonTarget.WinsRequired(3, 10, 7, SeasonAmbition.Ambitious));
        // The wins count follows the public season length, with a stated fallback when the calendar is not stored yet.
        Assert.Equal(4, SeasonTarget.WinsRequired(1, 10, 0, SeasonAmbition.Ambitious));
    }

    [Fact]
    public void APositionWithWinsObjectiveNeedsBothAndSurvivesTheSaveRoundTrip()
    {
        var facts = new ObjectiveFactRegistry();
        decimal position = 1m;
        decimal wins = 2m;
        facts.RegisterNumber(ObjectiveFactKeys.ChampionshipPosition, _ => position);
        facts.RegisterNumber(ObjectiveFactKeys.SeasonWins, _ => wins);
        var predicate = new ChampionshipPositionWithWins(1, 3);
        Assert.False(predicate.Evaluate(T1, facts));
        wins = 3m;
        Assert.True(predicate.Evaluate(T1, facts));
        position = 2m;
        Assert.False(predicate.Evaluate(T1, facts));

        var again = ObjectivePredicate.FromParts(predicate.Name, predicate.Parameter);
        Assert.Equal(predicate, again);
    }

    [Fact]
    public void AHumanOfAFavouriteGetsADistinctAmbitiousOptionThatAsksForWins()
    {
        var lab = new Lab(noRaces: true);
        lab.Appoint(Pam, T1);
        lab.Advance(1);

        var decision = Assert.Single(lab.Inbox.Section.ItemsOf(Pam.Value), item => item.Kind == BoardEngine.SeasonTargetKind && item.IsOpen);
        Assert.Equal("1", decision.Arguments["expectedTarget"]);
        Assert.Equal("2", decision.Arguments["safeTarget"]);
        Assert.Equal("1", decision.Arguments["ambitiousTarget"]);
        Assert.True(int.Parse(decision.Arguments["ambitiousWins"], System.Globalization.CultureInfo.InvariantCulture) > 0);
        Assert.Equal(BoardKeys.SeasonTargetWinsSubject, decision.SubjectKey);
        Assert.IsType<CommandResult.Accepted>(lab.Submit(new ResolveInboxItemCommand
        {
            ManagerId = Pam,
            IssuedOn = Date(lab.Today),
            ItemId = decision.Id,
            OptionId = SeasonTarget.Ambitious,
        }));
        var season = lab.Board.Objectives.OwnedBy(T1).Single(objective => objective.IsOpen && objective.KindKey == BoardKeys.ObjectiveSeason);
        var asked = Assert.IsType<ChampionshipPositionWithWins>(season.Predicate);
        Assert.Equal(1, asked.Position);
        Assert.Equal(int.Parse(decision.Arguments["ambitiousWins"], System.Globalization.CultureInfo.InvariantCulture), asked.Wins);
        Assert.Equal("1", season.EffectOnFailed.Arguments["dismiss"]);
    }

    [Fact]
    public void AHumanIsAskedBeforeTheSeasonAndAnAmbitiousFailureDismissesOnlyWithoutProtection()
    {
        var lab = new Lab(noRaces: true);
        lab.Appoint(Pam, T3);
        lab.Advance(1);

        var decision = Assert.Single(lab.Inbox.Section.ItemsOf(Pam.Value), item => item.Kind == BoardEngine.SeasonTargetKind && item.IsOpen);
        Assert.Equal(SeasonTarget.Expected, decision.DefaultOptionId);
        Assert.Equal("5", decision.Arguments["safeTarget"]);
        Assert.Equal("3", decision.Arguments["expectedTarget"]);
        Assert.Equal("1", decision.Arguments["ambitiousTarget"]);
        Assert.DoesNotContain(lab.Board.Objectives.OwnedBy(T3), objective => objective.IsOpen && objective.KindKey == BoardKeys.ObjectiveSeason);

        Assert.IsType<CommandResult.Accepted>(lab.Submit(new ResolveInboxItemCommand
        {
            ManagerId = Pam,
            IssuedOn = Date(lab.Today),
            ItemId = decision.Id,
            OptionId = SeasonTarget.Ambitious,
        }));
        var season = lab.Board.Objectives.OwnedBy(T3).Single(objective => objective.IsOpen && objective.KindKey == BoardKeys.ObjectiveSeason);
        Assert.Equal(1, ((ChampionshipPositionAtMost)season.Predicate).Position);
        Assert.Equal("30", season.EffectOnMet.Arguments["points"]);
        Assert.Equal("-400", season.EffectOnFailed.Arguments["tenths"]);
        Assert.Equal("1", season.EffectOnFailed.Arguments["dismiss"]);

        lab.Facts.Position(T3, 5);
        lab.AdvanceTo(new GameDate(1955, 12, 31));
        lab.Advance(1);
        Assert.Equal(Pam.Value, lab.Section.Board(T3)!.Principal!.Subject);

        var sacked = new Lab(noRaces: true);
        sacked.Appoint(Pam, T3);
        sacked.Advance(1);
        var again = Assert.Single(sacked.Inbox.Section.ItemsOf(Pam.Value), item => item.Kind == BoardEngine.SeasonTargetKind && item.IsOpen);
        Assert.IsType<CommandResult.Accepted>(sacked.Submit(new ResolveInboxItemCommand
        {
            ManagerId = Pam,
            IssuedOn = Date(sacked.Today),
            ItemId = again.Id,
            OptionId = SeasonTarget.Ambitious,
        }));
        var board = sacked.Section.Board(T3)!;
        sacked.Board.Update(sacked.Section.WithBoard(board with { Principal = board.Principal! with { ProtectedUntil = new GameDate(1955, 1, 2) } }));
        sacked.Facts.Position(T3, 5);
        sacked.AdvanceTo(new GameDate(1955, 12, 31));
        sacked.Advance(1);
        Assert.NotEqual(Pam.Value, sacked.Section.Board(T3)!.Principal!.Subject);
        Assert.NotNull(sacked.Section.UnemployedManager(Pam.Value));
    }

    [Fact]
    public void ALapsedSeasonTargetBecomesTheExpectedFinish()
    {
        var lab = new Lab(noRaces: true);
        lab.Appoint(Pam, T3);
        lab.Advance(1);
        lab.AdvanceTo(new GameDate(1955, 1, 16));

        var decision = lab.Inbox.Section.ItemsOf(Pam.Value).Single(item => item.Kind == BoardEngine.SeasonTargetKind);
        Assert.Equal(InboxStatus.Expired, decision.Status);
        Assert.Equal(SeasonTarget.Expected, decision.ChosenOptionId);
        var season = lab.Board.Objectives.OwnedBy(T3).Single(objective => objective.KindKey == BoardKeys.ObjectiveSeason);
        Assert.Equal(3, ((ChampionshipPositionAtMost)season.Predicate).Position);
        Assert.Equal("15", season.EffectOnMet.Arguments["points"]);
        Assert.Equal("0", season.EffectOnFailed.Arguments["dismiss"]);
    }

    [Fact]
    public void AnAiContenderTakesTheAmbitiousTargetAndTheChoiceIsInTheTrace()
    {
        var sink = new MemoryOfTraces();
        var lab = new Lab(trace: sink, noRaces: true);
        lab.Contracts.UseWorld(lab.World.WithSection(PrincipalsSection.Empty.With(
            new AiPrincipalRecord(T3, "Contender", null, Start, null, Start, 0, 0, ""))));
        lab.Advance(1);

        var season = lab.Board.Objectives.OwnedBy(T3).Single(objective => objective.KindKey == BoardKeys.ObjectiveSeason);
        Assert.Equal(1, ((ChampionshipPositionAtMost)season.Predicate).Position);
        Assert.Equal(SeasonTarget.Ambitious, season.EffectOnMet.Arguments["ambition"]);
        var trace = Assert.Single(sink.Traces, item => item.Trigger == "board.seasonTarget:" + T3.Value);
        Assert.Equal(SeasonTarget.Ambitious, trace.ChosenOptionId);
    }

    private sealed class MemoryOfTraces : ITraceSink
    {
        public bool IsEnabled => true;

        public List<DecisionTrace> Traces { get; } = [];

        public void Record(DecisionTrace trace) => Traces.Add(trace);
    }
}
