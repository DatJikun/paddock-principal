using Paddock.Application.Access;
using Paddock.Application.Board;
using Paddock.Application.Commands;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Domain.Board;
using Paddock.Domain.Contracts;
using Paddock.Domain.Inbox;
using Paddock.Domain.People;
using Paddock.Domain.Random;
using Paddock.Domain.Spy;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Time;
using static Paddock.Tests.Board.BoardKit;
using AccessId = Paddock.Application.Access.ManagerId;

namespace Paddock.Tests.Board;

/// <summary>
/// The player's career through the board: dismissal, the neutral observer, the job market, resignation, and the hook that lets a
/// principal's reputation move a negotiation. Fixtures are SYNTHETIC (see <see cref="BoardKit"/>), every threshold is an ESTIMATE.
/// </summary>
public class BoardCareerTests
{
    private static readonly GameDate Dec1955 = new(1955, 12, 31);

    private sealed class ListSink : ITraceSink
    {
        public List<DecisionTrace> Traces { get; } = [];

        public bool IsEnabled => true;

        public void Record(DecisionTrace trace) => Traces.Add(trace);
    }

    private static Lab DismissedPlayerLab(ulong seed = 42UL, ITraceSink? trace = null)
    {
        var lab = new Lab(seed, free: 5, trace: trace);
        lab.Appoint(Pam, T3);
        lab.Facts.Position(T3, 5);
        lab.AdvanceTo(new GameDate(1956, 6, 8));
        return lab;
    }

    // --- A fixed scenario: poor results dismiss the player, the market delivers an offer, the career continues ---

    [Fact]
    public void PoorResultsDismissThePlayerAtTheReviewTheyCannotSurviveThenAnOfferArrivesAndTheCareerContinues()
    {
        var lab = new Lab(42UL, free: 5);
        lab.Appoint(Pam, T3);
        lab.Facts.Position(T3, 5);
        var tenure = lab.Section.Board(T3)!.Principal!;

        // Protected through the first full season and 90 days more (30 reputation points at 3 days each).
        Assert.Equal(Dec1955.AddDays(90), tenure.ProtectedUntil);
        lab.AdvanceTo(tenure.ProtectedUntil.AddDays(1));
        Assert.Equal(Pam.Value, lab.Section.Board(T3)!.Principal!.Subject);

        // The first review after the protection ends is the race of 12 April 1956; five in a row below the bar dismiss on 7 June.
        lab.AdvanceTo(new GameDate(1956, 6, 8));
        var dismissal = Assert.Single(lab.Events, e => e.TypeId == BoardEventTypes.ManagerDismissed && e.Payload is BoardFactPayload { Kind: "Human" });
        Assert.Equal(new GameDate(1956, 6, 7), dismissal.Date);
        var payload = Assert.IsType<BoardFactPayload>(dismissal.Payload);
        Assert.Equal(Pam.Value, payload.Subject);
        Assert.Equal(50_000, payload.Amount);

        var record = lab.Section.UnemployedManager(Pam.Value)!;
        Assert.Equal(T3, record.FormerOrganization);
        Assert.Equal(new GameDate(1956, 6, 7), record.Since);
        Assert.Equal(50_000, record.Severance);
        Assert.Equal(PrincipalKind.Ai, lab.Section.Board(T3)!.Principal!.Kind);
        Assert.Null(lab.Section.OrganizationOf(Pam.Value));
        Assert.False(lab.Environment.Control.Controls(Pam, T3));
        var notice = Assert.Single(lab.Inbox.Section.ItemsOf(Pam.Value), item => item.Kind == BoardEngine.NoticeKind);
        Assert.Equal(BoardKeys.NoticeDismissed, notice.SubjectKey);
        Assert.False(notice.NeedsDecision);

        // Reputation: 30, minus 4 for the failed season, minus 8 for the dismissal.
        Assert.Equal(300 - 40 - 80, lab.Section.ReputationTenths(Pam.Value));

        // No team before the window is out; the guaranteed minimum offer then arrives from the cheapest team to ask.
        lab.Advance(BoardEstimates.GuaranteeWindowDays - 1);
        Assert.Empty(lab.OffersEver(Pam));
        lab.Advance(2);
        var offer = Assert.Single(lab.OpenOffers(Pam));
        Assert.Equal(T1.Value, offer.Arguments[BoardEngine.OrganizationArgument]);
        Assert.Equal(["accept", "decline"], offer.Options.Select(option => option.Id));
        Assert.Equal("decline", offer.DefaultOptionId);
        Assert.Equal(InboxBook.BlockingKind, lab.Managers.Get(Pam).BlockingItem!.Kind);

        // The player takes it: they run team 1, its principal is gone, and the seat starts protected again.
        var accepted = lab.Submit(new AcceptJobOfferCommand { ManagerId = Pam, IssuedOn = Date(lab.Today), ItemId = offer.Id });
        var events = Assert.IsType<CommandResult.Accepted>(accepted).Events;
        Assert.Contains(events, e => e is BoardFactEvent { TypeId: BoardEventTypes.ManagerAppointed });
        Assert.Equal(T1, lab.Section.OrganizationOf(Pam.Value));
        Assert.Null(lab.Section.UnemployedManager(Pam.Value));
        Assert.True(lab.Environment.Control.Controls(Pam, T1));
        Assert.False(lab.Environment.Control.Controls(Pam, T3));
        Assert.Null(lab.Managers.Get(Pam).BlockingItem);
        var boardOne = lab.Section.Board(T1)!;
        Assert.Equal(Pam.Value, boardOne.Principal!.Subject);
        Assert.Equal(lab.Today, boardOne.Principal.Since);
        Assert.True(boardOne.Principal.ProtectedUntil >= new GameDate(1957, 12, 31));
        Assert.Equal(BoardEstimates.InitialConfidenceTenths, boardOne.ConfidenceTenths);
        Assert.True(lab.World.Contracts.Single(contract => contract.PersonId == Principal(1)).End < lab.Today);

        // Time keeps running: the career goes on.
        lab.Advance(30);
        Assert.Equal(T1, lab.Section.OrganizationOf(Pam.Value));
    }

