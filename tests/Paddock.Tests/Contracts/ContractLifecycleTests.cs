using Paddock.Application.Access;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Inbox;
using Paddock.Domain.Contracts;
using Paddock.Domain.People;
using Paddock.Domain.Spy;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Time;
using static Paddock.Tests.Contracts.ContractKit;

namespace Paddock.Tests.Contracts;

/// <summary>
/// What happens to a contract as time passes: the renewal prompt, the person's option, the exit clause after a race, and free
/// agency at expiry. Fixtures are SYNTHETIC (see <see cref="ContractKit"/>) and every threshold is an ESTIMATE.
/// </summary>
public class ContractLifecycleTests
{
    private static readonly GameDate VeteranEnd = new(1955, 12, 31);

    private static Lab LabWithPersonOption(ITraceSink? trace = null) => new(
        trace: trace,
        customize: world => world.AddContract(new ContractSpec(
            DriverY,
            TeamB,
            ContractRole.Driver(SeatStatus.Equal),
            new GameDate(1955, 1, 1),
            new GameDate(1955, 12, 31),
            100_000,
            true,
            new ContractOption(new GameDate(1955, 9, 1), 1),
            null)).State,
        customizeSection: section => section.WithTerms(new ContractTerms(ContractId.Generated(4), 0, 0, 0, OptionHolder.Person, null)));

    private static void ScheduleRace(Lab lab, int daysAhead)
    {
        lab.Clock = lab.Clock.Enqueue(new ScheduledEvent(
            new EventId("race-" + daysAhead),
            lab.Today.AddDays(daysAhead),
            ScheduledEventType.Race,
            new RaceSessionPayload(1955, 1, "fixture_layout")));
    }

    // --- Renewal prompt ---

    [Fact]
    public void SixMonthsBeforeAContractEndsTheEmployersHumanManagerIsAskedToRenewOnce()
    {
        var lab = new Lab();

        lab.Advance(30);
        Assert.DoesNotContain(lab.Inbox.Section.Items, item => item.Kind == ContractEngine.RenewalKind);
        Assert.Equal(VeteranEnd.AddDays(-NegotiationEstimates.RenewalPromptDays), new GameDate(1955, 7, 1));

        lab.Advance(1);

        var item = Assert.Single(lab.Inbox.Section.Items, candidate => candidate.Kind == ContractEngine.RenewalKind);
        Assert.Equal(Bram.Value, item.ManagerId);
        Assert.True(item.IsOpenDecision);
        Assert.Equal([ContractEngine.OptionRenew, ContractEngine.OptionExtend, ContractEngine.OptionRelease], item.Options.Select(option => option.Id));
        Assert.Equal(ContractEngine.OptionRelease, item.DefaultOptionId);
        Assert.Equal(VeteranEnd.ToString(), item.Arguments["end"]);
        Assert.Null(lab.Managers.Get(Bram).BlockingItem);
        Assert.Null(lab.Managers.Get(Anna).BlockingItem);
        Assert.Single(lab.EventsOf(ContractEventTypes.RenewalPrompt));
        Assert.True(lab.Section.WasPrompted(lab.World.Contracts.Single(contract => contract.PersonId == Veteran).Id));

        lab.Advance(20);
        Assert.Single(lab.Inbox.Section.Items, candidate => candidate.Kind == ContractEngine.RenewalKind);
        Assert.Single(lab.EventsOf(ContractEventTypes.RenewalPrompt));
    }

    [Fact]
    public void AnAiManagerGetsNoRenewalPromptBecauseItActsThroughCommands()
    {
        var lab = new Lab(customize: world => world.AddContract(new ContractSpec(
            DriverY, TeamC, ContractRole.Driver(SeatStatus.Equal), new GameDate(1955, 1, 1), new GameDate(1955, 12, 31), 100_000, true, null, null)).State);

        lab.Advance(40);

        Assert.Empty(lab.Inbox.Section.ItemsOf(Bot.Value));
        Assert.Contains(lab.EventsOf(ContractEventTypes.RenewalPrompt), e => ((MarkerPayload)e.Payload).Marker == "con:4");
    }

