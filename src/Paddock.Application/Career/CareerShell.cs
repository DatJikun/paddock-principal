using Paddock.Application.Board;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Development;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Application.World;
using Paddock.Domain.Development;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Career;

namespace Paddock.Application.Career;

/// <summary>
/// One human career on the existing day loop. It is a host: it attaches <see cref="CareerModules.Default"/>, dispatches
/// commands, and asks <see cref="ReadyGate"/> before a day moves. It does not add the development or supply day handlers,
/// the season-change host, or the AI-principal director; those stay with the career-loop work. Development
/// <em>commands</em> are registered here when the module list has not registered them, so the player can set a split
/// (<see cref="DevelopmentRegistration"/>). The day that spends that split is not started.
/// </summary>
public sealed class CareerShell
{
    /// <summary>How many days <see cref="AdvanceUntilBlocked"/> will live before it stops on its own. A safety cap, not a rule.</summary>
    public const int AdvanceUntilCap = 400;

    private readonly CareerSession _session;
    private readonly CareerModuleHost _modules;
    private readonly CommandDispatcher _dispatcher;
    private readonly CommandQueue _queue;
    private readonly ManagerRegistry _managers;
    private readonly ReadyGate _gate = new();
    private readonly ClockWorld _clock;
    private GameDate? _morningOf;

    private CareerShell(
        CareerSession session,
        CareerModuleHost modules,
        CommandDispatcher dispatcher,
        CommandQueue queue,
        ManagerRegistry managers,
        ManagerId player,
        DevelopmentQuery development)
    {
        _session = session;
        _modules = modules;
        _dispatcher = dispatcher;
        _queue = queue;
        _managers = managers;
        Player = player;
        Development = development;
        _clock = new ClockWorld(session);
    }

    public CareerSession Session => _session;

    public ManagerId Player { get; private set; }

    public DevelopmentQuery Development { get; }

    public CareerModuleContext Modules => _modules.Context;

    public string WorldHash => _session.World.StateHash();

    public GameDate Date => _session.Date;

    public CareerHostState HostState => new(_managers, _dispatcher.Log, _queue.NextSubmissionNumber);

    public bool QueueIsEmpty => _queue.Count == 0;

    /// <summary>A new career. The human is registered and the modules are open. The first morning has not run yet.</summary>
    public static CareerShell Open(CareerSession session, CareerRunOptions options, string playerName, string? managerId = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(playerName);
        var managers = new ManagerRegistry();
        var shell = Attach(session, managers, new CommandDispatcher(), new CommandQueue(), options);
        shell.Player = managerId is null ? shell.RegisterHuman(playerName) : shell.RegisterNamedHuman(playerName, managerId);
        return shell;
    }

    /// <summary>A career read back from a save. <paramref name="player"/> must already be in <paramref name="host"/>.</summary>
    public static CareerShell Resume(CareerSession session, CareerHostState host, CareerRunOptions options, ManagerId player)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(options);
        if (!host.Managers.Contains(player) || host.Managers.KindOf(player) != ManagerKind.Human)
        {
            throw new ArgumentException("The loaded career has no such human manager.", nameof(player));
        }

