using System.Globalization;
using Paddock.Data.Authored;

namespace Paddock.DataPipeline;

public static class ValidateAuthoredCommand
{
    public static int Execute(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (args.Length == 0 || !string.Equals(args[0], "validate-authored", StringComparison.Ordinal))
        {
            stderr.WriteLine("Unknown command. Expected: validate-authored [--data-root <path>] [--with-cache [path]]");
            return 1;
        }

        string? dataRoot = null;
        string? cachePath = null;
        var withCache = false;
        for (var i = 1; i < args.Length; i++)
        {
            var flag = args[i];
            if (string.Equals(flag, "--data-root", StringComparison.Ordinal))
            {
                if (dataRoot is not null)
                {
                    stderr.WriteLine("Duplicate --data-root.");
                    return 1;
                }

                if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
                {
                    stderr.WriteLine("Missing value for --data-root.");
                    return 1;
                }

                dataRoot = args[++i];
                continue;
            }

            if (string.Equals(flag, "--with-cache", StringComparison.Ordinal))
            {
                if (withCache)
                {
                    stderr.WriteLine("Duplicate --with-cache.");
                    return 1;
                }

                withCache = true;
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(args[i + 1]))
                    {
                        stderr.WriteLine("Missing value for --with-cache.");
                        return 1;
                    }

                    cachePath = args[++i];
                }

                continue;
            }

