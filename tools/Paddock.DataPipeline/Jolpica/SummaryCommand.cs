using System.Globalization;
using System.Text.Json;
using Paddock.Data.Historical;

namespace Paddock.DataPipeline;

public static class SummaryCommand
{
    public const int DefaultFrom = 1950;
    public const int DefaultTo = 2025;

    public static int Execute(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (!CommandArgs.TryParse(args, start: 1, ["--cache", "--from", "--to"], stderr, out var options, out _))
        {
            return 1;
        }

        var from = DefaultFrom;
        var to = DefaultTo;
        if (options.ContainsKey("--from") && !TryYear(options, "--from", stderr, out from))
        {
            return 1;
        }

        if (options.ContainsKey("--to") && !TryYear(options, "--to", stderr, out to))
        {
            return 1;
        }

        if (from > to)
        {
            stderr.WriteLine("--from must not be greater than --to.");
            return 1;
        }

        var cacheRoot = options.TryGetValue("--cache", out var cache) ? cache : JolpicaCache.DefaultRoot();
        var normalized = JolpicaCache.Normalized(cacheRoot);
        var drivers = Load<HistoricalDriversDocument>(Path.Combine(normalized, "drivers.json"));
        var constructors = Load<HistoricalConstructorsDocument>(Path.Combine(normalized, "constructors.json"));
        var circuits = Load<HistoricalCircuitsDocument>(Path.Combine(normalized, "circuits.json"));
        var races = Load<HistoricalRacesDocument>(Path.Combine(normalized, "races.json"));
        var results = Load<HistoricalResultsDocument>(Path.Combine(normalized, "results.json"));
        var qualifying = Load<HistoricalQualifyingDocument>(Path.Combine(normalized, "qualifying.json"));

        var rawRoot = JolpicaCache.Raw(cacheRoot);
        var sprintRows = ReadSprintRows(rawRoot);
        var seasonIndex = ReadSeasonIndex(rawRoot);

        var driverIds = drivers.Drivers.Select(driver => driver.DriverId).ToHashSet(StringComparer.Ordinal);
        var constructorIds = constructors.Constructors.Select(constructor => constructor.ConstructorId).ToHashSet(StringComparer.Ordinal);
        var circuitIds = circuits.Circuits.Select(circuit => circuit.CircuitId).ToHashSet(StringComparer.Ordinal);
        var raceKeys = races.Races.Select(race => (race.Season, race.Round)).ToHashSet();
        var indyKeys = races.Races.Where(race => race.IsIndianapolis500).Select(race => (race.Season, race.Round)).ToHashSet();

        var shared = HistoricalEdges.SharedDriveCars(results.Results.Select(row => new CarEntry(row.Season, row.Round, row.CarNumber, row.ConstructorId)));
        var errors = new List<string>();
        CheckSchema(errors, "drivers.json", drivers.SchemaVersion);
        CheckSchema(errors, "constructors.json", constructors.SchemaVersion);
        CheckSchema(errors, "circuits.json", circuits.SchemaVersion);
        CheckSchema(errors, "races.json", races.SchemaVersion);
        CheckSchema(errors, "results.json", results.SchemaVersion);
        CheckSchema(errors, "qualifying.json", qualifying.SchemaVersion);

        foreach (var race in races.Races)
        {
            if (!circuitIds.Contains(race.CircuitId))
            {
                errors.Add($"race {Year(race.Season)}/{Year(race.Round)} references missing circuit {race.CircuitId}");
            }
        }

        foreach (var row in results.Results)
        {
            CheckRow(errors, "result", row.Season, row.Round, row.DriverId, row.ConstructorId, raceKeys, driverIds, constructorIds);
            var key = new CarEntry(row.Season, row.Round, row.CarNumber, row.ConstructorId);
            if (shared.Contains(key) != row.IsSharedDrive)
            {
                errors.Add($"result {Year(row.Season)}/{Year(row.Round)} {row.DriverId} shared-drive flag does not match car number {row.CarNumber}");
            }
        }

        foreach (var row in qualifying.Qualifying)
        {
            CheckRow(errors, "qualifying", row.Season, row.Round, row.DriverId, row.ConstructorId, raceKeys, driverIds, constructorIds);
        }

        foreach (var row in sprintRows)
        {
            CheckRow(errors, "sprint", row.Season, row.Round, row.DriverId, row.ConstructorId, raceKeys, driverIds, constructorIds);
        }

        var raceSeasons = races.Races.Select(race => race.Season).ToHashSet();
        var missingSeasons = new List<int>();
        var presentSeasons = 0;
        for (var year = from; year <= to; year++)
        {
            if (raceSeasons.Contains(year))
            {
                presentSeasons++;
            }
            else
            {
                missingSeasons.Add(year);
                errors.Add($"season {Year(year)} has no races");
            }

            if (seasonIndex.Count > 0 && !seasonIndex.Contains(year))
            {
                errors.Add($"season {Year(year)} is missing from the seasons index");
            }
        }

        errors.Sort(StringComparer.Ordinal);

        var qualifyingSeasons = qualifying.Qualifying.Select(row => row.Season).ToHashSet();
        var seasonsWithoutQualifying = raceSeasons.Where(year => !qualifyingSeasons.Contains(year)).OrderBy(year => year).ToList();
        var decades = BuildDecades(races.Races, results.Results, qualifying.Qualifying, sprintRows);

        var sharedRows = results.Results.Count(row => row.IsSharedDrive);
        var indyResultRows = results.Results.Count(row => indyKeys.Contains((row.Season, row.Round)));
        var nonClassified = results.Results.Count(row => !row.IsClassified);
        var disqualifications = results.Results.Count(row => row.IsDisqualified);

        var lines = new List<string>
        {
            $"drivers: {Count(drivers.Drivers.Count)}",
            $"constructors: {Count(constructors.Constructors.Count)}",
            $"circuits: {Count(circuits.Circuits.Count)}",
            $"races: {Count(races.Races.Count)}",
            $"results: {Count(results.Results.Count)}",
            $"qualifying: {Count(qualifying.Qualifying.Count)}",
            $"sprint result rows: {Count(sprintRows.Count)}",
            $"non-classified results: {Count(nonClassified)}",
            $"disqualifications: {Count(disqualifications)}",
            $"shared-drive rows: {Count(sharedRows)}",
            $"shared-drive cars: {Count(shared.Count)}",
            $"indianapolis 500 races (1950-1960): {Count(indyKeys.Count)}",
            $"indianapolis 500 result rows: {Count(indyResultRows)}",
            "seasons with no qualifying rows: " + (seasonsWithoutQualifying.Count == 0
                ? "none"
                : string.Join(", ", seasonsWithoutQualifying.Select(Year))),
        };

        foreach (var decade in decades)
        {
            lines.Add(
                $"decade {Year(decade.Key)}s: races {Count(decade.Value.Races)}, results {Count(decade.Value.Results)}, qualifying {Count(decade.Value.Qualifying)}, sprint rows {Count(decade.Value.SprintRows)}, shared-drive rows {Count(decade.Value.SharedDriveRows)}, indianapolis 500 races {Count(decade.Value.Indianapolis500Races)}");
        }

        lines.Add(
            "raw pages: "
            + $"seasons {Count(Pages(rawRoot, "seasons"))}, "
            + $"circuits {Count(Pages(rawRoot, "circuits"))}, "
            + $"drivers {Count(Pages(rawRoot, "drivers"))}, "
            + $"constructors {Count(Pages(rawRoot, "constructors"))}, "
            + $"races {Count(Pages(rawRoot, "races"))}, "
            + $"results {Count(Pages(rawRoot, "results"))}, "
            + $"qualifying {Count(Pages(rawRoot, "qualifying"))}, "
            + $"sprint {Count(Pages(rawRoot, "sprint"))}, "
            + $"driver-standings {Count(Pages(rawRoot, "driver-standings"))}, "
            + $"constructor-standings {Count(Pages(rawRoot, "constructor-standings"))}");
        lines.Add($"seasons {Year(from)}-{Year(to)}: present {Count(presentSeasons)}, missing {Count(missingSeasons.Count)}");
        if (missingSeasons.Count > 0)
        {
            lines.Add("missing seasons: " + string.Join(", ", missingSeasons.Select(Year)));
        }

        lines.Add($"reference errors: {Count(errors.Count)}");
        foreach (var error in errors)
        {
            lines.Add("- " + error);
        }

        foreach (var line in lines)
        {
            stdout.WriteLine(line);
        }

        return errors.Count == 0 ? 0 : 1;
    }

