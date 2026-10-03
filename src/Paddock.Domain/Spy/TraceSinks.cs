namespace Paddock.Domain.Spy;

/// <summary>
/// Receives decision traces. Implementations are passive observers: they must not touch RNG or
/// simulation state (INV-006). Producers may skip building a trace when <see cref="IsEnabled"/> is false.
/// </summary>
public interface ITraceSink
{
    bool IsEnabled { get; }

    void Record(DecisionTrace trace);
}

public sealed class NullSink : ITraceSink
{
    public static NullSink Instance { get; } = new();

    public bool IsEnabled => false;

    public void Record(DecisionTrace trace)
    {
    }
}

/// <summary>
/// RAM buffer for the current weekend: a ring buffer that drops the oldest trace when full and is
/// cleared when a trace for another weekend arrives. Key decisions survive weekend changes, since
/// those are what a save would keep.
/// </summary>
public sealed class MemorySink : ITraceSink
{
    private readonly int _capacity;
    private readonly Queue<DecisionTrace> _ring = new();
    private readonly List<DecisionTrace> _keyDecisions = [];
    private WeekendKey? _weekend;

    public MemorySink(int capacity = 4096)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
    }

    public bool IsEnabled => true;

    public WeekendKey? CurrentWeekend => _weekend;

    public IReadOnlyList<DecisionTrace> Traces => _ring.ToArray();

    public IReadOnlyList<DecisionTrace> KeyDecisions => _keyDecisions.ToArray();

    public void Record(DecisionTrace trace)
    {
        ArgumentNullException.ThrowIfNull(trace);
        if (_weekend != trace.Weekend)
        {
            _ring.Clear();
            _weekend = trace.Weekend;
        }

        if (_ring.Count == _capacity)
        {
            _ring.Dequeue();
        }

        _ring.Enqueue(trace);
        if (trace.IsKeyDecision)
        {
            _keyDecisions.Add(trace);
        }
    }
}
