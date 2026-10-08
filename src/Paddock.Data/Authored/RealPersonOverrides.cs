using System.Text.Json;
using System.Text.Json.Serialization;

namespace Paddock.Data.Authored;

/// <summary>One authored correction of a real driver: the full name the person is known by, and the gender.</summary>
public sealed record RealPersonOverride(
    [property: JsonPropertyName("driver_id")] string DriverId,
    [property: JsonPropertyName("given_name")] string? GivenName = null,
    [property: JsonPropertyName("family_name")] string? FamilyName = null,
    [property: JsonPropertyName("female")] bool Female = false);

/// <summary>The file <c>data/authored/people/real_person_overrides.json</c> (#265).</summary>
public sealed record RealPersonOverridesFile(
    [property: JsonPropertyName("notes")] string Notes,
    [property: JsonPropertyName("overrides")] IReadOnlyList<RealPersonOverride> Overrides);

/// <summary>
/// Loads the authored overrides that sit on top of the local Jolpica driver table: a full name where Jolpica has a shorter one
/// ("Juan Manuel Fangio" for "Juan Fangio") and the few women of the data. The file is optional: a data folder without it
/// changes nothing. Strict like the other authored files.
/// </summary>
public static class RealPersonOverridesLoader
{
    public const string RelativePath = "authored/people/real_person_overrides.json";

    /// <param name="dataRoot">The <c>data</c> directory.</param>
    public static IReadOnlyDictionary<string, RealPersonOverride> Load(string dataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        var path = Path.Combine(Path.GetFullPath(dataRoot), RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var map = new Dictionary<string, RealPersonOverride>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return map;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var file = JsonSerializer.Deserialize<RealPersonOverridesFile>(stream, AuthoredJson.Options)
                ?? throw new AuthoredDataLoadException(path + ": JSON value is null.");
            foreach (var entry in file.Overrides)
            {
                if (string.IsNullOrWhiteSpace(entry.DriverId) || !map.TryAdd(entry.DriverId, entry))
                {
                    throw new AuthoredDataLoadException(path + ": the driver id '" + entry.DriverId + "' is empty or listed twice.");
                }
            }

            return map;
        }
        catch (JsonException exception)
        {
            throw new AuthoredDataLoadException(path + ": " + exception.Message);
        }
    }
}
