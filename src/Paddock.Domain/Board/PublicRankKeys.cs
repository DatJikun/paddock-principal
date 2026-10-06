using Paddock.Domain.Cars;
using Paddock.Domain.Finance;
using Paddock.Domain.World;

namespace Paddock.Domain.Board;

/// <summary>
/// What orders two teams of the same budget level (#234). The budget level itself is the organization's opening budget, which the
/// world initializer sets from the same tier source finance opens its books from. Inside one level the order is: the place the
/// authored order gives the team for the previous season (a team that has one is ahead of a team that has none), then the car
/// strength the authored ESTIMATE gives for the season (higher is ahead), and only then the id, so a field with no facts at all
/// still has one deterministic order. A source that is null knows nothing. Both facts are public: a table of last season, and how
/// good the cars looked.
/// </summary>
public sealed class PublicRankKeys
{
    private readonly ITeamTierSource? _tiers;
    private readonly ICarStrengthSource? _carStrength;

    public PublicRankKeys(ITeamTierSource? tiers, ICarStrengthSource? carStrength)
    {
        _tiers = tiers;
        _carStrength = carStrength;
    }

    /// <summary>The place of the team in the season before <paramref name="season"/>, or null when the authored order has none.</summary>
    public int? PreviousPlace(OrganizationId team, int season) => _tiers?.PreviousPlaceOf(team, season);

    /// <summary>The authored ESTIMATE of the strength of the team's car in <paramref name="season"/>, or null.</summary>
    public double? CarStrength(OrganizationId team, int season) =>
        _carStrength is not null && _carStrength.TryGet(team.Value, season, out var strength) ? strength : null;

    /// <summary>True when <paramref name="a"/> is ahead of <paramref name="b"/> by the previous place, then the car strength. False for a tie.</summary>
    public bool Ahead(OrganizationId a, OrganizationId b, int season) => Compare(a, b, season) < 0;

    private int Compare(OrganizationId a, OrganizationId b, int season)
    {
        var byPlace = CompareLowerIsAhead(PreviousPlace(a, season), PreviousPlace(b, season));
        if (byPlace != 0)
        {
            return byPlace;
        }

        return CompareLowerIsAhead(Negate(CarStrength(a, season)), Negate(CarStrength(b, season)));
    }

    private static double? Negate(double? value) => value is double number ? -number : null;

    private static int CompareLowerIsAhead<T>(T? a, T? b)
        where T : struct, IComparable<T>
    {
        if (a is null || b is null)
        {
            return a is null ? (b is null ? 0 : 1) : -1;
        }

        return a.Value.CompareTo(b.Value);
    }
}