    [Fact]
    public void ADismissedManagerWatchesTheWorldAsAnObserverWithNoTeamView()
    {
        var lab = DismissedPlayerLab();
        var pam = AccessContext.ForManager(new AccessId(Pam.Value));

        var view = lab.Query.View(pam, lab.Today);

        Assert.Null(view.Own);
        Assert.NotNull(view.Observer);
        Assert.Equal(BoardKeys.ObserverStatus, view.Observer!.Status.Key);
        Assert.Equal(new DateOnly(1956, 6, 7), view.Observer.Since);
        Assert.Equal(0, view.Observer.OpenOffers);
        Assert.Equal(18, view.Reputation!.Points);
        var objectives = new Paddock.Application.Objectives.ObjectiveQuery(lab.Facts, lab.Board.Organizations());
        Assert.Empty(objectives.View(pam, lab.Board.Objectives, lab.Today).Items);
    }

    [Fact]
    public void AnOpenDecisionOfADismissedManagerIsWithdrawnSoItCannotHoldTheClockForEver()
    {
        var lab = new Lab(7UL, free: 5);
        lab.Appoint(Pam, T3);
        lab.Inbox.Post(
            lab.Managers,
            Pam,
            new InboxItemDraft(
                DecisionKind,
                "test.subject",
                null,
                [new InboxOption("yes", "test.yes", "test.yes.consequence"), new InboxOption("no", "test.no", "test.no.consequence")],
                null,
                null),
            lab.Today);
        Assert.NotNull(lab.Managers.Get(Pam).BlockingItem);
        lab.Facts.Position(T3, 5);

        lab.AdvanceTo(new GameDate(1956, 6, 8));

        Assert.Single(lab.EventsOf(BoardEventTypes.ManagerDismissed), e => e.Payload is BoardFactPayload { Kind: "Human" });
        var item = lab.Inbox.Section.ItemsOf(Pam.Value).Single(candidate => candidate.Kind == DecisionKind);
        Assert.Equal(InboxStatus.Expired, item.Status);
        Assert.Null(item.ChosenOptionId);
        Assert.Null(lab.Managers.Get(Pam).BlockingItem);
    }

