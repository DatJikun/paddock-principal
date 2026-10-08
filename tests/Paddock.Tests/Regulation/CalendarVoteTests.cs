using Paddock.Application.Racing;
using Paddock.Application.Regulation;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Regulation;
using Paddock.Simulation.Time;

namespace Paddock.Tests.Regulation;

/// <summary>
/// The race calendar as a vote (#275, owner decision 2): dropping Indianapolis voted in 1955 gives a 1956 calendar without it, the
/// 1955 calendar already laid out keeps it, and the policy stands for the seasons after.
/// </summary>
public class CalendarVoteTests
{
    private const string Series = PoliticsHarness.Series;

    private static IReadOnlyList<TrackLayout> Layouts => PoliticsHarness.Data.Layouts;

    private static IReadOnlyList<RaceAssignment> Assignments => PoliticsHarness.Data.RaceAssignments;

    private static string CircuitOf(string layoutId) => Layouts.First(layout => layout.Id == layoutId).CircuitId;

    private static IEnumerable<string> CircuitsOf(int season) =>
        Assignments.Where(assignment => assignment.Season == season).Select(assignment => CircuitOf(assignment.LayoutId));

    /// <summary>A circuit that races in both 1955 and 1956, so dropping it shows in the one and not the other.</summary>
    private static string SharedCircuit() => CircuitsOf(1955).Intersect(CircuitsOf(1956)).Order(StringComparer.Ordinal).First();

    private static IEnumerable<string> PlannedCircuits(IReadOnlyList<SeasonCalendar.PlannedSession> plan) =>
        plan.Where(session => session.TypeId == ScheduledEventType.Race).Select(session => CircuitOf(session.LayoutId));

    private static PoliticsHarness AdoptDrop(string circuit)
    {
        var harness = PoliticsHarness.Create(new HarnessOptions { AllHuman = true, WithCalendar = true });
        // The season in progress is laid out, as the career does when it opens.
        var (_, laid) = SeasonPlans.Ensure(harness.World, 1955, Layouts, Assignments, null);
        harness.SetWorld(laid);
        harness.LiveFrom(new GameDate(1955, 1, 2), new GameDate(1955, 2, 10));
        harness.Politics.Propose(Series, "t01", CalendarPolicy.DimensionOf(circuit), CalendarPolicy.Dropped, harness.Today);
        harness.LiveToTeamBallot();
        var item = harness.Items().Single(i => i.DimensionId == CalendarPolicy.DimensionOf(circuit));
        foreach (var team in harness.Teams)
        {
            harness.Politics.Cast(Series, team, item.Id, "v1", 0, harness.Today);
        }

        harness.LiveTo(item.Deadline);
        Assert.Equal(BallotOutcome.Adopted, harness.Of().ItemOf(item.Id)!.Result!.Outcome);
        return harness;
    }

    [Fact]
    public void ARaceDroppedByVoteIsMissingFromTheNextSeasonAndThePlanOfTheCurrentSeasonKeepsIt()
    {
        var circuit = SharedCircuit();
        var harness = AdoptDrop(circuit);
        harness.LiveTo(new GameDate(1955, 12, 31));

        var current = SeasonPlans.Stored(harness.World, 1955)!;
        var (next, world) = SeasonPlans.Ensure(harness.World, 1956, Layouts, Assignments, null);

        Assert.Contains(circuit, PlannedCircuits(current));
        Assert.Equal(CircuitsOf(1955).Count(), PlannedCircuits(current).Count());
        Assert.DoesNotContain(circuit, PlannedCircuits(next));
        Assert.Equal(CircuitsOf(1956).Count() - 1, PlannedCircuits(next).Count());
        Assert.Equal(next.Count / 3, SeasonPlans.Rounds(world, 1956)!.Value.Rounds);
        Assert.Equal(Enumerable.Range(1, next.Count / 3), next.Where(s => s.TypeId == ScheduledEventType.Race).Select(s => s.Round).Order());
        Assert.Equal(next.Count / 3, SeasonPlans.Rounds(world, 1956)!.Value.LastRound);
    }

