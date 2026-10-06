using System.Text.Json;
using System.Text.Json.Serialization;
using Paddock.Domain.People;

namespace Paddock.Data.World;

/// <summary>ESTIMATE constants of the mapping from the pipeline's fitted ratings to the game's 1-20 attribute scale (PP-047).</summary>
public static class FittedRatingEstimates
{
    /// <summary>The pipeline's 1-100 overall (already scaled against each season's own field) divided by this gives the 1-20 level. ESTIMATE.</summary>
    public const double OverallPerLevel = 5.0;

    /// <summary>Levels a driver has not yet grown, per year before his first rated season. ESTIMATE.</summary>
    public const double GrowthLevelsPerYearBeforeFirstSeason = 1.0;

    /// <summary>Beyond this many years before the first rated season the discount stops growing. ESTIMATE.</summary>
    public const int MaxYearsBeforeFirstSeason = 4;

    /// <summary>Lowest level a fitted driver gets. ESTIMATE.</summary>
    public const int MinLevel = 1;

    /// <summary>Highest level on the attribute scale.</summary>
    public const int MaxLevel = 20;
}

/// <summary>
/// Real-driver ratings read from the pipeline's <c>ratings.json</c> (the <c>driverRatings</c> list only; nothing is refitted).
/// Current = the driver's own rated season (nearest one; before the first it is discounted for growth), Potential = the career
/// peak (the arc's peak level, else the best rated season). Truth only: the world stores these as hidden truth (INV truth vs knowledge).
/// A driver with no entry returns null and keeps the flat stand-in and the gap.
/// </summary>
public sealed class FittedDriverRatings
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly Dictionary<string, Entry> _byDriver;

    private FittedDriverRatings(Dictionary<string, Entry> byDriver)
    {
        _byDriver = byDriver;
    }

    public int Count => _byDriver.Count;

    public static FittedDriverRatings Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Parse(File.ReadAllText(path));
    }

    public static FittedDriverRatings Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var document = JsonSerializer.Deserialize<Document>(json, ReadOptions)
            ?? throw new JsonException("The ratings file is empty.");
        var byDriver = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (var rating in document.DriverRatings ?? [])
        {
            if (rating.DriverId is null || rating.RatingBySeason is not { Count: > 0 } seasons)
            {
                continue;
            }

            var ordered = seasons.OrderBy(season => season.Season).ToArray();
            var peak = rating.Arc?.PeakLevel ?? ordered.Max(season => season.Overall) / FittedRatingEstimates.OverallPerLevel;
            byDriver[rating.DriverId] = new Entry(ordered, peak);
        }

        return new FittedDriverRatings(byDriver);
    }

    /// <summary>The rating of a driver for a season, or null when the fit does not cover the driver.</summary>
    public DriverRating? RatingFor(string driverId, int season)
    {
        if (!_byDriver.TryGetValue(driverId, out var entry))
        {
            return null;
        }

        var nearest = entry.Seasons[0];
        foreach (var candidate in entry.Seasons)
        {
            if (Math.Abs(candidate.Season - season) < Math.Abs(nearest.Season - season))
            {
                nearest = candidate;
            }
        }

        var level = nearest.Overall / FittedRatingEstimates.OverallPerLevel;
        var first = entry.Seasons[0].Season;
        if (season < first)
        {
            level -= FittedRatingEstimates.GrowthLevelsPerYearBeforeFirstSeason
                * Math.Min(first - season, FittedRatingEstimates.MaxYearsBeforeFirstSeason);
        }

        var current = Clamp(level);
        var potential = Math.Max(current, Clamp(entry.PeakLevel));
        return new DriverRating(Flat(current), Flat(potential));
    }

    private static int Clamp(double level) =>
        Math.Clamp((int)Math.Round(level, MidpointRounding.AwayFromZero), FittedRatingEstimates.MinLevel, FittedRatingEstimates.MaxLevel);

    private static DriverAttributes Flat(int v) => new(v, v, v, v, v, v, v, v, v, v, v);

    private sealed record Entry(SeasonEntry[] Seasons, double PeakLevel);

    private sealed class Document
    {
        public List<RatingEntry>? DriverRatings { get; set; }
    }

    private sealed class RatingEntry
    {
        public string? DriverId { get; set; }

        public List<SeasonEntry>? RatingBySeason { get; set; }

        public ArcEntry? Arc { get; set; }
    }

    private sealed class ArcEntry
    {
        public double? PeakLevel { get; set; }
    }

    private sealed class SeasonEntry
    {
        public int Season { get; set; }

        public int Overall { get; set; }
    }
}