    [Fact]
    public void RenewingFromThePromptOpensARenewalNegotiationWithTheCurrentTermsAsTheFirstOffer()
    {
        var lab = new Lab();
        lab.Advance(31);
        var item = lab.Inbox.Section.Items.Single(candidate => candidate.Kind == ContractEngine.RenewalKind);

        var result = lab.Submit(new ResolveInboxItemCommand { ManagerId = Bram, IssuedOn = Date(lab.Today), ItemId = item.Id, OptionId = ContractEngine.OptionRenew });

        var events = Assert.IsType<CommandResult.Accepted>(result).Events;
        Assert.Contains(events, e => e is NegotiationOpened);
        var negotiation = lab.Find("neg:1");
        Assert.NotNull(negotiation.RenewalOf);
        Assert.Equal(Veteran, negotiation.Counterparty);
        Assert.Equal(100_000, negotiation.CurrentOffer!.Salary);
        Assert.Equal(ContractEngine.DefaultRenewalYears, negotiation.CurrentOffer.Years);
        Assert.Equal(SeatStatus.Equal, negotiation.CurrentOffer.Seat);
        Assert.Null(lab.Managers.Get(Bram).BlockingItem);

        Assert.Equal(NegotiationStatus.PersonAgreed, lab.AdvanceUntilAnswered("neg:1").Status);
        Assert.IsType<CommandResult.Accepted>(lab.Accept(Bram, "neg:1"));
        Assert.Equal(2, lab.World.Contracts.Count(contract => contract.PersonId == Veteran));
    }

    [Fact]
    public void LettingAContractRunOutFromThePromptOpensNothingAndTheContractEndsAsAgreed()
    {
        var lab = new Lab();
        lab.Advance(31);
        var item = lab.Inbox.Section.Items.Single(candidate => candidate.Kind == ContractEngine.RenewalKind);

        lab.Submit(new ResolveInboxItemCommand { ManagerId = Bram, IssuedOn = Date(lab.Today), ItemId = item.Id, OptionId = ContractEngine.OptionRelease });

        Assert.Empty(lab.Section.Negotiations);
        Assert.Null(lab.Managers.Get(Bram).BlockingItem);
        lab.Advance(183);
        Assert.Equal(new GameDate(1956, 1, 1), lab.Today);
    }

    // --- Free agency at expiry ---

    [Fact]
    public void TheDayAfterAContractEndsThePersonIsAFreeAgentAndTheEmployerIsTold()
    {
        var lab = new Lab();

        lab.Advance(214);
        Assert.DoesNotContain(lab.EventsOf(ContractEventTypes.FreeAgent), e => ((MarkerPayload)e.Payload).Marker == Veteran.Value);
        lab.Advance(1);

        Assert.Equal(new GameDate(1956, 1, 2), lab.Today);
        var freed = Assert.Single(lab.EventsOf(ContractEventTypes.FreeAgent), e => ((MarkerPayload)e.Payload).Marker == Veteran.Value);
        Assert.Equal(new GameDate(1956, 1, 1), freed.Date);
        Assert.Contains(lab.Inbox.Section.ItemsOf(Bram.Value), item => item.SubjectKey == ContractKeys.NoticeExpired && !item.NeedsDecision);
    }

    [Fact]
    public void APersonWhoHasSignedTheNextContractIsNotAFreeAgentAtExpiry()
    {
        var lab = new Lab();
        var id = lab.OpenOk(Anna, TeamA, Veteran);
        lab.Offer(Anna, id, Terms(130_000));
        lab.AdvanceUntilAnswered(id);
        lab.Accept(Anna, id);

        lab.Advance(215);

        Assert.DoesNotContain(lab.EventsOf(ContractEventTypes.FreeAgent), e => ((MarkerPayload)e.Payload).Marker == Veteran.Value);
    }

    [Fact]
    public void TheFreeAgentListShowsOnlyBandsToAManagerAndOnlyForTheirOwnOrganization()
    {
        var lab = new Lab();
        lab.Advance(214);
        var query = new FreeAgentQuery(lab.Book);
        var anna = AccessContext.ForManager(new ManagerId(Anna.Value));

        var list = query.List(anna, TeamA, lab.Today);

        Assert.Contains(list, agent => agent.Person == Veteran);
        Assert.Contains(list, agent => agent.Person == DriverX);
        Assert.DoesNotContain(list, agent => agent.Person == Locked);
        var veteran = list.Single(agent => agent.Person == Veteran);
        Assert.Equal(new DateOnly(1956, 1, 1), veteran.FreeSince);
        Assert.Null(list.Single(agent => agent.Person == DriverX).FreeSince);
        Assert.NotEmpty(veteran.KnownAttributes);
        Assert.All(veteran.KnownAttributes, attribute => Assert.True(attribute.Low < attribute.High));
        Assert.Empty(query.List(anna, TeamB, lab.Today));
        Assert.Empty(query.List(AccessContext.ForManager(new ManagerId("mgr-ghost")), TeamA, lab.Today));
        Assert.Equal(list.Select(agent => agent.Person.Value).Order(StringComparer.Ordinal), list.Select(agent => agent.Person.Value));

        var developer = query.List(AccessContext.Developer, TeamA, lab.Today).Single(agent => agent.Person == Veteran);
        Assert.All(developer.KnownAttributes, attribute => Assert.Equal(attribute.Low, attribute.High));
        Assert.Equal(
            lab.World.TruthOf(Veteran).Attributes.Select(attribute => (attribute.Key, attribute.Value)),
            developer.KnownAttributes.Select(attribute => (attribute.Key, attribute.Low)));
    }

