using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Managers;
using Paddock.Domain.Contracts;
using Paddock.Domain.People;
using Paddock.Domain.World;
using static Paddock.Tests.Contracts.ContractKit;

namespace Paddock.Tests.Contracts;

/// <summary>
/// The six contract commands: who may send them, what they refuse, and what they change. Fixtures are SYNTHETIC (see
/// <see cref="ContractKit"/>) and every threshold is an ESTIMATE.
/// </summary>
public class NegotiationCommandTests
{
    private static readonly PersonalityTraits ShortFuse = new(PrimaryPersonality.TeamPlayer, 10, 10, 20, 1, 10);

    // --- Opening ---

    [Fact]
    public void OnlyAManagerWhoRunsTheOrganizationMayOpenANegotiationForIt()
    {
        var lab = new Lab();
        var before = lab.Hash();

        var stranger = lab.Open(Bram, TeamA, DriverX);

        Assert.Equal(ContractKeys.NotController, Reason(stranger));
        Assert.Equal(before, lab.Hash());
        Assert.Empty(lab.Section.Negotiations);
        Assert.Equal(ContractKeys.NotController, Reason(lab.Open(Anna, TeamC, DriverX)));
        Assert.Equal(TranslationKeys.ManagerUnknown, Reason(lab.Open(new ManagerId("mgr-ghost"), TeamA, DriverX)));
    }

    [Fact]
    public void TheFirstSeatIsTakenForEveryoneButItsHolder()
    {
        // #325: team B's Locked is its number one. Anyone else weighing a seat there cannot ask for that seat in a counter.
        var lab = new Lab();
        var driver = NegotiationSubject.DriverSeat;

        Assert.True(lab.Book.ContextFor(TeamB, DriverX, driver, lab.Today, Reference).FirstSeatTaken);
        Assert.False(lab.Book.ContextFor(TeamB, Locked, driver, lab.Today, Reference).FirstSeatTaken);
        Assert.False(lab.Book.ContextFor(TeamA, DriverX, driver, lab.Today, Reference).FirstSeatTaken);
        Assert.False(lab.Book.ContextFor(TeamB, Strategist, NegotiationSubject.Staff(StaffRole.Strategist), lab.Today, Reference).FirstSeatTaken);
    }

    [Fact]
    public void AnOpenedNegotiationHoldsItsPartiesDeadlineAndPatience()
    {
        var lab = new Lab();
        lab.Personality.Set(DriverX, ShortFuse);

        var id = lab.OpenOk(Anna, TeamA, DriverX);

        var negotiation = lab.Find(id);
        Assert.Equal(NegotiationStatus.Open, negotiation.Status);
        Assert.Equal(TeamA, negotiation.Proposer);
        Assert.Equal(DriverX, negotiation.Counterparty);
        Assert.Equal(Anna.Value, negotiation.ManagerId);
        Assert.Equal(lab.Today.AddDays(NegotiationEstimates.DefaultDeadlineDays), negotiation.Deadline);
        Assert.Equal(NegotiationEstimates.MinRounds, negotiation.MaxRounds);
        Assert.Equal(0, negotiation.RoundsUsed);
        Assert.Null(negotiation.CurrentOffer);
    }

    [Fact]
    public void ThePersonMustBeAbleToFillTheRoleAndStillBeInTheSport()
    {
        var lab = new Lab();

        Assert.Equal(ContractKeys.NotThatRole, Reason(lab.Open(Anna, TeamA, Strategist)));
        Assert.Equal(ContractKeys.NotThatRole, Reason(lab.Open(Anna, TeamA, DriverX, NegotiationSubject.Staff(StaffRole.Strategist))));
        Assert.Equal(ContractKeys.UnknownPerson, Reason(lab.Open(Anna, TeamA, PersonId.Real("nobody"))));

        var retired = new Lab(customize: world => world.RetirePerson(DriverY, new Paddock.Domain.Time.GameDate(1955, 5, 1)));
        Assert.Equal(ContractKeys.PersonRetired, Reason(retired.Open(Anna, TeamA, DriverY)));
    }