            stderr.WriteLine($"Unknown argument: {flag}");
            return 1;
        }

        try
        {
            var root = dataRoot ?? FindDataRoot();
            var data = AuthoredDataLoader.Load(root);
            var errors = AuthoredDataValidator.Validate(data);
            WriteReport(stdout, data, errors);
            if (!withCache)
            {
                return errors.Count == 0 ? 0 : 1;
            }

            string cacheDirectory;
            if (cachePath is null)
            {
                try
                {
                    cacheDirectory = AuthoredCacheCrossCheck.DefaultCacheDirectory();
                }
                catch (InvalidOperationException ex)
                {
                    stdout.WriteLine("cache: " + ex.Message);
                    return 1;
                }
            }
            else
            {
                cacheDirectory = cachePath;
            }

            var cache = AuthoredCacheCrossCheck.Run(root, data, cacheDirectory);
            WriteCacheReport(stdout, cache);
            var cacheFailed = cache.CacheMessage is not null
                || cache.LoadFailures.Count > 0
                || cache.Errors.Count > 0;
            return errors.Count == 0 && !cacheFailed ? 0 : 1;
        }
        catch (AuthoredDataLoadException ex)
        {
            stdout.WriteLine("authored data: load failed");
            stdout.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string FindDataRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var asDataDirectory = Path.Combine(dir.FullName, "authored", "regulations", "catalog.json");
            if (File.Exists(asDataDirectory))
            {
                return dir.FullName;
            }

            var asRepoRoot = Path.Combine(dir.FullName, "data", "authored", "regulations", "catalog.json");
            if (File.Exists(asRepoRoot))
            {
                return Path.Combine(dir.FullName, "data");
            }

            dir = dir.Parent;
        }

        throw new AuthoredDataLoadException(
            "Could not find data/authored. Pass --data-root pointing at the data directory.");
    }

    private static void WriteReport(TextWriter stdout, AuthoredData data, IReadOnlyList<AuthoredDataError> errors)
    {
        var layoutCount = 0;
        foreach (var circuit in data.Circuits.Circuits)
        {
            layoutCount += circuit.Layouts.Count;
        }

        stdout.WriteLine(errors.Count == 0
            ? "authored data: ok"
            : "authored data: " + errors.Count.ToString(CultureInfo.InvariantCulture) + (errors.Count == 1 ? " error" : " errors"));
        stdout.WriteLine("catalog dimensions: " + data.Catalog.Count.ToString(CultureInfo.InvariantCulture));
        stdout.WriteLine("timeline periods: " + data.Timeline.Count.ToString(CultureInfo.InvariantCulture));
        stdout.WriteLine("other series ideas: " + data.OtherSeriesIdeas.Count.ToString(CultureInfo.InvariantCulture));
        stdout.WriteLine("circuits: " + data.Circuits.Circuits.Count.ToString(CultureInfo.InvariantCulture));
        stdout.WriteLine("layouts: " + layoutCount.ToString(CultureInfo.InvariantCulture));
        stdout.WriteLine("race map entries: " + data.RaceLayoutMap.Count.ToString(CultureInfo.InvariantCulture));
        stdout.WriteLine("technologies: " + data.Technologies.Count.ToString(CultureInfo.InvariantCulture));
        stdout.WriteLine("engine entries: " + data.Engines.Entries.Count.ToString(CultureInfo.InvariantCulture));
        stdout.WriteLine("constructors: " + data.ConstructorIds.Count.ToString(CultureInfo.InvariantCulture));
        stdout.WriteLine("lineages: " + data.Lineage.Lineages.Count.ToString(CultureInfo.InvariantCulture));
        stdout.WriteLine("founder organizations: " + data.Founders.Organizations.Count.ToString(CultureInfo.InvariantCulture));
        stdout.WriteLine("staff: " + data.Staff.Count.ToString(CultureInfo.InvariantCulture));
        stdout.WriteLine("era catalog dimensions: " + data.EraCatalog.Count.ToString(CultureInfo.InvariantCulture));
        stdout.WriteLine("era timeline periods: " + data.EraTimeline.Count.ToString(CultureInfo.InvariantCulture));
        stdout.WriteLine("cpi years: " + data.CpiYears.Count.ToString(CultureInfo.InvariantCulture));
        stdout.WriteLine("track geometries: " + data.TrackGeometries.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var error in errors)
        {
            stdout.WriteLine("error: " + error.Message);
        }
    }

    private static void WriteCacheReport(TextWriter stdout, AuthoredCacheCrossCheckResult cache)
    {
        if (cache.CacheMessage is not null)
        {
            stdout.WriteLine(cache.CacheMessage);
            return;
        }

        if (cache.LoadFailures.Count > 0)
        {
            stdout.WriteLine("cache cross-check: load failed");
            foreach (var failure in cache.LoadFailures)
            {
                stdout.WriteLine(failure);
            }

            return;
        }

        var errorCount = cache.Errors.Count;
        stdout.WriteLine(errorCount == 0
            ? "cache cross-check: ok"
            : "cache cross-check: " + errorCount.ToString(CultureInfo.InvariantCulture) + (errorCount == 1 ? " error" : " errors"));
        var counts = cache.Counts ?? throw new InvalidOperationException("Cache cross-check produced no counts.");
        stdout.WriteLine(
            "engine seasons: "
            + counts.EngineSeasonsChecked.ToString(CultureInfo.InvariantCulture)
            + " checked, "
            + Plural(counts.EngineSeasonGaps, "gap", "gaps"));
        stdout.WriteLine(
            "constructors: "
            + counts.ConstructorsChecked.ToString(CultureInfo.InvariantCulture)
            + " checked, "
            + counts.UnknownConstructors.ToString(CultureInfo.InvariantCulture)
            + " unknown");
        stdout.WriteLine(
            "drivers: "
            + counts.DriversChecked.ToString(CultureInfo.InvariantCulture)
            + " checked, "
            + counts.UnknownDrivers.ToString(CultureInfo.InvariantCulture)
            + " unknown");
        stdout.WriteLine(
            "races: "
            + counts.RacesMapped.ToString(CultureInfo.InvariantCulture)
            + " mapped, "
            + counts.RacesInCache.ToString(CultureInfo.InvariantCulture)
            + " in cache, "
            + counts.RacesMissing.ToString(CultureInfo.InvariantCulture)
            + " missing");
        foreach (var error in cache.Errors)
        {
            stdout.WriteLine("error [" + error.Code + "]: " + error.Message);
        }
    }

    private static string Plural(int count, string singular, string plural)
    {
        return count.ToString(CultureInfo.InvariantCulture) + " " + (count == 1 ? singular : plural);
    }
}
