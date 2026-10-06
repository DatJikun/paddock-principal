namespace Paddock.Domain.Infrastructure;

/// <summary>Authored kind: when it unlocks. Dyno/foundry are not in this catalog (PP-064).</summary>
public sealed record FacilityKindSpec(FacilityKind Kind, int UnlockYear);

/// <summary>
/// Starting levels and kind rules from authored ESTIMATE data. Unknown / fictional teams use <see cref="DefaultQualityMilli"/>.
/// </summary>
public sealed class FacilityCatalog
{
    public FacilityCatalog(
        IReadOnlyList<FacilityKindSpec> kinds,
        IReadOnlyDictionary<FacilityKind, int> defaultQualityMilli,
        IReadOnlyDictionary<string, IReadOnlyDictionary<FacilityKind, int>> teamQualityMilli)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        ArgumentNullException.ThrowIfNull(defaultQualityMilli);
        ArgumentNullException.ThrowIfNull(teamQualityMilli);
        Kinds = kinds;
        DefaultQualityMilli = defaultQualityMilli;
        TeamQualityMilli = teamQualityMilli;
    }

    public static FacilityCatalog Empty { get; } = new(
        [],
        new Dictionary<FacilityKind, int>(),
        new Dictionary<string, IReadOnlyDictionary<FacilityKind, int>>(StringComparer.Ordinal));

    public IReadOnlyList<FacilityKindSpec> Kinds { get; }

    public IReadOnlyDictionary<FacilityKind, int> DefaultQualityMilli { get; }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<FacilityKind, int>> TeamQualityMilli { get; }

    public FacilityKindSpec? SpecOf(FacilityKind kind)
    {
        foreach (var spec in Kinds)
        {
            if (spec.Kind == kind)
            {
                return spec;
            }
        }

        return null;
    }

    public bool Unlocked(FacilityKind kind, int year) => SpecOf(kind) is { } spec && year >= spec.UnlockYear;

    public int StartingQualityMilli(string organizationId, FacilityKind kind)
    {
        if (TeamQualityMilli.TryGetValue(organizationId, out var team)
            && team.TryGetValue(kind, out var authored))
        {
            return Math.Clamp(authored, 0, InfrastructureEstimates.MaxQualityMilli);
        }

        return DefaultQualityMilli.TryGetValue(kind, out var fallback)
            ? Math.Clamp(fallback, 0, InfrastructureEstimates.MaxQualityMilli)
            : 0;
    }
}