    [Fact]
    public void APersonUnderALongContractCannotBeApproachedButOneInTheLastYearCan()
    {
        var lab = new Lab();

        Assert.Equal(ContractKeys.PersonUnderContract, Reason(lab.Open(Anna, TeamA, Locked)));
        Assert.IsType<CommandResult.Accepted>(lab.Open(Anna, TeamA, Veteran));
        Assert.Equal(ContractKeys.OwnEmployee, Reason(lab.Open(Bram, TeamB, Veteran)));
    }

    [Fact]
    public void AnExitClauseThatHasTriggeredOpensTheDoorToALongContract()
    {
        var lab = new Lab(customizeSection: section => section.WithTerms(new ContractTerms(ContractId.Generated(2), 0, 0, 0, null, new ExitClause(4))));
        Assert.Equal(ContractKeys.PersonUnderContract, Reason(lab.Open(Anna, TeamA, Locked)));

        lab.Standings.Set(TeamB, 6);

        Assert.IsType<CommandResult.Accepted>(lab.Open(Anna, TeamA, Locked));
    }

    [Fact]
    public void ANegotiationWithTheSamePersonCannotBeOpenedTwice()
    {
        var lab = new Lab();
        lab.OpenOk(Anna, TeamA, DriverX);

        Assert.Equal(ContractKeys.AlreadyNegotiating, Reason(lab.Open(Anna, TeamA, DriverX)));
        Assert.IsType<CommandResult.Accepted>(lab.Open(Bram, TeamB, DriverX));
    }

    [Fact]
    public void TheNumberOfTalksAtOnceFollowsThePrincipalsNegotiationSkill()
    {
        var lab = new Lab();

        // Team A knows its principal's negotiation attribute as the band 13-15 (middle 14): 3 + 14/5 = 5 (ESTIMATES).
        Assert.Equal(5, lab.Book.Capacity(TeamA));
        Assert.Equal(NegotiationEstimates.ParallelBase, lab.Book.Capacity(TeamB));

        lab.OpenOk(Bot, TeamC, DriverX);
        lab.OpenOk(Bot, TeamC, DriverY);
        lab.OpenOk(Bot, TeamC, Strategist, NegotiationSubject.Staff(StaffRole.Strategist));
        var fourth = lab.Open(Bot, TeamC, Veteran);

        Assert.Equal(ContractKeys.TooManyOpen, Reason(fourth));
        Assert.Equal("3", ((CommandResult.Rejected)fourth).Reason.Parameters["limit"]);
    }

    [Fact]
    public void ADeadlineMustBeBetweenAWeekAndThreeMonths()
    {
        var lab = new Lab();

        Assert.Equal(ContractKeys.BadDeadline, Reason(lab.Open(Anna, TeamA, DriverX, deadlineDays: 3)));
        Assert.Equal(ContractKeys.BadDeadline, Reason(lab.Open(Anna, TeamA, DriverX, deadlineDays: 100)));
        Assert.IsType<CommandResult.Accepted>(lab.Open(Anna, TeamA, DriverX, deadlineDays: 7));
        Assert.Equal(lab.Today.AddDays(7), lab.Find("neg:1").Deadline);
    }

    // --- Offers: rounds and nudging ---

    [Fact]
    public void OnlyTheOwnerOfANegotiationCanTouchItAndOthersLearnNothingFromTheReply()
    {
        var lab = new Lab();
        var id = lab.OpenOk(Anna, TeamA, DriverX);

        var missing = Reason(lab.Offer(Bram, "neg:99", Terms()));
        Assert.Equal(ContractKeys.UnknownNegotiation, missing);
        Assert.Equal(missing, Reason(lab.Offer(Bram, id, Terms())));
        Assert.Equal(missing, Reason(lab.Walk(Bram, id)));
        Assert.Equal(missing, Reason(lab.Accept(Bram, id)));
        Assert.Equal(NegotiationStatus.Open, lab.Find(id).Status);
    }

