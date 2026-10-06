using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Infrastructure;

/// <summary>
/// Opening facilities of each team from authored ESTIMATE levels. Every racing team gets a factory (PP-064). Later kinds
/// stay out of the section until their era, even though they exist in the catalog.
/// </summary>
public static class InitialInfrastructureFactory
{
    public static InfrastructureSection Install(WorldState world, FacilityCatalog catalog, GameDate opening)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(catalog);
        var section = InfrastructureSection.Empty;
        foreach (var organization in world.Organizations)
        {
            if (organization.Kind != OrganizationKind.Team || organization.Dissolved is not null)
            {
                continue;
            }

            foreach (var spec in catalog.Kinds)
            {
                if (opening.Year < spec.UnlockYear)
                {
                    continue;
                }

                var quality = catalog.StartingQualityMilli(organization.Id.Value, spec.Kind);
                if (quality <= 0)
                {
                    continue;
                }

                section = section.Upsert(new Facility(
                    organization.Id,
                    spec.Kind,
                    quality,
                    null,
                    null,
                    null,
                    0));
            }
        }

        return section;
    }
}
