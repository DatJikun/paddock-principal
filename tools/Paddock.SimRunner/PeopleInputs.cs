using System.Text.Json;
using Paddock.Data.Historical;
using Paddock.Data.World;

namespace Paddock.SimRunner;

/// <summary>
/// The local files that make real drivers: the people schedule, the normalized drivers table and, optionally, the ratings
/// model output. <see cref="Ratings"/> is null when the run has none; then every real driver is "unrated" (a flat stand-in).
/// </summary>
public sealed record PeopleInputs(string Schedule, string Drivers, string? Ratings)
{
    /// <summary>
    /// Finds the files. With neither <paramref name="schedulePath"/> nor <paramref name="driversPath"/> the pair is looked up in
    /// <c>&lt;data&gt;/cache</c> (none found gives null, the empty provider) and so is <c>reports/ratings.json</c>, used when present.
    /// With an explicit pair the ratings are only those named by <paramref name="ratingsPath"/>, so a run named by its files does
    /// not depend on whatever else sits in the cache.
    /// </summary>
    public static PeopleInputs? Resolve(string dataRoot, string? schedulePath, string? driversPath, string? ratingsPath)
    {
        var cache = Path.Combine(Path.GetFullPath(dataRoot), "cache");
        var explicitPair = schedulePath is not null;
        if (!explicitPair)
        {
            schedulePath = Path.Combine(cache, "reports", "people_schedule.json");
            driversPath = Path.Combine(cache, "jolpica", "normalized", "drivers.json");
            if (!File.Exists(schedulePath) || !File.Exists(driversPath))
            {
                return null;
            }
        }

        if (ratingsPath is null && !explicitPair)
        {
            var found = Path.Combine(cache, "reports", "ratings.json");
            ratingsPath = File.Exists(found) ? found : null;
        }

        return new PeopleInputs(schedulePath!, driversPath!, ratingsPath);
    }

    public IEnumerable<string> Files => Ratings is null ? [Schedule, Drivers] : [Schedule, Drivers, Ratings];

    public IPeopleProvider Load()
    {
        var schedule = JsonSerializer.Deserialize<PeopleScheduleReport>(File.ReadAllText(Schedule), HistoricalJson.Options)
            ?? throw new JsonException("The people schedule file is empty.");
        var drivers = JsonSerializer.Deserialize<HistoricalDriversDocument>(File.ReadAllText(Drivers), HistoricalJson.Options)
            ?? throw new JsonException("The drivers file is empty.");
        if (Ratings is null)
        {
            return new ScheduleBackedPeopleProvider(schedule, drivers.Drivers);
        }

        var ratings = JsonSerializer.Deserialize<HistoricalRatingsDocument>(File.ReadAllText(Ratings), HistoricalJson.Options)
            ?? throw new JsonException("The ratings file is empty.");
        return new ScheduleBackedPeopleProvider(schedule, drivers.Drivers, new HistoricalRatingTable(ratings).RatingFor);
    }
}
