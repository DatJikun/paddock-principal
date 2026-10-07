using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Paddock.Data.Authored;
using Paddock.Data.Historical;
using Paddock.Data.World;
using Paddock.Domain.World;

namespace Paddock.Career;

/// <summary>
/// The files a career is built from, and the hash a save stores so a resume can refuse a changed base (INV-002).
/// Shared by the batch runner and the desktop window. The people cache stays optional (PP-041).
/// </summary>
public static class CareerData
{
    /// <summary>
    /// The base data a save depends on: every authored file, plus the people schedule and drivers files when the run used them
    /// (without them the hash is that of the authored files alone), plus the local starting data when there is one (#271).
    /// A resumed run must see the same hash.
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

        var starting = StartingDataLoader.DefaultPath(dataRoot);
        if (File.Exists(starting))
        {
            hash.AppendData("starting\n"u8);
            hash.AppendData(File.ReadAllBytes(starting));
            hash.AppendData("\n"u8);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    /// <summary>
    /// The car strengths and tiers a career that starts in <paramref name="startYear"/> is built with: the authored ones of
    /// <paramref name="data"/>, layered over the local starting data under <paramref name="dataRoot"/> when the pipeline has written it
    /// (#271). The same call for a new career and for a resumed one, so both read the same sources. The tests do not call it, so they
    /// never depend on the cache.
    /// </summary>
    public static StartingSources LoadStartingSources(string dataRoot, AuthoredData data, int startYear)
    {
        ArgumentNullException.ThrowIfNull(data);
        return StartingSources.For(startYear, StartingDataLoader.TryLoad(dataRoot), data.CarStrength, data.TeamTiers);
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

    /// <summary>
    /// The real race dates in the local cache under <paramref name="dataRoot"/>, or an empty book when there is no cache. The same
    /// cache folder as <see cref="ResolveProviderFiles"/>. A host passes it to <see cref="CareerInputsLoader"/>; the tests do not,
    /// so they never depend on the cache (#229).
    /// </summary>
    public static RaceDateBook LoadRaceDates(string dataRoot) => RaceDateLoader.Load(dataRoot);

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
