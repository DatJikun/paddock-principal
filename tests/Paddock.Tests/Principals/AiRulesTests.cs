using Paddock.Domain.Random;
using Paddock.Domain.Spy;
using Paddock.Simulation.Ai;

namespace Paddock.Tests.Principals;

/// <summary>The pure rules of <c>Paddock.Simulation.Ai</c>. Every number here is an ESTIMATE of the module under test, not a calibrated value.</summary>
public class AiRulesTests
{
    private static readonly DateOnly Today = new(1956, 2, 1);

    private static DecisionContext Context(PrincipalArchetype archetype, PrincipalSkillset? skills = null, ITraceSink? sink = null, IHistoricalBias? bias = null, string team = "team") =>
        new(team, "ai:" + team, Today, archetype, skills ?? new PrincipalSkillset(4, 4, 4, 4), AiRandom.ForSeason(1UL, 1956), bias, sink);

    private static PersonView Person(string id, double low, double high, int age = 27) =>
        new(id, age, new Believed(low, high), new Believed(low, Math.Min(5, high + 0.5)));

    private static AiFunds Rich => new(500_000, 15_000, 10_000, 60_000, 20_000);

    // ---------------------------------------------------------------- level

    [Fact]
    public void BelievedAttributesGiveFourTiersAndAnUnknownPrincipalIsAverage()
    {
        PrincipalAttributes Bands(int value) => new(new BandOfBelief(value, value), new BandOfBelief(value, value), new BandOfBelief(value, value), new BandOfBelief(value, value));

        Assert.Equal(1, PrincipalLevels.From(Bands(5)).Level);
        Assert.Equal(2, PrincipalLevels.From(Bands(10)).Level);
        Assert.Equal(3, PrincipalLevels.From(Bands(13)).Level);
        Assert.Equal(4, PrincipalLevels.From(Bands(18)).Level);
        Assert.Equal(2, PrincipalLevels.From(PrincipalAttributes.Unknown).Level);
        Assert.Equal(2, PrincipalLevels.From(null).Level);
        Assert.Equal(AiEstimates.LevelCount, PrincipalLevels.From(Bands(20)).Level);
    }

    [Fact]
    public void AWeakPrincipalMisjudgesMoreThanAStrongOne()
    {
        double Spread(int tier)
        {
            var context = Context(PrincipalArchetype.Builder, new PrincipalSkillset(tier, tier, tier, tier));
            return Enumerable.Range(0, 400).Average(i => Math.Abs(context.Noise(DecisionFacet.Market, "d" + i, "option")));
        }

        Assert.True(Spread(1) > Spread(2));
        Assert.True(Spread(2) > Spread(3));
        Assert.True(Spread(3) > Spread(4));
    }

    // ---------------------------------------------------------------- the chooser

    [Fact]
    public void TheChoiceIsTheSameWithAndWithoutASinkAndTheTraceHidesNoiseAndHistoryFromTheViewer()
    {
        var sink = new RecordingSink();
        OptionDraft[] options =
        [
            new("a", [new FactorDraft("ai.factor.quality", 0.20), new FactorDraft("ai.factor.style", 0.5, PlayerVisible: false)]),
            new("b", [new FactorDraft("ai.factor.quality", 0.25)]),
        ];
        var note = new TraceNote("test.kind", "key", "trigger", "reason", AiTextKeys.ReasonSplit, IsKeyDecision: true);
        var quiet = UtilityChooser.Choose(Context(PrincipalArchetype.Contender), DecisionFacet.Market, note, options);
        var loud = UtilityChooser.Choose(Context(PrincipalArchetype.Contender, sink: sink), DecisionFacet.Market, note, options);

        Assert.Equal(quiet.Id, loud.Id);
        Assert.Equal(quiet.Utilities, loud.Utilities);
        var trace = Assert.Single(sink.Traces);
        Assert.True(trace.IsKeyDecision);
        Assert.Contains(trace.Options[0].Factors, factor => factor.Name == AiTextKeys.FactorNoise && !factor.PlayerVisible);
        Assert.Contains(trace.Options[0].Factors, factor => factor.Name == AiTextKeys.FactorHistory && !factor.PlayerVisible);
        Assert.Equal(loud.Id, trace.ChosenOptionId);
    }

