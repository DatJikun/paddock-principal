using Paddock.Application.Access;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Application.Spy;
using Paddock.Domain.Contracts;
using Paddock.Domain.Inbox;
using Paddock.Domain.People;
using Paddock.Domain.Random;
using Paddock.Domain.Spy;
using Paddock.Domain.World;
using Paddock.Simulation.Time;
using static Paddock.Tests.Contracts.ContractKit;
using AccessManagerId = Paddock.Application.Access.ManagerId;

namespace Paddock.Tests.Contracts;

/// <summary>
/// The person's side of a negotiation as time passes: the scheduled answer, the inbox decision that holds the clock, rival
/// offers, the Market stream, lapses, determinism and Spy. Fixtures are SYNTHETIC (see <see cref="ContractKit"/>) and every
/// threshold is an ESTIMATE.
/// </summary>
public class NegotiationDayTests
{
    private static InboxItem? OpenResponse(Lab lab, Paddock.Application.Managers.ManagerId manager) =>
        lab.Inbox.Section.ItemsOf(manager.Value).FirstOrDefault(item => item.IsOpenDecision && item.Kind == ContractEngine.ResponseKind);

    // --- The answer is scheduled and holds the clock ---

    [Fact]
    public void AnOfferIsAnsweredAfterADelayThatIsScheduledOnTheClocksQueue()
    {
        var lab = new Lab();
        var id = lab.OpenOk(Anna, TeamA, DriverX);
        lab.Offer(Anna, id, Terms(60_000));
        Assert.Null(lab.Find(id).RespondOn);

        lab.Advance(1);

        var respondOn = lab.Find(id).RespondOn!.Value;
        var delay = Start.DaysUntil(respondOn);
        Assert.InRange(
            delay,
            NegotiationEstimates.ResponseDelayMinDays,
            NegotiationEstimates.ResponseDelayMinDays + NegotiationEstimates.ResponseDelayJitterDays);
        var scheduled = Assert.Single(lab.Clock.Queue.Events, e => e.TypeId == ContractEventTypes.Respond);
        Assert.Equal(respondOn, scheduled.Date);
        Assert.Equal("neg:1#1", Assert.IsType<MarkerPayload>(scheduled.Payload).Marker);

        lab.Advance(delay - 1);
        Assert.Equal(NegotiationStatus.AwaitingResponse, lab.Find(id).Status);
        lab.Advance(1);
        Assert.Equal(NegotiationStatus.Countered, lab.Find(id).Status);
        Assert.Contains(lab.EventsOf(ContractEventTypes.Countered), e => ((MarkerPayload)e.Payload).Marker == id);
    }

    [Fact]
    public void ThePersonsAnswerBecomesAnInboxDecisionThatHoldsTheClockForTheHumanOnly()
    {
        var lab = new Lab();
        var id = lab.OpenOk(Anna, TeamA, DriverX);
        lab.Offer(Anna, id, Terms(60_000));

        var answered = lab.AdvanceUntilAnswered(id);

        Assert.Equal(NegotiationStatus.Countered, answered.Status);
        var item = OpenResponse(lab, Anna)!;
        Assert.Equal([ContractEngine.OptionAccept, ContractEngine.OptionWalk, ContractEngine.OptionRevise], item.Options.Select(option => option.Id));
        Assert.Equal(ContractKeys.InboxCounterSubject, item.SubjectKey);
        Assert.Equal(id, item.Arguments[ContractEngine.NegotiationArgument]);
        Assert.Equal(answered.Counter!.Salary.ToString(System.Globalization.CultureInfo.InvariantCulture), item.Arguments["salary"]);
        Assert.Equal(InboxBook.BlockingKind, lab.Managers.Get(Anna).BlockingItem!.Kind);
        Assert.Null(lab.Managers.Get(Bram).BlockingItem);

        lab.Managers.SetReady(Anna, true);
        lab.Managers.SetReady(Bram, true);
        var refused = Assert.IsType<AdvanceResult.Refused>(lab.Gate.RequestAdvance(lab.Managers, lab.Time));
        var waiting = Assert.Single(refused.Refusal.WaitingFor);
        Assert.Equal((Anna, WaitingCause.BlockingItem), (waiting.ManagerId, waiting.Cause));
    }

