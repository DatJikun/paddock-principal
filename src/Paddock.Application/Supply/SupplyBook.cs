using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Managers;
using Paddock.Domain.Finance;
using Paddock.Domain.Spy;
using Paddock.Domain.Supply;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Career;

namespace Paddock.Application.Supply;

/// <summary>
/// The live world as supply commands see it: the host owns the world, this reads it and puts sections back. One
/// <see cref="Update"/> changes the supply, finance and cars sections together, so a deal and the car that names it are never
/// half written.
/// </summary>
public sealed class SupplyBook
{
    private readonly Func<WorldState> _read;
    private readonly Action<WorldState> _write;

    public SupplyBook(Func<WorldState> read, Action<WorldState> write)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(write);
        _read = read;
        _write = write;
    }

    public WorldState World => _read();

    public SupplySection Section => World.Section<SupplySection>(SupplySection.SectionName) ?? SupplySection.Empty;

    public FinanceSection Finance => World.Section<FinanceSection>(FinanceSection.SectionName) ?? FinanceSection.Empty;

    public static SupplyBook ForSession(CareerSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new SupplyBook(() => session.World, session.StoreWorld);
    }

    public void Write(SupplySection supply, FinanceSection? finance = null)
    {
        ArgumentNullException.ThrowIfNull(supply);
        var world = World.WithSection(supply);
        if (finance is not null)
        {
            world = world.WithSection(finance);
        }

        _write(world);
    }

    /// <summary>Replaces the whole world, for a change that also touched the cars section (<see cref="SupplyCars.Sync"/>).</summary>
    public void Update(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        _write(world);
    }
}

public static class SupplyEventTypes
{
    public const string Proposed = "supply.proposed";

    public const string Signed = "supply.signed";

    public const string Closed = "supply.closed";
}

public sealed record SupplyDealProposed(ManagerId ManagerId, DateOnly OccurredOn, string NegotiationId, string SupplierId, SupplyItem Item) : IDomainEvent
{
    public string TypeId => SupplyEventTypes.Proposed;
}

public sealed record SupplyDealSigned(ManagerId ManagerId, DateOnly OccurredOn, string DealId, string NegotiationId) : IDomainEvent
{
    public string TypeId => SupplyEventTypes.Signed;
}

public sealed record SupplyTalksClosed(ManagerId ManagerId, DateOnly OccurredOn, string NegotiationId) : IDomainEvent
{
    public string TypeId => SupplyEventTypes.Closed;
}

/// <summary>Era facts supply reads: the yardstick for prices, and whether the rules let a team choose its fuel.</summary>
public interface ISupplyEras
{
    /// <summary>A typical team's budget in cents for the season.</summary>
    long ReferenceBudgetCents(int season);

    /// <summary>True when the era's fuel rule lets a team pick a fuel (the <c>fuel_specification</c> rule allows special blends).</summary>
    bool FuelChoice(int season);
}

/// <summary>Era facts from the authored rule periods.</summary>
public sealed class PeriodSupplyEras : ISupplyEras
{
    public const string FuelDimension = "fuel_specification";

    public const string SpecialBlends = "special_blends_allowed";

    private readonly IReadOnlyList<RulePeriod> _periods;

    public PeriodSupplyEras(IReadOnlyList<RulePeriod> periods)
    {
        ArgumentNullException.ThrowIfNull(periods);
        _periods = periods;
    }

    public long ReferenceBudgetCents(int season) => SupplyPricing.ReferenceBudgetCents(_periods, season);

    public bool FuelChoice(int season)
    {
        foreach (var period in _periods)
        {
            if (string.Equals(period.DimensionId, FuelDimension, StringComparison.Ordinal)
                && period.FromYear <= season && (period.ToYear is null || season <= period.ToYear.Value))
            {
                return period.Value == SpecialBlends;
            }
        }

        return false;
    }
}

/// <summary>The ports and data of the supply system. A host replaces the ones it has a real system for (T42 brings the engine programmes).</summary>
public sealed class SupplyEnvironment
{
    public SupplyEnvironment(
        IOrganizationControl control,
        ISupplyEras eras,
        ISupplierProfiles profiles,
        IEngineProgrammes? programmes = null,
        ITraceSink? trace = null)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(eras);
        ArgumentNullException.ThrowIfNull(profiles);
        Control = control;
        Eras = eras;
        Profiles = profiles;
        Programmes = programmes ?? NoEngineProgrammes.Instance;
        Trace = trace ?? NullSink.Instance;
    }

    public IOrganizationControl Control { get; }

    public ISupplyEras Eras { get; }

    public ISupplierProfiles Profiles { get; }

    /// <summary>The own-engine programmes. Empty until T42.</summary>
    public IEngineProgrammes Programmes { get; }

    public ITraceSink Trace { get; }
}