    [Fact]
    public void ATieIsBrokenByTheAiDecisionsStreamAndNotByTheOrderOfTheOptions()
    {
        OptionDraft[] forward = [new("a", [new FactorDraft("f", 1.0)]), new("b", [new FactorDraft("f", 1.0)])];
        OptionDraft[] backward = [forward[1], forward[0]];
        var skills = new PrincipalSkillset(4, 4, 4, 4);
        var chosen = new HashSet<string>();
        for (var team = 0; team < 30; team++)
        {
            var context = Context(PrincipalArchetype.Builder, skills, team: "team" + team);
            var note = new TraceNote("tie", "k", "t", "r", null, IsKeyDecision: false);
            var one = UtilityChooser.Choose(context, DecisionFacet.Market, note, forward).Id;
            var two = UtilityChooser.Choose(context, DecisionFacet.Market, note, backward).Id;
            chosen.Add(one);

            // Noise is keyed by the option, so the order does not matter unless an exact tie is left.
            Assert.Equal(one, two);
        }

        Assert.True(chosen.Count >= 1);
    }

    private sealed class PullTo(string option, double amount) : IHistoricalBias
    {
        public double Adjustment(string organization, string decisionKind, string optionId, DateOnly today) => optionId == option ? amount : 0.0;
    }

    [Fact]
    public void TheHistoricalBiasHookMovesAChoiceAndIsNeutralByDefault()
    {
        Assert.Equal(0.0, NeutralHistoricalBias.Instance.Adjustment("x", "k", "o", Today));
        OptionDraft[] options = [new("a", [new FactorDraft("f", 1.0)]), new("b", [new FactorDraft("f", 0.0)])];
        var note = new TraceNote("bias", "k", "t", "r", null, IsKeyDecision: false);
        Assert.Equal("a", UtilityChooser.Choose(Context(PrincipalArchetype.Builder), DecisionFacet.Market, note, options).Id);
        Assert.Equal("b", UtilityChooser.Choose(Context(PrincipalArchetype.Builder, bias: new PullTo("b", 50.0)), DecisionFacet.Market, note, options).Id);
    }

    // ---------------------------------------------------------------- the market

    private static MarketInput Input(
        AiFunds funds,
        IReadOnlyList<Incumbent>? incumbents = null,
        IReadOnlyList<Candidate>? candidates = null,
        IReadOnlyList<Talk>? talks = null,
        IReadOnlyList<Vacancy>? vacancies = null,
        int slots = 3) =>
        new(Today, funds, incumbents ?? [], candidates ?? [], talks ?? [], vacancies ?? [], [], slots);

    [Fact]
    public void ACandidateTheTeamCannotAffordIsNeverApproachedAndNoMoneyMeansNoOffer()
    {
        var candidate = new Candidate(Person("rich", 4.0, 4.5), "driver", 80_000);
        var vacancy = new Vacancy("driver", 1, 60);
        var broke = new AiFunds(-14_000, 15_000, 10_000, 60_000, 20_000);

        Assert.Empty(MarketDecider.Review(Context(PrincipalArchetype.Contender), Input(broke, candidates: [candidate], vacancies: [vacancy])));
        Assert.Single(MarketDecider.Review(Context(PrincipalArchetype.Contender), Input(Rich, candidates: [candidate], vacancies: [vacancy])));

        var renewal = new Incumbent("con:1", Person("old", 3.0, 3.5), "driver", AiSeats.Equal, new DateOnly(1956, 6, 30), 5_000, 5_000, false, false);
        Assert.DoesNotContain(
            MarketDecider.Review(Context(PrincipalArchetype.Contender), Input(broke, incumbents: [renewal])),
            action => action is MarketAction.Renew);
    }

    [Fact]
    public void ADriverSeatIsFilledOnceTheVacancyHasStoodTooLongEvenWhenNoCandidateLooksGood()
    {
        var weak = new Candidate(Person("meh", 0.5, 1.0, age: 41), "driver", 6_000);
        var fresh = MarketDecider.Review(Context(PrincipalArchetype.Contender), Input(Rich, candidates: [weak], vacancies: [new Vacancy("driver", 1, 0)]));
        var stale = MarketDecider.Review(Context(PrincipalArchetype.Contender), Input(Rich, candidates: [weak], vacancies: [new Vacancy("driver", 1, AiEstimates.MaxVacancyDays)]));

        Assert.Empty(fresh);
        Assert.IsType<MarketAction.Approach>(Assert.Single(stale));
    }