    [Fact]
    public void AcceptingTheCounterFromTheInboxSignsTheContractAndFreesTheClock()
    {
        var lab = new Lab();
        var id = lab.OpenOk(Anna, TeamA, DriverX);
        lab.Offer(Anna, id, Terms(60_000));
        lab.AdvanceUntilAnswered(id);
        var item = OpenResponse(lab, Anna)!;

        var result = lab.Submit(new ResolveInboxItemCommand
        {
            ManagerId = Anna,
            IssuedOn = Date(lab.Today),
            ItemId = item.Id,
            OptionId = ContractEngine.OptionAccept,
        });

        var events = Assert.IsType<CommandResult.Accepted>(result).Events;
        Assert.Contains(events, e => e is ContractSigned);
        Assert.Equal(NegotiationStatus.Agreed, lab.Find(id).Status);
        Assert.Single(lab.World.Contracts, contract => contract.PersonId == DriverX);
        Assert.Null(lab.Managers.Get(Anna).BlockingItem);
        Assert.Equal(InboxStatus.Resolved, lab.Inbox.Section.Find(item.Id)!.Status);
        Assert.Equal(ContractEngine.OptionAccept, lab.Inbox.Section.Find(item.Id)!.ChosenOptionId);
    }

    [Fact]
    public void WalkingAwayFromTheInboxEndsTheNegotiationAndReviseKeepsItOpen()
    {
        var walkLab = new Lab();
        var walked = walkLab.OpenOk(Anna, TeamA, DriverX);
        walkLab.Offer(Anna, walked, Terms(60_000));
        walkLab.AdvanceUntilAnswered(walked);
        var walkItem = OpenResponse(walkLab, Anna)!;
        walkLab.Submit(new ResolveInboxItemCommand { ManagerId = Anna, IssuedOn = Date(walkLab.Today), ItemId = walkItem.Id, OptionId = ContractEngine.OptionWalk });
        Assert.Equal(NegotiationStatus.WalkedAway, walkLab.Find(walked).Status);
        Assert.Null(walkLab.Managers.Get(Anna).BlockingItem);

        var reviseLab = new Lab();
        var revised = reviseLab.OpenOk(Anna, TeamA, DriverX);
        reviseLab.Offer(Anna, revised, Terms(60_000));
        reviseLab.AdvanceUntilAnswered(revised);
        var reviseItem = OpenResponse(reviseLab, Anna)!;
        reviseLab.Submit(new ResolveInboxItemCommand { ManagerId = Anna, IssuedOn = Date(reviseLab.Today), ItemId = reviseItem.Id, OptionId = ContractEngine.OptionRevise });
        Assert.Equal(NegotiationStatus.Countered, reviseLab.Find(revised).Status);
        Assert.Null(reviseLab.Managers.Get(Anna).BlockingItem);
        Assert.IsType<CommandResult.Accepted>(reviseLab.Offer(Anna, revised, Terms(60_000, SeatStatus.NumberOne)));
    }

    [Fact]
    public void ANewOfferOrAWalkAwayClosesTheOpenInboxItemSoNothingStaysBlocked()
    {
        var lab = new Lab();
        var id = lab.OpenOk(Anna, TeamA, DriverX);
        lab.Offer(Anna, id, Terms(60_000));
        lab.AdvanceUntilAnswered(id);
        Assert.NotNull(OpenResponse(lab, Anna));

        lab.Offer(Anna, id, Terms(60_000, SeatStatus.NumberOne));

        Assert.Null(OpenResponse(lab, Anna));
        Assert.Null(lab.Managers.Get(Anna).BlockingItem);
        lab.AdvanceUntilAnswered(id);
        Assert.NotNull(OpenResponse(lab, Anna));
        lab.Walk(Anna, id);
        Assert.Null(OpenResponse(lab, Anna));
        Assert.Null(lab.Managers.Get(Anna).BlockingItem);
    }

