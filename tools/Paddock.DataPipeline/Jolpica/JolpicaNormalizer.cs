using System.Globalization;
using System.Text.Json;
using Paddock.Data.Historical;

namespace Paddock.DataPipeline;

/// <summary>
/// Reads the raw Jolpica cache and produces the typed historical documents.
/// Edge cases kept in the output (not dropped):
/// - Shared drives: multiple drivers per car. Jolpica emits one result row per driver with the
///   same car number, constructor and position. Points are already split by the API; we do not split them again.
/// - Indianapolis 500, 1950–1960: championship rounds with circuitId "indianapolis". Flagged, not removed.
/// - Non-classified finishers: numeric <c>position</c> is only an ordering slot. Classification follows
///   <c>positionText</c> (digits). <c>R</c> includes retirements and Jolpica's replacement for Ergast <c>N</c>.
/// - Disqualifications: <c>positionText</c> "D" or status "Disqualified". Flagged and kept.
/// - Missing qualifying: many seasons before 1994 return an empty RaceTable (total 0). That is valid.
///   The grid slot still lives on the result row. Qualifying sessions that only published Q1 leave Q2 and Q3 null.
/// - Sprint races: a separate SprintResults payload from 2021 onward. Not merged into race results and not
///   written into the six normalized files. <see cref="SummaryCommand"/> counts them from the raw cache.
/// Pagination can split one race across pages, so result and qualifying rows are appended, not replaced.
/// Fastest laps are ignored; they are not part of this schema.
/// </summary>
public static class JolpicaNormalizer
{
    public static NormalizedHistoricalData ReadRaw(string rawRoot)
    {
        ArgumentException.ThrowIfNullOrEmpty(rawRoot);

        var drivers = new Dictionary<string, HistoricalDriver>(StringComparer.Ordinal);
        var constructors = new Dictionary<string, HistoricalConstructor>(StringComparer.Ordinal);
        var circuits = new Dictionary<string, HistoricalCircuit>(StringComparer.Ordinal);
        var races = new Dictionary<(int Season, int Round), HistoricalRace>();
        var results = new List<HistoricalResult>();
        var qualifying = new List<HistoricalQualifying>();

        foreach (var file in RawFiles(rawRoot, "drivers"))
        {
            ReadDrivers(file, drivers);
        }

        foreach (var file in RawFiles(rawRoot, "constructors"))
        {
            ReadConstructors(file, constructors);
        }

        foreach (var file in RawFiles(rawRoot, "circuits"))
        {
            ReadCircuits(file, circuits);
        }

        foreach (var file in RawFiles(rawRoot, "races"))
        {
            ReadRaces(file, races);
        }

        foreach (var file in RawFiles(rawRoot, "results"))
        {
            ReadResults(file, results);
        }

        foreach (var file in RawFiles(rawRoot, "qualifying"))
        {
            ReadQualifying(file, qualifying);
        }

        var shared = HistoricalEdges.SharedDriveCars(results.Select(row => new CarEntry(row.Season, row.Round, row.CarNumber, row.ConstructorId)));
        if (shared.Count > 0)
        {
            for (var i = 0; i < results.Count; i++)
            {
                var row = results[i];
                var key = new CarEntry(row.Season, row.Round, row.CarNumber, row.ConstructorId);
                if (shared.Contains(key) && !row.IsSharedDrive)
                {
                    results[i] = row with { IsSharedDrive = true };
                }
            }
        }

        var driverList = drivers.Values.OrderBy(driver => driver.DriverId, StringComparer.Ordinal).ToList();
        var constructorList = constructors.Values.OrderBy(constructor => constructor.ConstructorId, StringComparer.Ordinal).ToList();
        var circuitList = circuits.Values.OrderBy(circuit => circuit.CircuitId, StringComparer.Ordinal).ToList();
        var raceList = races.Values
            .OrderBy(race => race.Season)
            .ThenBy(race => race.Round)
            .ToList();
        results.Sort(CompareResults);
        qualifying.Sort(CompareQualifying);

        return new NormalizedHistoricalData(
            new HistoricalDriversDocument(HistoricalSchema.Version, driverList),
            new HistoricalConstructorsDocument(HistoricalSchema.Version, constructorList),
            new HistoricalCircuitsDocument(HistoricalSchema.Version, circuitList),
            new HistoricalRacesDocument(HistoricalSchema.Version, raceList),
            new HistoricalResultsDocument(HistoricalSchema.Version, results),
            new HistoricalQualifyingDocument(HistoricalSchema.Version, qualifying));
    }

    private static int CompareResults(HistoricalResult left, HistoricalResult right)
    {
        var season = left.Season.CompareTo(right.Season);
        if (season != 0)
        {
            return season;
        }

        var round = left.Round.CompareTo(right.Round);
        if (round != 0)
        {
            return round;
        }

        var position = left.Position.CompareTo(right.Position);
        if (position != 0)
        {
            return position;
        }

        return string.CompareOrdinal(left.DriverId, right.DriverId);
    }