    [Fact]
    public void ReadyForTheNextDayIsNotBlockedByADismissedPlayerWithNoOffer()
    {
        var lab = DismissedPlayerLab();
        lab.Managers.SetReady(Pam, true);
        lab.Managers.SetReady(Quinn, true);

        var result = lab.Gate.RequestAdvance(lab.Managers, lab.Time);

        Assert.IsType<AdvanceResult.Advanced>(result);
    }

    // --- Resigning ---

    [Fact]
    public void AManagerCanResignWithNoSeveranceAtASmallCostInReputation()
    {
        var lab = new Lab(5UL, free: 5, noRaces: true);
        lab.Appoint(Pam, T3);
        lab.Advance(2);

        var result = lab.Submit(new ResignFromTeamCommand { ManagerId = Pam, IssuedOn = Date(lab.Today) });

        var events = Assert.IsType<CommandResult.Accepted>(result).Events;
        Assert.Contains(events, e => e is BoardFactEvent { TypeId: BoardEventTypes.ManagerResigned });
        Assert.Contains(events, e => e is BoardFactEvent { TypeId: BoardEventTypes.ManagerAppointed });
        var record = lab.Section.UnemployedManager(Pam.Value)!;
        Assert.Equal(0, record.Severance);
        Assert.Equal(T3, record.FormerOrganization);
        Assert.Equal(300 - BoardEstimates.ResignationTenths, lab.Section.ReputationTenths(Pam.Value));
        Assert.Equal(PrincipalKind.Ai, lab.Section.Board(T3)!.Principal!.Kind);
        Assert.NotEqual(BoardEngine.DefaultArchetype, lab.Section.Board(T3)!.Archetype);

        // Resigning again is refused: there is no team to leave.
        var again = lab.Submit(new ResignFromTeamCommand { ManagerId = Pam, IssuedOn = Date(lab.Today) });
        Assert.Equal(BoardKeys.NotEmployed, Assert.IsType<CommandResult.Rejected>(again).Reason.Key);
    }

    [Fact]
    public void TheFounderOfATeamCannotResign()
    {
        var lab = new Lab(noRaces: true);
        lab.Appoint(Pam, T3, founder: true);

        var result = lab.Submit(new ResignFromTeamCommand { ManagerId = Pam, IssuedOn = Date(lab.Today) });

        Assert.Equal(BoardKeys.FounderCannotResign, Assert.IsType<CommandResult.Rejected>(result).Reason.Key);
        Assert.Equal(T3, lab.Section.OrganizationOf(Pam.Value));
    }

    // --- Offers ---

    [Fact]
    public void DecliningAnOfferClosesItAndLeavesThePlayerWithoutATeam()
    {
        var lab = new Lab(3UL, free: 5, noRaces: true);
        lab.Dismissed(Pam, 900, T3);
        lab.Advance(BoardEstimates.FirstOfferDelayDays + 1);
        var offer = Assert.Single(lab.OpenOffers(Pam));

        var result = lab.Submit(new DeclineJobOfferCommand { ManagerId = Pam, IssuedOn = Date(lab.Today), ItemId = offer.Id });

        Assert.IsType<CommandResult.Accepted>(result);
        Assert.Equal(InboxStatus.Resolved, lab.Inbox.Section.Find(offer.Id)!.Status);
        Assert.Equal("decline", lab.Inbox.Section.Find(offer.Id)!.ChosenOptionId);
        Assert.NotNull(lab.Section.UnemployedManager(Pam.Value));
        Assert.Null(lab.Managers.Get(Pam).BlockingItem);
    }

