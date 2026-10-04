using Paddock.Domain.Codec;
using Paddock.Domain.Time;
using Paddock.Simulation.Codec;
using Paddock.Simulation.Time;

namespace Paddock.Tests.Time;

/// <summary>The save codec of event payloads and the exact restore of the event queue (issue #123).</summary>
public class EventCodecTests
{
    [Fact]
    public void EveryPayloadKindRoundTripsThroughItsTag()
    {
        EventPayload[] payloads =
        [
            new RaceSessionPayload(1955, 3, "spa;1954 \"fast\" layout"),
            new MarkerPayload("gen:17"),
            new MarkerPayload("ünïcode ✓ marker"),
        ];

        foreach (var payload in payloads)
        {
            var encoded = EventPayloadCodec.Encode(payload);
            Assert.Equal(payload, EventPayloadCodec.Decode(encoded.Tag, encoded.Text));
            Assert.Equal(encoded, EventPayloadCodec.Encode(EventPayloadCodec.Decode(encoded.Tag, encoded.Text)));
        }

        Assert.Equal("race-session/1", EventPayloadCodec.Encode(payloads[0]).Tag);
        Assert.Equal("marker/1", EventPayloadCodec.Encode(payloads[1]).Tag);
        Assert.Equal("""{"marker":"gen:17"}""", EventPayloadCodec.Encode(payloads[1]).Text);
    }

    [Fact]
    public void EveryPayloadSubtypeOfTheSimulationHasACodec()
    {
        // WorldClockHash is closed over the same set. A new payload must be added to the codec and to the hash together,
        // unless it is only emitted and never scheduled (it never sits in the queue, so a save never holds it).
        var emittedOnly = new[] { typeof(Paddock.Simulation.Objectives.ObjectiveOutcomePayload) };
        var subtypes = typeof(EventPayload).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(EventPayload)) && !type.IsAbstract && !emittedOnly.Contains(type))
            .ToHashSet();
        Assert.NotEmpty(subtypes);
        Assert.Equal(subtypes, EventPayloadCodec.SavedTypes.ToHashSet());
    }

    [Fact]
    public void AnEmittedOnlyPayloadCannotBeSavedByAccident()
    {
        var outcome = new Paddock.Simulation.Objectives.ObjectiveOutcomePayload("obj:1", "org:a", "org:b", true, 3m);
        var failure = Assert.Throws<InvalidOperationException>(() => EventPayloadCodec.Encode(outcome));
        Assert.Contains("ObjectiveOutcomePayload", failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("marker/2")]
    [InlineData("marker")]
    [InlineData("MARKER/1")]
    [InlineData("Paddock.Simulation.Time.MarkerPayload")]
    [InlineData("")]
    public void AnUnknownTagOrVersionFailsLoudly(string tag)
    {
        var failure = Assert.Throws<UnknownTagException>(() => EventPayloadCodec.Decode(tag, """{"marker":"x"}"""));
        Assert.Equal(tag, failure.Tag);
        Assert.Equal("event payload", failure.Kind);
        Assert.Contains(tag, failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"marker":"x","extra":"y"}""")]
    [InlineData("""{}""")]
    [InlineData("""{"marker":3}""")]
    [InlineData("""["x"]""")]
    [InlineData("not json")]
    [InlineData("""{"marker":""}""")]
    public void AMalformedBodyIsRefusedInsteadOfGuessed(string body)
    {
        Assert.ThrowsAny<Exception>(() => EventPayloadCodec.Decode(EventPayloadCodec.MarkerTag, body));
        Assert.IsAssignableFrom<InvalidDataException>(
            Record.Exception(() => EventPayloadCodec.Decode(EventPayloadCodec.MarkerTag, body)));
    }

    [Fact]
    public void ARaceSessionBodyThatBreaksTheTypeRulesIsRefused()
    {
        Assert.IsAssignableFrom<InvalidDataException>(Record.Exception(() => EventPayloadCodec.Decode(
            EventPayloadCodec.RaceSessionTag,
            FlatJson.Write(("season", 1955), ("round", 0), ("layoutId", "spa")))));
        Assert.IsAssignableFrom<InvalidDataException>(Record.Exception(() => EventPayloadCodec.Decode(
            EventPayloadCodec.RaceSessionTag,
            """{"season":"abc","round":"1","layoutId":"spa"}""")));
    }

    [Fact]
    public void RestoredQueueKeepsEverySequenceAndTheNextOneContinuesIt()
    {
        var queue = EventQueue.Empty
            .Enqueue(Event("e1", new GameDate(1951, 3, 1)))
            .Enqueue(Event("e2", new GameDate(1950, 3, 1)))
            .Enqueue(Event("e3", new GameDate(1951, 3, 1)));
        var taken = queue.TakeDue(new GameDate(1950, 3, 1));
        queue = taken.Queue;
        Assert.Equal(3UL, queue.NextSequence);

        var restored = EventQueue.Restore(queue.Events, queue.NextSequence);

        Assert.Equal(queue.NextSequence, restored.NextSequence);
        Assert.Equal(queue.Events.Select(Describe), restored.Events.Select(Describe));
        var next = queue.Enqueue(Event("e4", new GameDate(1951, 3, 1)));
        var nextRestored = restored.Enqueue(Event("e4", new GameDate(1951, 3, 1)));
        Assert.Equal(next.Events.Select(Describe), nextRestored.Events.Select(Describe));
        Assert.Equal(3UL, nextRestored.Events[^1].Sequence);

        // The input order does not matter: the queue sorts by date, then by sequence.
        var shuffled = EventQueue.Restore([.. queue.Events.Reverse()], queue.NextSequence);
        Assert.Equal(queue.Events.Select(Describe), shuffled.Events.Select(Describe));
    }

    [Fact]
    public void ARestoredQueueIsCheckedLikeAnEditedOne()
    {
        var one = Event("e1", new GameDate(1951, 1, 1)) with { Sequence = 4 };
        var two = Event("e2", new GameDate(1951, 1, 1)) with { Sequence = 5 };

        Assert.Throws<ArgumentException>(() => EventQueue.Restore([one, two], 5));
        var sameSequence = Event("e9", new GameDate(1951, 1, 1)) with { Sequence = 4 };
        var sameId = Event("e1", new GameDate(1951, 1, 1)) with { Sequence = 5 };
        Assert.Throws<ArgumentException>(() => EventQueue.Restore([one, sameSequence], 9));
        Assert.Throws<ArgumentException>(() => EventQueue.Restore([one, sameId], 9));
        Assert.Throws<ArgumentException>(() => EventQueue.Restore([one, null!], 9));
        Assert.Equal(2, EventQueue.Restore([one, two], 6).Count);
        Assert.Equal(0, EventQueue.Restore([], 0).Count);
        Assert.Equal(7UL, EventQueue.Restore([], 7).NextSequence);
    }

    private static ScheduledEvent Event(string id, GameDate date) =>
        new(new EventId(id), date, ScheduledEventType.Race, new MarkerPayload(id));

    private static string Describe(ScheduledEvent scheduled) =>
        scheduled.Id.Value + "|" + scheduled.Date + "|" + scheduled.Sequence;
}