    [Fact]
    public void ASurvivorTakesTheCheaperDriverAndAContenderTheBetterOne()
    {
        var star = new Candidate(Person("star", 4.2, 4.6), "driver", 24_000);
        var cheap = new Candidate(Person("cheap", 2.4, 2.8), "driver", 3_000);
        var vacancy = new Vacancy("driver", 1, 60);
        string Pick(PrincipalArchetype archetype)
        {
            var action = Assert.IsType<MarketAction.Approach>(Assert.Single(MarketDecider.Review(Context(archetype), Input(Rich, candidates: [star, cheap], vacancies: [vacancy]))));
            return action.PersonId;
        }

        Assert.Equal("star", Pick(PrincipalArchetype.Contender));
        Assert.Equal("cheap", Pick(PrincipalArchetype.Survivor));
    }

    [Fact]
    public void ABuilderValuesYouthAndPotentialAndSignsItLonger()
    {
        var young = new PersonView("kid", 20, new Believed(2.4, 2.8), new Believed(4.0, 4.8));
        var old = new PersonView("vet", 36, new Believed(2.8, 3.0), new Believed(2.8, 3.0));
        var builder = Context(PrincipalArchetype.Builder);
        Assert.Equal(AiEstimates.LongContractYears, MarketDecider.Years(builder, young, "driver"));
        Assert.Equal(1, MarketDecider.Years(Context(PrincipalArchetype.Survivor), old, "driver"));

        // A level-1 principal does not plan beyond two seasons.
        Assert.True(MarketDecider.Years(Context(PrincipalArchetype.Builder, new PrincipalSkillset(1, 1, 1, 1)), young, "driver") <= AiEstimates.ShortContractYears);
        var kid = new Candidate(young, "driver", 4_000);
        var vet = new Candidate(old, "driver", 4_000);
        var action = Assert.IsType<MarketAction.Approach>(Assert.Single(MarketDecider.Review(builder, Input(Rich, candidates: [kid, vet], vacancies: [new Vacancy("driver", 1, 60)]))));
        Assert.Equal("kid", action.PersonId);
    }

    [Fact]
    public void ACounterWithinTheCeilingIsAcceptedAndOneFarAboveItIsNot()
    {
        Talk Counter(long counter) => new("neg:1", Person("p", 3.0, 3.4), "driver", TalkState.Countered, 4_000, 2, counter, 2, AiSeats.Equal, 3, new DateOnly(1956, 3, 1), false, 5_000, 0);
        var context = Context(PrincipalArchetype.Builder, new PrincipalSkillset(4, 4, 4, 4));

        var fair = MarketDecider.Review(context, Input(Rich, talks: [Counter(5_200)]));
        Assert.IsType<MarketAction.Accept>(Assert.Single(fair));
        var greedy = MarketDecider.Review(context, Input(Rich, talks: [Counter(40_000)]));
        Assert.IsType<MarketAction.WalkAway>(Assert.Single(greedy));
    }

    [Fact]
    public void ARenewalPromptTurnsIntoARenewalForAValuedPersonAndAnExpiryForAWeakOne()
    {
        var good = new Incumbent("con:1", Person("good", 3.8, 4.2), "driver", AiSeats.Equal, new DateOnly(1956, 6, 30), 6_000, 8_000, false, false);
        var poor = new Incumbent("con:2", Person("poor", 0.8, 1.2, age: 40), "driver", AiSeats.Equal, new DateOnly(1956, 6, 30), 6_000, 1_500, false, false);
        var better = new Candidate(Person("better", 3.5, 3.9), "driver", 5_000);
        var actions = MarketDecider.Review(Context(PrincipalArchetype.Contender), Input(Rich, incumbents: [good, poor], candidates: [better]));

        Assert.Contains(actions, action => action is MarketAction.Renew { ContractId: "con:1" });
        Assert.Contains(actions, action => action is MarketAction.LetExpire { ContractId: "con:2" });
    }

    // ---------------------------------------------------------------- development

    private static DevelopmentInput Development(int? last, int? previous, double nextRules, DevelopmentPlanView? plan = null) =>
        new(
            new DateOnly(1956, 3, 1),
            plan ?? new DevelopmentPlanView(60, 20, 20, 5, 5, 5, 5),
            50, 40, 60, 55,
            last,
            previous,
            last,
            10,
            nextRules,
            0,
            false,
            []);

    [Fact]
    public void AContenderNeverWritesTheSeasonOffAndAPlannerDoesWhenTheRulesChangeABit()
    {
        var input = Development(last: 8, previous: 8, nextRules: 0.6);
        var builder = DevelopmentDecider.Review(Context(PrincipalArchetype.Builder), input);
        var contender = DevelopmentDecider.Review(Context(PrincipalArchetype.Contender), input);

        Assert.True(builder.Sacrifice);
        Assert.False(contender.Sacrifice);
        Assert.NotNull(contender.Plan);
        Assert.True(contender.Plan!.Current >= 60);
        Assert.True(builder.Plan!.Current <= 10);
    }

