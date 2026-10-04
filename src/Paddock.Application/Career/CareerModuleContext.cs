using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Managers;
using Paddock.Simulation.Career;
using Paddock.Simulation.Time;

namespace Paddock.Application.Career;

/// <summary>
/// What the host hands a module (<see cref="ICareerModule"/>): the session and the managers it works with, a place to register
/// what it adds, and a small bag of per-run services shared between modules. A module registers through this and nowhere else,
/// so the host is the only place that knows how a day is put together.
/// </summary>
public sealed class CareerModuleContext
{
    private readonly CommandDispatcher _dispatcher;
    private readonly Dictionary<Type, object> _services = [];
    private readonly List<IDayHandler> _handlers = [];
    private readonly List<WorldSync> _syncs = [];
    private readonly List<Action<CommandQueue>> _morning = [];
    private bool _frozen;

    internal CareerModuleContext(CareerSession session, ManagerRegistry managers, CommandDispatcher dispatcher, ManagerId ai)
    {
        Session = session;
        Managers = managers;
        _dispatcher = dispatcher;
        Ai = ai;
    }

    public CareerSession Session { get; }

    public ManagerRegistry Managers { get; }

    /// <summary>The manager the host files placeholder AI commands as, until the AI principals (T44) own that.</summary>
    public ManagerId Ai { get; }

    internal IReadOnlyList<IDayHandler> DayHandlers => _handlers;

    /// <summary>Makes a per-run service available to the modules that run after this one (and to a late-bound delegate).</summary>
    public void Provide<T>(T service)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(service);
        Guard();
        if (!_services.TryAdd(typeof(T), service))
        {
            throw new InvalidOperationException("A service of type " + typeof(T).Name + " is already provided.");
        }
    }

    /// <summary>
    /// Wraps a service the host or an earlier module provided (the board wraps who-runs-what with who-holds-the-post). Throws when
    /// there is nothing to wrap.
    /// </summary>
    public void Decorate<T>(Func<T, T> wrap)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(wrap);
        Guard();
        var inner = Require<T>();
        _services[typeof(T)] = wrap(inner) ?? throw new InvalidOperationException("A decorator of " + typeof(T).Name + " returned null.");
    }

    /// <summary>The provided service of that type, or null when no module offered one.</summary>
    public T? TryGet<T>()
        where T : class =>
        _services.TryGetValue(typeof(T), out var service) ? (T)service : null;

    /// <summary>The provided service of that type. Throws when no module offered one, which means a module is missing from the list.</summary>
    public T Require<T>()
        where T : class =>
        TryGet<T>() ?? throw new InvalidOperationException("No module provided a " + typeof(T).Name + ". Is its module in the list?");

    /// <summary>Registers a command handler. Two handlers for one command type are refused by the dispatcher.</summary>
    public void AddCommandHandler(ICommandHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Guard();
        _dispatcher.Register(handler);
    }

    /// <summary>
    /// Registers the handlers a system already has a registration helper for (<see cref="ContractRegistration"/> and the like),
    /// which take the dispatcher itself.
    /// </summary>
    public void AddCommandHandlers(Action<CommandDispatcher> registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        Guard();
        registration(_dispatcher);
    }

    /// <summary>
    /// Registers a day handler that reads and writes the session's world itself. Its place in the day is its
    /// <see cref="IDayHandler.Order"/>, nothing else.
    /// </summary>
    public void AddDayHandler(IDayHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Guard();
        _handlers.Add(handler);
    }

    /// <summary>
    /// Registers a day handler that works on books (see <see cref="AddWorldSync"/>): every registered book is refreshed from the
    /// session's world before the handler runs and flushed back after it.
    /// </summary>
    public void AddBookDayHandler(IDayHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Guard();
        _handlers.Add(new SyncedDayHandler(handler, this));
    }

    /// <summary>
    /// Registers a book that holds part of the world (a section, or a working copy of it) between the moments the session's
    /// world is read. <paramref name="refresh"/> takes the session's world in, <paramref name="flush"/> puts the book's state back.
    /// The host calls every refresh before a morning and a book day handler, and every flush after them, in registration order.
    /// </summary>
    public void AddWorldSync(Action refresh, Action flush)
    {
        ArgumentNullException.ThrowIfNull(refresh);
        ArgumentNullException.ThrowIfNull(flush);
        Guard();
        _syncs.Add(new WorldSync(refresh, flush));
    }

    /// <summary>
    /// Registers something that runs each morning after the books are refreshed and before the queue is dispatched: it files the
    /// commands a placeholder AI gives today. It may enqueue commands and nothing else.
    /// </summary>
    public void AddMorning(Action<CommandQueue> morning)
    {
        ArgumentNullException.ThrowIfNull(morning);
        Guard();
        _morning.Add(morning);
    }

    internal void Freeze() => _frozen = true;

    internal void Refresh()
    {
        foreach (var sync in _syncs)
        {
            sync.Refresh();
        }
    }

    internal void Flush()
    {
        foreach (var sync in _syncs)
        {
            sync.Flush();
        }
    }

    internal void RunMorning(CommandQueue queue)
    {
        foreach (var morning in _morning)
        {
            morning(queue);
        }
    }

    private void Guard()
    {
        if (_frozen)
        {
            throw new InvalidOperationException("Modules register while the host is attaching them, not later.");
        }
    }

    private readonly record struct WorldSync(Action Refresh, Action Flush);

    private sealed class SyncedDayHandler : IDayHandler
    {
        private readonly IDayHandler _inner;
        private readonly CareerModuleContext _context;

        public SyncedDayHandler(IDayHandler inner, CareerModuleContext context)
        {
            _inner = inner;
            _context = context;
            Order = inner.Order;
        }

        public int Order { get; }

        public void OnDay(DayContext context)
        {
            _context.Refresh();
            _inner.OnDay(context);
            _context.Flush();
        }
    }
}