    [Fact]
    public void ARefusalIsAnInformationItemAndDoesNotHoldTheClock()
    {
        var lab = new Lab();
        lab.Appeal.Set(TeamA, new OrganizationAppeal(0.0, 0.0, 1.0));
        lab.Personality.Set(DriverX, new PersonalityTraits(PrimaryPersonality.SeeksSecurity, 10, 10, 10, 10, 10));
        var id = lab.OpenOk(Anna, TeamA, DriverX);
        lab.Offer(Anna, id, Terms(100_000));

        var answered = lab.AdvanceUntilAnswered(id);

        Assert.Equal(NegotiationStatus.Refused, answered.Status);
        var notice = Assert.Single(lab.Inbox.Section.ItemsOf(Anna.Value));
        Assert.False(notice.NeedsDecision);
        Assert.Equal(ContractKeys.InboxRefusedSubject, notice.SubjectKey);
        Assert.Null(lab.Managers.Get(Anna).BlockingItem);
        Assert.Contains(lab.EventsOf(ContractEventTypes.Refused), e => ((MarkerPayload)e.Payload).Marker == id);
    }

    // --- An AI proposer reads the negotiation itself ---

    [Fact]
    public void AnAiProposerSignsAtOnceWhenThePersonAgreesAndGetsNoInboxItems()
    {
        var lab = new Lab();
        var id = lab.OpenOk(Bot, TeamC, DriverX);
        lab.Offer(Bot, id, Terms(130_000));

        var answered = lab.AdvanceUntilAnswered(id);

        Assert.Equal(NegotiationStatus.Agreed, answered.Status);
        var contract = Assert.Single(lab.World.Contracts, candidate => candidate.PersonId == DriverX);
        Assert.Equal(TeamC, contract.OrganizationId);
        Assert.Contains(lab.EventsOf(ContractEventTypes.Signed), e => ((MarkerPayload)e.Payload).Marker == contract.Id.Value);
        Assert.Empty(lab.Inbox.Section.ItemsOf(Bot.Value));
    }

    [Fact]
    public void AnAiProposerWhoIsCounteredHoldsTheCounterUntilItActsOrTheDeadlinePasses()
    {
        var lab = new Lab();
        var id = lab.OpenOk(Bot, TeamC, DriverX);
        lab.Offer(Bot, id, Terms(60_000));

        Assert.Equal(NegotiationStatus.Countered, lab.AdvanceUntilAnswered(id).Status);
        Assert.Empty(lab.Inbox.Section.ItemsOf(Bot.Value));
        Assert.DoesNotContain(lab.World.Contracts, contract => contract.PersonId == DriverX);

        lab.Advance(40);

        var lapsed = lab.Find(id);
        Assert.Equal(NegotiationStatus.Lapsed, lapsed.Status);
        Assert.Equal([NegotiationReasons.DeadlinePassed], lapsed.Reasons);
    }

    // --- Rival offers ---

    [Fact]
    public void APersonWithSeveralAcceptableOffersDecidesAtTheEarliestDeadlineAndTheBestOfferWins()
    {
        var lab = new Lab();
        var anna = lab.OpenOk(Anna, TeamA, DriverX, deadlineDays: 20);
        var bram = lab.OpenOk(Bram, TeamB, DriverX, deadlineDays: 40);
        lab.Offer(Anna, anna, Terms(100_000));
        lab.Offer(Bram, bram, Terms(130_000));

        // Both answer within a week, both would sign, and the person waits for the earliest deadline (day 20).
        lab.Advance(19);
        Assert.Equal(NegotiationStatus.Considering, lab.Find(anna).Status);
        Assert.Equal(NegotiationStatus.Considering, lab.Find(bram).Status);
        Assert.Null(lab.Managers.Get(Anna).BlockingItem);
        Assert.Null(lab.Managers.Get(Bram).BlockingItem);
        Assert.Equal(2, lab.EventsOf(ContractEventTypes.Considering).Count);

        lab.Advance(1);
        Assert.Equal(NegotiationStatus.Considering, lab.Find(anna).Status);
        lab.Advance(1);

        var winner = lab.Find(bram);
        Assert.Equal(NegotiationStatus.PersonAgreed, winner.Status);
        var loser = lab.Find(anna);
        Assert.Equal(NegotiationStatus.Lost, loser.Status);
        Assert.Equal([NegotiationReasons.BetterOffer], loser.Reasons);
        Assert.Contains(lab.Inbox.Section.ItemsOf(Anna.Value), item => item.SubjectKey == ContractKeys.InboxLostSubject && !item.NeedsDecision);
        Assert.Equal(ContractEngine.OptionSign, OpenResponse(lab, Bram)!.Options[0].Id);
        Assert.Equal(InboxBook.BlockingKind, lab.Managers.Get(Bram).BlockingItem!.Kind);
        Assert.Null(lab.Managers.Get(Anna).BlockingItem);
    }