    [Fact]
    public void AnOfferUsesARoundAndAnswersCostTimeNotState()
    {
        var lab = new Lab();
        var id = lab.OpenOk(Anna, TeamA, DriverX);

        var result = lab.Offer(Anna, id, Terms(60_000));

        var accepted = Assert.IsType<CommandResult.Accepted>(result);
        var submitted = Assert.IsType<OfferSubmitted>(Assert.Single(accepted.Events));
        Assert.Equal(1, submitted.Round);
        Assert.False(submitted.WasNudge);
        var negotiation = lab.Find(id);
        Assert.Equal(NegotiationStatus.AwaitingResponse, negotiation.Status);
        Assert.Equal(1, negotiation.RoundsUsed);
        Assert.Equal(Terms(60_000), negotiation.CurrentOffer);
        Assert.Equal(ContractKeys.WrongStatus, Reason(lab.Offer(Anna, id, Terms(70_000))));
    }

    [Fact]
    public void TermsThatDoNotFitTheSubjectAreRefused()
    {
        var lab = new Lab();
        var id = lab.OpenOk(Anna, TeamA, DriverX);
        var staff = lab.OpenOk(Anna, TeamA, Strategist, NegotiationSubject.Staff(StaffRole.Strategist));

        Assert.Equal(ContractKeys.TermsDontFit, Reason(lab.Offer(Anna, id, Terms(seat: null))));
        Assert.Equal(ContractKeys.TermsDontFit, Reason(lab.Offer(Anna, staff, Terms())));
        Assert.IsType<CommandResult.Accepted>(lab.Offer(Anna, staff, Terms(seat: null)));
    }

    [Fact]
    public void ARefusedOfferGivesAtLeastOneReasonKey()
    {
        var lab = new Lab();
        lab.Appeal.Set(TeamA, new OrganizationAppeal(0.0, 0.0, 1.0));
        lab.Personality.Set(DriverX, new PersonalityTraits(PrimaryPersonality.SeeksSecurity, 10, 10, 10, 10, 10));
        var id = lab.OpenOk(Anna, TeamA, DriverX);
        lab.Offer(Anna, id, Terms(100_000));

        var answered = lab.AdvanceUntilAnswered(id);

        Assert.Equal(NegotiationStatus.Refused, answered.Status);
        Assert.NotEmpty(answered.Reasons);
        Assert.All(answered.Reasons, key => Assert.Contains(key, NegotiationReasons.All));
        var view = lab.Query.View(Paddock.Application.Access.AccessContext.ForManager(new Paddock.Application.Access.ManagerId(Anna.Value))).Items.Single();
        Assert.NotEmpty(view.Reasons);
        Assert.StartsWith("negotiation.reason.", view.Reasons[0].Key, StringComparison.Ordinal);
    }

    [Fact]
    public void AFewMeaninglessNudgesWearThePersonOutWhileARealChangeDoesNot()
    {
        var lab = new Lab();
        lab.Personality.Set(DriverX, new PersonalityTraits(PrimaryPersonality.TeamPlayer, 10, 10, 1, 20, 10));
        var id = lab.OpenOk(Anna, TeamA, DriverX);
        Assert.Equal(5, lab.Find(id).MaxRounds);

        lab.Offer(Anna, id, Terms(50_000));
        Assert.Equal(NegotiationStatus.Countered, lab.AdvanceUntilAnswered(id).Status);
        var interest = new List<int> { lab.Find(id).Interest };

        // Each of these changes the salary by well under 2 percent: a nudge. It costs a round and interest.
        for (var salary = 50_100; salary <= 50_300; salary += 100)
        {
            var result = lab.Offer(Anna, id, Terms(salary));
            Assert.True(Assert.IsType<OfferSubmitted>(Assert.Single(Assert.IsType<CommandResult.Accepted>(result).Events)).WasNudge);
            interest.Add(lab.Find(id).Interest);
            Assert.Equal(NegotiationStatus.Countered, lab.AdvanceUntilAnswered(id).Status);
        }

        Assert.Equal(
            [1000, 750, 500, 250],
            interest);
        var view = lab.Query.View(Paddock.Application.Access.AccessContext.ForManager(new Paddock.Application.Access.ManagerId(Anna.Value))).Items.Single();
        Assert.Equal(ContractKeys.InterestLow, view.Interest.Key);
        Assert.Equal(NegotiationReasons.NoRealChange, view.Reasons[0].Key);

        // A real change (a better seat) costs a round but no interest.
        var real = lab.Offer(Anna, id, Terms(50_300, SeatStatus.NumberOne));
        Assert.False(Assert.IsType<OfferSubmitted>(Assert.Single(Assert.IsType<CommandResult.Accepted>(real).Events)).WasNudge);
        Assert.Equal(250, lab.Find(id).Interest);
    }

