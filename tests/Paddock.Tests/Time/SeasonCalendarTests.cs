using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Time;

namespace Paddock.Tests.Time;

public class SeasonCalendarTests
{
    [Fact]
    public void EachRoundBecomesAPlaceholderWeekendInRoundOrder()
    {
        var state = SeasonCalendar.Schedule(
            new WorldClockState(new GameDate(1950, 1, 1), 1),
            1950,
            Layouts(),
            [
                new RaceAssignment(1951, 1, "alpha"),
                new RaceAssignment(1950, 2, "beta"),
                new RaceAssignment(1950, 1, "alpha"),
            ]);

        Assert.Equal(6, state.Queue.Count);
        Assert.Equal(7UL, state.NextEventId);
        Assert.Equal(
            [
                (ScheduledEventType.Practice, new GameDate(1950, 3, 1), 1, "alpha"),
                (ScheduledEventType.Qualifying, new GameDate(1950, 3, 2), 1, "alpha"),
                (ScheduledEventType.Race, new GameDate(1950, 3, 3), 1, "alpha"),
                (ScheduledEventType.Practice, new GameDate(1950, 11, 28), 2, "beta"),
                (ScheduledEventType.Qualifying, new GameDate(1950, 11, 29), 2, "beta"),
                (ScheduledEventType.Race, new GameDate(1950, 11, 30), 2, "beta"),
            ],
            state.Queue.Events.Select(Describe).ToArray());
    }

    [Fact]
    public void ALeapYearUsesTheSamePlaceholderWindow()
    {
        var state = SeasonCalendar.Schedule(
            new WorldClockState(GameDate.SeasonStart(1952), 1),
            1952,
            Layouts(),
            [new RaceAssignment(1952, 1, "alpha"), new RaceAssignment(1952, 2, "beta")]);

        Assert.Equal(new GameDate(1952, 3, 3), state.Queue.Events[2].Date);
        Assert.Equal(new GameDate(1952, 11, 30), state.Queue.Events[5].Date);
    }

    [Fact]
    public void AnEmptySeasonDoesNotTouchTheQueue()
    {
        var state = new WorldClockState(new GameDate(1950, 1, 1), 1);
        var scheduled = SeasonCalendar.Schedule(state, 1955, Layouts(), [new RaceAssignment(1950, 1, "alpha")]);

        Assert.Equal(state, scheduled);
        Assert.Same(state.Queue, scheduled.Queue);
    }

    [Fact]
    public void AMissingLayoutIsRejectedByTheRaceCalendar()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => SeasonCalendar.Schedule(
            new WorldClockState(new GameDate(1950, 1, 1), 1),
            1950,
            Layouts(),
            [new RaceAssignment(1950, 1, "missing")]));
        Assert.Contains("missing", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TooManyRoundsRefuseToOverlap()
    {
        var assignments = new RaceAssignment[100];
        var layouts = new List<TrackLayout>(100);
        for (var round = 1; round <= assignments.Length; round++)
        {
            var id = "layout-" + round.ToString(System.Globalization.CultureInfo.InvariantCulture);
            layouts.Add(new TrackLayout(id, id, 1.0, new Dictionary<string, double> { ["balance"] = 1.0 }, []));
            assignments[round - 1] = new RaceAssignment(1950, round, id);
        }

        var ex = Assert.Throws<InvalidOperationException>(() => SeasonCalendar.Schedule(
            new WorldClockState(new GameDate(1950, 1, 1), 1),
            1950,
            layouts,
            assignments));
        Assert.Contains("overlapping", ex.Message, StringComparison.Ordinal);
    }

    private static (string TypeId, GameDate Date, int Round, string LayoutId) Describe(ScheduledEvent scheduled)
    {
        var payload = Assert.IsType<RaceSessionPayload>(scheduled.Payload);
        return (scheduled.TypeId, scheduled.Date, payload.Round, payload.LayoutId);
    }

    private static IReadOnlyList<TrackLayout> Layouts() =>
    [
        new TrackLayout("alpha", "alpha_circuit", 5.0, new Dictionary<string, double> { ["balance"] = 1.0 }, ["fast"]),
        new TrackLayout("beta", "beta_circuit", 3.3, new Dictionary<string, double> { ["balance"] = 1.0 }, ["street"]),
    ];
}