    [Fact]
    public void ALevelOnePrincipalDoesNotPlanAheadOrReadTheCar()
    {
        var weak = Context(PrincipalArchetype.Builder, new PrincipalSkillset(1, 1, 1, 1));
        var outcome = DevelopmentDecider.Review(weak, Development(last: 8, previous: 8, nextRules: 0.9));
        Assert.False(outcome.Sacrifice);
        Assert.True(outcome.Plan is null || (outcome.Plan.Aero == 5 && outcome.Plan.Chassis == 5 && outcome.Plan.Reliability == 5 && outcome.Plan.Tyres == 5));
    }

    [Fact]
    public void PrioritiesGoToTheWeakestAreasFirst()
    {
        var outcome = DevelopmentDecider.Review(Context(PrincipalArchetype.Contender), Development(last: 3, previous: 4, nextRules: 0.0));
        Assert.NotNull(outcome.Plan);
        Assert.True(outcome.Plan!.Chassis > outcome.Plan.Reliability);
        Assert.True(outcome.Plan.Reliability > outcome.Plan.Tyres || outcome.Plan.Aero >= outcome.Plan.Tyres);
        Assert.Equal(100, outcome.Plan.Current + outcome.Plan.Account + outcome.Plan.NextYear);
    }

    [Fact]
    public void AReadyConceptIsHeldForNextSeasonByAPlannerLateInTheSeason()
    {
        var late = Development(last: 5, previous: 5, nextRules: 0.0) with { Today = new DateOnly(1956, 11, 20), Concepts = [new ConceptCase("dev:1", Ready: true, CurrentTiming: "WhenReady", ExpectedGainMid: 9)] };
        var outcome = DevelopmentDecider.Review(Context(PrincipalArchetype.Builder), late);
        var timing = Assert.Single(outcome.Timings);
        Assert.Equal("NextSeason", timing.Timing);
    }

    // ---------------------------------------------------------------- supply

    [Fact]
    public void ASupplyNeedGetsAProposalAtOrAboveTheFloorAndAnUnaffordableOneGetsNone()
    {
        var quotes = new[]
        {
            new SupplyQuote("supplier:a", "Customer", 15_000_00, 1, 0.5),
            new SupplyQuote("supplier:b", "Customer", 15_000_00, 1, -0.5),
        };
        var need = new SupplyNeed("Engine", 1956, null, quotes);
        SupplyInput Input(long headroom) => new(Today, 60_000_00, headroom, 20, 80, [need], []);

        var action = Assert.IsType<SupplyAction.Propose>(Assert.Single(SupplyDecider.Review(Context(PrincipalArchetype.Contender), Input(900_000_00))));
        Assert.Equal("supplier:a", action.SupplierId);
        Assert.True(action.PriceCents >= (long)(15_000_00 * (1000 - Math.Min(80, (action.Seasons - 1) * 20)) / 1000.0));
        Assert.Empty(SupplyDecider.Review(Context(PrincipalArchetype.Contender), Input(100)));
    }

    [Fact]
    public void ASupplyCounterIsAcceptedWhenItFitsAndDeclinedWhenItDoesNot()
    {
        var talk = new SupplyTalk("sneg:1", "supplier:a", "Engine", "Customer", 1956, SupplyTalkState.Countered, 14_000_00, 15_000_00, 0, 2, 1, 0.2, null);
        SupplyInput Input(long headroom) => new(Today, 60_000_00, headroom, 20, 80, [], [talk]);

        Assert.True(Assert.IsType<SupplyAction.Respond>(Assert.Single(SupplyDecider.Review(Context(PrincipalArchetype.Contender), Input(900_000_00)))).Accept);
        Assert.False(Assert.IsType<SupplyAction.Respond>(Assert.Single(SupplyDecider.Review(Context(PrincipalArchetype.Contender), Input(100)))).Accept);
    }

    // ---------------------------------------------------------------- sponsors, scouting

