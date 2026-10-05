using System.Globalization;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Domain.Finance;
using Paddock.Domain.Inbox;
using Paddock.Domain.Infrastructure;
using Paddock.Domain.Time;
using Paddock.Simulation.Time;

namespace Paddock.Application.Infrastructure;

/// <summary>
/// One lived day of facilities: a build that has reached its end day becomes usable and posts an inbox notice; on 1 January
/// each owned facility pays upkeep. Quiet days write nothing.
/// </summary>
public sealed class InfrastructureDayHandler : IDayHandler
{
    /// <summary>ESTIMATE: after supply (760), before development (780) so a finished build feeds that morning's car work.</summary>
    public const int DefaultOrder = 770;

    private readonly InfrastructureBook _book;
    private readonly InfrastructureEnvironment _environment;
    private readonly InboxBook? _inbox;
    private readonly ManagerRegistry? _managers;

    public InfrastructureDayHandler(
        InfrastructureBook book,
        InfrastructureEnvironment environment,
        InboxBook? inbox = null,
        ManagerRegistry? managers = null,
        int order = DefaultOrder)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
        _inbox = inbox;
        _managers = managers;
        Order = order;
    }

    public int Order { get; }

    public void OnDay(DayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var today = context.Today;
        var section = _book.Section;
        if (section.IsEmpty)
        {
            return;
        }

        var finance = _book.Finance;
        var changed = false;
        foreach (var facility in section.Facilities)
        {
            if (facility.BuildEnds is { } ends && today >= ends)
            {
                var completed = facility.Complete();
                section = section.Upsert(completed);
                changed = true;
                Notice(facility.Organization, facility.Kind, today);
            }
        }

        if (today.IsSeasonStart)
        {
            var typical = finance.TypicalCents;
            foreach (var facility in section.Facilities)
            {
                var due = InfrastructureMath.UpkeepCents(
                    facility.QualityMilli,
                    today.Year,
                    typical,
                    facility.IsBuilding);
                if (due <= 0 || !finance.HasBook(facility.Organization))
                {
                    continue;
                }

                finance = finance.Post(
                    facility.Organization,
                    today,
                    LedgerCategories.Infrastructure,
                    FacilityKindIds.Of(facility.Kind),
                    -due,
                    InfrastructureKeys.LedgerUpkeep);
                changed = true;
            }
        }

        if (changed)
        {
            _book.Write(section, finance.HasBooks ? finance : null);
        }
    }

    private void Notice(Paddock.Domain.World.OrganizationId organization, FacilityKind kind, GameDate today)
    {
        if (_inbox is null || _managers is null)
        {
            return;
        }

        foreach (var manager in _environment.Control.ManagersOf(organization))
        {
            var draft = new InboxItemDraft(
                InfrastructureKeys.NoticeKind,
                InfrastructureKeys.BuildFinishedSubject,
                [
                    new KeyValuePair<string, string>("kind", FacilityKindIds.Of(kind)),
                    new KeyValuePair<string, string>("organization", organization.Value),
                    new KeyValuePair<string, string>("year", today.Year.ToString(CultureInfo.InvariantCulture)),
                ],
                options: null,
                validUntil: null,
                defaultOptionId: null);
            _inbox.Post(_managers, manager, draft, today);
        }
    }
}
