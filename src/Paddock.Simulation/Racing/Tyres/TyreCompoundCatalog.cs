using System.Collections.Immutable;
using System.Globalization;

namespace Paddock.Simulation.Racing.Tyres;

/// <summary>The compounds on offer in one span of seasons. A null <see cref="ToSeason"/> means the span is still open.</summary>
public sealed record TyreCompoundEra(string Name, int FromSeason, int? ToSeason, ImmutableArray<TyreCompound> Compounds)
{
    public bool Covers(int season) => season >= FromSeason && (ToSeason is null || season <= ToSeason);
}

/// <summary>
/// Which tyre compounds exist in which season. Data-driven: any list of <see cref="TyreCompoundEra"/> can be passed;
/// <see cref="Default"/> is a synthetic ESTIMATE set shaped like history (one hard compound in the 1950s, three plus
/// qualifying specials in the 1970s-80s, a grooved set in 1998-2008, five numbered compounds from 2010).
/// None of its numbers is a recorded fact, see <see cref="TyreFuelConstants"/>.
/// </summary>
public sealed class TyreCompoundCatalog
{
    private readonly ImmutableArray<TyreCompoundEra> _eras;

    public TyreCompoundCatalog(IEnumerable<TyreCompoundEra> eras, TyreCompound wetCompound)
    {
        ArgumentNullException.ThrowIfNull(eras);
        ArgumentNullException.ThrowIfNull(wetCompound);
        if (wetCompound.Kind != TyreCompoundKind.Wet)
        {
            throw new ArgumentException("The wet compound must have kind Wet.", nameof(wetCompound));
        }

        _eras = [.. eras.OrderBy(era => era.FromSeason)];
        if (_eras.IsEmpty)
        {
            throw new ArgumentException("A catalog needs at least one era.", nameof(eras));
        }

        var ids = new HashSet<string>(StringComparer.Ordinal) { wetCompound.Id };
        for (var i = 0; i < _eras.Length; i++)
        {
            var era = _eras[i];
            if (era.Compounds.IsDefaultOrEmpty)
            {
                throw new ArgumentException($"Era '{era.Name}' has no compounds.", nameof(eras));
            }

            if (era.ToSeason is { } to && to < era.FromSeason)
            {
                throw new ArgumentException($"Era '{era.Name}' ends before it starts.", nameof(eras));
            }

            if (i > 0)
            {
                var previous = _eras[i - 1];
                if (previous.ToSeason is null || previous.ToSeason >= era.FromSeason)
                {
                    throw new ArgumentException($"Eras '{previous.Name}' and '{era.Name}' overlap.", nameof(eras));
                }
            }

            foreach (var compound in era.Compounds)
            {
                if (compound.Kind == TyreCompoundKind.Wet)
                {
                    throw new ArgumentException("Eras list dry compounds; the wet compound is separate.", nameof(eras));
                }

                if (!ids.Add(compound.Id))
                {
                    throw new ArgumentException($"Compound id '{compound.Id}' is used twice.", nameof(eras));
                }
            }
        }

        WetCompoundValue = wetCompound;
    }

    private TyreCompound WetCompoundValue { get; }

    /// <summary>The synthetic ESTIMATE catalog.</summary>
    public static TyreCompoundCatalog Default { get; } = BuildDefault();

    /// <summary>
    /// Every dry compound of the season, softest first: race compounds and qualifying specials.
    /// Throws if the season is before the first era; a season after a closed last era also throws.
    /// </summary>
    public ImmutableArray<TyreCompound> AvailableCompounds(int season) => EraOf(season).Compounds;

    /// <summary>The compounds of <see cref="AvailableCompounds"/> that may be raced, softest first.</summary>
    public ImmutableArray<TyreCompound> RaceCompounds(int season) =>
        [.. AvailableCompounds(season).Where(compound => compound.Kind == TyreCompoundKind.Dry)];

    /// <summary>
    /// The era (a named span of seasons) that holds the season.
    /// </summary>
    public TyreCompoundEra EraOf(int season)
    {
        foreach (var era in _eras)
        {
            if (era.Covers(season))
            {
                return era;
            }
        }

        throw new ArgumentOutOfRangeException(
            nameof(season),
            string.Create(CultureInfo.InvariantCulture, $"No tyre era covers season {season}."));
    }

    /// <summary>
    /// The wet-weather compound of the season. A hook only: its numbers describe running it on a dry track, and
    /// what it does in the wet belongs to the weather and lap-time layers (out of scope here). The same compound
    /// is returned for every season.
    /// </summary>
    public TyreCompound WetCompound(int season)
    {
        _ = EraOf(season);
        return WetCompoundValue;
    }

    private static TyreCompoundEra Era(string name, int from, int? to, params TyreCompound[] compounds) =>
        new(name, from, to, [.. compounds]);

    private static TyreCompound Dry(string id, int hardness, double grip, double wear, double cliff, double warmUp) =>
        new(id, TyreCompoundKind.Dry, hardness, grip, wear, cliff, warmUp);

    private static TyreCompound Qualifier(string id) =>
        new(id, TyreCompoundKind.QualifyingSpecial, -1, -0.6, 0.15, 3, 0.5);

    private static TyreCompoundCatalog BuildDefault() => new(
        [
            Era("treaded_single", 1950, 1969, Dry("treaded.hard", 0, 0.0, 0.008, 70, 0)),
            Era(
                "slick_multi",
                1970,
                1997,
                Qualifier("slick.qualifier"),
                Dry("slick.soft", 0, 0.0, 0.045, 22, 1),
                Dry("slick.medium", 1, 0.5, 0.030, 32, 1.5),
                Dry("slick.hard", 2, 1.0, 0.018, 45, 2)),
            Era(
                "grooved",
                1998,
                2008,
                Dry("grooved.soft", 0, 0.0, 0.040, 24, 1),
                Dry("grooved.medium", 1, 0.45, 0.028, 34, 1.5),
                Dry("grooved.hard", 2, 0.9, 0.017, 46, 2)),
            Era(
                "slick_return",
                2009,
                2009,
                Dry("slick2009.soft", 0, 0.0, 0.050, 20, 1),
                Dry("slick2009.medium", 1, 0.5, 0.032, 30, 1.5),
                Dry("slick2009.hard", 2, 1.0, 0.020, 42, 2)),
            Era(
                "numbered",
                2010,
                null,
                Dry("C5", 0, 0.0, 0.060, 16, 1),
                Dry("C4", 1, 0.25, 0.048, 22, 1.2),
                Dry("C3", 2, 0.5, 0.037, 30, 1.5),
                Dry("C2", 3, 0.75, 0.028, 40, 1.8),
                Dry("C1", 4, 1.0, 0.020, 50, 2)),
        ],
        new TyreCompound(
            "wet",
            TyreCompoundKind.Wet,
            10,
            TyreFuelConstants.WetCompoundDryGripLossSeconds,
            TyreFuelConstants.WetCompoundDryWearRate,
            TyreFuelConstants.WetCompoundDryCliffLap,
            TyreFuelConstants.WetCompoundWarmUpLaps));
}