    [Fact]
    public void AnEqualPairOfOffersIsDecidedByTrustAndWhenTrustIsEqualByTheMarketStream()
    {
        // Trust decides.
        var trusting = new Lab();
        trusting.Trust.Set(TeamB, 90);
        var annaTrust = trusting.OpenOk(Anna, TeamA, DriverX, deadlineDays: 10);
        var bramTrust = trusting.OpenOk(Bram, TeamB, DriverX, deadlineDays: 10);
        trusting.Offer(Anna, annaTrust, Terms());
        trusting.Offer(Bram, bramTrust, Terms());
        trusting.Advance(12);
        Assert.Equal(NegotiationStatus.PersonAgreed, trusting.Find(bramTrust).Status);
        Assert.Equal(NegotiationStatus.Lost, trusting.Find(annaTrust).Status);

        // Equal trust: the person's choice is the higher draw of a child of the Market stream keyed by negotiation and day.
        var neutral = new Lab();
        var first = neutral.OpenOk(Anna, TeamA, DriverX, deadlineDays: 10);
        var second = neutral.OpenOk(Bram, TeamB, DriverX, deadlineDays: 10);
        neutral.Offer(Anna, first, Terms());
        neutral.Offer(Bram, second, Terms());
        var decisionDay = neutral.Find(first).Deadline;
        neutral.Advance(12);

        var market = RngStream.Derive(7UL, RngStreamName.Market, decisionDay.Year);
        double Draw(string id) => market.DeriveChild($"tiebreak:{id}:{decisionDay}").NextDouble();
        var expected = Draw(first) > Draw(second) ? first : second;
        Assert.Equal(NegotiationStatus.PersonAgreed, neutral.Find(expected).Status);
        Assert.Equal(NegotiationStatus.Lost, neutral.Find(expected == first ? second : first).Status);
    }

    [Fact]
    public void ASingleAcceptableOfferWithNoRivalOnTheTableIsAgreedAtOnce()
    {
        var lab = new Lab();
        var id = lab.OpenOk(Anna, TeamA, DriverX);
        lab.Offer(Anna, id, Terms());

        var answered = lab.AdvanceUntilAnswered(id);

        Assert.Equal(NegotiationStatus.PersonAgreed, answered.Status);
        Assert.Equal(answered.CurrentOffer, answered.Counter);
        Assert.Equal(ContractKeys.InboxAgreedSubject, OpenResponse(lab, Anna)!.SubjectKey);
        Assert.Contains(lab.EventsOf(ContractEventTypes.PersonAgreed), e => ((MarkerPayload)e.Payload).Marker == id);
    }

    // --- Deadlines ---

    [Fact]
    public void ANegotiationPastItsDeadlineLapsesAndTheManagerIsTold()
    {
        var lab = new Lab();
        var id = lab.OpenOk(Anna, TeamA, DriverX, deadlineDays: 7);

        lab.Advance(8);
        Assert.Equal(NegotiationStatus.Open, lab.Find(id).Status);
        lab.Advance(1);

        var lapsed = lab.Find(id);
        Assert.Equal(NegotiationStatus.Lapsed, lapsed.Status);
        Assert.Equal([NegotiationReasons.DeadlinePassed], lapsed.Reasons);
        Assert.Contains(lab.Inbox.Section.ItemsOf(Anna.Value), item => item.SubjectKey == ContractKeys.InboxLapsedSubject);
        Assert.Contains(lab.EventsOf(ContractEventTypes.Lapsed), e => ((MarkerPayload)e.Payload).Marker == id);
    }

    [Fact]
    public void AnOfferMadeOnTheLastDayIsAnsweredThatDay()
    {
        var lab = new Lab();
        var id = lab.OpenOk(Anna, TeamA, DriverX, deadlineDays: 7);
        lab.Advance(7);

        lab.Offer(Anna, id, Terms(60_000));
        lab.Advance(1);

        Assert.Equal(NegotiationStatus.Countered, lab.Find(id).Status);
        Assert.Equal(Start.AddDays(7), lab.Find(id).History[1].On);
    }