    [Fact]
    public void TheDropIsStoredForTheNextSeasonAndNeverInForceInTheSeasonOfTheVote()
    {
        var circuit = SharedCircuit();
        var harness = AdoptDrop(circuit);
        var dimension = CalendarPolicy.DimensionOf(circuit);

        Assert.Empty(harness.Of().Calendar);
        Assert.Contains(harness.Of().NextCalendar!, pair => pair.Key == dimension && pair.Value == CalendarPolicy.Dropped);
        Assert.Empty(harness.Section.Find(Series)!.CalendarFor(1955)!);
        Assert.Equal(CalendarPolicy.Dropped, harness.Section.Find(Series)!.CalendarFor(1956)![dimension]);

        // A season planned now would still be the authored 1955 one: the layout of a season reads its own year's policy.
        var (plan1955, _) = SeasonPlans.Ensure(PoliticsHarness.Create(new HarnessOptions { WithCalendar = true }).World, 1955, Layouts, Assignments, null);
        Assert.Contains(circuit, PlannedCircuits(plan1955));

        harness.LiveTo(new GameDate(1956, 1, 1));
        Assert.Equal(CalendarPolicy.Dropped, harness.Of().Calendar.Single(pair => pair.Key == dimension).Value);
        Assert.Null(harness.Of().NextCalendar);
    }

    [Fact]
    public void ADropStandsForTheSeasonsAfter()
    {
        var circuit = SharedCircuit();
        var harness = AdoptDrop(circuit);
        harness.LiveTo(new GameDate(1956, 12, 31));

        var (plan, _) = SeasonPlans.Ensure(harness.World, 1957, Layouts, Assignments, null);

        Assert.DoesNotContain(circuit, PlannedCircuits(plan));
        Assert.Equal(CircuitsOf(1957).Count(c => c != circuit), PlannedCircuits(plan).Count());
    }

    [Fact]
    public void ADroppedRaceKeepsTheRealDatesOfTheOthersAndAnAddedOneFallsBackToEvenSpacing()
    {
        var circuit = SharedCircuit();
        var rounds = Assignments.Where(a => a.Season == 1956).OrderBy(a => a.Round).ToArray();
        var dates = RaceDateBook.Create(rounds.Select((a, i) => (1956, a.Round, new GameDate(1956, 4, 1).AddDays(i * 20))));
        var policy = new Dictionary<string, string> { [CalendarPolicy.DimensionOf(circuit)] = CalendarPolicy.Dropped };

        var (assignments, book) = CalendarPolicy.Resolve(1956, Layouts, Assignments, dates, policy);

        var kept = rounds.Where(a => CircuitOf(a.LayoutId) != circuit).ToArray();
        Assert.Equal(kept.Length, assignments.Count(a => a.Season == 1956));
        for (var i = 0; i < kept.Length; i++)
        {
            Assert.True(book!.TryGet(1956, i + 1, out var date));
            Assert.True(dates.TryGet(1956, kept[i].Round, out var original));
            Assert.Equal(original, date);
            Assert.Equal(kept[i].LayoutId, assignments.Single(a => a.Season == 1956 && a.Round == i + 1).LayoutId);
        }

        // Other seasons are not touched.
        Assert.Equal(Assignments.Count(a => a.Season != 1956), assignments.Count(a => a.Season != 1956));

        var addOption = CalendarPolicy.Options(1956, Layouts, Assignments, new Dictionary<string, string>()).First(o => !o.Authored);
        var add = new Dictionary<string, string> { [addOption.DimensionId] = addOption.Alternatives[0] };
        var (withAdded, addedBook) = CalendarPolicy.Resolve(1956, Layouts, Assignments, dates, add);
        Assert.Equal(rounds.Length + 1, withAdded.Count(a => a.Season == 1956));
        Assert.False(addedBook!.TryGet(1956, 1, out _), "an added race has no real date, so the season is spaced evenly as a whole");
        var plan = SeasonCalendar.Plan(1956, Layouts, withAdded, addedBook);
        Assert.Equal((rounds.Length + 1) * 3, plan.Count);
        Assert.Equal(plan.Select(s => s.Date).Order(), plan.Select(s => s.Date));
    }

    [Fact]
    public void ACircuitCanBeGivenAnotherLayoutOrAddedAndAnEmptyPolicyChangesNothing()
    {
        var empty = new Dictionary<string, string>();
        var (untouched, book) = CalendarPolicy.Resolve(1956, Layouts, Assignments, null, empty);
        Assert.Same(Assignments, untouched);
        Assert.Null(book);

        var options = CalendarPolicy.Options(1956, Layouts, Assignments, empty);
        var relayout = options.First(option => option.Authored && option.Alternatives.Any(a => a.StartsWith(CalendarPolicy.LayoutPrefix, StringComparison.Ordinal)));
        var layoutValue = relayout.Alternatives.First(a => a.StartsWith(CalendarPolicy.LayoutPrefix, StringComparison.Ordinal));
        var rows = CalendarPolicy.RoundsFor(1956, Layouts, Assignments, null, new Dictionary<string, string> { [relayout.DimensionId] = layoutValue });
        Assert.Equal(Assignments.Count(a => a.Season == 1956), rows.Count);
        Assert.Contains(rows, row => row.Layout.Id == layoutValue[CalendarPolicy.LayoutPrefix.Length..]);

        var addable = options.First(option => !option.Authored);
        var added = CalendarPolicy.RoundsFor(1956, Layouts, Assignments, null, new Dictionary<string, string> { [addable.DimensionId] = addable.Alternatives[0] });
        Assert.Equal(Assignments.Count(a => a.Season == 1956) + 1, added.Count);
        Assert.Equal(addable.CircuitId, added[^1].Layout.CircuitId);
    }