    [Fact]
    public void RoundsRunOutButTheLastCounterCanStillBeAccepted()
    {
        var lab = new Lab();
        lab.Personality.Set(DriverX, ShortFuse);
        var id = lab.OpenOk(Anna, TeamA, DriverX);
        Assert.Equal(3, lab.Find(id).MaxRounds);

        for (var round = 1; round <= 3; round++)
        {
            Assert.IsType<CommandResult.Accepted>(lab.Offer(Anna, id, Terms(50_000, years: round)));
            Assert.Equal(NegotiationStatus.Countered, lab.AdvanceUntilAnswered(id).Status);
        }

        Assert.Equal(0, lab.Find(id).RoundsLeft);
        Assert.Equal(ContractKeys.NoRoundsLeft, Reason(lab.Offer(Anna, id, Terms(60_000, years: 4))));
        Assert.IsType<CommandResult.Accepted>(lab.Accept(Anna, id));
        Assert.Equal(NegotiationStatus.Agreed, lab.Find(id).Status);
    }

    [Fact]
    public void AnOfferAboveWhatTheTeamHasIsNotRefusedForMoney()
    {
        // Owner decision (#253): no budget gate on contracts; the board judges cash, the contract rules do not.
        var lab = new Lab();
        lab.Payroll.Cap(TeamA, 80_000);
        var id = lab.OpenOk(Anna, TeamA, DriverX);

        Assert.IsType<CommandResult.Accepted>(lab.Offer(Anna, id, Terms(90_000)));
    }

    // --- Signing ---

    [Fact]
    public void AcceptingACounterSignsAnExclusiveContractOnTheCounterTerms()
    {
        var lab = new Lab();
        var id = lab.OpenOk(Anna, TeamA, DriverX);
        lab.Offer(Anna, id, Terms(50_000, seat: SeatStatus.NumberTwo));
        var answered = lab.AdvanceUntilAnswered(id);
        Assert.Equal(NegotiationStatus.Countered, answered.Status);
        var counter = answered.Counter!;
        var signedOn = lab.Today;

        var result = lab.Accept(Anna, id);

        var accepted = Assert.IsType<CommandResult.Accepted>(result);
        var signed = Assert.IsType<ContractSigned>(Assert.Single(accepted.Events));
        var contract = lab.World.Contracts.Single(candidate => candidate.PersonId == DriverX);
        Assert.Equal(signed.ContractId, contract.Id.Value);
        Assert.Equal(TeamA, contract.OrganizationId);
        Assert.True(contract.Exclusive);
        Assert.Equal(counter.Salary, contract.Salary);
        Assert.Equal(counter.Seat, contract.Role.Seat);
        Assert.Equal(signedOn, contract.Start);
        Assert.Equal(new Paddock.Domain.Time.GameDate(signedOn.Year + counter.Years - 1, 12, 31), contract.End);
        Assert.Equal(NegotiationStatus.Agreed, lab.Find(id).Status);
        Assert.Equal(contract.Id, lab.Find(id).SignedContract);
        Assert.Equal(ContractKeys.WrongStatus, Reason(lab.Accept(Anna, id)));
    }

    [Fact]
    public void ABonusAnOptionAndAnExitClauseAreKeptWithTheContract()
    {
        var lab = new Lab();
        var id = lab.OpenOk(Anna, TeamA, DriverX);
        var offer = Terms(
            130_000,
            SeatStatus.NumberOne,
            3,
            pointsBonus: 1_000,
            winBonus: 20_000,
            titleBonus: 100_000,
            option: new OfferOption(OptionHolder.Person, 1),
            exit: new ExitClause(5));
        lab.Offer(Anna, id, offer);
        var answered = lab.AdvanceUntilAnswered(id);
        Assert.Equal(NegotiationStatus.PersonAgreed, answered.Status);
        Assert.Equal(offer, answered.Counter);

        Assert.IsType<CommandResult.Accepted>(lab.Accept(Anna, id));

        var contract = lab.World.Contracts.Single(candidate => candidate.PersonId == DriverX);
        var terms = lab.Section.TermsOf(contract.Id)!;
        Assert.Equal((1_000L, 20_000L, 100_000L), (terms.PointsBonus, terms.WinBonus, terms.TitleBonus));
        Assert.Equal(OptionHolder.Person, terms.OptionHolder);
        Assert.Equal(new ExitClause(5), terms.Exit);
        Assert.Equal(1, contract.Option!.Value.ExtraYears);
        Assert.Equal(contract.End.AddDays(-NegotiationEstimates.OptionNoticeDays), contract.Option.Value.Deadline);
        Assert.Equal(SeatStatus.NumberOne, contract.Role.Seat);
    }

