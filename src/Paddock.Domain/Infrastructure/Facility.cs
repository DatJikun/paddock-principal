using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Infrastructure;

/// <summary>
/// One facility of one organization. <see cref="QualityMilli"/> is absolute capability; relative quality is this against
/// the year's frontier. While <see cref="BuildEnds"/> is set the facility is degraded.
/// </summary>
public sealed record Facility(
    OrganizationId Organization,
    FacilityKind Kind,
    int QualityMilli,
    GameDate? BuildStarted,
    GameDate? BuildEnds,
    int? TargetQualityMilli,
    long CostCents)
{
    public bool IsBuilding => BuildEnds is not null;

    public Facility StartBuild(int targetMilli, long costCents, GameDate started, GameDate ends)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(targetMilli, QualityMilli);
        ArgumentOutOfRangeException.ThrowIfLessThan(costCents, 1);
        if (ends <= started)
        {
            throw new ArgumentOutOfRangeException(nameof(ends), ends, "A build must end after it starts.");
        }

        return this with
        {
            BuildStarted = started,
            BuildEnds = ends,
            TargetQualityMilli = targetMilli,
            CostCents = costCents,
        };
    }

    public Facility Complete()
    {
        if (TargetQualityMilli is not { } target)
        {
            throw new InvalidOperationException("A facility with no target cannot complete a build.");
        }

        return this with
        {
            QualityMilli = Math.Clamp(target, 0, InfrastructureEstimates.MaxQualityMilli),
            BuildStarted = null,
            BuildEnds = null,
            TargetQualityMilli = null,
            CostCents = 0,
        };
    }
}

/// <summary>One paid private test-track rental. Availability is the era cap minus how many of these the team already has this year.</summary>
public sealed record TestBooking(OrganizationId Organization, GameDate Date, long CostCents);
