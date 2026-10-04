using Paddock.Domain.Cars;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Supply;

/// <summary>
/// An engine supplied to a team in the start season, in the shape of the world initializer's <c>EngineSupplyLink</c>
/// (Paddock.Data cannot be referenced from here, so the host maps one to the other field by field).
/// </summary>
public readonly record struct SupplyLink(OrganizationId Constructor, OrganizationId Supplier, string EngineName, string SupplyType);

/// <summary>
/// Builds the opening engine deals from the authored engine book (<c>engines.json</c>, through the world initializer's links) and
/// points the cars at them. One engine deal per team: when the data lists two engines for a team in the start season (Ferrari 1955),
/// the first listed wins. Rows whose team is not a racing team of the world are skipped. Run it after
/// <see cref="InitialCarFactory.Install"/>. Tyre and fuel deals have no authored start data and are not created.
/// </summary>
public static class InitialSupplyFactory
{
    public static WorldState Install(
        WorldState world,
        IReadOnlyList<SupplyLink> links,
        int season,
        long referenceBudgetCents)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentOutOfRangeException.ThrowIfNegative(referenceBudgetCents);
        var section = world.Section<SupplySection>(SupplySection.SectionName) ?? SupplySection.Empty;
        var opening = GameDate.SeasonStart(season);
        foreach (var link in links)
        {
            var kind = SupplyKindMap.FromAuthored(link.SupplyType);
            var customer = world.Organizations.FirstOrDefault(organization => organization.Id == link.Constructor);
            if (customer is not { Kind: OrganizationKind.Team } || section.InForce(link.Constructor, SupplyItem.Engine, opening) is not null)
            {
                continue;
            }

            var seasons = SupplyEstimates.InitialSeasons(kind);
            var shape = new SupplyTerms(1, seasons, false);

            // A works deal costs nothing and is never charged; SupplyTerms needs a positive price, so it stores the floor, at least one cent.
            var terms = new SupplyTerms(Math.Max(1, SupplyPricing.FloorCents(SupplyItem.Engine, kind, shape, referenceBudgetCents)), seasons, false);
            (section, _) = section.AddDeal(new SupplyDeal(
                section.NextDeal,
                SupplyItem.Engine,
                kind,
                link.Supplier,
                link.Constructor,
                season,
                terms,
                opening,
                SupplyDealStatus.Active,
                null,
                0,
                link.EngineName));
        }

        world = world.WithSection(section);
        return SupplyCars.Sync(world, section, opening);
    }
}

/// <summary>Keeps <see cref="TeamCar.EngineKey"/> equal to the id of the engine deal in force, so a car always names the engine it races.</summary>
public static class SupplyCars
{
    /// <summary>
    /// Points every car at the engine deal in force for its team on <paramref name="date"/>, and clears a key that names a deal
    /// that is no longer in force. A key that is not a deal id is left alone. Returns the same world when nothing changes.
    /// </summary>
    public static WorldState Sync(WorldState world, SupplySection supply, GameDate date)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(supply);
        var cars = world.Section<CarsSection>(CarsSection.SectionName);
        if (cars is null)
        {
            return world;
        }

        var updated = cars;
        foreach (var car in cars.Cars)
        {
            var wanted = supply.InForce(car.Organization, SupplyItem.Engine, date)?.Id;
            var current = car.EngineKey;
            if (wanted == current || (wanted is null && current is not null && !current.StartsWith(SupplyIds.DealPrefix, StringComparison.Ordinal)))
            {
                continue;
            }

            updated = updated.Replace(car.WithEngine(wanted));
        }

        return ReferenceEquals(updated, cars) ? world : world.WithSection(updated);
    }
}
