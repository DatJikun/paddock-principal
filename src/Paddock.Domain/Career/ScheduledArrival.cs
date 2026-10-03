using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Career;

/// <summary>
/// A person who joins the world, and the talent pool, on <see cref="On"/>.
/// The spec is already rolled: admitting it does not draw again.
/// </summary>
public sealed class ScheduledArrival
{
    public ScheduledArrival(GameDate on, PersonSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        On = on;
        Spec = spec;
    }

    public GameDate On { get; }

    public PersonSpec Spec { get; }
}
