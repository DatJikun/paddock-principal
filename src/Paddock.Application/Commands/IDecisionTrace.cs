using Paddock.Application.World;

namespace Paddock.Application.Commands;

/// <summary>
/// Hook for the AI step of the command pipeline (TECH §5): validate, then this, then execute.
/// The default <see cref="NoOpDecisionTrace"/> records nothing.
/// Implementations must not mutate the world and must not draw RNG.
/// </summary>
public interface IDecisionTrace
{
    void Record(ICommand command, IWorldState world);
}
