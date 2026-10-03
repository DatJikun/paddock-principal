namespace Paddock.Simulation.Time;

/// <summary>
/// The state after one lived day, plus the domain events emitted while living it.
/// <see cref="WorldClockState.Date"/> on <see cref="State"/> is the next morning.
/// </summary>
public readonly record struct DayAdvance(WorldClockState State, IReadOnlyList<DomainEvent> Events)
{
    public static IReadOnlyList<DomainEvent> None { get; } = [];
}
