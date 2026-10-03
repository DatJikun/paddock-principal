using Paddock.Application.Managers;
using Paddock.Application.World;

namespace Paddock.Application.Commands;

public sealed class CommandContext
{
    public CommandContext(IWorldState world, ManagerRegistry managers)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(managers);
        World = world;
        Managers = managers;
    }

    public IWorldState World { get; }

    public ManagerRegistry Managers { get; }
}