    [Fact]
    public void ThePersonRetiringBeforeAnsweringEndsTheNegotiation()
    {
        var lab = new Lab();
        var id = lab.OpenOk(Anna, TeamA, DriverX);
        lab.Offer(Anna, id, Terms());
        lab.Advance(1);
        lab.Book.UseWorld(lab.World.RetirePerson(DriverX, lab.Today));

        lab.Advance(8);

        Assert.Equal(NegotiationStatus.Lost, lab.Find(id).Status);
        Assert.NotEmpty(lab.Find(id).Reasons);
    }

    // --- Randomness lives in the Market stream, keyed by negotiation ---

    [Fact]
    public void AnUnrelatedNegotiationDoesNotShiftAnotherOnesDraws()
    {
        var alone = new Lab();
        var first = alone.OpenOk(Anna, TeamA, DriverX);
        alone.Offer(Anna, first, Terms(60_000));
        alone.Advance(1);
        var aloneRespondOn = alone.Find(first).RespondOn;

        var busy = new Lab();
        var same = busy.OpenOk(Anna, TeamA, DriverX);
        var other = busy.OpenOk(Bot, TeamC, DriverY);
        var third = busy.OpenOk(Bram, TeamB, Strategist, NegotiationSubject.Staff(StaffRole.Strategist));
        busy.Offer(Anna, same, Terms(60_000));
        busy.Offer(Bot, other, Terms(60_000));
        busy.Offer(Bram, third, Terms(20_000, null));
        busy.Advance(1);

        Assert.Equal(first, same);
        Assert.Equal(aloneRespondOn, busy.Find(same).RespondOn);
        Assert.Equal(alone.AdvanceUntilAnswered(first).Counter, busy.AdvanceUntilAnswered(same).Counter);
    }

    [Fact]
    public void OnlyTheMarketStreamIsEverTouchedAndIdleDaysTouchNothing()
    {
        var idle = new Lab();
        idle.Advance(60);
        Assert.Empty(idle.Clock.RngStates);

        var lab = new Lab();
        var anna = lab.OpenOk(Anna, TeamA, DriverX, deadlineDays: 20);
        var bram = lab.OpenOk(Bram, TeamB, DriverX, deadlineDays: 20);
        lab.Offer(Anna, anna, Terms());
        lab.Offer(Bram, bram, Terms());
        lab.Advance(25);

        Assert.NotEmpty(lab.Clock.RngStates);
        Assert.All(lab.Clock.RngStates.Keys, slot => Assert.Equal(RngStreamName.Market, slot.Name));
        Assert.All(lab.Clock.RngStates.Keys, slot => Assert.Equal(1955, slot.Season));
    }

    // --- Determinism ---

    private static string Script(ulong seed)
    {
        var lab = new Lab(masterSeed: seed);
        var anna = lab.OpenOk(Anna, TeamA, DriverX, deadlineDays: 25);
        var bram = lab.OpenOk(Bram, TeamB, DriverX, deadlineDays: 40);
        var bot = lab.OpenOk(Bot, TeamC, DriverY);
        lab.Offer(Anna, anna, Terms(110_000));
        lab.Offer(Bram, bram, Terms(60_000));
        lab.Offer(Bot, bot, Terms(130_000));
        lab.Advance(8);
        lab.Offer(Bram, bram, Terms(95_000, SeatStatus.NumberOne));
        lab.Advance(45);
        return lab.Hash();
    }

    [Fact]
    public void TheSameSeedAndCommandsGiveTheSameHashTwice()
    {
        var first = Script(11);

        Assert.Equal(first, Script(11));
        Assert.NotEqual(first, Script(12));
    }

    [Fact]
    public void AWorldThatNeverUsedContractsKeepsItsHash()
    {
        var lab = new Lab();

        lab.Advance(30);

        Assert.Empty(lab.Book.Into().Sections);
        Assert.Equal(lab.World.StateHash(), lab.Book.Into().StateHash());
    }

    // --- Spy ---

