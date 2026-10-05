using Paddock.Domain.Development;
using Paddock.Domain.Infrastructure;
using Paddock.Domain.World;

namespace Paddock.Application.Infrastructure;

/// <summary>How this team's factory (and later aero tools) feed development this year. Never a race result.</summary>
public sealed record InfrastructureFactors(
    double ExecutionQuality,
    double DurationScale,
    double UnderstandingScale);

/// <summary>
/// Reads the infrastructure section and turns it into the multipliers development already uses (capacity quality, duration,
/// understanding growth). Reliability of parts still comes from people, budget and the existing development rules: there is
/// no dyno in this update (PP-064).
/// </summary>
public static class InfrastructureEffect
{
    public static EngineeringCapacity Apply(EngineeringCapacity capacity, WorldState world, OrganizationId organization, int year)
    {
        var factors = Factors(world, organization, year);
        return capacity.WithInfrastructure(factors.ExecutionQuality, factors.DurationScale);
    }

    public static InfrastructureFactors Factors(WorldState world, OrganizationId organization, int year)
    {
        ArgumentNullException.ThrowIfNull(world);
        var section = world.Section<InfrastructureSection>(InfrastructureSection.SectionName) ?? InfrastructureSection.Empty;
        var owned = section.Of(organization);
        return From(owned, year);
    }

    public static InfrastructureFactors From(IReadOnlyList<Facility> owned, int year)
    {
        ArgumentNullException.ThrowIfNull(owned);
        var factory = RelativeOf(owned, FacilityKind.Factory, year) ?? 0d;
        var tunnel = RelativeOf(owned, FacilityKind.WindTunnel, year) ?? 0d;
        var cfd = RelativeOf(owned, FacilityKind.Cfd, year) ?? 0d;
        var simulator = RelativeOf(owned, FacilityKind.Simulator, year) ?? 0d;
        return new InfrastructureFactors(
            InfrastructureMath.ExecutionQualityScale(factory, tunnel, cfd),
            InfrastructureMath.DurationScale(factory),
            InfrastructureMath.UnderstandingScale(factory, simulator));
    }

    private static double? RelativeOf(IReadOnlyList<Facility> owned, FacilityKind kind, int year)
    {
        foreach (var facility in owned)
        {
            if (facility.Kind == kind)
            {
                return InfrastructureMath.Relative(facility.QualityMilli, year, facility.IsBuilding);
            }
        }

        return null;
    }
}
