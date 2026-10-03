namespace Paddock.Simulation.Time;

/// <summary>
/// Handlers ordered by <see cref="IDayHandler.Order"/> ascending, then by registration order.
/// </summary>
public sealed class DayHandlerRegistry
{
    public static DayHandlerRegistry Empty { get; } = new(Array.Empty<IDayHandler>());

    private readonly IDayHandler[] _handlers;

    public DayHandlerRegistry(IEnumerable<IDayHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        var indexed = new List<(IDayHandler Handler, int Index)>();
        var index = 0;
        foreach (var handler in handlers)
        {
            ArgumentNullException.ThrowIfNull(handler);
            indexed.Add((handler, index));
            index++;
        }

        indexed.Sort(static (left, right) =>
        {
            var order = left.Handler.Order.CompareTo(right.Handler.Order);
            return order != 0 ? order : left.Index.CompareTo(right.Index);
        });

        _handlers = new IDayHandler[indexed.Count];
        for (var i = 0; i < indexed.Count; i++)
        {
            _handlers[i] = indexed[i].Handler;
        }
    }

    public IReadOnlyList<IDayHandler> Handlers => _handlers;
}