    private static Lab TracedScenario(ITraceSink? sink)
    {
        var lab = new Lab(trace: sink);
        var anna = lab.OpenOk(Anna, TeamA, DriverX, deadlineDays: 20);
        var bram = lab.OpenOk(Bram, TeamB, DriverX, deadlineDays: 30);
        var bot = lab.OpenOk(Bot, TeamC, DriverY);
        lab.Offer(Anna, anna, Terms(100_000));
        lab.Offer(Bram, bram, Terms(130_000));
        lab.Offer(Bot, bot, Terms(50_000));
        lab.Advance(30);
        return lab;
    }

    [Fact]
    public void EnablingTheSpySinkDoesNotChangeTheHash()
    {
        var withSink = new MemorySink();

        var plain = TracedScenario(null).Hash();
        var disabled = TracedScenario(NullSink.Instance).Hash();
        var traced = TracedScenario(withSink).Hash();

        Assert.Equal(plain, disabled);
        Assert.Equal(plain, traced);
        Assert.NotEmpty(withSink.Traces);
    }

    [Fact]
    public void EveryPersonDecisionLeavesATraceWithUtilitiesAndFactors()
    {
        var sink = new MemorySink();

        var lab = TracedScenario(sink);

        var answers = new[] { ContractEventTypes.Countered, ContractEventTypes.PersonAgreed, ContractEventTypes.Considering, ContractEventTypes.Refused }
            .Sum(type => lab.EventsOf(type).Count);
        // Anna and Bram both answered (acceptable, so Considering), Bot's offer was countered, and the person chose between Anna and Bram.
        var responses = sink.Traces.Where(trace => trace.Trigger.StartsWith(PersonTraces.TriggerResponse, StringComparison.Ordinal)).ToArray();
        var choices = sink.Traces.Where(trace => trace.Trigger == PersonTraces.TriggerChoice).ToArray();
        Assert.Equal(3, responses.Length);
        Assert.Equal(3, lab.EventsOf(ContractEventTypes.Considering).Count + lab.EventsOf(ContractEventTypes.Countered).Count + lab.EventsOf(ContractEventTypes.Refused).Count);
        Assert.True(answers >= responses.Length);
        Assert.Single(choices);
        Assert.Equal(["neg:1", "neg:2"], choices[0].Options.Select(option => option.Id).Order(StringComparer.Ordinal));
        Assert.Equal("neg:2", choices[0].ChosenOptionId);
        Assert.All(sink.Traces, trace =>
        {
            Assert.NotEmpty(trace.Options);
            Assert.Contains(trace.Options, option => option.Id == trace.ChosenOptionId);
            Assert.StartsWith("fixture_driver_", trace.Who, StringComparison.Ordinal);
        });
        Assert.All(responses, trace => Assert.True(trace.Options.First().Factors.Count >= 5));
    }

    [Fact]
    public void OnlyWhatThePersonStatesIsMarkedPlayerVisibleAndTruthStaysWithTheDeveloper()
    {
        var sink = new MemorySink();
        var lab = new Lab(trace: sink);
        var id = lab.OpenOk(Anna, TeamA, DriverX);
        lab.Offer(Anna, id, Terms(50_000));
        lab.AdvanceUntilAnswered(id);

        var trace = Assert.Single(sink.Traces);

        Assert.Equal(NegotiationReasons.SalaryTooLow, trace.PlayerReason);
        var visible = trace.Options.SelectMany(option => option.Factors).Where(factor => factor.PlayerVisible).Select(factor => factor.Name).Distinct().ToArray();
        Assert.Equal([UtilityTerms.Salary], visible);
        Assert.Contains("personality.primary", trace.TruthContext.Keys);
        Assert.Equal("counter", trace.ChosenOptionId);

        var asManager = WhyView.For(AccessContext.ForManager(new AccessManagerId(Anna.Value)), trace);
        Assert.Null(asManager);
        var asPerson = WhyView.For(AccessContext.ForManager(new AccessManagerId(DriverX.Value)), trace)!;
        Assert.Empty(asPerson.TruthContext);
        Assert.All(asPerson.Options.SelectMany(option => option.Factors), factor => Assert.Equal(UtilityTerms.Salary, factor.Name));
        var asDeveloper = WhyView.For(AccessContext.Developer, trace)!;
        Assert.NotEmpty(asDeveloper.TruthContext);
    }
}
