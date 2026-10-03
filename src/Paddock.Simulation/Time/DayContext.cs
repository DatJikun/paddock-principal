using Paddock.Domain.Random;
using Paddock.Domain.Time;

namespace Paddock.Simulation.Time;

/// <summary>
/// What one handler may observe and record while a day is lived.
/// RNG is requested by stream name only. The season is today's year and no round is passed, via <see cref="RngStreams.Derive"/>.
/// </summary>
public sealed class DayContext
{
    private readonly Dictionary<RngStreamSlot, Xoshiro256StarStar> _streams = new();
    private readonly List<DomainEvent> _emitted = new();
    private readonly List<ScheduledEvent> _scheduled = new();
    private readonly IReadOnlyDictionary<RngStreamSlot, RngState> _saved;
    private ulong _nextEventId;

    internal DayContext(
        ulong masterSeed,
        GameDate today,
        IReadOnlyList<ScheduledEvent> dueEvents,
        IReadOnlyDictionary<RngStreamSlot, RngState> savedStreams,
        ulong nextEventId)
    {
        ArgumentNullException.ThrowIfNull(dueEvents);
        ArgumentNullException.ThrowIfNull(savedStreams);
        MasterSeed = masterSeed;
        Today = today;
        DueEvents = dueEvents;
        _saved = savedStreams;
        _nextEventId = nextEventId;
    }

    public ulong MasterSeed { get; }

    public GameDate Today { get; }

    public IReadOnlyList<ScheduledEvent> DueEvents { get; }

    internal IReadOnlyList<DomainEvent> Emitted => _emitted;

    internal IReadOnlyList<ScheduledEvent> Scheduled => _scheduled;

    internal IReadOnlyDictionary<RngStreamSlot, Xoshiro256StarStar> Streams => _streams;

    internal ulong NextEventId => _nextEventId;

    internal bool TouchedRng => _streams.Count > 0;

    /// <summary>
    /// The named stream for this season. The first request derives it from the master seed; a later day in the same season
    /// continues the saved state, so it does not repeat the first roll. The same name on the same day returns the same generator.
    /// A new season derives a fresh stream, so it does not depend on how many rolls the previous season used.
    /// </summary>
    public Xoshiro256StarStar Stream(string streamName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamName);
        var slot = new RngStreamSlot(streamName, Today.Year);
        if (_streams.TryGetValue(slot, out var existing))
        {
            return existing;
        }

        var created = _saved.TryGetValue(slot, out var saved)
            ? new Xoshiro256StarStar(saved)
            : RngStreams.Derive(MasterSeed, streamName, Today.Year);
        _streams.Add(slot, created);
        return created;
    }

    /// <summary>Queues an event strictly after today. The id is the next stable counter value.</summary>
    public void Schedule(GameDate date, string typeId, EventPayload payload)
    {
        if (date <= Today)
        {
            throw new ArgumentOutOfRangeException(
                nameof(date),
                date,
                "Handlers can only schedule an event after the current day.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
        ArgumentNullException.ThrowIfNull(payload);
        _scheduled.Add(new ScheduledEvent(TakeId(), date, typeId, payload));
    }

    /// <summary>Emits an extra domain event for today. The id is the next stable counter value.</summary>
    public void Emit(string typeId, EventPayload payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
        ArgumentNullException.ThrowIfNull(payload);
        _emitted.Add(new DomainEvent(TakeId(), Today, typeId, payload));
    }

    private EventId TakeId()
    {
        var id = EventId.FromCounter(_nextEventId);
        _nextEventId = checked(_nextEventId + 1);
        return id;
    }
}
