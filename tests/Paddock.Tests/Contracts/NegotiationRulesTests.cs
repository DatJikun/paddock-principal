using Paddock.Application.Contracts;
using Paddock.Domain.Contracts;
using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using static Paddock.Tests.Contracts.ContractKit;

namespace Paddock.Tests.Contracts;

/// <summary>
/// The pure rules: terms, rounds and patience, the person's utility and answer, the choice among rival offers, the reference
/// offer. All inputs are SYNTHETIC fixtures (see <see cref="ContractKit"/>) and every threshold is an ESTIMATE under test, not a
/// calibrated value.
/// </summary>
public class NegotiationRulesTests
{
    private static Negotiation Pending(OfferTerms offer, int maxRounds = 4, int interest = NegotiationEstimates.InterestStart)
    {
        var negotiation = Negotiation.Start(1, "mgr-anna", TeamA, DriverX, NegotiationSubject.DriverSeat, null, Start, Start.AddDays(30), maxRounds)
            .WithOffer(offer, Start);
        return interest == NegotiationEstimates.InterestStart
            ? negotiation
            : new Negotiation(
                negotiation.Number,
                negotiation.ManagerId,
                negotiation.Proposer,
                negotiation.Counterparty,
                negotiation.Subject,
                null,
                negotiation.Opened,
                negotiation.Deadline,
                negotiation.MaxRounds,
                negotiation.RoundsUsed,
                interest,
                negotiation.Status,
                negotiation.CurrentOffer,
                null,
                null,
                0,
                [],
                negotiation.History,
                null,
                null);
    }

    private static EvaluationContext Context(
        OrganizationAppeal? appeal = null,
        PersonalityTraits? traits = null,
        int age = 30,
        NegotiationSubject? subject = null,
        bool currentEmployer = false,
        bool firstSeatTaken = false) =>
        new(traits ?? Balanced, age, appeal ?? OrganizationAppeal.Neutral, Reference, subject ?? NegotiationSubject.DriverSeat, currentEmployer, firstSeatTaken);

    // --- Terms ---

    [Fact]
    public void ASalaryRiseSmallerThanTwoPercentIsNudgingAndAStructuralChangeIsNot()
    {
        var baseline = Terms(100_000);

        Assert.False(Terms(101_999).IsMeaningfulChangeFrom(baseline));
        Assert.True(Terms(102_000).IsMeaningfulChangeFrom(baseline));
        Assert.False(Terms(90_000).IsMeaningfulChangeFrom(baseline));
        Assert.False(Terms(100_000).IsMeaningfulChangeFrom(baseline));
        Assert.True(Terms(100_000, SeatStatus.NumberOne).IsMeaningfulChangeFrom(baseline));
        Assert.True(Terms(100_000, years: 3).IsMeaningfulChangeFrom(baseline));
        Assert.True(Terms(100_000, winBonus: 1).IsMeaningfulChangeFrom(baseline));
        Assert.True(Terms(100_000, exit: new ExitClause(5)).IsMeaningfulChangeFrom(baseline));
        Assert.True(Terms(100_000, option: new OfferOption(OptionHolder.Team, 1)).IsMeaningfulChangeFrom(baseline));
    }

