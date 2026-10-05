using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Paddock.Domain.Cars;

namespace Paddock.Data.Authored;

public sealed record CarStrengthFile(
    [property: JsonPropertyName("notes")] string Notes,
    [property: JsonPropertyName("strengths")] IReadOnlyList<CarStrengthEntry> Strengths);

public sealed record CarStrengthEntry(
    [property: JsonPropertyName("season")] int Season,
    [property: JsonPropertyName("constructorId")] string ConstructorId,
    [property: JsonPropertyName("strength")] double Strength,
    [property: JsonPropertyName("confidence")] string Confidence,
    [property: JsonPropertyName("note")] string Note,
    [property: JsonPropertyName("startingReliability")] double? StartingReliability = null);

/// <summary>
/// Loads <c>data/authored/teams/car_strength_estimates.json</c>: an authored ESTIMATE of a constructor's car strength (0..100) in a
/// season, the stand-in for the ratings car effect while the Jolpica results stay local (PP-041). Strict like the other authored files.
/// </summary>
public static class CarStrengthLoader
{
    public const string RelativePath = "authored/teams/car_strength_estimates.json";

    /// <param name="dataRoot">The <c>data</c> directory.</param>
    public static CarStrengthFile Load(string dataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        var path = Path.Combine(Path.GetFullPath(dataRoot), RelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            throw new AuthoredDataLoadException("Missing file: " + path);
        }

        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<CarStrengthFile>(stream, AuthoredJson.Options)
                ?? throw new AuthoredDataLoadException(path + ": JSON value is null.");
        }
        catch (JsonException exception)
        {
            throw new AuthoredDataLoadException(path + ": " + exception.Message);
        }
    }

    /// <summary>The strength source the world initializer reads.</summary>
    public static ICarStrengthSource ToSource(CarStrengthFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return new MapCarStrengthSource([.. file.Strengths.Select(entry => (entry.ConstructorId, entry.Season, entry.Strength, entry.StartingReliability))]);
    }
}

/// <summary>Checks the strength file against the constructors the authored teams know. Developer-facing codes, not player text.</summary>
public static class CarStrengthValidator
{
    public const string NotEstimate = "car-strength-not-estimate";
    public const string UnknownConstructor = "car-strength-unknown-constructor";
    public const string Duplicate = "car-strength-duplicate";
    public const string BadNumber = "car-strength-number";

    public static IReadOnlyList<AuthoredDataError> Validate(CarStrengthFile file, IReadOnlySet<string> constructorIds)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(constructorIds);
        var errors = new List<AuthoredDataError>();
        if (!file.Notes.Contains("ESTIMATE", StringComparison.Ordinal))
        {
            errors.Add(new AuthoredDataError(NotEstimate, "the file notes must say that the strengths are an ESTIMATE"));
        }

        var seen = new HashSet<(int Season, string Id)>();
        foreach (var entry in file.Strengths)
        {
            var label = entry.ConstructorId + " " + entry.Season.ToString(CultureInfo.InvariantCulture);
            if (!string.Equals(entry.Confidence, "estimate", StringComparison.Ordinal))
            {
                errors.Add(new AuthoredDataError(NotEstimate, label + " must have confidence 'estimate'"));
            }

            if (!constructorIds.Contains(entry.ConstructorId))
            {
                errors.Add(new AuthoredDataError(UnknownConstructor, label + " is not a constructor in engines.json"));
            }

            if (entry.Season < 1950 || entry.Strength is < 0d or > 100d || entry.StartingReliability is < 0d or > 100d)
            {
                errors.Add(new AuthoredDataError(BadNumber, label + " needs a season from 1950 and a strength and starting reliability from 0 to 100"));
            }

            if (!seen.Add((entry.Season, entry.ConstructorId)))
            {
                errors.Add(new AuthoredDataError(Duplicate, label + " is listed twice"));
            }
        }

        return errors;
    }
}
