using Paddock.Domain.Time;

namespace Paddock.Simulation.Time;

/// <summary>
/// A fact emitted while living one day. Due calendar entries are emitted with their own id; handler emissions get a new id.
/// </summary>
public sealed record DomainEvent(EventId Id, GameDate Date, string TypeId, EventPayload Payload);
