using Paddock.Data.Historical;
using Paddock.Domain.People;

namespace Paddock.Data.World;

/// <summary>
/// Turns the ratings model output (<see cref="HistoricalRatingsDocument"/>) into the attributes and hidden ceiling of
/// a real driver for any season, which is what <see cref="IPeopleProvider.RatingFor"/> promises. Pure and deterministic:
/// no RNG, the same document and arguments always give the same answer (INV-002, INV-005).
/// <para>
/// The model rates one number per raced season (overall = level x 5, era-relative), so the season's level is spread over
/// the eleven attributes: every attribute gets the whole part of the level and as many as the fraction asks for get one
/// more, starting at a per-driver stable position. The mean attribute (and so the stars, PP-047) matches the rating, and
/// no attribute-level differences are invented (the data has none). ESTIMATE, to be replaced by per-attribute evidence.
/// </para>
/// <para>
/// Seasons the model has no value for: between two raced seasons the level is interpolated; before the first it grows
/// linearly from <see cref="WorldInitEstimates.RatedGrowthStartLevel"/> at the arc's growth start age; after the last it is
/// held until the arc's decline starts and then falls by the arc's rate. Without an arc it is held flat.
/// The ceiling is the career peak (never below the current level).
/// </para>
/// </summary>
public sealed class HistoricalRatingTable
{
    private readonly Dictionary<string, Career> _careers = new(StringComparer.Ordinal);

    public HistoricalRatingTable(HistoricalRatingsDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        foreach (var rating in document.DriverRatings)
        {
            if (rating.RatingBySeason.Count == 0)
            {
                continue;
            }

            var seasons = rating.RatingBySeason
                .OrderBy(season => season.Season)
                .Select(season => (season.Season, Level: season.Overall / LevelToOverall))
                .ToArray();
            _careers[rating.DriverId] = new Career(seasons, rating.Arc);
        }
    }

    /// <summary>How many drivers have a rating at all.</summary>
    public int Count => _careers.Count;

    /// <summary>The rating of a driver in a season, or null when the model did not rate the driver.</summary>
    public DriverRating? RatingFor(string driverId, int season)
    {
        ArgumentNullException.ThrowIfNull(driverId);
        if (!_careers.TryGetValue(driverId, out var career))
        {
            return null;
        }

        var level = career.LevelIn(season);
        var ceiling = Math.Max(level, Math.Max(career.PeakLevel, career.Arc?.PeakLevel ?? 0.0));
        var current = Spread(level, driverId);
        var potential = Spread(ceiling, driverId);
        for (var i = 0; i < AttributeCount; i++)
        {
            potential[i] = Math.Max(potential[i], current[i]);
        }

        return new DriverRating(Attributes(current), Attributes(potential));
    }

    private const double LevelToOverall = 5.0;

    private const int AttributeCount = 11;

    private static int[] Spread(double level, string driverId)
    {
        var clamped = Math.Clamp(level, GenerationEstimates.AttributeMin, GenerationEstimates.AttributeMax);
        var whole = (int)Math.Floor(clamped);
        var extra = whole >= GenerationEstimates.AttributeMax ? 0 : (int)Math.Round((clamped - whole) * AttributeCount, MidpointRounding.AwayFromZero);
        var start = (int)(StableHash(driverId) % AttributeCount);
        var values = new int[AttributeCount];
        for (var i = 0; i < AttributeCount; i++)
        {
            var rank = (i - start + AttributeCount) % AttributeCount;
            values[i] = whole + (rank < extra ? 1 : 0);
        }

        return values;
    }

    // Same order as DriverAttributes and GenerationEstimates.DriverAttributeKeys.
    private static DriverAttributes Attributes(int[] v) =>
        new(v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8], v[9], v[10]);

    // FNV-1a over the id's UTF-16 code units: the same driver always gets the same position.
    private static uint StableHash(string text)
    {
        var hash = 2166136261u;
        foreach (var c in text)
        {
            hash ^= c;
            hash *= 16777619u;
        }

        return hash;
    }

    private sealed class Career
    {
        private readonly (int Season, double Level)[] _seasons;

        public Career((int Season, double Level)[] seasons, HistoricalCareerArc? arc)
        {
            _seasons = seasons;
            Arc = arc;
            PeakLevel = seasons.Max(season => season.Level);
        }

        public HistoricalCareerArc? Arc { get; }

        public double PeakLevel { get; }

        public double LevelIn(int season)
        {
            var first = _seasons[0];
            var last = _seasons[^1];
            if (season < first.Season)
            {
                return Before(first, season);
            }

            if (season > last.Season)
            {
                return After(last, season);
            }

            for (var i = 0; i < _seasons.Length; i++)
            {
                if (_seasons[i].Season == season)
                {
                    return _seasons[i].Level;
                }

                if (_seasons[i].Season > season)
                {
                    var (s0, l0) = _seasons[i - 1];
                    var (s1, l1) = _seasons[i];
                    return l0 + (l1 - l0) * (season - s0) / (s1 - s0);
                }
            }

            return last.Level;
        }

        private double Before((int Season, double Level) first, int season)
        {
            if (Arc is null)
            {
                return first.Level;
            }

            var span = Arc.DebutAge - Arc.GrowthStartAge;
            if (span <= 0)
            {
                return first.Level;
            }

            var start = Math.Min(WorldInitEstimates.RatedGrowthStartLevel, first.Level);
            var progress = 1.0 - (first.Season - season) / (double)span;
            return progress <= 0.0 ? start : start + (first.Level - start) * progress;
        }

        private double After((int Season, double Level) last, int season)
        {
            if (Arc is null)
            {
                return last.Level;
            }

            var age = Arc.LastSeasonAge + (season - last.Season);
            var over = age - Math.Max(Arc.DeclineStartAge, Arc.LastSeasonAge);
            return over <= 0 ? last.Level : Math.Max(GenerationEstimates.AttributeMin, last.Level - Arc.DeclineLevelsPerYear * over);
        }
    }
}