    [Fact]
    public void AKeyStaffMemberSignsWithoutASeatStatus()
    {
        var lab = new Lab();
        var subject = NegotiationSubject.Staff(StaffRole.Strategist);
        var id = lab.OpenOk(Anna, TeamA, Strategist, subject);
        lab.Offer(Anna, id, Terms(25_000, null));

        var answered = lab.AdvanceUntilAnswered(id);
        Assert.True(answered.Status is NegotiationStatus.PersonAgreed or NegotiationStatus.Countered);
        Assert.IsType<CommandResult.Accepted>(lab.Accept(Anna, id));

        var contract = lab.World.Contracts.Single(candidate => candidate.PersonId == Strategist);
        Assert.True(contract.Role.IsStaff);
        Assert.Equal(StaffRole.Strategist, contract.Role.StaffRole);
    }

    [Fact]
    public void SigningTheNextSeasonOnTopOfACurrentContractStartsTheDayAfterItEnds()
    {
        var lab = new Lab();
        var id = lab.OpenOk(Anna, TeamA, Veteran);
        lab.Offer(Anna, id, Terms(130_000));
        Assert.Equal(NegotiationStatus.PersonAgreed, lab.AdvanceUntilAnswered(id).Status);

        Assert.IsType<CommandResult.Accepted>(lab.Accept(Anna, id));

        var next = lab.World.Contracts.Single(contract => contract.PersonId == Veteran && contract.OrganizationId == TeamA);
        Assert.Equal(new Paddock.Domain.Time.GameDate(1956, 1, 1), next.Start);
        Assert.Equal(new Paddock.Domain.Time.GameDate(1957, 12, 31), next.End);
        Assert.Equal(2, lab.World.Contracts.Count(contract => contract.PersonId == Veteran));
        Assert.Null(lab.World.FindExclusiveOverlap(Veteran, new Paddock.Domain.Time.GameDate(1958, 1, 1), new Paddock.Domain.Time.GameDate(1958, 12, 31)));
        Assert.NotNull(lab.World.FindExclusiveOverlap(Veteran, new Paddock.Domain.Time.GameDate(1957, 1, 1), new Paddock.Domain.Time.GameDate(1958, 12, 31)));
    }

    [Fact]
    public void WhenOneTeamSignsAPersonEveryOtherLiveNegotiationAboutThemIsLostAndTheirManagerIsTold()
    {
        var lab = new Lab();
        var anna = lab.OpenOk(Anna, TeamA, DriverX);
        var bram = lab.OpenOk(Bram, TeamB, DriverX);
        lab.Offer(Anna, anna, Terms(130_000));
        lab.Offer(Bram, bram, Terms(60_000));
        lab.Advance(12);
        // Both answered: Anna's offer is acceptable, Bram's was countered. With no rival left waiting, the person agrees to Anna.
        Assert.Equal(NegotiationStatus.Countered, lab.Find(bram).Status);
        var winner = lab.Find(anna);
        Assert.Equal(NegotiationStatus.PersonAgreed, winner.Status);

        Assert.IsType<CommandResult.Accepted>(lab.Accept(Anna, anna));
        Assert.Equal(ContractKeys.WrongStatus, Reason(lab.Accept(Bram, bram)));
        var lost = lab.Find(bram);
        Assert.Equal(NegotiationStatus.Lost, lost.Status);
        Assert.Equal([NegotiationReasons.SignedElsewhere], lost.Reasons);
        Assert.Contains(lab.Inbox.Section.ItemsOf(Bram.Value), item => item.SubjectKey == ContractKeys.InboxLostSubject);
    }

