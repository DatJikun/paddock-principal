using System.Text.Json;

namespace Paddock.Data.Authored;

/// <summary>
/// Loads <c>data/authored/regulations</c> and <c>data/authored/tracks</c>.
/// Unknown JSON properties are errors. Every file is attempted so load failures are reported together.
/// </summary>
public static class AuthoredDataLoader
{
    /// <param name="dataRoot">The <c>data</c> directory, the parent of <c>authored</c>.</param>
    public static AuthoredData Load(string dataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        var root = Path.GetFullPath(dataRoot);
        var failures = new List<string>();

        var catalog = TryRead<List<CatalogDimension>>(
            Path.Combine(root, "authored", "regulations", "catalog.json"),
            failures);
        var timeline = TryRead<List<TimelinePeriod>>(
            Path.Combine(root, "authored", "regulations", "f1_timeline.json"),
            failures);
        var ideas = TryRead<List<OtherSeriesIdea>>(
            Path.Combine(root, "authored", "regulations", "other_series_ideas.json"),
            failures);
        var circuits = TryRead<CircuitsFile>(
            Path.Combine(root, "authored", "tracks", "circuits.json"),
            failures);
        var raceMap = TryRead<List<RaceLayoutEntry>>(
            Path.Combine(root, "authored", "tracks", "race_layout_map.json"),
            failures);

        if (failures.Count > 0 || catalog is null || timeline is null || ideas is null || circuits is null || raceMap is null)
        {
            if (failures.Count == 0)
            {
                failures.Add("Authored data failed to load.");
            }

            throw new AuthoredDataLoadException(string.Join(Environment.NewLine, failures));
        }

        return new AuthoredData(catalog, timeline, ideas, circuits, raceMap);
    }

    private static T? TryRead<T>(string path, List<string> failures)
        where T : class
    {
        if (!File.Exists(path))
        {
            failures.Add($"Missing file: {path}");
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var value = JsonSerializer.Deserialize<T>(stream, AuthoredJson.Options);
            if (value is null)
            {
                failures.Add($"{path}: JSON value is null.");
                return null;
            }

            return value;
        }
        catch (JsonException ex)
        {
            failures.Add($"{path}: {ex.Message}");
            return null;
        }
    }
}
