using Paddock.Application.Racing;
using Paddock.Application.Regulation;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Regulation;
using Paddock.Simulation.Time;

namespace Paddock.Tests.Regulation;

/// <summary>
/// The dates of the political year (#275, owner decision of round 2): ballots are spread evenly over the gaps between race weekends,
/// each open between two weekends and counted before the next, the dates move with the calendar, and the year is stored.
/// </summary>
public class PoliticalScheduleTests
{
    private const string Series = PoliticsHarness.Series;

    private static WeekendSpan Weekend(int season, int month, int day) =>
        new(new GameDate(season, month, day).AddDays(-SeasonCalendar.PracticeDaysBeforeRace), new GameDate(season, month, day));

    /// <summary>Weekends every <paramref name="every"/> days from <paramref name="first"/>.</summary>
    private static List<WeekendSpan> Every(int season, GameDate first, int every, int count) =>
        [.. Enumerable.Range(0, count).Select(i => new WeekendSpan(first.AddDays(i * every - 2), first.AddDays(i * every)))];

    private static bool Touches(BallotSlot slot, WeekendSpan weekend) => slot.Opens <= weekend.Last && slot.Closes >= weekend.First;

    private static void AssertBetweenWeekends(PoliticalSchedule schedule, IReadOnlyList<WeekendSpan> weekends)
    {
        foreach (var slot in schedule.Slots)
        {
            Assert.All(weekends, weekend => Assert.False(Touches(slot, weekend), $"slot {slot.Opens}..{slot.Closes} touches the weekend {weekend.First}..{weekend.Last}"));
            Assert.True(slot.Closes <= RegulationSchedule.LastCountingDay(schedule.Season));
            Assert.True(slot.Opens.DaysUntil(slot.Closes) + 1 >= RegulationEstimates.MinimumWindowDays);
        }
    }

    [Fact]
    public void BallotsAreSpreadEvenlyOverTheGapsBetweenWeekendsEachInItsOwnGapWhenThereAreEnough()
    {
        var weekends = Every(1960, new GameDate(1960, 3, 20), 14, 19); // 18 gaps
        var schedule = RegulationSchedule.Plan(1960, new GameDate(1960, 1, 1), weekends, 5);

        Assert.Equal(6, schedule.Slots.Count);
        Assert.Equal(5, schedule.FiaSlots.Count);
        Assert.NotNull(schedule.TeamsSlot);
        AssertBetweenWeekends(schedule, weekends);

        // Six ballots over eighteen gaps land in gaps 1, 4, 7, 10, 13 and 16: every third one, none shared.
        var gap = weekends.Select((_, i) => i).Take(18).ToArray();
        var indexes = schedule.Slots.Select(slot => gap.Single(i => slot.Opens == weekends[i].Last.AddDays(1))).ToArray();
        Assert.Equal([1, 4, 7, 10, 13, 16], indexes);

        // And each is open for the whole gap, from the day after a race to the day before the next weekend.
        Assert.All(schedule.Slots, slot => Assert.Equal(14 - 3, slot.Opens.DaysUntil(slot.Closes) + 1));
    }

    [Fact]
    public void TheTeamsBallotSitsInTheMiddleAndTheWindowForProposalsRunsFromTheFirstDayOfTheSeasonUntilTheDayBefore()
    {
        var weekends = Every(1960, new GameDate(1960, 3, 20), 14, 19);
        var schedule = RegulationSchedule.Plan(1960, new GameDate(1960, 1, 1), weekends, 5);

        Assert.Equal(BallotSlotKind.Teams, schedule.Slots[3].Kind);
        Assert.Equal(3, schedule.Slots.Take(3).Count(slot => slot.Kind == BallotSlotKind.Fia));
        Assert.Equal(new GameDate(1960, 1, 1), schedule.ProposalsOpen);
        Assert.Equal(schedule.TeamsSlot!.Opens.AddDays(-1), schedule.ProposalsClose);
        Assert.True(schedule.AcceptsProposalsOn(new GameDate(1960, 1, 1)));
        Assert.True(schedule.AcceptsProposalsOn(schedule.ProposalsClose));
        Assert.False(schedule.AcceptsProposalsOn(schedule.TeamsSlot.Opens));
        Assert.False(schedule.AcceptsProposalsOn(new GameDate(1959, 12, 31)));
        // The teams have the winter and several weekends to file their one proposal.
        Assert.True(schedule.ProposalsOpen.DaysUntil(schedule.ProposalsClose) > 60);
    }

