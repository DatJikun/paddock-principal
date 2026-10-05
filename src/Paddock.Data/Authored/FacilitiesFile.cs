using System.Text.Json.Serialization;
using Paddock.Domain.Infrastructure;

namespace Paddock.Data.Authored;

public sealed record FacilitiesFile(
    [property: JsonPropertyName("notes")] string Notes,
    [property: JsonPropertyName("kinds")] IReadOnlyList<FacilityKindEntry> Kinds,
    [property: JsonPropertyName("defaultQualityMilli")] IReadOnlyDictionary<string, int> DefaultQualityMilli,
    [property: JsonPropertyName("teams")] IReadOnlyList<FacilityTeamEntry> Teams);

public sealed record FacilityKindEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("unlockYear")] int UnlockYear);

public sealed record FacilityTeamEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("factory")] int? Factory = null,
    [property: JsonPropertyName("windTunnel")] int? WindTunnel = null,
    [property: JsonPropertyName("cfd")] int? Cfd = null,
    [property: JsonPropertyName("simulator")] int? Simulator = null);

/// <summary>
/// Loads <c>data/authored/infrastructure/facilities.json</c>. Every starting level is an ESTIMATE (PP-026). Unknown
/// properties are errors. A missing file is an empty catalog so authored fixtures that do not copy this file still load.
/// </summary>
public static class FacilitiesLoader
{
    public const string RelativePath = "authored/infrastructure/facilities.json";

    public static FacilityCatalog ToCatalog(FacilitiesFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var kinds = file.Kinds.Select(entry =>
        {
            if (!FacilityKindIds.TryParse(entry.Id, out var kind))
            {
                throw new AuthoredDataLoadException("Unknown facility kind '" + entry.Id + "'.");
            }

            return new FacilityKindSpec(kind, entry.UnlockYear);
        }).ToArray();

        var defaults = new Dictionary<FacilityKind, int>();
        foreach (var (id, milli) in file.DefaultQualityMilli)
        {
            if (!FacilityKindIds.TryParse(id, out var kind))
            {
                throw new AuthoredDataLoadException("Unknown facility kind '" + id + "' in defaultQualityMilli.");
            }

            defaults[kind] = milli;
        }

        var teams = new Dictionary<string, IReadOnlyDictionary<FacilityKind, int>>(StringComparer.Ordinal);
        foreach (var team in file.Teams)
        {
            var map = new Dictionary<FacilityKind, int>();
            Add(map, FacilityKind.Factory, team.Factory);
            Add(map, FacilityKind.WindTunnel, team.WindTunnel);
            Add(map, FacilityKind.Cfd, team.Cfd);
            Add(map, FacilityKind.Simulator, team.Simulator);
            teams[team.Id] = map;
        }

        return new FacilityCatalog(kinds, defaults, teams);
    }

    private static void Add(Dictionary<FacilityKind, int> map, FacilityKind kind, int? milli)
    {
        if (milli is { } value)
        {
            map[kind] = value;
        }
    }
}