    [Fact]
    public void TheFreeAgentListCanBeNarrowedToARole()
    {
        var lab = new Lab();
        var query = new FreeAgentQuery(lab.Book);
        var anna = AccessContext.ForManager(new ManagerId(Anna.Value));

        var strategists = query.List(anna, TeamA, lab.Today, NegotiationSubject.Staff(StaffRole.Strategist));

        Assert.Equal([Strategist], strategists.Select(agent => agent.Person));
        Assert.Equal([StaffRole.Strategist], strategists[0].StaffRoles);
        Assert.False(strategists[0].IsDriver);
    }

    // --- A person's option ---

    [Fact]
    public void APersonOptionIsSkippedWhenARenewalIsAlreadySigned()
    {
        var lab = new Lab(
            customize: world =>
            {
                var (withCurrent, _) = world.AddContract(new ContractSpec(
                    DriverY,
                    TeamB,
                    ContractRole.Driver(SeatStatus.Equal),
                    new GameDate(1955, 1, 1),
                    new GameDate(1955, 12, 31),
                    100_000,
                    true,
                    new ContractOption(new GameDate(1955, 9, 1), 1),
                    null));
                var (withRenewal, _) = withCurrent.AddContract(new ContractSpec(
                    DriverY,
                    TeamB,
                    ContractRole.Driver(SeatStatus.Equal),
                    new GameDate(1956, 1, 1),
                    new GameDate(1956, 12, 31),
                    100_000,
                    true,
                    null,
                    null));
                return withRenewal;
            },
            customizeSection: section => section.WithTerms(new ContractTerms(ContractId.Generated(4), 0, 0, 0, OptionHolder.Person, null)));

        var exception = Record.Exception(() => lab.Advance(95));

        Assert.Null(exception);
        Assert.Equal(new GameDate(1955, 12, 31), lab.World.Contracts.Single(contract => contract.Id == ContractId.Generated(4)).End);
        Assert.Empty(lab.EventsOf(ContractEventTypes.OptionExercised));
    }

    [Fact]
    public void APersonWhoHoldsTheOptionExtendsTheContractWhenStayingIsWorthIt()
    {
        var sink = new MemorySink();
        var lab = LabWithPersonOption(sink);

        lab.Advance(95);

        var contract = lab.World.Contracts.Single(candidate => candidate.Id == ContractId.Generated(4));
        Assert.Equal(new GameDate(1956, 12, 31), contract.End);
        Assert.Null(contract.Option);
        Assert.Null(lab.Section.TermsOf(contract.Id)!.OptionHolder);
        Assert.Single(lab.EventsOf(ContractEventTypes.OptionExercised));
        Assert.Contains(lab.Inbox.Section.ItemsOf(Bram.Value), item => item.SubjectKey == ContractKeys.NoticeOptionExercised);
        var trace = Assert.Single(sink.Traces);
        Assert.Equal(PersonTraces.TriggerOption + ":con:4", trace.Trigger);
        Assert.Equal("exercise", trace.ChosenOptionId);
    }

    [Fact]
    public void APersonWhoHoldsTheOptionLetsItLapseWhenTheTeamHasFallenApart()
    {
        var sink = new MemorySink();
        var lab = LabWithPersonOption(sink);
        lab.Appeal.Set(TeamB, new OrganizationAppeal(0.0, 0.0, 1.0));

        lab.Advance(95);

        Assert.Equal(new GameDate(1955, 12, 31), lab.World.Contracts.Single(candidate => candidate.Id == ContractId.Generated(4)).End);
        Assert.Empty(lab.EventsOf(ContractEventTypes.OptionExercised));
        Assert.Equal("decline", Assert.Single(sink.Traces).ChosenOptionId);
    }

    // --- Exit clause after each race ---

