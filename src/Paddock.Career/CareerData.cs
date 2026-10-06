using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Paddock.Data.Historical;
using Paddock.Data.World;

namespace Paddock.Career;

/// <summary>
/// The files a career is built from, and the hash a save stores so a resume can refuse a changed base (INV-002).
/// Shared by the batch runner and the desktop window. The people cache stays optional (PP-041).
/// </summary>
public static class CareerData
{
    /// <summary>
    /// The base data a save depends on: every authored file, plus the people schedule and drivers files when the run used them
    /// (without them the hash is that of the authored files alone). A resumed run must see the same hash.
    /// </summary>
    public static string HashWorldData(string dataRoot, (string Schedule, string Drivers)? people)
    {
        var authored = Path.Combine(Path.GetFullPath(dataRoot), "authored");
        var files = Directory.EnumerateFiles(authored, "*.json", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(authored, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var relative in files)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(relative));
            hash.AppendData("\n"u8);
            hash.AppendData(File.ReadAllBytes(Path.Combine(authored, relative.Replace('/', Path.DirectorySeparatorChar))));
            hash.AppendData("\n"u8);
        }

        if (people is var (schedule, drivers))
        {
            foreach (var file in new[] { schedule, drivers })
            {
                hash.AppendData("people\n"u8);
                hash.AppendData(File.ReadAllBytes(file));
                hash.AppendData("\n"u8);
            }

            if (RatingsPathFor(schedule) is { } ratings)
            {
                hash.AppendData("ratings\n"u8);
                hash.AppendData(File.ReadAllBytes(ratings));
                hash.AppendData("\n"u8);
            }
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    /// <summary>The people schedule and drivers files in use, or null when the run has none (the empty provider).</summary>
    public static (string Schedule, string Drivers)? ResolveProviderFiles(string dataRoot, string? schedulePath, string? driversPath)
    {
        if (schedulePath is null || driversPath is null)
        {
            var cache = Path.Combine(Path.GetFullPath(dataRoot), "cache");
            schedulePath = Path.Combine(cache, "reports", "people_schedule.json");
            driversPath = Path.Combine(cache, "jolpica", "normalized", "drivers.json");
            if (!File.Exists(schedulePath) || !File.Exists(driversPath))
            {
                return null;
            }
        }

        return (schedulePath, driversPath);
    }

    public static IPeopleProvider LoadProvider((string Schedule, string Drivers)? files)
    {
        if (files is not var (schedulePath, driversPath))
        {
            return EmptyPeopleProvider.Instance;
        }

        var schedule = JsonSerializer.Deserialize<PeopleScheduleReport>(File.ReadAllText(schedulePath), HistoricalJson.Options)
            ?? throw new JsonException("The people schedule file is empty.");
        var drivers = JsonSerializer.Deserialize<HistoricalDriversDocument>(File.ReadAllText(driversPath), HistoricalJson.Options)
            ?? throw new JsonException("The drivers file is empty.");
        var ratingsPath = RatingsPathFor(schedulePath);
        var ratings = ratingsPath is null ? null : FittedDriverRatings.Load(ratingsPath);
        return new ScheduleBackedPeopleProvider(schedule, drivers.Drivers, ratings is null ? null : ratings.RatingFor);
    }

    /// <summary>The fitted ratings file written next to the people schedule (<c>reports/ratings.json</c>), or null when absent.</summary>
    public static string? RatingsPathFor(string schedulePath)
    {
        var path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(schedulePath)) ?? ".", "ratings.json");
        return File.Exists(path) ? path : null;
    }
}
