using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Paddock.Data.Historical;

/// <summary>
/// Stable schema for Jolpica-F1 data after normalization.
/// Identifiers are Jolpica ids unchanged: <c>driverId</c>, <c>constructorId</c>, <c>circuitId</c>.
/// A race has no separate Jolpica id; its identity is <c>season</c> + <c>round</c>.
/// </summary>
public static class HistoricalSchema
{
    public const int Version = 1;
}

public static class HistoricalJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };
    }
}

public sealed record HistoricalDriversDocument(int SchemaVersion, IReadOnlyList<HistoricalDriver> Drivers);

public sealed record HistoricalDriver(
    string DriverId,
    string GivenName,
    string FamilyName,
    string DateOfBirth,
    string Nationality,
    string? Code,
    string? PermanentNumber,
    string Url);

public sealed record HistoricalConstructorsDocument(int SchemaVersion, IReadOnlyList<HistoricalConstructor> Constructors);

public sealed record HistoricalConstructor(
    string ConstructorId,
    string Name,
    string Nationality,
    string Url);

public sealed record HistoricalCircuitsDocument(int SchemaVersion, IReadOnlyList<HistoricalCircuit> Circuits);

public sealed record HistoricalCircuit(
    string CircuitId,
    string Name,
    string Locality,
    string Country,
    string Latitude,
    string Longitude,
    string Url);

public sealed record HistoricalRacesDocument(int SchemaVersion, IReadOnlyList<HistoricalRace> Races);

public sealed record HistoricalRace(
    int Season,
    int Round,
    string Name,
    string CircuitId,
    string Date,
    string? Time,
    string Url,
    bool IsIndianapolis500);

public sealed record HistoricalResultsDocument(int SchemaVersion, IReadOnlyList<HistoricalResult> Results);

public sealed record HistoricalResult(
    int Season,
    int Round,
    string DriverId,
    string ConstructorId,
    string CarNumber,
    int Position,
    string PositionText,
    string Status,
    decimal Points,
    int Grid,
    int Laps,
    string? Time,
    long? TimeMillis,
    bool IsClassified,
    bool IsDisqualified,
    bool IsSharedDrive);

public sealed record HistoricalQualifyingDocument(int SchemaVersion, IReadOnlyList<HistoricalQualifying> Qualifying);

public sealed record HistoricalQualifying(
    int Season,
    int Round,
    string DriverId,
    string ConstructorId,
    string CarNumber,
    int Position,
    string? Q1,
    string? Q2,
    string? Q3);

/// <summary>
/// Edge cases that the Jolpica payload does not label on its own.
/// Shared drives, the 1950–1960 Indianapolis 500, classification and disqualification
/// are derived here so the pipeline and the summary use one definition.
/// </summary>
public static class HistoricalEdges
{
    public const string IndianapolisCircuitId = "indianapolis";
    public const int IndianapolisFirstSeason = 1950;
    public const int IndianapolisLastSeason = 1960;

    /// <summary>
    /// Championship rounds at Indianapolis from 1950 through 1960 were the Indy 500,
    /// not Formula 1 races. Later use of the same circuit id would not be that event.
    /// </summary>
    public static bool IsIndianapolis500(int season, string circuitId)
    {
        return season >= IndianapolisFirstSeason
            && season <= IndianapolisLastSeason
            && string.Equals(circuitId, IndianapolisCircuitId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Jolpica still sends a numeric <c>position</c> for drivers who did not classify.
    /// That number is an ordering slot. A classified finish is a <c>positionText</c> made only of digits.
    /// <c>R</c> is a retirement. Jolpica also uses <c>R</c> where Ergast used <c>N</c> (not classified);
    /// the <c>status</c> string still says "Not classified" in that case.
    /// <c>D</c> disqualified, <c>E</c> excluded, <c>W</c> withdrew, <c>F</c> failed to qualify.
    /// </summary>
    public static bool IsClassified(string positionText)
    {
        if (positionText.Length == 0)
        {
            return false;
        }

        foreach (var character in positionText)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsDisqualified(string positionText, string status)
    {
        return positionText == "D"
            || string.Equals(status, "Disqualified", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A shared drive is two or more drivers on the same car in one race.
    /// Jolpica keeps one result row per driver. The car is the race plus car number plus constructor
    /// (1950 British Grand Prix car 10: Fry and Shawe-Taylor, both Maserati, same position).
    /// Rows with an empty car number are not grouped.
    /// </summary>
    public static HashSet<CarEntry> SharedDriveCars(IEnumerable<CarEntry> entries)
    {
        var counts = new Dictionary<CarEntry, int>();
        foreach (var entry in entries)
        {
            if (entry.CarNumber.Length == 0)
            {
                continue;
            }

            counts.TryGetValue(entry, out var count);
            counts[entry] = count + 1;
        }

        var shared = new HashSet<CarEntry>();
        foreach (var pair in counts)
        {
            if (pair.Value > 1)
            {
                shared.Add(pair.Key);
            }
        }

        return shared;
    }
}

public readonly record struct CarEntry(int Season, int Round, string CarNumber, string ConstructorId);
