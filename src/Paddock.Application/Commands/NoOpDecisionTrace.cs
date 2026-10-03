using Paddock.Application.World;

namespace Paddock.Application.Commands;

public sealed class NoOpDecisionTrace : IDecisionTrace
{
    public void Record(ICommand command, IWorldState world)
    {
    }
}
