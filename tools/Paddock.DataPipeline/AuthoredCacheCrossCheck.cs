using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Paddock.Data.Authored;
using Paddock.Data.Historical;
using Paddock.Data.World;

namespace Paddock.DataPipeline;

/// <summary>
/// Compares authored research ids with a normalized Jolpica cache.
/// A constructor "raced" when a result row names it. An F1 staff stint is a career row
/// with no series, or with series "f1"; other series are not championship constructor ids.
/// The allow-list exempts ids from the Jolpica constructor lookup only.
/// </summary>
public static class AuthoredCacheCrossCheck
{
    public const string EngineSeasonGap = "engine-season-gap";
    public const string UnknownJolpicaConstructor = "unknown-jolpica-constructor";
    public const string UnknownJolpicaDriver = "unknown-jolpica-driver";
    public const string UnknownJolpicaRace = "unknown-jolpica-race";
    public const string UnmappedJolpicaRace = "unmapped-jolpica-race";

    private static readonly string[] CacheFiles =
    [
        "drivers.json",
        "constructors.json",
        "races.json",
        "results.json",
    ];

    private static readonly JsonSerializerOptions LooseJson = new()
    {
        AllowTrailingCommas = false,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
    };

    public static string DefaultCacheDirectory()
    {
        return JolpicaCache.Normalized(JolpicaCache.DefaultRoot());
    }

    public static AuthoredCacheCrossCheckResult Run(string dataRoot, AuthoredData data, string cacheDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);

        var fullCache = Path.GetFullPath(cacheDirectory);
        if (!Directory.Exists(fullCache))
        {
            return AuthoredCacheCrossCheckResult.Missing("cache: missing " + fullCache);
        }

        var missingFiles = new List<string>();
        foreach (var fileName in CacheFiles)
        {
            if (!File.Exists(Path.Combine(fullCache, fileName)))
            {
                missingFiles.Add(fileName);
            }
        }

        if (missingFiles.Count > 0)
        {
            return AuthoredCacheCrossCheckResult.Missing(
                "cache: missing " + string.Join(", ", missingFiles) + " in " + fullCache);
        }

        var failures = new List<string>();
        var drivers = ReadHistorical<HistoricalDriversDocument>(fullCache, "drivers.json", failures);
        var constructors = ReadHistorical<HistoricalConstructorsDocument>(fullCache, "constructors.json", failures);
        var races = ReadHistorical<HistoricalRacesDocument>(fullCache, "races.json", failures);
        var results = ReadHistorical<HistoricalResultsDocument>(fullCache, "results.json", failures);
        CheckSchema(failures, "drivers.json", drivers?.SchemaVersion);
        CheckSchema(failures, "constructors.json", constructors?.SchemaVersion);
        CheckSchema(failures, "races.json", races?.SchemaVersion);
        CheckSchema(failures, "results.json", results?.SchemaVersion);

        var rankingsPath = Path.Combine(dataRoot, "authored", "ratings", "reference_rankings.json");
        var allowPath = Path.Combine(dataRoot, "authored", "teams", "non_jolpica_constructors.json");
        var rankings = ReadLoose<List<RankingDocument>>(rankingsPath, failures);
        var allowList = ReadLoose<AllowListFile>(allowPath, failures);
        if (rankings is not null)
        {
            for (var i = 0; i < rankings.Count; i++)
            {
                if (rankings[i].Entries is null)
                {
                    failures.Add(rankingsPath + ": document " + (i + 1).ToString(CultureInfo.InvariantCulture) + " is missing entries.");
                }
            }
        }