    [Fact]
    public void TheDatesMoveWithTheCalendar()
    {
        var early = Every(1960, new GameDate(1960, 3, 20), 14, 12);
        var late = Every(1960, new GameDate(1960, 5, 8), 14, 12);

        var one = RegulationSchedule.Plan(1960, new GameDate(1960, 1, 1), early, 5);
        var two = RegulationSchedule.Plan(1960, new GameDate(1960, 1, 1), late, 5);

        Assert.Equal(one.Slots.Select(slot => slot.Opens.AddDays(49)), two.Slots.Select(slot => slot.Opens));
        Assert.Equal(one.Slots.Select(slot => slot.Closes.AddDays(49)), two.Slots.Select(slot => slot.Closes));
        Assert.Equal(one.ProposalsClose.AddDays(49), two.ProposalsClose);
        AssertBetweenWeekends(two, late);
    }

    [Fact]
    public void WithFewerGapsThanBallotsSeveralShareAGapAndStillNeverTouchAWeekend()
    {
        var weekends = Every(1960, new GameDate(1960, 4, 10), 30, 5); // the minimum calendar: 4 gaps for 7 ballots
        var schedule = RegulationSchedule.Plan(1960, new GameDate(1960, 1, 1), weekends, 6);

        Assert.Equal(7, schedule.Slots.Count);
        AssertBetweenWeekends(schedule, weekends);
        Assert.True(schedule.Slots.Select(slot => slot.Opens).Distinct().Count() < 7, "some ballots share a gap");
        Assert.True(schedule.Slots.Select(slot => slot.Opens).Distinct().Count() >= 4, "and all four gaps are used");
        Assert.Equal(schedule.Slots.OrderBy(slot => slot.Opens).Select(slot => slot.Opens), schedule.Slots.Select(slot => slot.Opens));
    }

