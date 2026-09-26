namespace Paddock.Tests.Authored;

internal sealed class TempAuthoredData : IDisposable
{
    public TempAuthoredData(
        string? catalog = null,
        string? timeline = null,
        string? ideas = null,
        string? circuits = null,
        string? raceMap = null,
        string? technologies = null,
        string? engines = null,
        string? lineage = null,
        string? founders = null,
        string? staff = null)
    {
        Root = Path.Combine(Path.GetTempPath(), "paddock-authored-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(Root, "authored", "regulations"));
        Directory.CreateDirectory(Path.Combine(Root, "authored", "tracks"));
        Directory.CreateDirectory(Path.Combine(Root, "authored", "tech"));
        Directory.CreateDirectory(Path.Combine(Root, "authored", "teams"));
        Directory.CreateDirectory(Path.Combine(Root, "authored", "people"));
        Write("authored/regulations/catalog.json", catalog ?? AuthoredSamples.Catalog);
        Write("authored/regulations/f1_timeline.json", timeline ?? AuthoredSamples.Timeline);
        Write("authored/regulations/other_series_ideas.json", ideas ?? AuthoredSamples.Ideas);
        Write("authored/tracks/circuits.json", circuits ?? AuthoredSamples.Circuits);
        Write("authored/tracks/race_layout_map.json", raceMap ?? AuthoredSamples.RaceMap);
        Write("authored/tech/technologies.json", technologies ?? AuthoredWorldSamples.Technologies);
        Write("authored/teams/engines.json", engines ?? AuthoredWorldSamples.Engines);
        Write("authored/teams/lineage.json", lineage ?? AuthoredWorldSamples.Lineage);
        Write("authored/teams/founders.json", founders ?? AuthoredWorldSamples.Founders);
        Write("authored/people/staff.json", staff ?? AuthoredWorldSamples.Staff);
    }

    public string Root { get; }

    public void Dispose() => Directory.Delete(Root, recursive: true);

    private void Write(string relativePath, string json)
    {
        var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.WriteAllText(path, json);
    }
}