        IReadOnlyList<AllowListEntry> allowEntries = [];
        if (allowList is not null)
        {
            if (allowList.Entries is null)
            {
                failures.Add(allowPath + ": missing entries.");
            }
            else
            {
                allowEntries = allowList.Entries;
                for (var i = 0; i < allowEntries.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(allowEntries[i].ConstructorId))
                    {
                        failures.Add(allowPath + ": entry " + (i + 1).ToString(CultureInfo.InvariantCulture) + " is missing constructorId.");
                    }
                }
            }
        }

        if (failures.Count > 0 || drivers is null || constructors is null || races is null || results is null || rankings is null || allowList is null)
        {
            return AuthoredCacheCrossCheckResult.Failed(failures);
        }

        var jolpicaDrivers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var driver in drivers.Drivers)
        {
            jolpicaDrivers.Add(driver.DriverId);
        }

        var jolpicaConstructors = new HashSet<string>(StringComparer.Ordinal);
        foreach (var constructor in constructors.Constructors)
        {
            jolpicaConstructors.Add(constructor.ConstructorId);
        }

        var jolpicaRaces = new HashSet<(int Season, int Round)>();
        foreach (var race in races.Races)
        {
            jolpicaRaces.Add((race.Season, race.Round));
        }

        var allowed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in allowEntries)
        {
            if (!string.IsNullOrWhiteSpace(entry.ConstructorId))
            {
                allowed.Add(entry.ConstructorId);
            }
        }

        var errors = new List<AuthoredDataError>();
        var engineGaps = EngineSeasonGaps(data, results.Results);
        foreach (var gap in engineGaps)
        {
            errors.Add(new AuthoredDataError(
                EngineSeasonGap,
                gap.ConstructorId + " " + gap.Season.ToString(CultureInfo.InvariantCulture)));
        }

        var constructorSources = ConstructorSources(data);
        var unknownConstructors = 0;
        foreach (var pair in constructorSources.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (jolpicaConstructors.Contains(pair.Key) || allowed.Contains(pair.Key))
            {
                continue;
            }

            unknownConstructors++;
            errors.Add(new AuthoredDataError(
                UnknownJolpicaConstructor,
                pair.Key + " (" + string.Join(", ", pair.Value) + ")"));
        }

        var driverSources = DriverSources(rankings);
        var unknownDrivers = 0;
        foreach (var pair in driverSources)
        {
            if (jolpicaDrivers.Contains(pair.Key))
            {
                continue;
            }

            unknownDrivers++;
            errors.Add(new AuthoredDataError(
                UnknownJolpicaDriver,
                pair.Key + " (" + string.Join(", ", pair.Value) + ")"));
        }

        // The authored first and second drivers (#325) name drivers as the people schedule spells them; a renamed one carries a prefix.
        foreach (var entry in data.DriverRolesFile?.Roles ?? [])
        {
            var source = entry.DriverId.StartsWith(ScheduleBackedPeopleProvider.RenamedDriverPrefix, StringComparison.Ordinal)
                ? entry.DriverId[ScheduleBackedPeopleProvider.RenamedDriverPrefix.Length..]
                : entry.DriverId;
            if (!string.IsNullOrWhiteSpace(source) && !jolpicaDrivers.Contains(source))
            {
                errors.Add(new AuthoredDataError(
                    UnknownJolpicaDriver,
                    entry.DriverId + " (driver_roles.json " + entry.Year.ToString(CultureInfo.InvariantCulture) + ")"));
            }
        }

        var mappedKeys = new HashSet<(int Season, int Round)>();
        var unknownRaces = new List<(int Season, int Round)>();
        foreach (var entry in data.RaceLayoutMap)
        {
            var key = (entry.Season, entry.Round);
            mappedKeys.Add(key);
            if (!jolpicaRaces.Contains(key))
            {
                unknownRaces.Add(key);
            }
        }

        unknownRaces.Sort(CompareRaces);
        var seenUnknown = new HashSet<(int Season, int Round)>();
        foreach (var race in unknownRaces)
        {
            if (!seenUnknown.Add(race))
            {
                continue;
            }

            errors.Add(new AuthoredDataError(
                UnknownJolpicaRace,
                "race " + FormatRace(race.Season, race.Round) + " is not in the Jolpica cache"));
        }

        var unmapped = new List<(int Season, int Round)>();
        foreach (var race in races.Races)
        {
            var key = (race.Season, race.Round);
            if (!mappedKeys.Contains(key))
            {
                unmapped.Add(key);
            }
        }

        unmapped.Sort(CompareRaces);
        var seenUnmapped = new HashSet<(int Season, int Round)>();
        foreach (var race in unmapped)
        {
            if (!seenUnmapped.Add(race))
            {
                continue;
            }

            errors.Add(new AuthoredDataError(
                UnmappedJolpicaRace,
                "Jolpica race " + FormatRace(race.Season, race.Round) + " has no layout"));
        }

        var raced = RacedConstructorSeasons(results.Results);
        var counts = new AuthoredCacheCrossCheckCounts(
            raced.Count,
            engineGaps.Count,
            constructorSources.Count,
            unknownConstructors,
            driverSources.Count,
            unknownDrivers,
            data.RaceLayoutMap.Count,
            races.Races.Count,
            seenUnknown.Count + seenUnmapped.Count);

        return new AuthoredCacheCrossCheckResult(null, [], counts, errors);
    }

    private static List<(string ConstructorId, int Season)> EngineSeasonGaps(
        AuthoredData data,
        IReadOnlyList<HistoricalResult> results)
    {
        var covered = new HashSet<(string ConstructorId, int Season)>();
        foreach (var entry in data.Engines.Entries)
        {
            covered.Add((entry.ConstructorId, entry.Year));
        }

        var gaps = new List<(string ConstructorId, int Season)>();
        foreach (var raced in RacedConstructorSeasons(results))
        {
            if (!covered.Contains(raced))
            {
                gaps.Add(raced);
            }
        }

        gaps.Sort(static (left, right) =>
        {
            var id = string.CompareOrdinal(left.ConstructorId, right.ConstructorId);
            return id != 0 ? id : left.Season.CompareTo(right.Season);
        });
        return gaps;
    }

    private static List<(string ConstructorId, int Season)> RacedConstructorSeasons(IReadOnlyList<HistoricalResult> results)
    {
        var raced = new HashSet<(string ConstructorId, int Season)>();
        foreach (var row in results)
        {
            raced.Add((row.ConstructorId, row.Season));
        }

        return raced.ToList();
    }

    private static SortedDictionary<string, SortedSet<string>> ConstructorSources(AuthoredData data)
    {
        var sources = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var entry in data.Engines.Entries)
        {
            AddSource(sources, entry.ConstructorId, "engines.json");
        }

        foreach (var group in data.Lineage.Lineages)
        {
            foreach (var entry in group.Entries)
            {
                AddSource(sources, entry.ConstructorId, "lineage.json");
            }
        }

        foreach (var organization in data.Founders.Organizations)
        {
            foreach (var entry in organization.ConstructorEntries)
            {
                AddSource(sources, entry.ConstructorId, "founders.json");
            }
        }

        foreach (var technology in data.Technologies)
        {
            if (technology.FirstUsed.Team is not null)
            {
                AddSource(sources, technology.FirstUsed.Team, "technologies.json");
            }
        }

        foreach (var person in data.Staff)
        {
            foreach (var stint in person.Career)
            {
                if (IsF1Stint(stint.Series))
                {
                    AddSource(sources, stint.Org, "staff.json");
                }
            }
        }

        return sources;
    }

    private static bool IsF1Stint(string? series)
    {
        return series is null || string.Equals(series, "f1", StringComparison.Ordinal);
    }

    private static SortedDictionary<string, SortedSet<string>> DriverSources(IReadOnlyList<RankingDocument> rankings)
    {
        var sources = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var document in rankings)
        {
            if (document.Entries is null)
            {
                continue;
            }

            var listId = string.IsNullOrWhiteSpace(document.Id) ? "(missing id)" : document.Id;
            foreach (var entry in document.Entries)
            {
                if (entry.DriverId is null)
                {
                    continue;
                }

                AddSource(sources, entry.DriverId, listId);
            }
        }

        return sources;
    }

    private static void AddSource(SortedDictionary<string, SortedSet<string>> sources, string id, string source)
    {
        if (!sources.TryGetValue(id, out var files))
        {
            files = new SortedSet<string>(StringComparer.Ordinal);
            sources.Add(id, files);
        }

        files.Add(source);
    }

    private static int CompareRaces((int Season, int Round) left, (int Season, int Round) right)
    {
        var season = left.Season.CompareTo(right.Season);
        return season != 0 ? season : left.Round.CompareTo(right.Round);
    }

    private static string FormatRace(int season, int round)
    {
        return season.ToString(CultureInfo.InvariantCulture)
            + " round "
            + round.ToString(CultureInfo.InvariantCulture);
    }

    private static void CheckSchema(List<string> failures, string fileName, int? schemaVersion)
    {
        if (schemaVersion is null || schemaVersion.Value == HistoricalSchema.Version)
        {
            return;
        }

        failures.Add(
            fileName
            + " schemaVersion is "
            + schemaVersion.Value.ToString(CultureInfo.InvariantCulture)
            + ", expected "
            + HistoricalSchema.Version.ToString(CultureInfo.InvariantCulture));
    }

    private static T? ReadHistorical<T>(string directory, string fileName, List<string> failures)
        where T : class
    {
        return Read<T>(Path.Combine(directory, fileName), fileName, failures, HistoricalJson.Options);
    }

    private static T? ReadLoose<T>(string path, List<string> failures)
        where T : class
    {
        return Read<T>(path, path, failures, LooseJson);
    }

    private static T? Read<T>(string path, string label, List<string> failures, JsonSerializerOptions options)
        where T : class
    {
        if (!File.Exists(path))
        {
            failures.Add("Missing file: " + path);
            return null;
        }

        try
        {
            var value = JsonSerializer.Deserialize<T>(File.ReadAllText(path), options);
            if (value is null)
            {
                failures.Add(label + ": JSON value is null.");
                return null;
            }

            return value;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            failures.Add(label + ": " + ex.Message);
            return null;
        }
    }

    private sealed record RankingDocument(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("entries")] IReadOnlyList<RankingEntry>? Entries);

    private sealed record RankingEntry(
        [property: JsonPropertyName("driverId")] string? DriverId);

    private sealed record AllowListFile(
        [property: JsonPropertyName("entries")] IReadOnlyList<AllowListEntry>? Entries);

    private sealed record AllowListEntry(
        [property: JsonPropertyName("constructorId")] string? ConstructorId);
}

public sealed record AuthoredCacheCrossCheckResult(
    string? CacheMessage,
    IReadOnlyList<string> LoadFailures,
    AuthoredCacheCrossCheckCounts? Counts,
    IReadOnlyList<AuthoredDataError> Errors)
{
    public static AuthoredCacheCrossCheckResult Missing(string message)
    {
        return new AuthoredCacheCrossCheckResult(message, [], null, []);
    }

    public static AuthoredCacheCrossCheckResult Failed(IReadOnlyList<string> failures)
    {
        return new AuthoredCacheCrossCheckResult(null, failures, null, []);
    }
}

public sealed record AuthoredCacheCrossCheckCounts(
    int EngineSeasonsChecked,
    int EngineSeasonGaps,
    int ConstructorsChecked,
    int UnknownConstructors,
    int DriversChecked,
    int UnknownDrivers,
    int RacesMapped,
    int RacesInCache,
    int RacesMissing);