    private static void CheckSchema(List<string> errors, string fileName, int schemaVersion)
    {
        if (schemaVersion != HistoricalSchema.Version)
        {
            errors.Add($"{fileName} schemaVersion is {Count(schemaVersion)}, expected {Count(HistoricalSchema.Version)}");
        }
    }

    private static void CheckRow(
        List<string> errors,
        string kind,
        int season,
        int round,
        string driverId,
        string constructorId,
        HashSet<(int Season, int Round)> raceKeys,
        HashSet<string> driverIds,
        HashSet<string> constructorIds)
    {
        var where = $"{kind} {Year(season)}/{Year(round)} {driverId}";
        if (!raceKeys.Contains((season, round)))
        {
            errors.Add($"{where} references missing race");
        }

        if (!driverIds.Contains(driverId))
        {
            errors.Add($"{where} references missing driver");
        }

        if (!constructorIds.Contains(constructorId))
        {
            errors.Add($"{where} references missing constructor {constructorId}");
        }
    }

    private static SortedDictionary<int, DecadeCounts> BuildDecades(
        IReadOnlyList<HistoricalRace> races,
        IReadOnlyList<HistoricalResult> results,
        IReadOnlyList<HistoricalQualifying> qualifying,
        IReadOnlyList<SprintRow> sprintRows)
    {
        var decades = new SortedDictionary<int, DecadeCounts>();
        DecadeCounts Bucket(int season)
        {
            var start = season / 10 * 10;
            if (!decades.TryGetValue(start, out var counts))
            {
                counts = new DecadeCounts();
                decades[start] = counts;
            }

            return counts;
        }

        foreach (var race in races)
        {
            var counts = Bucket(race.Season);
            counts.Races++;
            if (race.IsIndianapolis500)
            {
                counts.Indianapolis500Races++;
            }
        }

        foreach (var row in results)
        {
            var counts = Bucket(row.Season);
            counts.Results++;
            if (row.IsSharedDrive)
            {
                counts.SharedDriveRows++;
            }
        }

        foreach (var row in qualifying)
        {
            Bucket(row.Season).Qualifying++;
        }

        foreach (var row in sprintRows)
        {
            Bucket(row.Season).SprintRows++;
        }

        return decades;
    }

