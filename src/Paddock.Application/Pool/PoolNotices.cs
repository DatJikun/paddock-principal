using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Application.Objectives;
using Paddock.Domain.Inbox;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Pool;
using Paddock.Simulation.Time;

namespace Paddock.Application.Pool;

/// <summary>
/// Tells a team when a junior of its academy leaves the pool without a contract (#268): his time ran out, or he grew too old. The day handler
/// removes him and records the academy he left on the lapsed career; this reads those records for the events of the day and posts one notice
/// to each human manager of that team. Nothing here changes the pool, and no random number is drawn. A team with no human manager gets nothing,
/// like every other notice (an AI has nobody to read an inbox).
/// </summary>
public static class PoolNotices
{
    public static int Apply(
        PoolBook book,
        IEnumerable<DomainEvent> events,
        InboxBook inbox,
        ManagerRegistry managers,
        IManagerOrganizations organizations)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(inbox);
        ArgumentNullException.ThrowIfNull(managers);
        ArgumentNullException.ThrowIfNull(organizations);
        var lapsed = book.Section.Lapsed.ToDictionary(career => career.Id.Value, StringComparer.Ordinal);
        var posted = 0;
        foreach (var domainEvent in events)
        {
            if (domainEvent.TypeId != PoolEventTypes.CareerLapsed
                || domainEvent.Payload is not MarkerPayload marker
                || !lapsed.TryGetValue(marker.Marker, out var career)
                || career.Academy is not { } academy)
            {
                continue;
            }

            var name = book.World.GetPerson(career.Id).Name;
            foreach (var manager in managers.All.Where(candidate => candidate.Kind == ManagerKind.Human))
            {
                if (organizations.OrganizationOf(manager.Id.Value) != academy)
                {
                    continue;
                }

                inbox.Post(
                    managers,
                    manager.Id,
                    new InboxItemDraft(
                        PoolKeys.InboxKind,
                        PoolKeys.InboxLapsedSubject,
                        [new KeyValuePair<string, string>("name", name)],
                        null,
                        null,
                        null),
                    new GameDate(domainEvent.Date.Year, domainEvent.Date.Month, domainEvent.Date.Day));
                posted++;
            }
        }

        return posted;
    }
}
