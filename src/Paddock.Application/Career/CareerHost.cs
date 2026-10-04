using Paddock.Application.Access;
using Paddock.Application.Ai;
using Paddock.Application.Commands;
using Paddock.Application.Managers;
using Paddock.Application.World;
using Paddock.Domain.People;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Simulation.Career;
using Paddock.Simulation.Racing;
using AccessManagerId = Paddock.Application.Access.ManagerId;
using HostManagerId = Paddock.Application.Managers.ManagerId;

namespace Paddock.Application.Career;

/// <summary>
/// What one AI-only run produced. Human managers stay at zero, so <see cref="ReadyGate"/> never blocks.
/// Commands dispatched stay at zero until a later task gives the AI something to order.
/// </summary>
public sealed class CareerRunResult
{
    internal CareerRunResult(CareerSession session, int aiManagers, int humanManagers, int commandsDispatched, CareerHostState host)
    {
        Session = session;
        AiManagers = aiManagers;
        HumanManagers = humanManagers;
        CommandsDispatched = commandsDispatched;
        Host = host;
    }

    public CareerSession Session { get; }

    public int AiManagers { get; }

    public int HumanManagers { get; }

    public int CommandsDispatched { get; }

    /// <summary>The host's own state at the end of the run: what a save keeps beside the session.</summary>
    public CareerHostState Host { get; }
}

/// <summary>
/// What the host keeps between days besides the world: the managers, the log of accepted commands, and the counter that
/// numbers the next command. Saved with the world so a loaded career carries on with the same managers and never reuses a
/// submission number (INV-009). The queue is not part of it: a save is taken on a day boundary, after the queue is drained.
/// </summary>
public sealed class CareerHostState
{
    public CareerHostState(ManagerRegistry managers, CommandLog log, long nextSubmissionNumber)
    {
        ArgumentNullException.ThrowIfNull(managers);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentOutOfRangeException.ThrowIfLessThan(nextSubmissionNumber, 1);
        Managers = managers;
        Log = log;
        NextSubmissionNumber = nextSubmissionNumber;
    }

    public ManagerRegistry Managers { get; }

    public CommandLog Log { get; }

    public long NextSubmissionNumber { get; }
}

/// <summary>
/// Wires the day clock (T16), the AI managers and the ready gate (T17), and a world already built (T20).
/// The systems themselves (contracts, the pool commands, and the ones that follow) are <see cref="ICareerModule"/>s listed in
/// <see cref="CareerModules.Default"/>; this class only runs the day. Each morning the modules file their placeholder AI
/// commands (contract renewals until T44), the queue is drained, and the gate advances one day. Zero human managers never block.
/// </summary>
public static class CareerHost
{
    public const string AiManagerId = "ai:paddock";

    /// <summary>
    /// Runs one race for the career through <see cref="IRaceSimulator"/>. The day clock does not schedule races yet;
    /// this is the call a race day will make, and a test can pass a stub simulator.
    /// </summary>
    public static RaceTape SimulateRace(IRaceSimulator simulator, RaceSimulationRequest request) =>
        RaceSession.Run(simulator, request);

    public static CareerRunResult Run(CareerSession session, int toYear) => Run(session, toYear, null);

    /// <summary>
    /// Runs through 31 December of <paramref name="toYear"/>. With <paramref name="resumeFrom"/> the managers, the command log
    /// and the submission counter are the saved ones; without it a fresh AI manager is registered.
    /// </summary>
    public static CareerRunResult Run(CareerSession session, int toYear, CareerHostState? resumeFrom) =>
        Run(session, toYear, resumeFrom, CareerModules.Default);

    /// <summary>
    /// The same run with the systems of <paramref name="modules"/>. <see cref="CareerModules.Default"/> is what a career runs;
    /// another list is for a test that adds one module, or a tool that runs a subset.
    /// </summary>
    public static CareerRunResult Run(CareerSession session, int toYear, CareerHostState? resumeFrom, IReadOnlyList<ICareerModule> modules)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(modules);
        if (session.Date.Month != 1 || session.Date.Day != 1)
        {
            throw new ArgumentException("The run starts on 1 January.", nameof(session));
        }

        // The morning after the last season must still be a calendar date, so the last season cannot be year 9999.
        if (toYear < session.Date.Year || toYear >= GenerationEstimates.MaxSeason)
        {
            throw new ArgumentOutOfRangeException(nameof(toYear), toYear, "The last season is out of range.");
        }

        var managers = resumeFrom?.Managers ?? new ManagerRegistry();
        var hostId = new HostManagerId(AiManagerId);
        if (!managers.Contains(hostId))
        {
            managers.Register(hostId, ManagerKind.Ai, "AI");
        }

        var ai = new AiActor(new AccessManagerId(AiManagerId));
        var view = new EmptyKnowledgeView(AccessContext.ForAi(ai.Id));
        var fact = new FactKey("career.day");
        var queue = resumeFrom is null ? new CommandQueue() : new CommandQueue(resumeFrom.NextSubmissionNumber);
        var dispatcher = new CommandDispatcher(log: resumeFrom?.Log);
        var modulesHost = CareerModuleHost.Attach(session, managers, dispatcher, hostId, modules);
        var world = new ClockWorld(session);
        var context = modulesHost.CommandContext(world, managers);
        var gate = new ReadyGate();
        var end = GameDate.SeasonStart(toYear + 1);
        var commands = 0;
        while (session.Date < end)
        {
            _ = ai.Perceive(view, fact);
            modulesHost.BeginMorning(queue);
            commands += dispatcher.DispatchAll(queue, context).Count;
            modulesHost.EndMorning();
            var step = gate.RequestAdvance(managers, world);
            if (step is AdvanceResult.Refused refused)
            {
                throw new InvalidOperationException(
                    "AI-only time was blocked: " + refused.Refusal.Reason.Key + ".");
            }
        }

        var humans = 0;
        var ais = 0;
        foreach (var manager in managers.All)
        {
            if (manager.Kind == ManagerKind.Human)
            {
                humans++;
            }
            else if (manager.Kind == ManagerKind.Ai)
            {
                ais++;
            }
        }

        return new CareerRunResult(
            session,
            ais,
            humans,
            commands,
            new CareerHostState(managers, dispatcher.Log, queue.NextSubmissionNumber));
    }

    private sealed class ClockWorld : IWorldState
    {
        private readonly CareerSession _session;

        public ClockWorld(CareerSession session) => _session = session;

        public DateOnly CurrentDate => new(_session.Date.Year, _session.Date.Month, _session.Date.Day);

        public void AdvanceDate() => _session.LiveDay();

        public string ContentHash() => _session.World.StateHash();
    }

    private sealed class EmptyKnowledgeView : IKnowledgeView
    {
        public EmptyKnowledgeView(AccessContext viewer) => Viewer = viewer;

        public AccessContext Viewer { get; }

        public Known<double> Get(FactKey fact) => Known<double>.Unknown;
    }
}