        var shell = Attach(
            session,
            host.Managers,
            new CommandDispatcher(log: host.Log),
            new CommandQueue(host.NextSubmissionNumber),
            options);
        shell.Player = player;
        shell.SeatPlayer();
        return shell;
    }

    public ManagerId RegisterHuman(string displayName)
    {
        var number = 1;
        ManagerId id;
        do
        {
            id = new ManagerId("human:" + number.ToString(System.Globalization.CultureInfo.InvariantCulture));
            number++;
        }
        while (_managers.Contains(id));

        _managers.Register(id, ManagerKind.Human, displayName);
        return id;
    }

    /// <summary>Registers <paramref name="managerId"/> as the human. The bridge uses a fixed id the page already knows.</summary>
    public ManagerId RegisterNamedHuman(string displayName, string managerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(managerId);
        var id = new ManagerId(managerId);
        if (_managers.Contains(id))
        {
            throw new ArgumentException("Manager '" + managerId + "' is already registered.", nameof(managerId));
        }

        _managers.Register(id, ManagerKind.Human, displayName);
        return id;
    }

    public void Ready(ManagerId manager) => _managers.SetReady(manager, true);

    public CommandResult Submit(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _queue.Enqueue(command);
        var results = _dispatcher.DispatchAll(_queue, _modules.CommandContext(_clock, _managers));
        _modules.EndMorning();
        SeatPlayer();
        return results[results.Count - 1];
    }

    /// <summary>Once the player runs a team (after taking it over, or on load), the AI principal director leaves it alone.</summary>
    private void SeatPlayer()
    {
        if (Player.IsAssigned && TeamOf(Player) is { } team)
        {
            _modules.SeatHuman(Player, team);
        }
    }

    /// <summary>Files and dispatches the morning's commands. Once per morning; a second call on the same date does nothing.</summary>
    public void BeginDay()
    {
        if (_morningOf == _session.Date)
        {
            return;
        }

        if (_queue.Count > 0)
        {
            _dispatcher.DispatchAll(_queue, _modules.CommandContext(_clock, _managers));
        }

        _modules.BeginMorning(_queue);
        _dispatcher.DispatchAll(_queue, _modules.CommandContext(_clock, _managers));
        _modules.EndMorning();
        _morningOf = _session.Date;
    }

    public AdvanceResult Advance()
    {
        var result = _gate.RequestAdvance(_managers, _clock);
        if (result is AdvanceResult.Advanced)
        {
            BeginDay();
        }

        return result;
    }

    /// <summary>
    /// Marks <see cref="Player"/> ready and lives days until a blocking item, another human who is not ready, or
    /// <see cref="AdvanceUntilCap"/> days. This is the "next" button (PP-016).
    /// </summary>
    public AdvanceResult AdvanceUntilBlocked()
    {
        AdvanceResult last = new AdvanceResult.Advanced();
        for (var i = 0; i < AdvanceUntilCap; i++)
        {
            Ready(Player);
            last = Advance();
            if (last is AdvanceResult.Refused)
            {
                return last;
            }
        }

        return last;
    }

    public OrganizationId? TeamOf(ManagerId manager)
    {
        var board = Modules.TryGet<BoardBook>();
        if (board is null)
        {
            return null;
        }

        return board.Section.OrganizationOf(manager.Value);
    }

    private static CareerShell Attach(
        CareerSession session,
        ManagerRegistry managers,
        CommandDispatcher dispatcher,
        CommandQueue queue,
        CareerRunOptions options)
    {
        var ai = new ManagerId(CareerHost.AiManagerId);
        if (!managers.Contains(ai))
        {
            managers.Register(ai, ManagerKind.Ai, "AI");
        }

        var modules = CareerModuleHost.Attach(session, managers, dispatcher, ai, options.Modules, options.Inputs);
        var book = DevelopmentBook.ForSession(session, session.Clock.MasterSeed);
        var trace = modules.Context.TryGet<ContractBook>()?.Environment.Trace;
        var environment = new DevelopmentEnvironment(
            new PeriodDevelopmentRules([]),
            modules.Context.Require<IOrganizationControl>(),
            trace);
        if (!dispatcher.IsRegistered(typeof(SetDevelopmentSplitCommand)))
        {
            DevelopmentRegistration.Register(dispatcher, modules.Context.Require<InboxResolvers>(), book, environment);
        }

        return new CareerShell(session, modules, dispatcher, queue, managers, ai, new DevelopmentQuery(book, environment));
    }

    private sealed class ClockWorld : IWorldState
    {
        private readonly CareerSession _session;

        public ClockWorld(CareerSession session) => _session = session;

        public DateOnly CurrentDate => new(_session.Date.Year, _session.Date.Month, _session.Date.Day);

        public void AdvanceDate() => _session.LiveDay();

        public string ContentHash() => _session.World.StateHash();
    }
}
