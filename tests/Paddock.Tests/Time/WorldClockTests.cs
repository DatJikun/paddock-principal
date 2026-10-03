using System.Diagnostics;
using Paddock.Domain.Random;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Time;

namespace Paddock.Tests.Time;

public class WorldClockTests
{
    [Fact]
    public void SameDayEventsKeepInsertionOrderAcrossRuns()
    {
        var first = LiveTwoDays();
        var second = LiveTwoDays();

        Assert.Equal(first, second);
        Assert.Equal(["first", "second"], first[0]);
        Assert.Equal(["later"], first[1]);
    }

    [Fact]
    public void AdvanceDayCrossesTheSeasonBoundaryAndTheLeapDay()
    {
        var newYear = WorldClock.AdvanceDay(new WorldClockState(new GameDate(1951, 12, 31), 7));
        Assert.Equal(new GameDate(1952, 1, 1), newYear.State.Date);
        Assert.True(newYear.State.Date.IsSeasonStart);
        Assert.Empty(newYear.Events);

        var leap = WorldClock.AdvanceDay(new WorldClockState(new GameDate(1952, 2, 28), 7));
        Assert.Equal(new GameDate(1952, 2, 29), leap.State.Date);
        var march = WorldClock.AdvanceDay(leap.State);
        Assert.Equal(new GameDate(1952, 3, 1), march.State.Date);
    }

    [Fact]
    public void HandlerCanScheduleAFutureEvent()
    {
        var handler = new OpeningRaceHandler();
        var registry = new DayHandlerRegistry([handler]);
        var state = SeasonCalendar.Schedule(
            new WorldClockState(new GameDate(1950, 1, 1), 42),
            1950,
            Layouts(),
            [new RaceAssignment(1950, 1, "alpha")]);

        GameDate? raceDay = null;
        GameDate? markerDay = null;
        var sawMarkerEarly = false;
        for (var i = 0; i < 90 && markerDay is null; i++)
        {
            var step = WorldClock.AdvanceDay(state, registry);
            foreach (var emitted in step.Events)
            {
                if (emitted.TypeId == ScheduledEventType.Race)
                {
                    raceDay = emitted.Date;
                    Assert.Contains(step.State.Queue.Events, item => item.TypeId == "marker.check");
                }

                if (emitted.TypeId == "marker.check")
                {
                    markerDay = emitted.Date;
                    var marker = Assert.IsType<MarkerPayload>(emitted.Payload);
                    Assert.Equal("opened", marker.Marker);
                }
                else if (raceDay is not null && emitted.Date > raceDay)
                {
                    sawMarkerEarly = true;
                }
            }

            state = step.State;
        }

        Assert.Equal(new GameDate(1950, 3, 3), raceDay);
        Assert.Equal(raceDay!.Value.AddDays(10), markerDay);
        Assert.False(sawMarkerEarly);
    }

    [Fact]
    public void HandlersRunInOrderAndDrawNamedStreamsBySeason()
    {
        var log = new List<string>();
        var registry = new DayHandlerRegistry(
        [
            new RecordingHandler(2, "b", log),
            new RecordingHandler(1, "a", log),
            new RecordingHandler(2, "c", log),
        ]);
        var step = WorldClock.AdvanceDay(new WorldClockState(new GameDate(1950, 1, 1), 42), registry);
        Assert.Equal(["a", "b", "c"], log);
        Assert.Same(EventQueue.Empty, step.State.Queue);

        var drawing = new DrawingHandler();
        var drawRegistry = new DayHandlerRegistry([drawing, drawing]);
        var first = WorldClock.AdvanceDay(new WorldClockState(new GameDate(1950, 1, 1), 42), drawRegistry);
        var second = WorldClock.AdvanceDay(first.State, drawRegistry);
        var continued = RngStreams.Derive(42, RngStreamName.Weather, 1950);
        Assert.Equal(continued.NextULong(), drawing.Draws[0]);
        Assert.Equal(continued.NextULong(), drawing.Draws[1]);
        Assert.Equal(continued.NextULong(), drawing.Draws[2]);
        Assert.Equal(continued.NextULong(), drawing.Draws[3]);

        var rolled = new DrawingHandler();
        var rollRegistry = new DayHandlerRegistry([rolled]);
        var yearsEnd = WorldClock.AdvanceDay(new WorldClockState(new GameDate(1950, 12, 31), 42), rollRegistry);
        var nextSeason = WorldClock.AdvanceDay(yearsEnd.State, rollRegistry);
        var seasonStart = RngStreams.Derive(42, RngStreamName.Weather, 1950);
        Assert.Equal(seasonStart.NextULong(), rolled.Draws[0]);
        Assert.NotEqual(seasonStart.NextULong(), rolled.Draws[1]);
        Assert.Equal(RngStreams.Derive(42, RngStreamName.Weather, 1951).NextULong(), rolled.Draws[1]);
        Assert.True(yearsEnd.State.Date.IsSeasonStart);
        Assert.Equal(new GameDate(1951, 1, 2), nextSeason.State.Date);
    }