    [Fact]
    public void TermsRejectNonsenseAndFitTheirSubject()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Terms(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Terms(years: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Terms(years: NegotiationEstimates.MaxYears + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExitClause(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OfferOption(OptionHolder.Team, 0));

        Assert.True(Terms().Fits(NegotiationSubject.DriverSeat));
        Assert.False(Terms().Fits(NegotiationSubject.Staff(StaffRole.Strategist)));
        Assert.True(Terms(seat: null).Fits(NegotiationSubject.Staff(StaffRole.Strategist)));
        Assert.False(Terms(seat: null).Fits(NegotiationSubject.DriverSeat));
    }

    [Fact]
    public void AnExitClauseTriggersOnlyBelowItsPosition()
    {
        var clause = new ExitClause(5);

        Assert.False(clause.IsTriggeredBy(5));
        Assert.True(clause.IsTriggeredBy(6));
    }

    [Fact]
    public void SubjectsHaveStableKeysAndKnowWhoCanFillThem()
    {
        Assert.Equal("driver", NegotiationSubject.DriverSeat.Key);
        Assert.Equal("staff:Strategist", NegotiationSubject.Staff(StaffRole.Strategist).Key);
        Assert.Equal(NegotiationSubject.Staff(StaffRole.Strategist), NegotiationSubject.Parse("staff:Strategist"));
        Assert.Throws<ArgumentException>(() => NegotiationSubject.Parse("staff:Nobody"));
        Assert.Throws<ArgumentException>(() => NegotiationSubject.Parse("staff:strategist"));
        Assert.True(NegotiationSubject.DriverSeat.IsHeldBy([PersonRole.Driver]));
        Assert.False(NegotiationSubject.DriverSeat.IsHeldBy([PersonRole.TeamPrincipal]));
        Assert.True(NegotiationSubject.Staff(StaffRole.TeamPrincipal).IsHeldBy([PersonRole.TeamPrincipal]));
    }

    // --- Rounds, patience, nudging ---

    [Fact]
    public void PatienceRunsFromThreeToFiveRoundsAndFollowsProfessionalismAndTemperament()
    {
        PersonalityTraits Traits(int professionalism, int temperament) =>
            new(PrimaryPersonality.Loyal, 10, 10, temperament, professionalism, 10);

        Assert.Equal(4, NegotiationCore.MaxRounds(Traits(10, 10)));
        Assert.Equal(5, NegotiationCore.MaxRounds(Traits(20, 1)));
        Assert.Equal(3, NegotiationCore.MaxRounds(Traits(1, 20)));
        Assert.True(NegotiationCore.MaxRounds(Traits(16, 8)) >= NegotiationCore.MaxRounds(Traits(8, 16)));
        for (var professionalism = 1; professionalism <= 20; professionalism++)
        {
            for (var temperament = 1; temperament <= 20; temperament++)
            {
                var rounds = NegotiationCore.MaxRounds(Traits(professionalism, temperament));
                Assert.InRange(rounds, NegotiationEstimates.MinRounds, NegotiationEstimates.MaxRounds);
            }
        }
    }

    [Fact]
    public void EveryOfferUsesARoundAndOnlyANudgeCostsInterest()
    {
        var first = Negotiation.Start(1, "mgr-anna", TeamA, DriverX, NegotiationSubject.DriverSeat, null, Start, Start.AddDays(30), 3)
            .WithOffer(Terms(100_000), Start);
        Assert.Equal(1, first.RoundsUsed);
        Assert.Equal(NegotiationEstimates.InterestStart, first.Interest);
        Assert.Equal(NegotiationStatus.AwaitingResponse, first.Status);

        var countered = first.WithResponseScheduled(Start.AddDays(2)).WithCounter(Terms(120_000), [NegotiationReasons.SalaryTooLow], Start.AddDays(2));
        var nudge = countered.WithOffer(Terms(100_500), Start.AddDays(3));
        Assert.Equal(2, nudge.RoundsUsed);
        Assert.Equal(NegotiationEstimates.InterestStart - NegotiationEstimates.NudgePenalty, nudge.Interest);
        Assert.True(countered.WouldBeNudge(Terms(100_500)));
        Assert.False(countered.WouldBeNudge(Terms(100_500, SeatStatus.NumberOne)));

        var real = countered.WithOffer(Terms(100_500, SeatStatus.NumberOne), Start.AddDays(3));
        Assert.Equal(NegotiationEstimates.InterestStart, real.Interest);
        Assert.Equal(2, real.RoundsUsed);
        Assert.Equal([RoundKind.Offer, RoundKind.Counter, RoundKind.Offer], real.History.Select(round => round.Kind));
    }

    [Fact]
    public void RoundsRunOutAndAnIllegalMoveThrows()
    {
        var negotiation = Negotiation.Start(1, "mgr-anna", TeamA, DriverX, NegotiationSubject.DriverSeat, null, Start, Start.AddDays(30), 3);
        for (var round = 1; round <= 3; round++)
        {
            negotiation = negotiation.WithOffer(Terms(100_000 + (round * 10_000)), Start)
                .WithResponseScheduled(Start.AddDays(2))
                .WithCounter(Terms(500_000), [NegotiationReasons.SalaryTooLow], Start.AddDays(2));
        }

        Assert.Equal(0, negotiation.RoundsLeft);
        Assert.Throws<InvalidOperationException>(() => negotiation.WithOffer(Terms(), Start));
        Assert.Throws<InvalidOperationException>(() => Negotiation.Start(2, "m", TeamA, DriverX, NegotiationSubject.DriverSeat, null, Start, Start, 3).WithCounter(Terms(), [NegotiationReasons.Risk], Start));
        Assert.Throws<ArgumentException>(() => negotiation.WithLoss([], Start));
    }

    [Fact]
    public void AClosedNegotiationStaysClosed()
    {
        var closed = Negotiation.Start(1, "mgr-anna", TeamA, DriverX, NegotiationSubject.DriverSeat, null, Start, Start.AddDays(30), 3)
            .WithWalkAway(Start.AddDays(1));

        Assert.False(closed.IsActive);
        Assert.Equal(NegotiationStatus.WalkedAway, closed.Status);
        Assert.Equal(Start.AddDays(1), closed.ClosedOn);
        Assert.Throws<InvalidOperationException>(() => closed.WithWalkAway(Start.AddDays(2)));
        Assert.Throws<InvalidOperationException>(() => closed.WithOffer(Terms(), Start.AddDays(2)));
    }

    // --- The person's utility, DESIGN section 8 ---

    [Fact]
    public void UtilityIsPrestigePlusCarPlusSalaryPlusStatusMinusRisk()
    {
        // Balanced team player, every appeal at 0.5, pay at the reference, equal seat, two years. Weights (ESTIMATES):
        // prestige 0.25*0.9, car 0.25, salary 0.30, status 0.15*0.7, risk 0.20; perceived risk 0.5*(1+0.1).
        var evaluation = CounterpartyEvaluator.Evaluate(Terms(), Context());

        Assert.Equal(0.1125, evaluation.Contribution(UtilityTerms.Prestige), 9);
        Assert.Equal(0.125, evaluation.Contribution(UtilityTerms.Car), 9);
        Assert.Equal(0.0, evaluation.Contribution(UtilityTerms.Salary), 9);
        Assert.Equal(0.063, evaluation.Contribution(UtilityTerms.Status), 9);
        Assert.Equal(-0.11, evaluation.Contribution(UtilityTerms.Risk), 9);
        Assert.Equal(0.1905, evaluation.Utility, 9);
        Assert.Equal(evaluation.Utility, evaluation.Factors.Sum(factor => factor.Contribution), 12);
    }

    [Fact]
    public void MorePayBetterSeatStrongerTeamAndLessRiskAllRaiseUtility()
    {
        var baseline = CounterpartyEvaluator.Evaluate(Terms(), Context()).Utility;

        Assert.True(CounterpartyEvaluator.Evaluate(Terms(150_000), Context()).Utility > baseline);
        Assert.True(CounterpartyEvaluator.Evaluate(Terms(seat: SeatStatus.NumberOne), Context()).Utility > baseline);
        Assert.True(CounterpartyEvaluator.Evaluate(Terms(seat: SeatStatus.Reserve), Context()).Utility < baseline);
        Assert.True(CounterpartyEvaluator.Evaluate(Terms(), Context(new OrganizationAppeal(0.9, 0.5, 0.5))).Utility > baseline);
        Assert.True(CounterpartyEvaluator.Evaluate(Terms(), Context(new OrganizationAppeal(0.5, 0.9, 0.5))).Utility > baseline);
        Assert.True(CounterpartyEvaluator.Evaluate(Terms(), Context(new OrganizationAppeal(0.5, 0.5, 0.9))).Utility < baseline);
        Assert.True(CounterpartyEvaluator.Evaluate(Terms(exit: new ExitClause(5)), Context()).Utility > baseline);
        Assert.True(CounterpartyEvaluator.Evaluate(Terms(winBonus: 10_000), Context()).Utility > baseline);
    }

    [Fact]
    public void PaySaturatesAtTwiceTheReference()
    {
        var doubled = CounterpartyEvaluator.Evaluate(Terms(200_000), Context()).Utility;
        var tripled = CounterpartyEvaluator.Evaluate(Terms(300_000), Context()).Utility;

        Assert.Equal(doubled, tripled, 12);
    }

    [Fact]
    public void PersonalityWeightsTheTermsDifferently()
    {
        var weak = new OrganizationAppeal(0.1, 0.1, 0.5);
        var rich = Terms(180_000);
        PersonalityTraits Of(PrimaryPersonality primary) => new(primary, 10, 10, 10, 10, 10);

        var mercenary = CounterpartyEvaluator.Evaluate(rich, Context(weak, Of(PrimaryPersonality.Mercenary))).Utility;
        var prestige = CounterpartyEvaluator.Evaluate(rich, Context(weak, Of(PrimaryPersonality.Prestige))).Utility;
        Assert.True(mercenary > prestige);

        var strong = new OrganizationAppeal(0.9, 0.9, 0.5);
        var poor = Terms(60_000);
        Assert.True(
            CounterpartyEvaluator.Evaluate(poor, Context(strong, Of(PrimaryPersonality.Prestige))).Utility
            > CounterpartyEvaluator.Evaluate(poor, Context(strong, Of(PrimaryPersonality.Mercenary))).Utility);

        var risky = new OrganizationAppeal(0.5, 0.5, 0.9);
        Assert.True(
            CounterpartyEvaluator.Evaluate(Terms(), Context(risky, Of(PrimaryPersonality.SeeksSecurity))).Utility
            < CounterpartyEvaluator.Evaluate(Terms(), Context(risky, Of(PrimaryPersonality.ShortTerm))).Utility);
    }

    [Fact]
    public void HiddenTraitsShiftWeightsAndLoyaltyHelpsTheCurrentEmployer()
    {
        var ambitious = new PersonalityTraits(PrimaryPersonality.TeamPlayer, 10, 20, 10, 10, 10);
        var modest = new PersonalityTraits(PrimaryPersonality.TeamPlayer, 10, 1, 10, 10, 10);
        var strongCar = new OrganizationAppeal(0.5, 0.9, 0.5);

        Assert.True(
            CounterpartyEvaluator.Evaluate(Terms(), Context(strongCar, ambitious)).Utility
            > CounterpartyEvaluator.Evaluate(Terms(), Context(strongCar, modest)).Utility);

        var loyal = new PersonalityTraits(PrimaryPersonality.TeamPlayer, 20, 10, 10, 10, 10);
        var home = CounterpartyEvaluator.Evaluate(Terms(), Context(traits: loyal, currentEmployer: true));
        var away = CounterpartyEvaluator.Evaluate(Terms(), Context(traits: loyal));
        Assert.Equal(NegotiationEstimates.LoyaltyBonusScale, home.Utility - away.Utility, 9);
        Assert.Contains(home.Factors, factor => factor.Name == UtilityTerms.Loyalty);
        Assert.DoesNotContain(away.Factors, factor => factor.Name == UtilityTerms.Loyalty);
    }

    [Fact]
    public void KeyStaffHaveNoSeatStatusSoAFixedNeutralScoreStandsInForIt()
    {
        var staff = NegotiationSubject.Staff(StaffRole.Strategist);
        var evaluation = CounterpartyEvaluator.Evaluate(Terms(seat: null), Context(subject: staff));

        Assert.Equal(NegotiationEstimates.StatusScoreStaff, evaluation.Score(UtilityTerms.Status), 12);
        Assert.Throws<ArgumentException>(() => CounterpartyEvaluator.Evaluate(Terms(seat: null), Context()));
    }

    [Fact]
    public void RetirementGetsMoreAttractiveWithAgeAndRaisesTheFloor()
    {
        Assert.True(CounterpartyEvaluator.RetirementUtility(40) > CounterpartyEvaluator.RetirementUtility(31));
        Assert.Equal(CounterpartyEvaluator.RetirementUtility(30), CounterpartyEvaluator.RetirementUtility(22), 12);
        Assert.Equal(NegotiationEstimates.ReservationUtility, CounterpartyEvaluator.Floor(30, null), 12);
        Assert.True(CounterpartyEvaluator.Floor(45, null) > NegotiationEstimates.ReservationUtility);
        Assert.Equal(0.9, CounterpartyEvaluator.Floor(30, 0.9), 12);
        Assert.Equal(
            NegotiationEstimates.ReservationUtility,
            CounterpartyEvaluator.Floor(30, 0.01),
            12);
    }

    [Fact]
    public void LostInterestRaisesTheBarAnOfferHasToClear()
    {
        var full = CounterpartyEvaluator.Threshold(0.15, NegotiationEstimates.InterestStart);
        var half = CounterpartyEvaluator.Threshold(0.15, NegotiationEstimates.InterestStart / 2);
        var none = CounterpartyEvaluator.Threshold(0.15, 0);

        Assert.Equal(0.15 + NegotiationEstimates.AcceptanceMargin, full, 12);
        Assert.True(full < half && half < none);
        Assert.Equal(full + NegotiationEstimates.InterestMargin, none, 12);
    }

    // --- The person's answer ---

    [Fact]
    public void AnAcceptableOfferIsAcceptedAndNeedsNoReason()
    {
        var response = NegotiationCore.Respond(Pending(Terms()), Context(), CounterpartyEvaluator.Floor(30, null));

        Assert.Equal(ResponseKind.Accept, response.Kind);
        Assert.Null(response.Counter);
        Assert.Empty(response.Reasons);
    }

    [Fact]
    public void ALowOfferIsCounteredWithTermsThatWouldBeAccepted()
    {
        var context = Context();
        var floor = CounterpartyEvaluator.Floor(30, null);
        var response = NegotiationCore.Respond(Pending(Terms(50_000)), context, floor);

        Assert.Equal(ResponseKind.Counter, response.Kind);
        Assert.Equal([NegotiationReasons.SalaryTooLow], response.Reasons);
        Assert.NotNull(response.Counter);
        Assert.InRange(response.Counter!.Salary, 93_000, 94_000);
        Assert.True(CounterpartyEvaluator.Evaluate(response.Counter, context).Utility >= response.Threshold);
        Assert.Equal(Terms(50_000).Seat, response.Counter.Seat);
        Assert.Equal(2, response.Counter.Years);
    }

    [Fact]
    public void ACounterMayAskForABetterSeatWhenPayAloneIsNotEnough()
    {
        var weakAndRisky = new OrganizationAppeal(0.0, 0.0, 0.9);
        var response = NegotiationCore.Respond(Pending(Terms(100_000, SeatStatus.NumberTwo)), Context(weakAndRisky), CounterpartyEvaluator.Floor(30, null));

        Assert.Equal(ResponseKind.Counter, response.Kind);
        Assert.Equal(SeatStatus.NumberOne, response.Counter!.Seat);
        Assert.Contains(NegotiationReasons.TeamTooWeak, response.Reasons);
        Assert.True(CounterpartyEvaluator.Evaluate(response.Counter, Context(weakAndRisky)).Utility >= response.Threshold);
    }

    [Fact]
    public void ASecondDriverCannotAskForTheFirstSeatWhileAnotherDriverHoldsIt()
    {
        // #325: the same offer and the same weak team as above, but the first seat is taken, so the counter never names it.
        var weakAndRisky = new OrganizationAppeal(0.0, 0.0, 0.9);
        var floor = CounterpartyEvaluator.Floor(30, null);
        var taken = Context(weakAndRisky, firstSeatTaken: true);
        var response = NegotiationCore.Respond(Pending(Terms(100_000, SeatStatus.NumberTwo)), taken, floor);

        Assert.NotEqual(SeatStatus.NumberOne, response.Counter?.Seat);
        Assert.NotEqual(
            SeatStatus.NumberOne,
            CounterpartyEvaluator.TryCounter(Terms(100_000, SeatStatus.Equal), taken, floor + 0.5)?.Seat);

        // The first driver himself may still ask for it on renewal: nobody else holds it.
        var free = Context(weakAndRisky, firstSeatTaken: false);
        Assert.Equal(SeatStatus.NumberOne, NegotiationCore.Respond(Pending(Terms(100_000, SeatStatus.NumberTwo)), free, floor).Counter!.Seat);
    }

    [Fact]
    public void WhenNoSalaryCanMakeUpForTheTeamThePersonRefusesAndSaysWhy()
    {
        var hopeless = new OrganizationAppeal(0.0, 0.0, 1.0);
        var traits = new PersonalityTraits(PrimaryPersonality.SeeksSecurity, 10, 10, 10, 10, 10);
        var response = NegotiationCore.Respond(Pending(Terms(100_000)), Context(hopeless, traits), CounterpartyEvaluator.Floor(30, null));

        Assert.Equal(ResponseKind.Refuse, response.Kind);
        Assert.Null(response.Counter);
        Assert.NotEmpty(response.Reasons);
        Assert.All(response.Reasons, reason => Assert.Contains(reason, NegotiationReasons.All));
    }

    [Fact]
    public void EveryShortfallNamesAtLeastOneReason()
    {
        var appeals = new[]
        {
            OrganizationAppeal.Neutral,
            new OrganizationAppeal(0, 0, 0),
            new OrganizationAppeal(1, 1, 1),
            new OrganizationAppeal(0.2, 0.9, 0.1),
            new OrganizationAppeal(0.9, 0.2, 0.9),
        };
        var floors = new[] { 0.0, 0.15, 0.4, 0.9 };
        foreach (var primary in Enum.GetValues<PrimaryPersonality>())
        {
            foreach (var appeal in appeals)
            {
                foreach (var salary in new long[] { 0, 30_000, 100_000, 400_000 })
                {
                    foreach (var floor in floors)
                    {
                        var traits = new PersonalityTraits(primary, 12, 8, 14, 6, 11);
                        var response = NegotiationCore.Respond(Pending(Terms(salary, SeatStatus.NumberTwo)), Context(appeal, traits), floor);
                        if (response.Kind != ResponseKind.Accept)
                        {
                            Assert.NotEmpty(response.Reasons);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void ANudgeIsNamedAndAPersonWithNoInterestLeftWalksAway()
    {
        var first = Negotiation.Start(1, "mgr-anna", TeamA, DriverX, NegotiationSubject.DriverSeat, null, Start, Start.AddDays(30), 5)
            .WithOffer(Terms(50_000), Start)
            .WithResponseScheduled(Start.AddDays(2))
            .WithCounter(Terms(93_167), [NegotiationReasons.SalaryTooLow], Start.AddDays(2));
        var nudged = first.WithOffer(Terms(50_100), Start.AddDays(3));

        Assert.True(NegotiationCore.LastOfferWasNudge(nudged));
        var response = NegotiationCore.Respond(nudged, Context(), CounterpartyEvaluator.Floor(30, null));
        Assert.Equal(NegotiationReasons.NoRealChange, response.Reasons[0]);
        Assert.False(NegotiationCore.LastOfferWasNudge(first));

        var exhausted = Pending(Terms(100_000), 4, interest: 0);
        var refusal = NegotiationCore.Respond(exhausted, Context(), CounterpartyEvaluator.Floor(30, null));
        Assert.Equal(ResponseKind.Refuse, refusal.Kind);
        Assert.Equal([NegotiationReasons.NoRealChange, NegotiationReasons.PatienceLost], refusal.Reasons);
    }

    // --- Rivals ---

    private static AcceptableOffer Rival(long number, double utility, OrganizationId proposer, int deadlineDays = 30)
    {
        var negotiation = Negotiation.Start(number, "mgr", proposer, DriverX, NegotiationSubject.DriverSeat, null, Start, Start.AddDays(deadlineDays), 4)
            .WithOffer(Terms(), Start);
        return new AcceptableOffer(negotiation, utility);
    }

    private sealed class LiftedTrust(Dictionary<string, double> trust) : ITrustSource
    {
        public double Trust(PersonId person, OrganizationId organization) => trust.GetValueOrDefault(organization.Value, NeutralTrustSource.Neutral);
    }

    [Fact]
    public void TheBestUtilityWinsAmongAcceptableOffers()
    {
        var winner = NegotiationCore.ChooseWinner(
            [Rival(1, 0.30, TeamA), Rival(2, 0.45, TeamB), Rival(3, 0.20, TeamC)],
            DriverX,
            new NeutralTrustSource(),
            _ => throw new InvalidOperationException("No draw is needed without a tie."));

        Assert.Equal("neg:2", winner.Negotiation.Id);
    }

    [Fact]
    public void ATieGoesToTheOrganizationTheMoreTrusted()
    {
        var trust = new LiftedTrust(new Dictionary<string, double> { [TeamC.Value] = 80 });

        var winner = NegotiationCore.ChooseWinner(
            [Rival(1, 0.40, TeamA), Rival(2, 0.4005, TeamB), Rival(3, 0.40, TeamC)],
            DriverX,
            trust,
            _ => throw new InvalidOperationException("Trust breaks this tie, no draw."));

        Assert.Equal("neg:3", winner.Negotiation.Id);
    }

    [Fact]
    public void AnEqualTrustLeavesTheTieToTheMarketDrawKeyedByNegotiation()
    {
        var asked = new List<string>();
        var winner = NegotiationCore.ChooseWinner(
            [Rival(1, 0.40, TeamA), Rival(2, 0.40, TeamB)],
            DriverX,
            new NeutralTrustSource(),
            id =>
            {
                asked.Add(id);
                return id == "neg:2" ? 0.9 : 0.1;
            });

        Assert.Equal("neg:2", winner.Negotiation.Id);
        Assert.Equal(["neg:1", "neg:2"], asked);
    }

    [Fact]
    public void ThePersonDecidesAtTheEarliestDeadlineAmongTheAcceptableOffers()
    {
        var day = NegotiationCore.DecisionDay([Rival(1, 0.4, TeamA, 40), Rival(2, 0.3, TeamB, 12), Rival(3, 0.2, TeamC, 25)]);

        Assert.Equal(Start.AddDays(12), day);
        Assert.Throws<ArgumentException>(() => NegotiationCore.DecisionDay([]));
    }

    // --- What a team can know ---

    private static PersonKnowledgeView Belief(int low, int high) => new(new PersonKnowledge(
        TeamA,
        DriverX,
        GenerationEstimates.DriverAttributeKeys.Select(key => new KnownAttribute(key, new AttributeBand(low, high))).ToArray(),
        null));

    [Fact]
    public void TheReferenceOfferDependsOnlyOnTheBandsTheTeamBelieves()
    {
        var sameBands = ReferenceOffer.Suggest(NegotiationSubject.DriverSeat, Belief(10, 14), 1955, new CliffPay());

        Assert.Equal(sameBands, ReferenceOffer.Suggest(NegotiationSubject.DriverSeat, Belief(10, 14), 1955, new CliffPay()));
        Assert.True(ReferenceOffer.Suggest(NegotiationSubject.DriverSeat, Belief(16, 20), 1955, new CliffPay()).Salary > sameBands.Salary);
        Assert.Equal(NegotiationEstimates.UnknownStars, ReferenceOffer.Stars(NegotiationSubject.DriverSeat, null));
        Assert.Equal(3.0, ReferenceOffer.Stars(NegotiationSubject.DriverSeat, Belief(11, 13)), 12);
        Assert.Null(ReferenceOffer.Suggest(NegotiationSubject.Staff(StaffRole.Strategist), null, 1955, new CliffPay()).Seat);
    }

    private sealed class CliffPay : IPayBenchmark
    {
        public long Reference(int season, NegotiationSubject subject, double stars) => (long)(stars * 10_000);
    }

    [Fact]
    public void TheEraBenchmarkInterpolatesBetweenMidfieldAndTopAndPaysStaffAShare()
    {
        var pay = new EraPayBenchmark(season => EraSet.For(
            season,
            [EraPayBenchmark.MidfieldDimension, EraPayBenchmark.TopDimension],
            [
                new RulePeriod(EraPayBenchmark.MidfieldDimension, "5000", 1950, 1957),
                new RulePeriod(EraPayBenchmark.TopDimension, "25000", 1950, 1957),
            ]));
        var driver = NegotiationSubject.DriverSeat;

        Assert.Equal(5_000, pay.Reference(1955, driver, 2.5));
        Assert.Equal(25_000, pay.Reference(1955, driver, 5.0));
        Assert.Equal(15_000, pay.Reference(1955, driver, 3.75));
        Assert.Equal(1_000, pay.Reference(1955, driver, 0.0));
        Assert.Equal(25_000, pay.Reference(1955, driver, 9.0));
        Assert.Equal(1_000, pay.Reference(1955, NegotiationSubject.Staff(StaffRole.Strategist), 2.5));
        Assert.Throws<InvalidOperationException>(() => pay.Reference(1960, driver, 2.5));
    }

    [Fact]
    public void DerivedPersonalityIsAPureFunctionOfTheSeedAndThePerson()
    {
        var source = new DerivedPersonalitySource(42);

        Assert.Equal(source.TraitsOf(DriverX), new DerivedPersonalitySource(42).TraitsOf(DriverX));
        Assert.Equal(source.TraitsOf(DriverX), source.TraitsOf(DriverX));
        Assert.True(Enumerable.Range(0, 20).Select(i => new DerivedPersonalitySource((ulong)i).TraitsOf(DriverX)).Distinct().Count() > 1);
    }

    // --- The section ---

    [Fact]
    public void NegotiationsGetStableIdsAndAreHashedWithTheWorld()
    {
        var (section, first) = ContractsSection.Empty.Open("mgr-anna", TeamA, DriverX, NegotiationSubject.DriverSeat, null, Start, Start.AddDays(30), 4);
        (section, var second) = section.Open("mgr-bram", TeamB, DriverY, NegotiationSubject.DriverSeat, null, Start, Start.AddDays(30), 4);

        Assert.Equal(["neg:1", "neg:2"], new[] { first.Id, second.Id });
        Assert.Same(first, section.Find("neg:1"));
        Assert.Null(section.Find("neg:3"));
        Assert.Null(section.Find("neg:01"));
        Assert.Equal(3, section.NextNegotiation);

        var world = WorldState.At(Start);
        Assert.Equal(world.WithSection(section).StateHash(), WorldState.At(Start).WithSection(section).StateHash());
        Assert.NotEqual(world.StateHash(), world.WithSection(section).StateHash());
        Assert.True(ContractsSection.Empty.IsEmpty);
        Assert.False(section.IsEmpty);
    }

    [Fact]
    public void TheHashSeesEveryFieldOfANegotiationAndOfTheTerms()
    {
        string Hash(Func<ContractsSection, ContractsSection> change) =>
            WorldState.At(Start).WithSection(change(ContractsSection.Empty.Open("m", TeamA, DriverX, NegotiationSubject.DriverSeat, null, Start, Start.AddDays(30), 4).Section)).StateHash();

        var baseline = Hash(section => section);
        Assert.Equal(baseline, Hash(section => section));
        Assert.NotEqual(baseline, Hash(section => section.Replace(section.Find("neg:1")!.WithOffer(Terms(), Start))));
        Assert.NotEqual(
            Hash(section => section.Replace(section.Find("neg:1")!.WithOffer(Terms(100_000), Start))),
            Hash(section => section.Replace(section.Find("neg:1")!.WithOffer(Terms(100_001), Start))));
        Assert.NotEqual(
            Hash(section => section.Replace(section.Find("neg:1")!.WithOffer(Terms(exit: new ExitClause(4)), Start))),
            Hash(section => section.Replace(section.Find("neg:1")!.WithOffer(Terms(exit: new ExitClause(5)), Start))));
        Assert.NotEqual(baseline, Hash(section => section.WithTerms(new ContractTerms(ContractId.Generated(1), 0, 0, 0, null, null))));
        Assert.NotEqual(
            Hash(section => section.WithTerms(new ContractTerms(ContractId.Generated(1), 1, 0, 0, null, null))),
            Hash(section => section.WithTerms(new ContractTerms(ContractId.Generated(1), 0, 1, 0, null, null))));
        Assert.NotEqual(
            Hash(section => section.WithTerms(new ContractTerms(ContractId.Generated(1), 0, 0, 0, OptionHolder.Team, null))),
            Hash(section => section.WithTerms(new ContractTerms(ContractId.Generated(1), 0, 0, 0, OptionHolder.Person, null))));
        Assert.NotEqual(baseline, Hash(section => section.WithRenewalPrompt(ContractId.Generated(1))));
    }

    [Fact]
    public void RestoreChecksTheCounterAndDuplicates()
    {
        var (section, negotiation) = ContractsSection.Empty.Open("m", TeamA, DriverX, NegotiationSubject.DriverSeat, null, Start, Start.AddDays(30), 4);

        Assert.Throws<InvalidOperationException>(() => ContractsSection.Restore(1, [], [negotiation], []));
        Assert.Throws<InvalidOperationException>(() => ContractsSection.Restore(2, [], [negotiation, negotiation], []));
        Assert.Throws<InvalidOperationException>(() => ContractsSection.Restore(
            2,
            [new ContractTerms(ContractId.Generated(1), 0, 0, 0, null, null), new ContractTerms(ContractId.Generated(1), 1, 0, 0, null, null)],
            [negotiation],
            []));
        Assert.Throws<InvalidOperationException>(() => section.Replace(Negotiation.Start(9, "m", TeamA, DriverX, NegotiationSubject.DriverSeat, null, Start, Start, 4)));
    }

    [Fact]
    public void PruningDropsTheTermsOfContractsThatNoLongerExist()
    {
        var section = ContractsSection.Empty
            .WithTerms(new ContractTerms(ContractId.Generated(1), 1, 0, 0, null, null))
            .WithTerms(new ContractTerms(ContractId.Generated(2), 2, 0, 0, null, null))
            .WithRenewalPrompt(ContractId.Generated(1))
            .WithRenewalPrompt(ContractId.Generated(2));

        var pruned = section.PruneTo(id => id == ContractId.Generated(2));

        Assert.Equal([ContractId.Generated(2)], pruned.Terms.Select(terms => terms.ContractId));
        Assert.Equal([ContractId.Generated(2)], pruned.PromptedRenewals);
        Assert.Same(pruned, pruned.PruneTo(_ => true));
    }

    [Fact]
    public void TheReasonKeysOfTheDomainAndOfTheTranslationListAreTheSame()
    {
        Assert.Equal(NegotiationReasons.All.Order(StringComparer.Ordinal), ContractKeys.ReasonKeys.Order(StringComparer.Ordinal));
        Assert.All(NegotiationReasons.All, key => Assert.StartsWith("negotiation.reason.", key, StringComparison.Ordinal));
    }
}
