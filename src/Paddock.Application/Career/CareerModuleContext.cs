using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Managers;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Career;
using Paddock.Simulation.Time;

namespace Paddock.Application.Career;

/// <summary>
/// What the host hands a module (<see cref="ICareerModule"/>): the session and the managers it works with, a place to register
/// what it adds, and a small bag of per-run services shared between modules. A module registers through this and nowhere else,
/// so the host is the only place that knows how a day is put together.
/// <para>
/// <b>One world.</b> The session owns the world. A system that keeps a mutable book of its own (contracts, the inbox) keeps its
/// section there and offers <see cref="AddFlush"/>, which puts the section into the session's world at the end of the morning and
/// of the day, before anything is hashed or saved. A book that changes people and contracts reads and writes the session's world
/// directly (see <see cref="ContractBook.Bind"/>), so no two systems ever hold two versions of it.
/// </para>
/// </summary>
public sealed class CareerModuleContext
{
    private readonly CommandDispatcher _dispatcher;
    private readonly Dictionary<Type, object> _services = [];
    private readonly Dictionary<Type, List<object>> _many = [];
    private readonly List<IDayHandler> _handlers = [];
    private readonly List<Func<WorldState, WorldState>> _flushes = [];
    private readonly List<(int Order, Action<IReadOnlyList<DomainEvent>> Hook)> _afterDay = [];
    private readonly List<Action<CommandQueue>> _morning = [];
    private readonly List<Action<GameDate>> _seasonChange = [];
    private readonly List<Action<ICommand>> _beforeCommand = [];
    private readonly List<Action<ICommand>> _afterCommand = [];
    private bool _frozen;

    internal CareerModuleContext(CareerSession session, ManagerRegistry managers, CommandDispatcher dispatcher, ManagerId ai, CareerInputs inputs)
    {
        Session = session;
        Managers = managers;
        _dispatcher = dispatcher;
        Ai = ai;
        Inputs = inputs;
    }

    /// <summary>The data the host loaded. A member that is null means the host has no such data.</summary>
    public CareerInputs Inputs { get; }

    public CareerSession Session { get; }

    public ManagerRegistry Managers { get; }

    /// <summary>The accepted commands of this run. A module may read them while attaching; it does not append.</summary>
    public CommandLog CommandLog => _dispatcher.Log;

    /// <summary>The manager the host files placeholder AI commands as, until the AI principals (T44) own that.</summary>
    public ManagerId Ai { get; }

    internal IReadOnlyList<IDayHandler> DayHandlers => _handlers;

    /// <summary>Makes a per-run service available to the modules that attach after this one.</summary>
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

    /// <summary>
    /// Adds one of several services of a kind (every system that owes or earns money adds an <c>ILedgerSource</c>). Read them with
    /// <see cref="All{T}"/>, at the time they are needed rather than when the module attaches, so the order of the list does not matter.
    /// </summary>
    public void Add<T>(T service)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(service);
        Guard();
        if (!_many.TryGetValue(typeof(T), out var list))
        {
            list = [];
            _many.Add(typeof(T), list);
        }

        list.Add(service);
    }

    /// <summary>Every service added of that kind, in the order they were added.</summary>
    public IReadOnlyList<T> All<T>()
        where T : class =>
        _many.TryGetValue(typeof(T), out var list) ? list.Cast<T>().ToArray() : [];

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
    /// Registers a day handler. Its place in the day is its <see cref="IDayHandler.Order"/>, nothing else: not the place of its
    /// module in the list. It reads and writes the session's world (through a bound book or the session itself).
    /// </summary>
    public void AddDayHandler(IDayHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Guard();
        _handlers.Add(handler);
    }

    /// <summary>
    /// Registers how a book puts its section into the session's world. The host calls every flush, in registration order, at the
    /// end of each morning and at the end of each lived day, so the world a hash or a save reads is complete.
    /// </summary>
    public void AddFlush(Func<WorldState, WorldState> into)
    {
        ArgumentNullException.ThrowIfNull(into);
        Guard();
        _flushes.Add(into);
    }

    /// <summary>
    /// Registers something that runs at the end of a lived day with the events the day emitted, after the day's handlers and
    /// before the flushes (the outcomes of objectives, sponsors and the board). Hooks run in ascending
    /// <paramref name="order"/>, then in registration order. It may change the world through the books it holds, and nothing else.
    /// </summary>
    public void AddAfterDay(int order, Action<IReadOnlyList<DomainEvent>> hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        Guard();
        _afterDay.Add((order, hook));
    }

    /// <summary>
    /// Registers something that runs each morning before the queue is dispatched: it files the commands a placeholder AI gives
    /// today. It may enqueue commands and nothing else.
    /// </summary>
    public void AddMorning(Action<CommandQueue> morning)
    {
        ArgumentNullException.ThrowIfNull(morning);
        Guard();
        _morning.Add(morning);
    }

    /// <summary>
    /// Runs before each command is dispatched. A book that keeps its own copy of the world refreshes it here, so a command sees
    /// what the previous command wrote.
    /// </summary>
    public void AddBeforeCommand(Action<ICommand> hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        Guard();
        _beforeCommand.Add(hook);
    }

    /// <summary>
    /// Runs after each command is dispatched, accepted or rejected. A book puts its section back into the session world here when
    /// the command was one of its own, so the next command reads one world.
    /// </summary>
    public void AddAfterCommand(Action<ICommand> hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        Guard();
        _afterCommand.Add(hook);
    }

    /// <summary>
    /// Registers a step of the change of season. The host runs every such step once, on 1 January, before any other day handler
    /// (order order 5), and emits <see cref="CareerEventType.SeasonChanged"/>. A system that has
    /// something to move to the new year (cars to the new model year, next year's budget) does it here, so the daily handlers only
    /// react to the season they find (owner decision, #160).
    /// </summary>
    public void AddSeasonChange(Action<GameDate> step)
    {
        ArgumentNullException.ThrowIfNull(step);
        Guard();
        _seasonChange.Add(step);
    }

    internal void ChangeSeason(GameDate today)
    {
        foreach (var step in _seasonChange)
        {
            step(today);
        }
    }

    internal void Freeze()
    {
        _frozen = true;
        var index = 0;
        var indexed = _afterDay.Select(entry => (entry.Order, Index: index++, entry.Hook)).ToList();
        indexed.Sort(static (left, right) =>
        {
            var order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : left.Index.CompareTo(right.Index);
        });
        _afterDay.Clear();
        foreach (var entry in indexed)
        {
            _afterDay.Add((entry.Order, entry.Hook));
        }
    }

    internal void Flush()
    {
        var world = Session.World;
        foreach (var into in _flushes)
        {
            world = into(world);
        }

        Session.StoreWorld(world);
    }

    internal void AfterDay(IReadOnlyList<DomainEvent> events)
    {
        foreach (var (_, hook) in _afterDay)
        {
            hook(events);
        }

        // Whatever a book still holds goes into the world before the world is hashed or saved.
        Flush();
    }

    internal void RunMorning(CommandQueue queue)
    {
        foreach (var morning in _morning)
        {
            morning(queue);
        }
    }

    internal void BeforeCommand(ICommand command)
    {
        foreach (var hook in _beforeCommand)
        {
            hook(command);
        }
    }

    internal void AfterCommand(ICommand command)
    {
        foreach (var hook in _afterCommand)
        {
            hook(command);
        }
    }

    private void Guard()
    {
        if (_frozen)
        {
            throw new InvalidOperationException("Modules register while the host is attaching them, not later.");
        }
    }
}