    [Fact]
    public void FiveSimulatedYearsMatchTheStoredStateHash()
    {
        var left = SimulateFiveYears(42);
        var right = SimulateFiveYears(42);
        var otherSeed = SimulateFiveYears(43);

        Assert.Equal(left.Hash, right.Hash);
        Assert.NotEqual(left.Hash, otherSeed.Hash);
        Assert.Equal(new GameDate(1955, 1, 1), left.Date);
        Assert.Equal(1826, left.Days);
        Assert.Equal(0, left.Queued);
        Assert.Equal(31UL, left.NextSequence);
        Assert.Equal(32UL, left.NextEventId);
        Assert.Equal(5, left.StreamCount);
        Assert.Equal(1, left.MarkerCount);
        Assert.Equal(10, left.RaceCount);
        Assert.Equal(6441419705885268966UL, left.Hash);
    }

    [Fact]
    public void EmptyDaysStayUnderTheDocumentedBudget()
    {
        var state = new WorldClockState(new GameDate(1951, 1, 1), 1);
        var cursor = state;
        var elapsed = Stopwatch.StartNew();
        for (var i = 0; i < 365; i++)
        {
            var step = WorldClock.AdvanceDay(cursor);
            Assert.Same(cursor.Queue, step.State.Queue);
            Assert.Empty(step.Events);
            cursor = step.State;
        }

        elapsed.Stop();
        Assert.True(
            elapsed.ElapsedMilliseconds < WorldClock.EmptyYearMillisecondBudget,
            $"365 empty days took {elapsed.ElapsedMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture)} ms; the documented budget is {WorldClock.EmptyYearMillisecondBudget.ToString(System.Globalization.CultureInfo.InvariantCulture)} ms.");
        Assert.Equal(new GameDate(1952, 1, 1), cursor.Date);
        Assert.Empty(cursor.RngStates);
    }

