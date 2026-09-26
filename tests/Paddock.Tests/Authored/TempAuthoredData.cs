namespace Paddock.Tests.Authored;

internal sealed class TempAuthoredData : IDisposable
{
    public TempAuthoredData(
        string? catalog = null,
        string? timeline = null,
        string? ideas = null,
        string? circuits = null,
        string? raceMap = null)
    {
        Root = Path.Combine(Path.GetTempPath(), "paddock-authored-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(Root, "authored", "regulations"));
        Directory.CreateDirectory(Path.Combine(Root, "authored", "tracks"));
        Write("authored/regulations/catalog.json", catalog ?? AuthoredSamples.Catalog);
        Write("authored/regulations/f1_timeline.json", timeline ?? AuthoredSamples.Timeline);
        Write("authored/regulations/other_series_ideas.json", ideas ?? AuthoredSamples.Ideas);
        Write("authored/tracks/circuits.json", circuits ?? AuthoredSamples.Circuits);
        Write("authored/tracks/race_layout_map.json", raceMap ?? AuthoredSamples.RaceMap);
    }

    public string Root { get; }

    public void Dispose() => Directory.Delete(Root, recursive: true);

    private void Write(string relativePath, string json)
    {
        var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.WriteAllText(path, json);
    }
}
