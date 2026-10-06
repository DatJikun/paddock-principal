using Paddock.Application.Contracts;
using Paddock.Domain.Cars;
using Paddock.Domain.Development;
using Paddock.Domain.Finance;
using Paddock.Domain.Infrastructure;
using Paddock.Domain.World;
using Paddock.Simulation.Career;

namespace Paddock.Application.Infrastructure;

/// <summary>The live world as infrastructure commands see it. The host owns the world; this replaces the section.</summary>
public sealed class InfrastructureBook
{
    private readonly Func<WorldState> _read;
    private readonly Action<WorldState> _write;

    public InfrastructureBook(Func<WorldState> read, Action<WorldState> write)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(write);
        _read = read;
        _write = write;
    }

    public WorldState World => _read();

    public InfrastructureSection Section =>
        World.Section<InfrastructureSection>(InfrastructureSection.SectionName) ?? InfrastructureSection.Empty;

    public FinanceSection Finance => World.Section<FinanceSection>(FinanceSection.SectionName) ?? FinanceSection.Empty;

    public CarsSection Cars => World.Section<CarsSection>(CarsSection.SectionName) ?? CarsSection.Empty;

    public static InfrastructureBook ForSession(CareerSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new InfrastructureBook(() => session.World, session.StoreWorld);
    }

    public void Write(InfrastructureSection infrastructure, FinanceSection? finance = null, CarsSection? cars = null)
    {
        ArgumentNullException.ThrowIfNull(infrastructure);
        var world = World.WithSection(infrastructure);
        if (finance is not null)
        {
            world = world.WithSection(finance);
        }

        if (cars is not null)
        {
            world = world.WithSection(cars);
        }

        _write(world);
    }
}

/// <summary>The ports of the infrastructure system: who runs a team, which kinds exist, home countries and testing rules.</summary>
public sealed class InfrastructureEnvironment
{
    public InfrastructureEnvironment(
        IOrganizationControl control,
        FacilityCatalog catalog,
        IReadOnlyDictionary<string, string>? teamCountries = null,
        IReadOnlyList<RulePeriod>? rulePeriods = null)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(catalog);
        Control = control;
        Catalog = catalog;
        TeamCountries = teamCountries ?? new Dictionary<string, string>(StringComparer.Ordinal);
        RulePeriods = rulePeriods ?? [];
    }

    public IOrganizationControl Control { get; }

    public FacilityCatalog Catalog { get; }

    public IReadOnlyDictionary<string, string> TeamCountries { get; }

    public IReadOnlyList<RulePeriod> RulePeriods { get; }

    public string? HomeCountry(OrganizationId organization) =>
        organization.IsAssigned && TeamCountries.TryGetValue(organization.Value, out var country) ? country : null;

    public string? TestingRule(int year)
    {
        foreach (var period in RulePeriods)
        {
            if (string.Equals(period.DimensionId, PeriodDevelopmentRules.InSeasonTestingDimension, StringComparison.Ordinal)
                && period.FromYear <= year
                && (period.ToYear is null || year <= period.ToYear.Value))
            {
                return period.Value;
            }
        }

        return null;
    }
}

public static class InfrastructureEventTypes
{
    public const string UpgradeStarted = "infrastructure.upgrade_started";

    public const string BuildFinished = "infrastructure.build_finished";

    public const string TestBooked = "infrastructure.test_booked";
}