    [Fact]
    public void AnOfferIsForItsOwnManagerAndOnlyOffersCanBeAnsweredThisWay()
    {
        var lab = new Lab(3UL, free: 5, noRaces: true);
        lab.Dismissed(Pam, 900, T3);
        lab.Advance(BoardEstimates.FirstOfferDelayDays + 1);
        var offer = Assert.Single(lab.OpenOffers(Pam));
        lab.Appoint(Quinn, T4);
        var notice = lab.Inbox.Post(
            lab.Managers,
            Pam,
            new InboxItemDraft(BoardEngine.NoticeKind, BoardKeys.NoticeResigned, null, null, null, null),
            lab.Today);

        var stolen = lab.Submit(new AcceptJobOfferCommand { ManagerId = Quinn, IssuedOn = Date(lab.Today), ItemId = offer.Id });
        var wrongKind = lab.Submit(new AcceptJobOfferCommand { ManagerId = Pam, IssuedOn = Date(lab.Today), ItemId = notice.ItemId });
        var employed = lab.Submit(new AcceptJobOfferCommand { ManagerId = Quinn, IssuedOn = Date(lab.Today), ItemId = "inb:999" });

        Assert.Equal(InboxKeys.ItemUnknown, Assert.IsType<CommandResult.Rejected>(stolen).Reason.Key);
        Assert.Equal(BoardKeys.NotAnOffer, Assert.IsType<CommandResult.Rejected>(wrongKind).Reason.Key);
        Assert.Equal(InboxKeys.ItemUnknown, Assert.IsType<CommandResult.Rejected>(employed).Reason.Key);
        Assert.NotNull(lab.Section.UnemployedManager(Pam.Value));
    }

    [Fact]
    public void AnOfferNobodyAnswersLapsesAsDeclined()
    {
        var lab = new Lab(3UL, free: 5, noRaces: true);
        lab.Dismissed(Pam, 900, T3);
        lab.Advance(BoardEstimates.FirstOfferDelayDays + 1);
        var offer = Assert.Single(lab.OpenOffers(Pam));

        lab.Advance(BoardEstimates.OfferValidDays + 2);

        var closed = lab.Inbox.Section.Find(offer.Id)!;
        Assert.Equal(InboxStatus.Expired, closed.Status);
        Assert.Equal("decline", closed.ChosenOptionId);
        Assert.NotNull(lab.Section.UnemployedManager(Pam.Value));
    }

    [Fact]
    public void ReputationDecidesWhichTeamsMakeAnOffer()
    {
        // Three tiers of teams: a famous one asks for 80 points, the others for 50. All three are unprotected and shaky.
        static Lab LabFor(int reputationTenths)
        {
            var lab = new Lab(21UL, free: 5, noRaces: true);
            lab.Appeal.Set(T1, new OrganizationAppeal(1.0, 0.5, 0.5));
            lab.Dismissed(Pam, reputationTenths, T3);
            lab.Advance(BoardEstimates.FirstOfferDelayDays + 1);
            return lab;
        }

        var modest = LabFor(520);
        var famous = LabFor(900);

        // Both can have team 2 (asks 50). Only the famous one can be offered team 1 (asks 80).
        Assert.Equal(T2.Value, Assert.Single(modest.OpenOffers(Pam)).Arguments[BoardEngine.OrganizationArgument]);
        var famousOffer = Assert.Single(famous.OpenOffers(Pam));
        Assert.Contains(famousOffer.Arguments[BoardEngine.OrganizationArgument], new[] { T1.Value, T2.Value });
        Assert.Equal(800, famous.Engine.Required(T1, famous.Today));
        Assert.Equal(500, famous.Engine.Required(T2, famous.Today));
    }

    [Fact]
    public void WithoutEnoughReputationNoOrdinaryOfferComesButTheGuaranteedOneDoesOnTheLastDayOfTheWindow()
    {
        var lab = new Lab(8UL, free: 5, noRaces: true);
        foreach (var team in Teams)
        {
            lab.Appeal.Set(team, new OrganizationAppeal(1.0, 0.5, 0.5));
        }

        lab.Dismissed(Pam, 300, T3);

        lab.Advance(BoardEstimates.GuaranteeWindowDays);
        Assert.Empty(lab.OffersEver(Pam));
        lab.Advance(1);
        var offer = Assert.Single(lab.OffersEver(Pam));
        Assert.Equal(lab.Today.AddDays(-1), offer.Created);
        Assert.Equal(T1.Value, offer.Arguments[BoardEngine.OrganizationArgument]);
    }

