using Paddock.Application.Access;
using Paddock.Application.Ai;
using Paddock.Application.Commands;
using Paddock.Application.Managers;
using Paddock.Application.World;
using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Simulation.Career;
using AccessManagerId = Paddock.Application.Access.ManagerId;
using HostManagerId = Paddock.Application.Managers.ManagerId;

namespace Paddock.Application.Career;

/// <summary>
/// What one AI-only run produced. Human managers stay at zero, so <see cref="ReadyGate"/> never blocks.
/// Commands dispatched stay at zero until a later task gives the AI something to order.
/// </summary>
public sealed class CareerRunResult
{
    internal CareerRunResult(CareerSession session, int aiManagers, int humanManagers, int commandsDispatched)
    {
        Session = session;
        AiManagers = aiManagers;
        HumanManagers = humanManagers;
        CommandsDispatched = commandsDispatched;
    }

    public CareerSession Session { get; }

    public int AiManagers { get; }

    public int HumanManagers { get; }

    public int CommandsDispatched { get; }
}

/// <summary>
/// Wires the day clock (T16), the AI managers and the ready gate (T17), and a world already built (T20).
/// Each morning the single AI manager looks at an empty knowledge view and files no command.
/// The queue is then drained, and the gate advances one day. Zero human managers never block.
/// </summary>
public static class CareerHost
{
    public const string AiManagerId = "ai:paddock";

    public static CareerRunResult Run(CareerSession session, int toYear)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Date.Month != 1 || session.Date.Day != 1)
        {
            throw new ArgumentException("The run starts on 1 January.", nameof(session));
        }

        // The morning after the last season must still be a calendar date, so the last season cannot be year 9999.
        if (toYear < session.Date.Year || toYear >= GenerationEstimates.MaxSeason)
        {
            throw new ArgumentOutOfRangeException(nameof(toYear), toYear, "The last season is out of range.");
        }

        var managers = new ManagerRegistry();
        var hostId = new HostManagerId(AiManagerId);
        managers.Register(hostId, ManagerKind.Ai, "AI");
        var ai = new AiActor(new AccessManagerId(AiManagerId));
        var view = new EmptyKnowledgeView(AccessContext.ForAi(ai.Id));
        var fact = new FactKey("career.day");
        var queue = new CommandQueue();
        var dispatcher = new CommandDispatcher();
        var world = new ClockWorld(session);
        var context = new CommandContext(world, managers);
        var gate = new ReadyGate();
        var end = GameDate.SeasonStart(toYear + 1);
        var commands = 0;
        while (session.Date < end)
        {
            _ = ai.Perceive(view, fact);
            commands += dispatcher.DispatchAll(queue, context).Count;
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

        return new CareerRunResult(session, ais, humans, commands);
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
