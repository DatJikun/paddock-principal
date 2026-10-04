using Paddock.Domain.Cars;
using Paddock.Domain.Supply;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Cars;
using Paddock.Simulation.Racing.Pace;
using Paddock.Simulation.Racing.Tyres;

namespace Paddock.Simulation.Supply;

/// <summary>
/// Where supply deals meet the race (T43). Simulation code only: a manager or an AI never receives these truth values (INV-003).
/// Pure functions (INV-005). The race host calls <see cref="Resolve"/> instead of <c>CarPerformanceFor.Resolve(car, limits)</c>
/// and <see cref="TyresFor"/> where it builds the tyre set; wiring them is the host's task, not this one's.
/// </summary>
public static class SupplyPerformance
{
    /// <summary>The engine the car's deal delivers on <paramref name="date"/>. None when the key names no deal in force.</summary>
    public static EngineContribution EngineFor(TeamCar car, SupplySection supply, ISupplierProfiles profiles, GameDate date)
    {
        ArgumentNullException.ThrowIfNull(car);
        ArgumentNullException.ThrowIfNull(supply);
        ArgumentNullException.ThrowIfNull(profiles);
        if (car.EngineKey is null || supply.FindDeal(car.EngineKey) is not { } deal || !deal.IsInForceOn(date) || deal.Item != SupplyItem.Engine)
        {
            return EngineContribution.None;
        }

        return SupplyContribution.Of(deal, profiles, date.Year);
    }

    /// <summary>The T41 vector with the engine of the car's supply deal. The race engine reads this.</summary>
    public static CarPerformance Resolve(TeamCar car, EraPerformanceLimits limits, SupplySection supply, ISupplierProfiles profiles, GameDate date) =>
        CarPerformanceFor.Resolve(car, limits, EngineFor(car, supply, profiles, date));

    /// <summary>
    /// The tyre supplier a team races on and whether its tyres are tuned for the team (a partner or works deal), for the T30 model.
    /// A team with no tyre deal gets <see cref="TyreSupplierProfile.Neutral"/>. ESTIMATE: the character comes from a stable hash of the
    /// supplier id, since no data names tyre makers' traits.
    /// </summary>
    public static (TyreSupplierProfile Profile, bool PartnerTuned) TyresFor(SupplySection supply, OrganizationId team, GameDate date)
    {
        ArgumentNullException.ThrowIfNull(supply);
        var deal = supply.InForce(team, SupplyItem.Tyres, date);
        if (deal is null)
        {
            return (TyreSupplierProfile.Neutral, false);
        }

        var id = deal.Supplier.Value;
        var profile = new TyreSupplierProfile(
            id,
            EstimateSupplierProfiles.Spread(id, "tyre-grip", SupplyEstimates.TyreGripSpread),
            SupplyEstimates.TyrePartnerBonus);
        return (profile, deal.Kind is SupplyKind.Partner or SupplyKind.Works);
    }
}