    [Fact]
    public void APersonWhoHasToldAnotherTeamTheyWouldSignMakesASecondTeamWait()
    {
        var lab = new Lab();
        var anna = lab.OpenOk(Anna, TeamA, DriverX);
        var bram = lab.OpenOk(Bram, TeamB, DriverX);
        lab.Offer(Bram, bram, Terms(50_000));
        Assert.Equal(NegotiationStatus.Countered, lab.AdvanceUntilAnswered(bram).Status);
        lab.Offer(Anna, anna, Terms(130_000));
        Assert.Equal(NegotiationStatus.PersonAgreed, lab.AdvanceUntilAnswered(anna).Status);

        Assert.Equal(ContractKeys.PersonTaken, Reason(lab.Accept(Bram, bram)));
        Assert.IsType<CommandResult.Accepted>(lab.Accept(Anna, anna));
    }

    [Fact]
    public void WalkingAwayEndsTheNegotiationAndOnlyOnce()
    {
        var lab = new Lab();
        var id = lab.OpenOk(Anna, TeamA, DriverX);
        lab.Offer(Anna, id, Terms(60_000));

        var result = lab.Walk(Anna, id);

        Assert.IsType<NegotiationEnded>(Assert.Single(Assert.IsType<CommandResult.Accepted>(result).Events));
        Assert.Equal(NegotiationStatus.WalkedAway, lab.Find(id).Status);
        Assert.Equal(ContractKeys.WrongStatus, Reason(lab.Walk(Anna, id)));
        Assert.Equal(ContractKeys.WrongStatus, Reason(lab.Offer(Anna, id, Terms())));
        lab.Advance(10);
        Assert.Equal(NegotiationStatus.WalkedAway, lab.Find(id).Status);
        Assert.DoesNotContain(lab.World.Contracts, contract => contract.PersonId == DriverX);
    }

    // --- Renewal, option, termination ---

    private static Lab LabWithTeamOption(bool renewed = false) => new(
        customize: world =>
        {
            var (withCurrent, _) = world.AddContract(new ContractSpec(
                DriverY,
                TeamB,
                ContractRole.Driver(SeatStatus.Equal),
                new Paddock.Domain.Time.GameDate(1955, 1, 1),
                new Paddock.Domain.Time.GameDate(1955, 12, 31),
                100_000,
                true,
                new ContractOption(new Paddock.Domain.Time.GameDate(1955, 9, 1), 2),
                null));
            if (!renewed)
            {
                return withCurrent;
            }

            var (withRenewal, _) = withCurrent.AddContract(new ContractSpec(
                DriverY,
                TeamB,
                ContractRole.Driver(SeatStatus.Equal),
                new Paddock.Domain.Time.GameDate(1956, 1, 1),
                new Paddock.Domain.Time.GameDate(1956, 12, 31),
                100_000,
                true,
                null,
                null));
            return withRenewal;
        },
        customizeSection: section => section.WithTerms(new ContractTerms(ContractId.Generated(4), 5, 6, 7, OptionHolder.Team, null)));

    [Fact]
    public void ATeamOptionIsRejectedWhenALaterContractIsAlreadySigned()
    {
        var lab = LabWithTeamOption(renewed: true);
        var contract = ContractId.Generated(4);

        var result = lab.Submit(new RenewContractCommand { ManagerId = Bram, IssuedOn = Date(lab.Today), Contract = contract, ExerciseOption = true });

        Assert.Equal(ContractKeys.PersonTaken, Reason(result));
        Assert.Equal(new Paddock.Domain.Time.GameDate(1955, 12, 31), lab.World.Contracts.Single(candidate => candidate.Id == contract).End);
    }

