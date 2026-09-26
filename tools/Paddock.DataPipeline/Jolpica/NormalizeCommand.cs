using System.Text.Json;
using Paddock.Data.Historical;

namespace Paddock.DataPipeline;

public static class NormalizeCommand
{
    public static int Execute(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (!CommandArgs.TryParse(args, start: 1, ["--cache"], stderr, out var options, out _))
        {
            return 1;
        }

        var cacheRoot = options.TryGetValue("--cache", out var cache) ? cache : JolpicaCache.DefaultRoot();
        var rawRoot = JolpicaCache.Raw(cacheRoot);
        if (!Directory.Exists(rawRoot))
        {
            stderr.WriteLine($"Raw cache not found: {rawRoot}");
            return 1;
        }

        var data = JolpicaNormalizer.ReadRaw(rawRoot);
        var directory = JolpicaCache.Normalized(cacheRoot);
        Directory.CreateDirectory(directory);
        Write(directory, "drivers.json", data.Drivers);
        Write(directory, "constructors.json", data.Constructors);
        Write(directory, "circuits.json", data.Circuits);
        Write(directory, "races.json", data.Races);
        Write(directory, "results.json", data.Results);
        Write(directory, "qualifying.json", data.Qualifying);

        stdout.WriteLine($"drivers: {data.Drivers.Drivers.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        stdout.WriteLine($"constructors: {data.Constructors.Constructors.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        stdout.WriteLine($"circuits: {data.Circuits.Circuits.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        stdout.WriteLine($"races: {data.Races.Races.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        stdout.WriteLine($"results: {data.Results.Results.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        stdout.WriteLine($"qualifying: {data.Qualifying.Qualifying.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        return 0;
    }

    private static void Write<T>(string directory, string fileName, T document)
    {
        var json = JsonSerializer.Serialize(document, HistoricalJson.Options);
        if (!json.EndsWith('\n'))
        {
            json += "\n";
        }

        JolpicaCache.WriteAtomic(Path.Combine(directory, fileName), json);
    }
}
