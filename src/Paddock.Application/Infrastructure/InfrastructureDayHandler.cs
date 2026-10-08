using System.Globalization;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Domain.Development;
using Paddock.Domain.Finance;
using Paddock.Domain.Inbox;
using Paddock.Domain.Infrastructure;
using Paddock.Domain.Time;
using Paddock.Simulation.Time;

namespace Paddock.Application.Infrastructure;

/// <summary>
/// One lived day of facilities: a booked private test runs (the car learns, the rental is charged, an inbox notice says what it gave); a build that has reached its end day becomes usable and posts an inbox notice; on 1 January
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
        var cars = _book.Cars;
        var changed = false;
        var carsChanged = false;
        var development = _book.World.Section<Paddock.Domain.Development.DevelopmentSection>(Paddock.Domain.Development.DevelopmentSection.SectionName)
            ?? Paddock.Domain.Development.DevelopmentSection.Empty;
        var noted = false;
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

        foreach (var booking in section.Tests.Where(test => test.Date == today).ToArray())
        {
            changed = true;
            var held = cars.Of(booking.Organization).OrderBy(car => car.Id, StringComparer.Ordinal).ToArray();
            if (held.Length == 0)
            {
                section = section.RemoveTest(booking);
                NoticeTest(booking.Organization, InfrastructureKeys.TestNoCarSubject, today, 0, 0);
                continue;
            }

            var cap = DevelopmentMath.UnderstandingCap(_environment.TestingRule(today.Year), aeroTesting: null);
            var gained = 0d;
            foreach (var car in held)
            {
                var grown = DevelopmentMath.GrowUnderstanding(car.Understanding, InfrastructureEstimates.UnderstandingPerTest, cap);
                gained += grown - car.Understanding;
                if (grown != car.Understanding)
                {
                    cars = cars.Replace(car.WithDesign(
                        car.Season,
                        car.Concept,
                        car.Levels,
                        car.ConceptCeiling,
                        grown,
                        car.TyreWearMultiplier,
                        car.SupplierChangeCost));
                    carsChanged = true;
                }
            }

            if (finance.HasBook(booking.Organization))
            {
                finance = finance.Post(
                    booking.Organization,
                    today,
                    LedgerCategories.Infrastructure,
                    "test",
                    -booking.CostCents,
                    InfrastructureKeys.LedgerTest);
            }

            if (gained > 0d)
            {
                // The Auto screen tells the principal what the test taught (PP-066).
                development = development.AddNote(new Paddock.Domain.Development.UnderstandingNote(
                    booking.Organization,
                    today,
                    Paddock.Domain.Development.UnderstandingSources.Test,
                    Paddock.Domain.Development.DevelopmentEstimates.Milli(gained / held.Length)));
                noted = true;
            }

            var average = (int)Math.Round(gained / held.Length, MidpointRounding.AwayFromZero);
            NoticeTest(
                booking.Organization,
                average > 0 ? InfrastructureKeys.TestDoneSubject : InfrastructureKeys.TestCappedSubject,
                today,
                average,
                booking.CostCents);
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
            _book.Write(section, finance.HasBooks ? finance : null, carsChanged ? cars : null, noted ? development : null);
        }
    }

    private void NoticeTest(Paddock.Domain.World.OrganizationId organization, string subject, GameDate today, int gain, long costCents)
    {
        if (_inbox is null || _managers is null)
        {
            return;
        }

        foreach (var manager in _environment.Control.ManagersOf(organization))
        {
            var draft = new InboxItemDraft(
                InfrastructureKeys.TestNoticeKind,
                subject,
                [
                    new KeyValuePair<string, string>("gain", gain.ToString(CultureInfo.InvariantCulture)),
                    new KeyValuePair<string, string>("cost", "$" + new Money(costCents).WholeDollars.ToString("N0", CultureInfo.InvariantCulture)),
                    new KeyValuePair<string, string>("organization", organization.Value),
                    new KeyValuePair<string, string>("date", today.ToString()),
                ],
                options: null,
                validUntil: null,
                defaultOptionId: null);
            _inbox.Post(_managers, manager, draft, today);
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