    [Fact]
    public void ATeamUsesItsOptionToRunTheContractLongerOnTheSameTerms()
    {
        var lab = LabWithTeamOption();
        var contract = ContractId.Generated(4);

        var stranger = lab.Submit(new RenewContractCommand { ManagerId = Anna, IssuedOn = Date(lab.Today), Contract = contract, ExerciseOption = true });
        Assert.Equal(ContractKeys.NotEmployer, Reason(stranger));

        var result = lab.Submit(new RenewContractCommand { ManagerId = Bram, IssuedOn = Date(lab.Today), Contract = contract, ExerciseOption = true });

        Assert.IsType<OptionExercised>(Assert.Single(Assert.IsType<CommandResult.Accepted>(result).Events));
        var extended = lab.World.Contracts.Single(candidate => candidate.Id == contract);
        Assert.Equal(new Paddock.Domain.Time.GameDate(1957, 12, 31), extended.End);
        Assert.Null(extended.Option);
        Assert.Equal(100_000, extended.Salary);
        var terms = lab.Section.TermsOf(contract)!;
        Assert.Equal((5L, 6L, 7L, (OptionHolder?)null), (terms.PointsBonus, terms.WinBonus, terms.TitleBonus, terms.OptionHolder));
        Assert.Equal(ContractKeys.OptionUnavailable, Reason(lab.Submit(new RenewContractCommand { ManagerId = Bram, IssuedOn = Date(lab.Today), Contract = contract, ExerciseOption = true })));
    }

    [Fact]
    public void ATeamOptionLapsesAfterItsDeadlineAndAPersonsOptionIsNotTheTeamsToUse()
    {
        var lab = LabWithTeamOption();
        lab.Advance(101);
        Assert.True(lab.Today > new Paddock.Domain.Time.GameDate(1955, 9, 1));

        Assert.Equal(
            ContractKeys.OptionUnavailable,
            Reason(lab.Submit(new RenewContractCommand { ManagerId = Bram, IssuedOn = Date(lab.Today), Contract = ContractId.Generated(4), ExerciseOption = true })));

        var personOption = new Lab(
            customize: world => world.AddContract(new ContractSpec(
                DriverY,
                TeamB,
                ContractRole.Driver(SeatStatus.Equal),
                new Paddock.Domain.Time.GameDate(1955, 1, 1),
                new Paddock.Domain.Time.GameDate(1955, 12, 31),
                100_000,
                true,
                new ContractOption(new Paddock.Domain.Time.GameDate(1955, 9, 1), 1),
                null)).State,
            customizeSection: section => section.WithTerms(new ContractTerms(ContractId.Generated(4), 0, 0, 0, OptionHolder.Person, null)));
        Assert.Equal(
            ContractKeys.OptionUnavailable,
            Reason(personOption.Submit(new RenewContractCommand { ManagerId = Bram, IssuedOn = Date(personOption.Today), Contract = ContractId.Generated(4), ExerciseOption = true })));
    }

    [Fact]
    public void ARenewalOpensANegotiationAndTheNewContractStartsWhenTheOldOneEnds()
    {
        var lab = new Lab();
        var veteran = lab.World.Contracts.Single(contract => contract.PersonId == Veteran).Id;

        Assert.Equal(
            ContractKeys.OfferRequired,
            Reason(lab.Submit(new RenewContractCommand { ManagerId = Bram, IssuedOn = Date(lab.Today), Contract = veteran })));
        var result = lab.Submit(new RenewContractCommand { ManagerId = Bram, IssuedOn = Date(lab.Today), Contract = veteran, Offer = Terms(130_000) });

        var events = Assert.IsType<CommandResult.Accepted>(result).Events;
        Assert.Collection(events, e => Assert.IsType<NegotiationOpened>(e), e => Assert.IsType<OfferSubmitted>(e));
        var negotiation = lab.Find("neg:1");
        Assert.Equal(veteran, negotiation.RenewalOf);
        Assert.Equal(TeamB, negotiation.Proposer);
        Assert.Equal(NegotiationStatus.AwaitingResponse, negotiation.Status);

        Assert.Equal(NegotiationStatus.PersonAgreed, lab.AdvanceUntilAnswered("neg:1").Status);
        Assert.IsType<CommandResult.Accepted>(lab.Accept(Bram, "neg:1"));

        var renewed = lab.World.Contracts.Where(contract => contract.PersonId == Veteran && contract.Id != veteran).Single();
        Assert.Equal(new Paddock.Domain.Time.GameDate(1956, 1, 1), renewed.Start);
        Assert.Equal(TeamB, renewed.OrganizationId);
    }

