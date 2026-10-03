using System.Globalization;

namespace Paddock.Simulation.Time;

/// <summary>
/// Stable identity of one scheduled or emitted event. Generated ids come from the clock counter and are never reused.
/// </summary>
public readonly record struct EventId : IComparable<EventId>
{
    public EventId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; } = "";

    public static EventId FromCounter(ulong counter) =>
        new(string.Create(CultureInfo.InvariantCulture, $"e{counter}"));

    public int CompareTo(EventId other) => string.CompareOrdinal(Value, other.Value);

    public override string ToString() => Value;
}
