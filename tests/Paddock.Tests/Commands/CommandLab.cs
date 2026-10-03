using Paddock.Application.Commands;
using Paddock.Application.Managers;
using Paddock.Application.World;

namespace Paddock.Tests.Commands;

internal static class CommandLab
{
    public static readonly DateOnly OpeningDay = new(1950, 5, 13);

    public static ManagerId Anna { get; } = new("mgr-anna");

    public static ManagerId Bram { get; } = new("mgr-bram");

    public static ManagerId Bot { get; } = new("mgr-bot");

    public static (ManagerRegistry Managers, StubWorldState World, CommandContext Context) Open(
        params (ManagerId Id, ManagerKind Kind, string Name)[] managers)
    {
        var registry = new ManagerRegistry();
        var world = new StubWorldState(OpeningDay);
        foreach (var (id, kind, name) in managers)
        {
            registry.Register(id, kind, name);
            world.SetManagerName(id.Value, name);
        }

        return (registry, world, new CommandContext(world, registry));
    }

    public static CommandDispatcher Dispatcher(IDecisionTrace? trace = null, params ICommandHandler[] handlers)
    {
        var dispatcher = new CommandDispatcher(trace);
        foreach (var handler in handlers)
        {
            dispatcher.Register(handler);
        }

        return dispatcher;
    }

    public static CommandDispatcher NotesAndNames(IDecisionTrace? trace = null)
    {
        return Dispatcher(trace, new SetTeamNoteHandler(), new RenameManagerHandler());
    }
}