    private static Lab LabWithExitClause(ITraceSink? trace = null) =>
        new(trace: trace, customizeSection: section => section.WithTerms(new ContractTerms(ContractId.Generated(1), 0, 0, 0, null, new ExitClause(5))));

    [Fact]
    public void AfterARaceAPersonWhoseTeamHasFallenBelowTheClausePositionAndWhoIsUnhappyLeaves()
    {
        var sink = new MemorySink();
        var lab = LabWithExitClause(sink);
        lab.Standings.Set(TeamB, 8);
        lab.Appeal.Set(TeamB, new OrganizationAppeal(0.1, 0.1, 0.9));
        ScheduleRace(lab, 3);
        var veteran = lab.World.Contracts.Single(contract => contract.PersonId == Veteran).Id;

        lab.Advance(3);
        Assert.Equal(VeteranEnd, lab.World.Contracts.Single(contract => contract.Id == veteran).End);
        lab.Advance(1);

        var raceDay = new GameDate(1955, 6, 4);
        Assert.Equal(raceDay, lab.World.Contracts.Single(contract => contract.Id == veteran).End);
        Assert.Single(lab.EventsOf(ContractEventTypes.ExitExercised));
        Assert.Contains(lab.Inbox.Section.ItemsOf(Bram.Value), item => item.SubjectKey == ContractKeys.NoticeExitExercised);
        var trace = Assert.Single(sink.Traces);
        Assert.Equal("exit", trace.ChosenOptionId);
        Assert.StartsWith(PersonTraces.TriggerExit, trace.Trigger, StringComparison.Ordinal);
        lab.Advance(2);
        Assert.Contains(lab.EventsOf(ContractEventTypes.FreeAgent), e => ((MarkerPayload)e.Payload).Marker == Veteran.Value);
    }

    [Fact]
    public void ALoyalContentedPersonStaysEvenWhenTheClauseHasTriggered()
    {
        var sink = new MemorySink();
        var lab = LabWithExitClause(sink);
        lab.Standings.Set(TeamB, 8);
        ScheduleRace(lab, 1);

        lab.Advance(3);

        Assert.Equal(VeteranEnd, lab.World.Contracts.Single(contract => contract.PersonId == Veteran).End);
        Assert.Empty(lab.EventsOf(ContractEventTypes.ExitExercised));
        Assert.Equal("stay", Assert.Single(sink.Traces).ChosenOptionId);
    }

    [Fact]
    public void ATeamStillInsideTheClausePositionOrAnUnknownPositionTriggersNothing()
    {
        var sink = new MemorySink();
        var lab = LabWithExitClause(sink);
        lab.Appeal.Set(TeamB, new OrganizationAppeal(0.1, 0.1, 0.9));
        lab.Standings.Set(TeamB, 5);
        ScheduleRace(lab, 1);
        lab.Advance(2);
        Assert.Empty(sink.Traces);

        var unknown = new Lab(trace: sink, customizeSection: section => section.WithTerms(new ContractTerms(ContractId.Generated(1), 0, 0, 0, null, new ExitClause(5))));
        unknown.Appeal.Set(TeamB, new OrganizationAppeal(0.1, 0.1, 0.9));
        ScheduleRace(unknown, 1);
        unknown.Advance(2);
        Assert.Empty(sink.Traces);
        Assert.Equal(VeteranEnd, unknown.World.Contracts.Single(contract => contract.PersonId == Veteran).End);
    }

    [Fact]
    public void TheClauseIsOnlyCheckedOnRaceDays()
    {
        var sink = new MemorySink();
        var lab = LabWithExitClause(sink);
        lab.Standings.Set(TeamB, 8);
        lab.Appeal.Set(TeamB, new OrganizationAppeal(0.1, 0.1, 0.9));

        lab.Advance(20);

        Assert.Empty(sink.Traces);
        Assert.Equal(VeteranEnd, lab.World.Contracts.Single(contract => contract.PersonId == Veteran).End);
    }

    // --- Housekeeping ---

    [Fact]
    public void TermsOfContractsThatNoLongerExistArePrunedAtTheStartOfASeason()
    {
        var lab = new Lab(customizeSection: section => section.WithTerms(new ContractTerms(ContractId.Generated(1), 1, 0, 0, null, null))
            .WithTerms(new ContractTerms(ContractId.Generated(77), 2, 0, 0, null, null)));

        lab.Advance(215);

        Assert.Equal([ContractId.Generated(1)], lab.Section.Terms.Select(terms => terms.ContractId));
    }
}
