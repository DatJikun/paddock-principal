using Paddock.Domain.Time;

namespace Paddock.Simulation.Time;

/// <summary>
/// One future fact on the calendar. <see cref="Sequence"/> is assigned by <see cref="EventQueue"/> and is not part of the caller's identity.
/// </summary>
public sealed record ScheduledEvent
{
    public ScheduledEvent(EventId id, GameDate date, string typeId, EventPayload payload)
    {
        if (string.IsNullOrWhiteSpace(id.Value))
        {
            throw new ArgumentException("Event id is required.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
        ArgumentNullException.ThrowIfNull(payload);

        Id = id;
        Date = date;
        TypeId = typeId;
        Payload = payload;
    }

    public EventId Id { get; }

    public GameDate Date { get; }

    public string TypeId { get; }

    public EventPayload Payload { get; }

    /// <summary>Insertion sequence assigned by the queue. Lower runs first when dates are equal.</summary>
    public ulong Sequence { get; init; }
}