    [Fact]
    public void HandlerCannotScheduleTheDayBeingLived()
    {
        var state = new WorldClockState(new GameDate(1950, 6, 1), 1)
            .Enqueue(new ScheduledEvent(
                new EventId("due"),
                new GameDate(1950, 6, 1),
                "marker.check",
                new MarkerPayload("due")));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorldClock.AdvanceDay(state, new DayHandlerRegistry([new SameDayScheduler()])));
    }

    [Fact]
    public void AMissedEventRefusesToSkipTheDay()
    {
        var state = new WorldClockState(new GameDate(1950, 6, 2), 1)
            .Enqueue(new ScheduledEvent(new EventId("past"), new GameDate(1950, 6, 1), "marker.check", new MarkerPayload("past")));

        var ex = Assert.Throws<InvalidOperationException>(() => WorldClock.AdvanceDay(state));
        Assert.Contains("1950-06-01", ex.Message, StringComparison.Ordinal);
    }

    private static List<string>[] LiveTwoDays()
    {
        var day = new GameDate(1950, 6, 1);
        var state = new WorldClockState(day, 42)
            .Enqueue(new ScheduledEvent(new EventId("later"), day.AddDays(1), "marker.check", new MarkerPayload("later")))
            .Enqueue(new ScheduledEvent(new EventId("first"), day, "marker.check", new MarkerPayload("first")))
            .Enqueue(new ScheduledEvent(new EventId("second"), day, "marker.check", new MarkerPayload("second")));

        var first = WorldClock.AdvanceDay(state);
        var second = WorldClock.AdvanceDay(first.State);
        return
        [
            first.Events.Select(item => item.Id.Value).ToList(),
            second.Events.Select(item => item.Id.Value).ToList(),
        ];
    }

    private static FiveYearResult SimulateFiveYears(ulong seed)
    {
        var registry = new DayHandlerRegistry([new OpeningRaceHandler()]);
        var state = new WorldClockState(new GameDate(1950, 1, 1), seed);
        var layouts = Layouts();
        for (var season = 1950; season <= 1954; season++)
        {
            state = SeasonCalendar.Schedule(
                state,
                season,
                layouts,
                [
                    new RaceAssignment(season, 1, "alpha"),
                    new RaceAssignment(season, 2, "beta"),
                ]);
        }

        var end = new GameDate(1955, 1, 1);
        var days = 0;
        var markers = 0;
        var races = 0;
        while (state.Date < end)
        {
            var step = WorldClock.AdvanceDay(state, registry);
            foreach (var emitted in step.Events)
            {
                if (emitted.TypeId == "marker.check")
                {
                    markers++;
                }

                if (emitted.TypeId == ScheduledEventType.Race)
                {
                    races++;
                }
            }

            state = step.State;
            days++;
        }

        return new FiveYearResult(
            WorldClockHash.Compute(state),
            state.Date,
            days,
            state.Queue.Count,
            state.Queue.NextSequence,
            state.NextEventId,
            state.RngStates.Count,
            markers,
            races);
    }

    private static IReadOnlyList<TrackLayout> Layouts() =>
    [
        new TrackLayout("alpha", "alpha_circuit", 5.0, new Dictionary<string, double> { ["balance"] = 1.0 }, ["fast"]),
        new TrackLayout("beta", "beta_circuit", 3.3, new Dictionary<string, double> { ["balance"] = 1.0 }, ["street"]),
    ];

    private sealed record FiveYearResult(
        ulong Hash,
        GameDate Date,
        int Days,
        int Queued,
        ulong NextSequence,
        ulong NextEventId,
        int StreamCount,
        int MarkerCount,
        int RaceCount);

    private sealed class SameDayScheduler : IDayHandler
    {
        public int Order => 0;

        public void OnDay(DayContext context) =>
            context.Schedule(context.Today, "marker.check", new MarkerPayload("nope"));
    }

    private sealed class OpeningRaceHandler : IDayHandler
    {
        public int Order => 0;

        public void OnDay(DayContext context)
        {
            var drew = false;
            foreach (var due in context.DueEvents)
            {
                if (due.TypeId == ScheduledEventType.Race
                    && due.Payload is RaceSessionPayload race
                    && race.Season == 1950
                    && race.Round == 1)
                {
                    context.Schedule(context.Today.AddDays(10), "marker.check", new MarkerPayload("opened"));
                }

                if (!drew)
                {
                    _ = context.Stream(RngStreamName.Weather).NextULong();
                    drew = true;
                }
            }
        }
    }

    private sealed class RecordingHandler : IDayHandler
    {
        private readonly List<string> _log;

        public RecordingHandler(int order, string name, List<string> log)
        {
            Order = order;
            Name = name;
            _log = log;
        }

        public int Order { get; }

        public string Name { get; }

        public void OnDay(DayContext context) => _log.Add(Name);
    }

    private sealed class DrawingHandler : IDayHandler
    {
        public int Order => 0;

        public List<ulong> Draws { get; } = [];

        public void OnDay(DayContext context) => Draws.Add(context.Stream(RngStreamName.Weather).NextULong());
    }
}
