using Paddock.Application.Access;
using Paddock.Application.Cars;
using Paddock.Application.Contracts;
using Paddock.Domain.Infrastructure;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using ManagerId = Paddock.Application.Managers.ManagerId;

namespace Paddock.Application.Infrastructure;

/// <summary>One of the team's own facilities, as the principal may know it: exact relative quality, never a rival's.</summary>
public sealed record OwnFacilityView(
    string Kind,
    int QualityMilli,
    double RelativeQuality,
    int FrontierMilli,
    bool Building,
    DateOnly? BuildEnds,
    long UpgradeCostCents,
    int UpgradeDays,
    bool Unlocked,
    bool Eligible);

/// <summary>Posted cost and remaining private tests this year. Availability is the era cap.</summary>
public sealed record TestRentalView(long CostCents, int Used, int Cap, bool Allowed);

/// <summary>ESTIMATE quote of the next race's transport from the team's home country.</summary>
public sealed record LogisticsView(string Mode, int Days, long CostCents, string? CircuitCountry);

/// <summary>Only the manager's own teams. Rivals' facilities are not in the type at all (INV-003).</summary>
public sealed record InfrastructureOverview(IReadOnlyList<OwnInfrastructureView> Own);

public sealed record OwnInfrastructureView(
    string OrganizationId,
    IReadOnlyList<OwnFacilityView> Facilities,
    TestRentalView Tests,
    string? HomeCountry,
    LogisticsView? NextTransport);

/// <summary>
/// What one manager may see of facilities. Own team is exact. Rivals are omitted. Pure: no change, no RNG (INV-005).
/// </summary>
public sealed class InfrastructureQuery
{
    private readonly InfrastructureBook _book;
    private readonly InfrastructureEnvironment _environment;

    public InfrastructureQuery(InfrastructureBook book, InfrastructureEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
    }

    public InfrastructureOverview View(AccessContext access, string? nextCircuitCountry = null)
    {
        ArgumentNullException.ThrowIfNull(access);
        if (access.Kind == AccessKind.Developer)
        {
            throw new InvalidOperationException("A developer reads Truth, not the manager overview.");
        }

        if (access.Manager is not { } actor)
        {
            throw new InvalidOperationException("A manager view needs a manager.");
        }

        var manager = new ManagerId(actor.Value);
        var world = _book.World;
        var today = new GameDate(world.CurrentDate.Year, world.CurrentDate.Month, world.CurrentDate.Day);
        var typical = _book.Finance.TypicalCents;
        var own = new List<OwnInfrastructureView>();
        foreach (var organization in world.Organizations)
        {
            if (organization.Kind != OrganizationKind.Team
                || organization.Dissolved is not null
                || !_environment.Control.Controls(manager, organization.Id))
            {
                continue;
            }

            own.Add(Own(organization.Id, today, typical, nextCircuitCountry));
        }

        return new InfrastructureOverview(own);
    }

    internal OwnInfrastructureView Own(OrganizationId organization, GameDate today, long typicalCents, string? nextCircuitCountry = null)
    {
        var year = today.Year;
        var section = _book.Section;
        var rows = new List<OwnFacilityView>();
        foreach (var spec in _environment.Catalog.Kinds)
        {
            var unlocked = year >= spec.UnlockYear;
            var facility = section.Find(organization, spec.Kind);
            var quality = facility?.QualityMilli ?? 0;
            var building = facility?.IsBuilding ?? false;
            var relative = unlocked
                ? InfrastructureMath.Relative(quality, year, building)
                : 0d;
            rows.Add(new OwnFacilityView(
                FacilityKindIds.Of(spec.Kind),
                quality,
                relative,
                InfrastructureMath.FrontierMilli(year),
                building,
                facility?.BuildEnds is { } ends ? new DateOnly(ends.Year, ends.Month, ends.Day) : null,
                unlocked ? InfrastructureMath.UpgradeCostCents(quality, year, typicalCents) : 0L,
                unlocked ? InfrastructureMath.UpgradeDays(quality, year) : 0,
                unlocked,
                unlocked));
        }

        var cap = InfrastructureMath.TestsAllowed(_environment.TestingRule(year));
        var used = section.TestsUsed(organization, year);
        var tests = new TestRentalView(InfrastructureMath.TestRentalCents(typicalCents), used, cap, cap > 0 && used < cap);
        var home = _environment.HomeCountry(organization);
        LogisticsView? transport = null;
        if (!string.IsNullOrWhiteSpace(nextCircuitCountry))
        {
            var quote = LogisticsMath.Quote(home, nextCircuitCountry, typicalCents);
            transport = new LogisticsView(LogisticsMath.Of(quote.Mode), quote.Days, quote.CostCents, nextCircuitCountry);
        }

        return new OwnInfrastructureView(organization.Value, rows, tests, home, transport);
    }
}
