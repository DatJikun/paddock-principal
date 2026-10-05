namespace Paddock.Simulation.Career;

/// <summary>Machine identifiers of the placeholder day events. Not player text. The pool's own events are <c>PoolEventTypes</c>.</summary>
public static class CareerEventType
{
    public const string Retired = "life.retired";

    public const string ContractExpired = "contract.expired";

    public const string SeasonClosed = "season.closed";

    /// <summary>1 January: the host has moved every system to the new season. Emitted once, before the day's other handlers.</summary>
    public const string SeasonChanged = "season.changed";
}
