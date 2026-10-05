using Paddock.Application.Development;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Career;
using Paddock.Simulation.Time;

namespace Paddock.Application.Racing;

/// <summary>
/// The next championship race on the day clock. Every team shares the calendar (PP-050: one grid). A pure read (INV-005).
/// </summary>
public sealed class ChampionshipCalendar : INextRaceSource
{
    private readonly CareerSession _session;

    public ChampionshipCalendar(CareerSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
    }

    public GameDate? NextRaceOnOrAfter(OrganizationId organization, GameDate day)
    {
        foreach (var scheduled in _session.Clock.Queue.Events)
        {
            if (scheduled.TypeId == ScheduledEventType.Race && scheduled.Date >= day)
            {
                return scheduled.Date;
            }
        }

        return null;
    }
}
