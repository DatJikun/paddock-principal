namespace Paddock.Simulation.Time;

/// <summary>
/// Immutable facts carried by a scheduled event. Subtypes are a closed set: the state hash names each one.
/// </summary>
public abstract record EventPayload;

/// <summary>
/// Which championship session a placeholder weekend day refers to. No race is run.
/// </summary>
public sealed record RaceSessionPayload : EventPayload
{
    public RaceSessionPayload(int season, int round, string layoutId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(season);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(round);
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutId);
        Season = season;
        Round = round;
        LayoutId = layoutId;
    }

    public int Season { get; }

    public int Round { get; }

    public string LayoutId { get; }
}

/// <summary>
/// A named token a handler can attach when it schedules follow-up work. This is an identifier, not player text.
/// </summary>
public sealed record MarkerPayload : EventPayload
{
    public MarkerPayload(string marker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);
        Marker = marker;
    }

    public string Marker { get; }
}