    [Fact]
    public void TwoWeekendsHaveOneGapAndEveryBallotSharesIt()
    {
        var weekends = new List<WeekendSpan> { Weekend(1960, 5, 10), Weekend(1960, 9, 10) };
        var schedule = RegulationSchedule.Plan(1960, new GameDate(1960, 1, 1), weekends, 4);

        Assert.Equal(5, schedule.Slots.Count);
        Assert.Single(schedule.Slots.Select(slot => (slot.Opens, slot.Closes)).Distinct());
        Assert.Equal(new GameDate(1960, 5, 11), schedule.Slots[0].Opens);
        Assert.Equal(new GameDate(1960, 9, 7), schedule.Slots[0].Closes); // the day before the practice day of the second weekend
        AssertBetweenWeekends(schedule, weekends);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ACalendarWithNoGapUsesThePlaceholderWindowFreeOfWeekendsAndCutsItForEvenSpacing(int rounds)
    {
        var weekends = rounds == 0 ? [] : new List<WeekendSpan> { Weekend(1960, 6, 20) };
        var schedule = RegulationSchedule.Plan(1960, new GameDate(1960, 1, 1), weekends, 5);

        Assert.Equal(6, schedule.Slots.Count);
        AssertBetweenWeekends(schedule, weekends);
        Assert.All(schedule.Slots, slot => Assert.InRange(slot.Opens.Month, SeasonCalendar.WindowStartMonth, SeasonCalendar.WindowEndMonth));
        Assert.Equal(6, schedule.Slots.Select(slot => slot.Opens).Distinct().Count());
    }

    [Fact]
    public void WeekendsBackToBackLeaveNoUsableGapAndTheFallbackStillAvoidsThem()
    {
        var weekends = Every(1960, new GameDate(1960, 4, 10), 4, 20); // 4 days apart: a gap of 4 - 3 = 1 day each
        var schedule = RegulationSchedule.Plan(1960, new GameDate(1960, 1, 1), weekends, 5);

        AssertBetweenWeekends(schedule, weekends);
        Assert.Equal(6, schedule.Slots.Count);
    }

    [Fact]
    public void ASeasonWithNoRoomAtAllHasNoSlotsAndAnEmptyProposalWindow()
    {
        // A weekend every three days all year round: no stretch of three free days exists anywhere.
        var weekends = Enumerable.Range(0, 120).Select(i => new WeekendSpan(new GameDate(1960, 1, 3).AddDays((i * 3) - 2), new GameDate(1960, 1, 3).AddDays(i * 3))).Where(w => w.Last.Year == 1960).ToList();
        var schedule = RegulationSchedule.Plan(1960, new GameDate(1960, 1, 1), weekends, 5);

        Assert.Empty(schedule.Slots);
        Assert.Null(schedule.TeamsSlot);
        Assert.False(schedule.AcceptsProposalsOn(new GameDate(1960, 6, 1)));
    }

    [Fact]
    public void ACareerThatStartsInsideTheSeasonOnlyGetsTheGapsStillAhead()
    {
        var weekends = Every(1960, new GameDate(1960, 3, 20), 14, 19);
        var today = new GameDate(1960, 7, 1);

        var schedule = RegulationSchedule.Plan(1960, today, weekends, 5);

        Assert.All(schedule.Slots, slot => Assert.True(slot.Opens >= today));
        AssertBetweenWeekends(schedule, weekends);
        Assert.Equal(6, schedule.Slots.Count);
        Assert.Equal(new GameDate(1960, 1, 1), schedule.ProposalsOpen);
    }

    [Fact]
    public void RandomCalendarsNeverPutABallotOnAWeekendOrAfterTheLastCountingDay()
    {
        var rng = new System.Random(2026);
        for (var trial = 0; trial < 300; trial++)
        {
            var count = rng.Next(0, 26);
            var day = new GameDate(1960, 3, 1).AddDays(rng.Next(0, 40));
            var weekends = new List<WeekendSpan>();
            for (var i = 0; i < count && day.Year == 1960; i++)
            {
                weekends.Add(new WeekendSpan(day.AddDays(-2), day));
                day = day.AddDays(rng.Next(3, 40));
            }

            var today = trial % 3 == 0 ? new GameDate(1960, 1, 1).AddDays(rng.Next(0, 330)) : new GameDate(1960, 1, 1);
            var schedule = RegulationSchedule.Plan(1960, today, weekends, 4 + (trial % 3));

            AssertBetweenWeekendsLoosely(schedule, weekends);
            Assert.Equal(schedule.Slots.OrderBy(slot => slot.Opens).Select(slot => slot.Opens), schedule.Slots.Select(slot => slot.Opens));
            Assert.Equal(schedule.Slots.Count == 0, schedule.TeamsSlot is null);
            Assert.True(schedule.Slots.Count == 0 || schedule.Slots.Count == 5 + (trial % 3));
        }
    }

    private static void AssertBetweenWeekendsLoosely(PoliticalSchedule schedule, IReadOnlyList<WeekendSpan> weekends)
    {
        foreach (var slot in schedule.Slots)
        {
            Assert.All(weekends, weekend => Assert.False(Touches(slot, weekend), $"slot {slot.Opens}..{slot.Closes} touches the weekend {weekend.First}..{weekend.Last}"));
            Assert.True(slot.Closes <= RegulationSchedule.LastCountingDay(schedule.Season));
        }
    }

    [Fact]
    public void ThePlanIsAPureFunctionOfItsInputs()
    {
        var weekends = Every(1960, new GameDate(1960, 3, 20), 14, 12);

        var one = RegulationSchedule.Plan(1960, new GameDate(1960, 1, 1), weekends, 5);
        var two = RegulationSchedule.Plan(1960, new GameDate(1960, 1, 1), [.. weekends.AsEnumerable().Reverse()], 5);

        Assert.Equal(one.Slots, two.Slots);
        Assert.Equal(one.ProposalsClose, two.ProposalsClose);
    }

    // ------------------------------------------------------------------ in a living series

    private static IReadOnlyList<WeekendSpan> WeekendsOfPlan(IReadOnlyList<SeasonCalendar.PlannedSession> plan) =>
        [.. plan.GroupBy(session => session.Round).Select(round => new WeekendSpan(round.Min(s => s.Date), round.Max(s => s.Date)))];

    [Fact]
    public void EveryBallotOfARealCareerIsOpenedAfterOneRaceAndCountedBeforeTheNextWeekend()
    {
        foreach (var seed in new ulong[] { 1, 2, 3 })
        {
            var harness = PoliticsHarness.Create(new HarnessOptions { Seed = seed, StoreCalendar = true, Capital = _ => 400_000 });
            for (var year = 1955; year <= 1960; year++)
            {
                harness.LiveTo(new GameDate(year, 12, 31));
                var weekends = WeekendsOfPlan(SeasonPlans.Stored(harness.World, year)!);
                var items = harness.Items().Where(item => item.Season == year).ToArray();

                Assert.NotEmpty(items);
                foreach (var item in items)
                {
                    Assert.True(item.IsResolved);
                    // Opened the day after a race, counted before the first session of a later weekend, and no weekend in between.
                    Assert.Contains(weekends, weekend => weekend.Last.AddDays(1) == item.Announced);
                    Assert.DoesNotContain(weekends, weekend => weekend.First <= item.Deadline && weekend.Last >= item.Announced);
                    Assert.Contains(weekends, weekend => weekend.First > item.Deadline);
                    Assert.True(item.Deadline <= RegulationSchedule.LastCountingDay(year));
                }
            }
        }
    }

    [Fact]
    public void TheScheduleIsStoredOnTheFirstDayAndIsTheSameWhateverHappensToTheCalendarDataLater()
    {
        var harness = PoliticsHarness.Create(new HarnessOptions { Seed = 4, StoreCalendar = true });
        harness.LiveTo(new GameDate(1955, 1, 2));
        var stored = harness.ScheduleOf();
        var snapshot = stored.Slots.Select(slot => (slot.Kind, slot.Opens, slot.Closes)).ToArray();
        Assert.Equal(1955, stored.Season);

        // The plan of the season is replaced by one shifted by a month: the schedule that was stored does not move.
        var shifted = SeasonPlans.Stored(harness.World, 1955)!
            .Select(session => new CalendarSession(session.Season, session.Round, session.TypeId, session.Date.AddDays(30), session.LayoutId))
            .ToArray();
        var section = harness.World.Section<RaceCalendarSection>(RaceCalendarSection.SectionName)!.WithSeason(1955, shifted);
        harness.SetWorld(harness.World.WithSection(section));
        harness.LiveTo(new GameDate(1955, 12, 31));

        Assert.Equal(snapshot, harness.ScheduleOf().Slots.Select(slot => (slot.Kind, slot.Opens, slot.Closes)));
        Assert.Equal(stored.ProposalsClose, harness.ScheduleOf().ProposalsClose);
        Assert.Equal(harness.ScheduleOf().FiaSlots.Select(slot => slot.Opens), harness.Items().Where(item => item.Season == 1955 && item.Origin == BallotOrigin.Fia).Select(item => item.Announced).Order());
    }

    [Fact]
    public void ACalendarThatIsNotLaidOutYetIsReadTheWayTheCareerWillLayItOut()
    {
        var laid = PoliticsHarness.Create(new HarnessOptions { Seed = 4, StoreCalendar = true });
        var notYet = PoliticsHarness.Create(new HarnessOptions { Seed = 4, WithCalendar = true });
        Assert.Null(SeasonPlans.Stored(notYet.World, 1955));

        laid.LiveTo(new GameDate(1955, 1, 2));
        notYet.LiveTo(new GameDate(1955, 1, 2));

        Assert.Equal(laid.ScheduleOf().Slots.Select(slot => (slot.Kind, slot.Opens, slot.Closes)), notYet.ScheduleOf().Slots.Select(slot => (slot.Kind, slot.Opens, slot.Closes)));
        Assert.Null(SeasonPlans.Stored(notYet.World, 1955)); // reading it does not lay it out
    }

    [Fact]
    public void WithNoCalendarAtAllTheYearStillHasItsBallotsAndTheyAreStored()
    {
        var harness = PoliticsHarness.Create(new HarnessOptions { Seed = 4 });

        harness.LiveTo(new GameDate(1955, 1, 2));

        var schedule = harness.ScheduleOf();
        Assert.Equal(RegulationSchedule.FiaVotesIn(4, Series, 1955) + 1, schedule.Slots.Count);
        Assert.All(schedule.Slots, slot => Assert.InRange(slot.Opens.Month, SeasonCalendar.WindowStartMonth, SeasonCalendar.WindowEndMonth));
    }

    [Fact]
    public void AProposalFiledOnTheLastDayOfTheWindowIsOnTheTeamsBallotThatOpensTheNextDay()
    {
        var harness = PoliticsHarness.Create(new HarnessOptions { Seed = 6, StoreCalendar = true, AllHuman = true });
        harness.LiveTo(new GameDate(1955, 1, 2));
        var schedule = harness.ScheduleOf();
        harness.LiveTo(schedule.ProposalsClose);

        // A dimension the FIA has not brought yet (one on the ballot already is not open to a proposal).
        var onBallot = new HashSet<string>(harness.Items().Select(item => item.DimensionId), StringComparer.Ordinal);
        var choice = harness.Politics.ChoicesFor(harness.Of(), 1955, onBallot).First();

        Assert.Null(harness.Politics.CheckPropose(Series, "t01", choice.DimensionId, choice.Value, harness.Today));
        harness.Politics.Propose(Series, "t01", choice.DimensionId, choice.Value, harness.Today);
        harness.LiveTo(schedule.TeamsSlot!.Opens);

        var item = harness.Items().Single(i => i.Origin == BallotOrigin.Teams);
        Assert.Equal(schedule.TeamsSlot.Opens, item.Announced);
        Assert.Equal(schedule.TeamsSlot.Closes, item.Deadline);
        Assert.Equal(["t01"], item.Variants.Single().ProposerTeamIds);
        Assert.Equal(1958, harness.Of().TeamOf("t01")!.ProposeFromSeason);
        Assert.Equal(RegulationKeys.WindowClosed, harness.Politics.CheckPropose(Series, "t02", "safety_car", "physical", harness.Today)!.Key);
    }

    [Fact]
    public void TheScheduleOfASeasonIsFixedBeforeItsFirstWeekendAndCountsEachVoteAtItsSlot()
    {
        var harness = PoliticsHarness.Create(new HarnessOptions { Seed = 6, StoreCalendar = true, AllHuman = true });
        harness.LiveTo(new GameDate(1955, 1, 2));
        var schedule = harness.ScheduleOf();

        foreach (var slot in schedule.FiaSlots)
        {
            harness.LiveTo(slot.Opens.AddDays(-1));
            var before = harness.Items().Count(item => item.Origin == BallotOrigin.Fia);
            harness.LiveTo(slot.Opens);
            Assert.Equal(before + 1, harness.Items().Count(item => item.Origin == BallotOrigin.Fia));
            var item = harness.Items().Where(i => i.Origin == BallotOrigin.Fia).OrderBy(i => i.Announced).Last();
            Assert.Equal(slot.Closes, item.Deadline);
            harness.LiveTo(slot.Closes.AddDays(-1));
            Assert.False(harness.Of().ItemOf(item.Id)!.IsResolved);
            harness.LiveTo(slot.Closes);
            Assert.True(harness.Of().ItemOf(item.Id)!.IsResolved);
        }
    }

    [Fact]
    public void ACalendarEditedByAnEarlierVoteMovesTheNextYearsDatesWithIt()
    {
        // The same career with and without a race dropped by a vote in 1955: the political year of 1956 follows the calendar of 1956.
        var circuit = Assignments(1955).Intersect(Assignments(1956)).Order(StringComparer.Ordinal).First();
        var dropped = Drop(circuit);
        var kept = PoliticsHarness.Create(new HarnessOptions { Seed = 8, AllHuman = true, StoreCalendar = true });
        kept.LiveTo(new GameDate(1956, 1, 2));
        dropped.LiveTo(new GameDate(1956, 1, 2));

        var weekendsKept = WeekendsOfPlan(SeasonPlans.Stored(kept.World, 1956)!);
        var weekendsDropped = WeekendsOfPlan(SeasonPlans.Stored(dropped.World, 1956)!);

        Assert.Equal(weekendsKept.Count - 1, weekendsDropped.Count);
        Assert.NotEqual(
            kept.ScheduleOf().Slots.Select(slot => (slot.Opens, slot.Closes)),
            dropped.ScheduleOf().Slots.Select(slot => (slot.Opens, slot.Closes)));
        AssertBetweenWeekends(dropped.ScheduleOf(), weekendsDropped);
        AssertBetweenWeekends(kept.ScheduleOf(), weekendsKept);
    }

    private static IEnumerable<string> Assignments(int season) =>
        PoliticsHarness.Data.RaceAssignments.Where(a => a.Season == season)
            .Select(a => PoliticsHarness.Data.Layouts.First(layout => layout.Id == a.LayoutId).CircuitId);

    private static PoliticsHarness Drop(string circuit)
    {
        var harness = PoliticsHarness.Create(new HarnessOptions { Seed = 8, AllHuman = true, StoreCalendar = true });
        harness.LiveTo(new GameDate(1955, 1, 2));
        harness.Politics.Propose(Series, "t01", CalendarPolicy.DimensionOf(circuit), CalendarPolicy.Dropped, harness.Today);
        harness.LiveToTeamBallot();
        var item = harness.Items().Single(i => i.DimensionId == CalendarPolicy.DimensionOf(circuit));
        foreach (var team in harness.Teams)
        {
            harness.Politics.Cast(Series, team, item.Id, "v1", 0, harness.Today);
        }

        harness.LiveTo(item.Deadline);
        Assert.Equal(BallotOutcome.Adopted, harness.Of().ItemOf(item.Id)!.Result!.Outcome);
        harness.LiveTo(new GameDate(1955, 12, 31));
        return harness;
    }
}
