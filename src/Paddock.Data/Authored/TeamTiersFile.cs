using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Paddock.Domain.Finance;

namespace Paddock.Data.Authored;

public sealed record TeamTiersFile(
    [property: JsonPropertyName("notes")] string Notes,
    [property: JsonPropertyName("standings")] IReadOnlyList<TeamTierEntry> Standings);

public sealed record TeamTierEntry(
    [property: JsonPropertyName("season")] int Season,
    [property: JsonPropertyName("constructorId")] string ConstructorId,
    [property: JsonPropertyName("position")] int Position,
    [property: JsonPropertyName("confidence")] string Confidence,
    [property: JsonPropertyName("note")] string Note);

/// <summary>
/// Loads <c>data/authored/commercial/team_tiers_estimates.json</c>: an authored ESTIMATE of the previous season's constructors'
/// order, the stand-in for the Jolpica standings that stay local (PP-041). Strict like the other authored files.
/// </summary>
public static class TeamTiersLoader
{
    public const string RelativePath = "authored/commercial/team_tiers_estimates.json";

    /// <param name="dataRoot">The <c>data</c> directory.</param>
    public static TeamTiersFile Load(string dataRoot)
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
            return JsonSerializer.Deserialize<TeamTiersFile>(stream, AuthoredJson.Options)
                ?? throw new AuthoredDataLoadException(path + ": JSON value is null.");
        }
        catch (JsonException exception)
        {
            throw new AuthoredDataLoadException(path + ": " + exception.Message);
        }
    }

    /// <summary>The tier source for a career: the previous season's place decides (<see cref="PreviousSeasonStandingTier"/>).</summary>
    public static ITeamTierSource ToSource(TeamTiersFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return new PreviousSeasonStandingTier(file.Standings.Select(entry =>
            new ConstructorStandingFact(entry.Season, entry.ConstructorId, entry.Position)));
    }
}

/// <summary>Checks the tiers file against the constructors the authored teams know. Developer-facing codes, not player text.</summary>
public static class TeamTiersValidator
{
    public const string NotEstimate = "team-tier-not-estimate";
    public const string UnknownConstructor = "team-tier-unknown-constructor";
    public const string Duplicate = "team-tier-duplicate";
    public const string BadNumber = "team-tier-number";

    public static IReadOnlyList<AuthoredDataError> Validate(TeamTiersFile file, IReadOnlySet<string> constructorIds)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(constructorIds);
        var errors = new List<AuthoredDataError>();
        if (!file.Notes.Contains("ESTIMATE", StringComparison.Ordinal))
        {
            errors.Add(new AuthoredDataError(NotEstimate, "the file notes must say that the standings are an ESTIMATE"));
        }

        var constructors = new HashSet<(int Season, string Id)>();
        var positions = new HashSet<(int Season, int Position)>();
        foreach (var entry in file.Standings)
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

            if (entry.Season < 1950 || entry.Position < 1)
            {
                errors.Add(new AuthoredDataError(BadNumber, label + " needs a season from 1950 and a position from 1"));
            }

            if (!constructors.Add((entry.Season, entry.ConstructorId)) || !positions.Add((entry.Season, entry.Position)))
            {
                errors.Add(new AuthoredDataError(Duplicate, label + " repeats a constructor or a position within its season"));
            }
        }

        return errors;
    }
}