    [Fact]
    public void ARenewalIsOnlyPossibleInTheLastYearOfAContract()
    {
        var lab = new Lab();
        var locked = lab.World.Contracts.Single(contract => contract.PersonId == Locked).Id;

        Assert.Equal(
            ContractKeys.TooEarlyToRenew,
            Reason(lab.Submit(new RenewContractCommand { ManagerId = Bram, IssuedOn = Date(lab.Today), Contract = locked, Offer = Terms() })));
    }

    [Fact]
    public void EndingAContractEarlyCostsAtLeastHalfTheRemainingSalaryAndTheContractEndsToday()
    {
        var lab = new Lab();
        var contract = lab.World.Contracts.Single(candidate => candidate.PersonId == Veteran);
        var days = lab.Today.DaysUntil(contract.End);
        var minimum = (long)Math.Ceiling(100_000 * (days / 365.0) * 0.5);
        Assert.Equal(minimum, ContractEngine.MinimumCompensation(contract, lab.Today));

        var low = lab.Submit(new TerminateContractCommand { ManagerId = Bram, IssuedOn = Date(lab.Today), Contract = contract.Id, Compensation = minimum - 1 });
        Assert.Equal(ContractKeys.CompensationTooLow, Reason(low));
        Assert.Equal(minimum.ToString(System.Globalization.CultureInfo.InvariantCulture), ((CommandResult.Rejected)low).Reason.Parameters["minimum"]);
        Assert.Equal(ContractKeys.NotEmployer, Reason(lab.Submit(new TerminateContractCommand { ManagerId = Anna, IssuedOn = Date(lab.Today), Contract = contract.Id, Compensation = minimum })));

        lab.Payroll.Cap(TeamB, minimum - 1);
        Assert.Equal(ContractKeys.CannotPay, Reason(lab.Submit(new TerminateContractCommand { ManagerId = Bram, IssuedOn = Date(lab.Today), Contract = contract.Id, Compensation = minimum })));
        lab.Payroll.Cap(TeamB, long.MaxValue);

        var ended = lab.Submit(new TerminateContractCommand { ManagerId = Bram, IssuedOn = Date(lab.Today), Contract = contract.Id, Compensation = minimum });

        Assert.IsType<ContractTerminated>(Assert.Single(Assert.IsType<CommandResult.Accepted>(ended).Events));
        Assert.Equal(lab.Today, lab.World.Contracts.Single(candidate => candidate.Id == contract.Id).End);
        Assert.Equal([(TeamB, Veteran, minimum)], lab.Payroll.Compensation);
        Assert.Equal(ContractKeys.ContractNotActive, Reason(lab.Submit(new TerminateContractCommand { ManagerId = Bram, IssuedOn = Date(lab.Today.AddDays(2)), Contract = contract.Id, Compensation = minimum })));
        lab.Advance(2);
        Assert.Contains(lab.EventsOf(ContractEventTypes.FreeAgent), e => ((Paddock.Simulation.Time.MarkerPayload)e.Payload).Marker == Veteran.Value);
    }

    [Fact]
    public void EveryContractCommandCarriesTheManagerWhoSentItAndRunsOnlyThroughTheQueue()
    {
        var lab = new Lab();
        var id = lab.OpenOk(Anna, TeamA, DriverX);
        lab.Offer(Anna, id, Terms());
        var queued = lab.Queue.Enqueue(new WalkAwayCommand { ManagerId = Anna, IssuedOn = Date(lab.Today), NegotiationId = id });

        Assert.Equal(Anna, queued.ManagerId);
        Assert.True(queued.SubmissionNumber > 0);
        Assert.Equal(NegotiationStatus.AwaitingResponse, lab.Find(id).Status);
        lab.Dispatcher.DispatchAll(lab.Queue, lab.Context);
        Assert.Equal(NegotiationStatus.WalkedAway, lab.Find(id).Status);
        Assert.Contains(lab.Dispatcher.Log.Entries, entry => entry is WalkAwayCommand walk && walk.ManagerId == Anna);
        Assert.All(lab.Dispatcher.Log.Entries, entry => Assert.True(entry.ManagerId.IsAssigned));
    }
}