    [Fact]
    public void TheCalendarNeverShrinksBelowTheMinimumNumberOfRounds()
    {
        var policy = new Dictionary<string, string>();
        var options = CalendarPolicy.Options(1956, Layouts, Assignments, policy);
        var authored = options.Where(option => option.Authored).ToArray();
        var dropped = 0;
        foreach (var option in authored)
        {
            var now = CalendarPolicy.RoundsFor(1956, Layouts, Assignments, null, policy).Count;
            var current = CalendarPolicy.Options(1956, Layouts, Assignments, policy).FirstOrDefault(o => o.DimensionId == option.DimensionId);
            var drops = current?.Alternatives.Contains(CalendarPolicy.Dropped) ?? false;
            Assert.Equal(now > RegulationEstimates.MinimumRounds, drops || current?.Current == CalendarPolicy.Dropped);
            if (drops)
            {
                policy[option.DimensionId] = CalendarPolicy.Dropped;
                dropped++;
            }
        }

        Assert.True(dropped > 0);
        Assert.Equal(RegulationEstimates.MinimumRounds, CalendarPolicy.RoundsFor(1956, Layouts, Assignments, null, policy).Count);
        Assert.False(CalendarPolicy.KeepsEnoughRounds(1956, Layouts, Assignments, new Dictionary<string, string>(), CalendarPolicy.DimensionOf(SharedCircuit()), CalendarPolicy.Dropped)
            && CalendarPolicy.RoundsFor(1956, Layouts, Assignments, null, new Dictionary<string, string>()).Count <= RegulationEstimates.MinimumRounds);
    }

    [Fact]
    public void ACalendarProposalIsCheckedLikeAnyOtherAndTheFiaMayBringOneToo()
    {
        var circuit = SharedCircuit();
        var dimension = CalendarPolicy.DimensionOf(circuit);
        var harness = PoliticsHarness.Create(new HarnessOptions { WithCalendar = true });
        harness.LiveFrom(new GameDate(1955, 1, 2), new GameDate(1955, 2, 10));

        Assert.Null(harness.Politics.CheckPropose(Series, "t01", dimension, CalendarPolicy.Dropped, harness.Today));
        Assert.Equal(RegulationKeys.SameValue, harness.Politics.CheckPropose(Series, "t01", dimension, CalendarPolicy.Kept, harness.Today)!.Key);
        Assert.Equal(RegulationKeys.ValueNotAllowed, harness.Politics.CheckPropose(Series, "t01", dimension, "nonsense", harness.Today)!.Key);
        Assert.Equal(RegulationKeys.NotLive, harness.Politics.CheckPropose(Series, "t01", CalendarPolicy.DimensionOf("no-such-circuit"), CalendarPolicy.Dropped, harness.Today)!.Key);

        var brought = false;
        foreach (var seed in Enumerable.Range(1, 12))
        {
            var run = PoliticsHarness.Create(new HarnessOptions { WithCalendar = true, Seed = (ulong)seed });
            run.LiveTo(new GameDate(1955, 12, 31));
            brought |= run.Items().Any(item => item.Origin == BallotOrigin.Fia && CalendarPolicy.IsCalendarDimension(item.DimensionId));
        }

        Assert.True(brought, "across twelve seeds the FIA brings at least one change of the calendar");
    }

    [Fact]
    public void TheTeamThatRacesAtHomeVotesAgainstLosingItsHomeRound()
    {
        var circuit = SharedCircuit();
        var country = Layouts.First(layout => layout.CircuitId == circuit).Country;
        Assert.NotEmpty(country);
        var option = CalendarPolicy.Options(1956, Layouts, Assignments, new Dictionary<string, string>())
            .First(o => o.CircuitId == circuit);

        Assert.Equal(-1, CalendarPolicy.HomeEffect(option, CalendarPolicy.Dropped, country));
        Assert.Equal(0, CalendarPolicy.HomeEffect(option, CalendarPolicy.Dropped, "ZZZ"));
        var added = new CalendarPolicy.Option("calendar.circuit.x", "x", country, false, CalendarPolicy.Kept, [CalendarPolicy.AddedPrefix + "x"]);
        Assert.Equal(1, CalendarPolicy.HomeEffect(added, CalendarPolicy.AddedPrefix + "x", country));
    }
}