    [Fact]
    public void ASeasonedPlayerHoldsAtMostOneOfferPerTeamAndNeverMoreThanTheLimit()
    {
        var lab = new Lab(13UL, free: 5, noRaces: true);
        lab.Dismissed(Pam, 1000, T3);

        lab.Advance(120);

        var open = lab.OpenOffers(Pam);
        Assert.True(open.Length <= BoardEstimates.MaxOpenOffers);
        Assert.Equal(open.Length, open.Select(item => item.Arguments[BoardEngine.OrganizationArgument]).Distinct().Count());
    }

    [Fact]
    public void ADismissedPlayerIsNeverLeftWithoutAnyOfferBeyondTheWindowAcrossTwoHundredSeeds()
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var rng = new System.Random((int)seed);
            var lab = new Lab(seed, free: 5, noRaces: true);
            foreach (var team in Teams)
            {
                lab.Appeal.Set(team, new OrganizationAppeal(rng.NextDouble(), 0.5, 0.5));
            }

            lab.Dismissed(Pam, rng.Next(0, 1001), rng.Next(0, 3) == 0 ? null : Teams[rng.Next(Teams.Length)]);
            var accepted = false;
            for (var day = 0; day < 150 && !accepted; day++)
            {
                lab.Advance(1);
                foreach (var offer in lab.OpenOffers(Pam))
                {
                    var roll = rng.Next(0, 10);
                    if (roll < 5)
                    {
                        Assert.IsType<CommandResult.Accepted>(lab.Submit(new DeclineJobOfferCommand { ManagerId = Pam, IssuedOn = Date(lab.Today), ItemId = offer.Id }));
                    }
                    else if (roll == 9)
                    {
                        Assert.IsType<CommandResult.Accepted>(lab.Submit(new AcceptJobOfferCommand { ManagerId = Pam, IssuedOn = Date(lab.Today), ItemId = offer.Id }));
                        accepted = true;
                        break;
                    }
                }

                if (accepted)
                {
                    Assert.NotNull(lab.Section.OrganizationOf(Pam.Value));
                    break;
                }

                // The offers of the last processed day are in: with none open, less than the window may have passed since the
                // last one (or since the dismissal).
                var record = lab.Section.UnemployedManager(Pam.Value)!;
                if (lab.OpenOffers(Pam).Length == 0)
                {
                    var waited = (record.LastOfferOn ?? record.Since).DaysUntil(lab.Today.AddDays(-1));
                    Assert.True(waited < BoardEstimates.GuaranteeWindowDays, $"seed {seed}: {waited} days with no offer on {lab.Today}");
                }
            }
        }
    }

    // --- Reputation in the negotiations of T39 (A/B) ---

    [Fact]
    public void AFamousPrincipalMakesATeamMoreAttractiveAndAnUnknownOneLess()
    {
        double Utility(int reputationTenths, out double appealPrestige)
        {
            var lab = new Lab(noRaces: true);
            lab.Advance(1);
            lab.Board.Update(lab.Section.WithReputationChange(Principal(1).Value, lab.Today, reputationTenths - 300, BoardKeys.ReputationSeason));
            appealPrestige = lab.Contracts.AppealOf(T1, lab.Today).Prestige;
            var subject = NegotiationSubject.Staff(StaffRole.Strategist);
            var context = lab.Contracts.ContextFor(T1, Principal(4), subject, lab.Today, 100_000);
            return CounterpartyEvaluator.Evaluate(Paddock.Tests.Contracts.ContractKit.Terms(100_000, null, 2), context).Utility;
        }

        var high = Utility(900, out var highPrestige);
        var neutral = Utility(500, out var neutralPrestige);
        var low = Utility(100, out var lowPrestige);

        Assert.Equal(0.5, neutralPrestige, 10);
        Assert.Equal(0.5 + 0.12, highPrestige, 10);
        Assert.Equal(0.5 - 0.12, lowPrestige, 10);
        Assert.True(high > neutral && neutral > low, $"{high} > {neutral} > {low}");
    }

    [Fact]
    public void ReputationChangesHowMuchAPersonNeedsToBePaidToSignWithATeam()
    {
        int CheapestAcceptableSalary(int reputationTenths)
        {
            var lab = new Lab(noRaces: true);
            lab.Advance(1);
            lab.Board.Update(lab.Section.WithReputationChange(Principal(1).Value, lab.Today, reputationTenths - 300, BoardKeys.ReputationSeason));
            var subject = NegotiationSubject.Staff(StaffRole.Strategist);
            var context = lab.Contracts.ContextFor(T1, Principal(4), subject, lab.Today, 100_000);
            var threshold = CounterpartyEvaluator.Threshold(CounterpartyEvaluator.Floor(context.Age, null), NegotiationEstimates.InterestStart);
            for (var salary = 10_000; salary <= 400_000; salary += 5_000)
            {
                if (CounterpartyEvaluator.Evaluate(Paddock.Tests.Contracts.ContractKit.Terms(salary, null, 2), context).Utility >= threshold)
                {
                    return salary;
                }
            }

            return int.MaxValue;
        }

        Assert.True(CheapestAcceptableSalary(950) < CheapestAcceptableSalary(500));
        Assert.True(CheapestAcceptableSalary(500) < CheapestAcceptableSalary(50));
    }

    // --- Truth versus knowledge, the view ---

    [Fact]
    public void AManagerSeesOnlyTheirOwnBoardAndTheirOwnReputation()
    {
        var lab = new Lab();
        lab.Appoint(Pam, T3);
        lab.Facts.Position(T3, 5);
        lab.AdvanceTo(new GameDate(1955, 3, 2));
        var pam = AccessContext.ForManager(new AccessId(Pam.Value));

        var view = lab.Query.View(pam, lab.Today);

        Assert.Empty(view.Boards);
        var own = view.Own!;
        Assert.Equal(T3.Value, own.OrganizationId);
        Assert.Equal(53, own.State.Confidence);
        Assert.Equal(BoardKeys.ConfidenceSteady, own.State.Band.Key);
        Assert.True(own.State.Protected);
        Assert.Equal(new DateOnly(1956, 3, 30), own.State.ProtectedUntil);
        Assert.Equal(0, own.State.ReviewsBelowBar);
        Assert.Equal(5, own.State.ReviewsToDismiss);
        Assert.Equal(3, own.Why.ExpectedPosition);
        Assert.Equal(5m, own.Why.CurrentPosition);
        Assert.Equal(33, own.Why.TargetConfidence);
        Assert.Equal([BoardKeys.ObjectiveSeason, BoardKeys.ObjectiveMultiYear], own.Why.Expectations.Select(item => item.Title.Key));
        Assert.All(own.Why.Expectations, item => Assert.Equal(T3.Value, item.OwnerId));
        Assert.Equal(BoardForecastKind.Protected, own.Forecast.Kind);
        Assert.Equal(30, view.Reputation!.Points);
        Assert.Equal(BoardKeys.BandModest, view.Reputation.Band.Key);

        // Someone who runs nothing and is not looking for a job sees nothing; an AI manager is limited like a person.
        Assert.Null(lab.Query.View(AccessContext.ForManager(new AccessId(Quinn.Value)), lab.Today).Own);
        Assert.Empty(lab.Query.View(AccessContext.ForAi(new AccessId(Quinn.Value)), lab.Today).Boards);
    }

    [Fact]
    public void TheDeveloperSeesEveryBoardWithTheTruthBehindIt()
    {
        var lab = new Lab(free: 5, noRaces: true);
        lab.Advance(1);

        var view = lab.Query.View(AccessContext.Developer, lab.Today);

        Assert.Equal(5, view.Boards.Count);
        Assert.Null(view.Own);
        var t4 = view.Boards.Single(line => line.OrganizationId == T4.Value);
        Assert.Equal(PrincipalKind.Ai, t4.Kind);
        Assert.NotEqual(BoardEngine.DefaultArchetype, t4.Archetype);
    }

    [Fact]
    public void TheForecastCountsTheReviewsLeftOnceTheProtectionIsOver()
    {
        var lab = new Lab();
        lab.Appoint(Pam, T3);
        lab.Facts.Position(T3, 5);
        lab.AdvanceTo(new GameDate(1956, 4, 13));
        var pam = AccessContext.ForManager(new AccessId(Pam.Value));

        var forecast = lab.Query.View(pam, lab.Today).Own!.Forecast;

        Assert.Equal(BoardForecastKind.AtRisk, forecast.Kind);
        Assert.InRange(forecast.ReviewsLeft!.Value, 1, 5);
        Assert.Equal(BoardKeys.ForecastAtRisk, forecast.Message.Key);

        lab.Facts.Position(T3, 1);
        lab.Advance(15);
        Assert.Equal(BoardForecastKind.Safe, lab.Query.View(pam, lab.Today).Own!.Forecast.Kind);
    }

    // --- Determinism, RNG, trace ---

    [Fact]
    public void TheSameSeedAndTheSameDaysGiveTheSameStateTwice()
    {
        static string Run()
        {
            var lab = DismissedPlayerLab(99UL);
            lab.Advance(60);
            return lab.Hash();
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void TheBoardDrawsOnlyKeyedChildrenOfTheMarketStreamAndLeavesEveryClockStreamAlone()
    {
        var lab = DismissedPlayerLab(5UL);
        lab.Advance(60);

        Assert.Empty(lab.Clock.RngStates);
        Assert.NotEmpty(lab.EventsOf(BoardEventTypes.ManagerAppointed));

        // The replacement does not depend on other draws of the Market stream: use the stream first and the same people are hired.
        var plain = new Lab(5UL, free: 5);
        plain.Facts.Position(T2, 5);
        plain.AdvanceTo(new GameDate(1955, 6, 1));
        var busy = new Lab(5UL, free: 5);
        var market = RngStreams.Derive(5UL, RngStreamName.Market, 1955);
        for (var i = 0; i < 17; i++)
        {
            market.NextULong();
        }

        busy.Clock = busy.Clock with
        {
            RngStates = new Dictionary<RngStreamSlot, RngState> { [new RngStreamSlot(RngStreamName.Market, 1955)] = market.State },
        };
        busy.Facts.Position(T2, 5);
        busy.AdvanceTo(new GameDate(1955, 6, 1));

        Assert.Equal(plain.Section.Board(T2)!.Principal!.Subject, busy.Section.Board(T2)!.Principal!.Subject);
        Assert.Equal(plain.Section.Board(T2)!.Archetype, busy.Section.Board(T2)!.Archetype);
    }

    [Fact]
    public void ADifferentSeedCanHireADifferentPrincipalButEachRunIsStable()
    {
        var hired = new HashSet<string>();
        for (ulong seed = 1; seed <= 12; seed++)
        {
            var lab = new Lab(seed, free: 5);
            lab.Facts.Position(T2, 5);
            lab.AdvanceTo(new GameDate(1955, 6, 1));
            hired.Add(lab.Section.Board(T2)!.Principal!.Subject);
        }

        Assert.True(hired.Count > 1);
    }

    [Fact]
    public void TheDismissalAndTheReplacementOfAnAiPrincipalAreTraced()
    {
        var sink = new ListSink();
        var lab = new Lab(11UL, free: 5, trace: sink);
        lab.Facts.Position(T2, 5);

        lab.AdvanceTo(new GameDate(1955, 6, 1));

        var dismissal = Assert.Single(sink.Traces, trace => trace.Trigger.StartsWith("board.dismiss:", StringComparison.Ordinal));
        Assert.Equal("dismiss", dismissal.ChosenOptionId);
        Assert.True(dismissal.IsKeyDecision);
        Assert.Equal(Principal(2).Value, dismissal.TruthContext["principal"]);
        var appointments = sink.Traces.Where(trace => trace.Trigger.StartsWith("board.appoint:", StringComparison.Ordinal)).ToArray();
        Assert.Equal(3, appointments.Length);
        var replacement = appointments.Single(trace => trace.Trigger == "board.appoint:" + T2.Value);
        Assert.Equal(lab.Section.Board(T2)!.Principal!.Subject, replacement.ChosenOptionId);
        Assert.Equal("balanced", replacement.TruthContext["oldArchetype"]);
        Assert.Equal(lab.Section.Board(T2)!.Archetype, replacement.TruthContext["newArchetype"]);
    }
}