    private static int CompareQualifying(HistoricalQualifying left, HistoricalQualifying right)
    {
        var season = left.Season.CompareTo(right.Season);
        if (season != 0)
        {
            return season;
        }

        var round = left.Round.CompareTo(right.Round);
        if (round != 0)
        {
            return round;
        }

        var position = left.Position.CompareTo(right.Position);
        if (position != 0)
        {
            return position;
        }

        return string.CompareOrdinal(left.DriverId, right.DriverId);
    }

    private static IEnumerable<string> RawFiles(string rawRoot, string resource)
    {
        var directory = Path.Combine(rawRoot, resource);
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        var files = Directory.EnumerateFiles(directory, "offset-*.json", SearchOption.AllDirectories)
            .OrderBy(path => Path.GetDirectoryName(path), StringComparer.Ordinal)
            .ThenBy(path => ParseOffset(Path.GetFileName(path)))
            .ToList();
        foreach (var file in files)
        {
            yield return file;
        }
    }

    private static int ParseOffset(string fileName)
    {
        const string Prefix = "offset-";
        const string Suffix = ".json";
        if (!fileName.StartsWith(Prefix, StringComparison.Ordinal) || !fileName.EndsWith(Suffix, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unexpected raw cache file name: {fileName}");
        }

        var number = fileName.Substring(Prefix.Length, fileName.Length - Prefix.Length - Suffix.Length);
        if (!int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var offset))
        {
            throw new InvalidDataException($"Unexpected raw cache file name: {fileName}");
        }

        return offset;
    }

    private static void ReadDrivers(string file, Dictionary<string, HistoricalDriver> drivers)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        var table = Table(document.RootElement, file, "DriverTable");
        if (!TryArray(table, "Drivers", out var rows))
        {
            return;
        }