    private static List<SprintRow> ReadSprintRows(string rawRoot)
    {
        var rows = new List<SprintRow>();
        var directory = Path.Combine(rawRoot, "sprint");
        if (!Directory.Exists(directory))
        {
            return rows;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "offset-*.json", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            if (!document.RootElement.TryGetProperty("MRData", out var mr)
                || !mr.TryGetProperty("RaceTable", out var table)
                || !table.TryGetProperty("Races", out var races)
                || races.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var race in races.EnumerateArray())
            {
                var season = ReadInt(race, "season", file);
                var round = ReadInt(race, "round", file);
                if (!race.TryGetProperty("SprintResults", out var results) || results.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var result in results.EnumerateArray())
                {
                    if (!result.TryGetProperty("Driver", out var driver) || !result.TryGetProperty("Constructor", out var constructor))
                    {
                        throw new InvalidDataException($"{file} sprint {Year(season)}/{Year(round)} is missing Driver or Constructor.");
                    }

                    var driverId = OptionalId(driver, "driverId");
                    var constructorId = OptionalId(constructor, "constructorId");
                    if (driverId is null || constructorId is null)
                    {
                        throw new InvalidDataException($"{file} sprint {Year(season)}/{Year(round)} is missing an id.");
                    }

                    rows.Add(new SprintRow(season, round, driverId, constructorId));
                }
            }
        }

        return rows;
    }

    private static HashSet<int> ReadSeasonIndex(string rawRoot)
    {
        var years = new HashSet<int>();
        var directory = Path.Combine(rawRoot, "seasons");
        if (!Directory.Exists(directory))
        {
            return years;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "offset-*.json", SearchOption.AllDirectories))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            if (!document.RootElement.TryGetProperty("MRData", out var mr)
                || !mr.TryGetProperty("SeasonTable", out var table)
                || !table.TryGetProperty("Seasons", out var seasons)
                || seasons.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var season in seasons.EnumerateArray())
            {
                years.Add(ReadInt(season, "season", file));
            }
        }

        return years;
    }

    private static string? OptionalId(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static int ReadInt(JsonElement obj, string name, string context)
    {
        if (!obj.TryGetProperty(name, out var value))
        {
            throw new InvalidDataException($"{context} is missing {name}.");
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => int.Parse(value.GetString()!, CultureInfo.InvariantCulture),
            JsonValueKind.Number => value.GetInt32(),
            _ => throw new InvalidDataException($"{context} has non-numeric {name}."),
        };
    }

    private static int Pages(string rawRoot, string resource)
    {
        var directory = Path.Combine(rawRoot, resource);
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        return Directory.EnumerateFiles(directory, "offset-*.json", SearchOption.AllDirectories).Count();
    }

    private static T Load<T>(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"Missing normalized file: {path}");
        }

        var document = JsonSerializer.Deserialize<T>(File.ReadAllText(path), HistoricalJson.Options);
        if (document is null)
        {
            throw new InvalidDataException($"Empty document: {path}");
        }

        return document;
    }

    private static bool TryYear(Dictionary<string, string> options, string flag, TextWriter stderr, out int year)
    {
        if (!int.TryParse(options[flag], NumberStyles.None, CultureInfo.InvariantCulture, out year))
        {
            stderr.WriteLine($"Invalid {flag} value: {options[flag]}");
            return false;
        }

        return true;
    }

    private static string Year(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string Count(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private sealed class DecadeCounts
    {
        public int Races { get; set; }
        public int Results { get; set; }
        public int Qualifying { get; set; }
        public int SprintRows { get; set; }
        public int SharedDriveRows { get; set; }
        public int Indianapolis500Races { get; set; }
    }

    private readonly record struct SprintRow(int Season, int Round, string DriverId, string ConstructorId);
}