    [Fact]
    public void SponsorsAreChosenForEmptySlotsAndAnOfferAtHalfThePriceIsDeclined()
    {
        var input = new SponsorInput(
            Today,
            60_000_00,
            [new SponsorSlotCase(1, [new SponsorCandidate("s1", 5_000_00), new SponsorCandidate("s2", 9_000_00)])],
            [new SponsorTalkCase("talk:1", "s3", 6_000_00, 6_000_00)],
            [new SponsorOfferCase("offer:1", "S4", 2_000_00, 9_000_00)]);
        var actions = SponsorDecider.Review(Context(PrincipalArchetype.Survivor), input);

        Assert.Equal("s2", actions.OfType<SponsorAction.BeginTalks>().Single().SponsorId);
        Assert.Single(actions.OfType<SponsorAction.SignAtCurrentTerms>());
        Assert.False(actions.OfType<SponsorAction.RespondToOffer>().Single().Accept);
    }

    [Fact]
    public void ABuilderScoutsThePoolAndASurvivorDoesNotAndNobodyDoesItTwiceInASeason()
    {
        var input = new ScoutingInput(Today, 0, 16);
        Assert.True(ScoutingDecider.Review(Context(PrincipalArchetype.Builder), input));
        Assert.False(ScoutingDecider.Review(Context(PrincipalArchetype.Survivor), input));
        Assert.False(ScoutingDecider.Review(Context(PrincipalArchetype.Builder), input with { ScoutSeason = 1956 }));
        Assert.False(ScoutingDecider.Review(Context(PrincipalArchetype.Builder), input with { PoolSize = 0 }));
    }

    // ---------------------------------------------------------------- archetype, schedule

    [Fact]
    public void ArchetypesFollowTierAndResultsAndAreDeterministic()
    {
        var stream = AiRandom.ForSeason(1UL, 1955);
        var rich = new ArchetypeInput(0.95, 45, [1, 2, 1], 10);
        var poor = new ArchetypeInput(0.05, 60, [10, 9, 10], 10);

        Assert.Equal(ArchetypeAssignment.Assign(stream, "team", rich), ArchetypeAssignment.Assign(stream, "team", rich));
        var richScores = ArchetypeAssignment.Scores(rich);
        var poorScores = ArchetypeAssignment.Scores(poor);
        Assert.True(richScores[(int)PrincipalArchetype.Contender] > richScores[(int)PrincipalArchetype.Survivor]);
        Assert.True(poorScores[(int)PrincipalArchetype.Survivor] > poorScores[(int)PrincipalArchetype.Contender]);

        // Across many teams the noise lets the rule vary but never invert it on average.
        var contenders = Enumerable.Range(0, 100).Count(i => ArchetypeAssignment.Assign(stream, "t" + i, rich) == PrincipalArchetype.Contender);
        var survivors = Enumerable.Range(0, 100).Count(i => ArchetypeAssignment.Assign(stream, "t" + i, poor) == PrincipalArchetype.Survivor);
        Assert.True(contenders > 50);
        Assert.True(survivors > 50);
    }

    [Fact]
    public void AQuietTeamIsLookedAtEveryMonthAndEventsPullTheReviewForward()
    {
        var today = new DateOnly(1956, 3, 10);
        Assert.Equal(today.AddDays(AiEstimates.ScheduledReviewDays), ReviewSchedule.Next(today, false, false, false, []));
        Assert.Equal(new DateOnly(1957, 1, 1), ReviewSchedule.Next(new DateOnly(1956, 12, 25), false, false, false, []));
        Assert.Equal(today.AddDays(AiEstimates.AfterActionDays), ReviewSchedule.Next(today, true, false, false, []));
        Assert.Equal(today.AddDays(AiEstimates.VacancyRetryDays), ReviewSchedule.Next(today, false, true, false, []));
        Assert.Equal(today.AddDays(ReviewSchedule.SponsorCheckDays), ReviewSchedule.Next(today, false, false, true, []));
        Assert.Equal(today.AddDays(9), ReviewSchedule.Next(today, false, false, false, [today.AddDays(9), today.AddDays(-3), today.AddDays(20)]));
    }

    [Fact]
    public void FundsCommitOnlyTheSeasonLeftAgainstTheHeadroom()
    {
        var funds = new AiFunds(1_000, 500, 800, 60_000, 0);
        Assert.Equal(700, funds.Headroom);
        Assert.True(funds.CanCommit(1_000, new DateOnly(1956, 12, 1)));
        Assert.False(funds.CanCommit(1_000, new DateOnly(1956, 1, 1)));
        Assert.True(funds.CanCommit(0, new DateOnly(1956, 1, 1)));
        Assert.Equal(1_000, AiFunds.PartOfSeasonLeft(1_000, new DateOnly(1956, 1, 1)));
    }
}