        foreach (var row in rows.EnumerateArray())
        {
            var id = RequiredString(row, "driverId", file);
            drivers[id] = new HistoricalDriver(
                id,
                RequiredString(row, "givenName", file),
                RequiredString(row, "familyName", file),
                RequiredString(row, "dateOfBirth", file),
                RequiredString(row, "nationality", file),
                OptionalString(row, "code"),
                OptionalString(row, "permanentNumber"),
                RequiredString(row, "url", file));
        }
    }

    private static void ReadConstructors(string file, Dictionary<string, HistoricalConstructor> constructors)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        var table = Table(document.RootElement, file, "ConstructorTable");
        if (!TryArray(table, "Constructors", out var rows))
        {
            return;
        }

        foreach (var row in rows.EnumerateArray())
        {
            var id = RequiredString(row, "constructorId", file);
            constructors[id] = new HistoricalConstructor(
                id,
                RequiredString(row, "name", file),
                RequiredString(row, "nationality", file),
                RequiredString(row, "url", file));
        }
    }

    private static void ReadCircuits(string file, Dictionary<string, HistoricalCircuit> circuits)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        var table = Table(document.RootElement, file, "CircuitTable");
        if (!TryArray(table, "Circuits", out var rows))
        {
            return;
        }

        foreach (var row in rows.EnumerateArray())
        {
            var id = RequiredString(row, "circuitId", file);
            if (!row.TryGetProperty("Location", out var location))
            {
                throw new InvalidDataException($"{file} circuit {id} is missing Location.");
            }

            circuits[id] = new HistoricalCircuit(
                id,
                RequiredString(row, "circuitName", file),
                RequiredString(location, "locality", file),
                RequiredString(location, "country", file),
                RequiredString(location, "lat", file),
                RequiredString(location, "long", file),
                RequiredString(row, "url", file));
        }
    }

    private static void ReadRaces(string file, Dictionary<(int Season, int Round), HistoricalRace> races)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        foreach (var race in Races(document.RootElement, file))
        {
            var season = ReadInt(race, "season", file);
            var round = ReadInt(race, "round", file);
            if (!race.TryGetProperty("Circuit", out var circuit))
            {
                throw new InvalidDataException($"{file} race {season}/{round} is missing Circuit.");
            }

            var circuitId = RequiredString(circuit, "circuitId", file);
            var parsed = new HistoricalRace(
                season,
                round,
                RequiredString(race, "raceName", file),
                circuitId,
                RequiredString(race, "date", file),
                OptionalString(race, "time"),
                RequiredString(race, "url", file),
                HistoricalEdges.IsIndianapolis500(season, circuitId));

            if (races.TryGetValue((season, round), out var existing) && existing != parsed)
            {
                throw new InvalidDataException($"{file} repeats race {season}/{round} with different data.");
            }

            races[(season, round)] = parsed;
        }
    }

    private static void ReadResults(string file, List<HistoricalResult> results)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        foreach (var race in Races(document.RootElement, file))
        {
            var season = ReadInt(race, "season", file);
            var round = ReadInt(race, "round", file);
            if (!TryArray(race, "Results", out var rows))
            {
                continue;
            }

            foreach (var row in rows.EnumerateArray())
            {
                results.Add(ParseResult(row, season, round, file));
            }
        }
    }

    private static HistoricalResult ParseResult(JsonElement row, int season, int round, string file)
    {
        var context = $"{file} result {season}/{round}";
        var driver = Nested(row, "Driver", context);
        var constructor = Nested(row, "Constructor", context);
        var positionText = RequiredString(row, "positionText", context);
        var status = RequiredString(row, "status", context);
        OptionalTime(row, out var time, out var millis);
        return new HistoricalResult(
            season,
            round,
            RequiredString(driver, "driverId", context),
            RequiredString(constructor, "constructorId", context),
            RequiredString(row, "number", context),
            ReadInt(row, "position", context),
            positionText,
            status,
            ReadDecimal(row, "points", context),
            ReadInt(row, "grid", context),
            ReadInt(row, "laps", context),
            time,
            millis,
            HistoricalEdges.IsClassified(positionText),
            HistoricalEdges.IsDisqualified(positionText, status),
            IsSharedDrive: false);
    }

    private static void ReadQualifying(string file, List<HistoricalQualifying> qualifying)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        foreach (var race in Races(document.RootElement, file))
        {
            var season = ReadInt(race, "season", file);
            var round = ReadInt(race, "round", file);
            if (!TryArray(race, "QualifyingResults", out var rows))
            {
                continue;
            }

            foreach (var row in rows.EnumerateArray())
            {
                var context = $"{file} qualifying {season}/{round}";
                var driver = Nested(row, "Driver", context);
                var constructor = Nested(row, "Constructor", context);
                qualifying.Add(new HistoricalQualifying(
                    season,
                    round,
                    RequiredString(driver, "driverId", context),
                    RequiredString(constructor, "constructorId", context),
                    RequiredString(row, "number", context),
                    ReadInt(row, "position", context),
                    OptionalString(row, "Q1"),
                    OptionalString(row, "Q2"),
                    OptionalString(row, "Q3")));
            }
        }
    }

    private static JsonElement Table(JsonElement root, string file, string name)
    {
        var mr = MrData(root, file);
        if (!mr.TryGetProperty(name, out var table))
        {
            return default;
        }

        return table;
    }

    private static IEnumerable<JsonElement> Races(JsonElement root, string file)
    {
        var mr = MrData(root, file);
        if (!mr.TryGetProperty("RaceTable", out var table) || !TryArray(table, "Races", out var races))
        {
            yield break;
        }

        foreach (var race in races.EnumerateArray())
        {
            yield return race;
        }
    }

    private static JsonElement MrData(JsonElement root, string file)
    {
        if (!root.TryGetProperty("MRData", out var mr))
        {
            throw new InvalidDataException($"{file} is missing MRData.");
        }

        return mr;
    }

    private static bool TryArray(JsonElement obj, string name, out JsonElement array)
    {
        if (obj.ValueKind == JsonValueKind.Object
            && obj.TryGetProperty(name, out array)
            && array.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        array = default;
        return false;
    }

    private static JsonElement Nested(JsonElement obj, string name, string context)
    {
        if (!obj.TryGetProperty(name, out var nested) || nested.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{context} is missing {name}.");
        }

        return nested;
    }

    private static void OptionalTime(JsonElement row, out string? time, out long? millis)
    {
        time = null;
        millis = null;
        if (!row.TryGetProperty("Time", out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        time = OptionalString(value, "time");
        if (!value.TryGetProperty("millis", out var millisElement) || millisElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return;
        }

        millis = millisElement.ValueKind switch
        {
            JsonValueKind.String => long.Parse(millisElement.GetString()!, CultureInfo.InvariantCulture),
            JsonValueKind.Number => millisElement.GetInt64(),
            _ => throw new InvalidDataException("Time.millis is not numeric."),
        };
    }

    private static string RequiredString(JsonElement obj, string name, string context)
    {
        var value = OptionalString(obj, name);
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidDataException($"{context} is missing {name}.");
        }

        return value;
    }

    private static string? OptionalString(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object
            || !obj.TryGetProperty(name, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.GetString();
    }

    private static int ReadInt(JsonElement obj, string name, string context)
    {
        if (!obj.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
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

    private static decimal ReadDecimal(JsonElement obj, string name, string context)
    {
        if (!obj.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw new InvalidDataException($"{context} is missing {name}.");
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => decimal.Parse(value.GetString()!, CultureInfo.InvariantCulture),
            JsonValueKind.Number => value.GetDecimal(),
            _ => throw new InvalidDataException($"{context} has non-numeric {name}."),
        };
    }
}

public sealed record NormalizedHistoricalData(
    HistoricalDriversDocument Drivers,
    HistoricalConstructorsDocument Constructors,
    HistoricalCircuitsDocument Circuits,
    HistoricalRacesDocument Races,
    HistoricalResultsDocument Results,
    HistoricalQualifyingDocument Qualifying);
