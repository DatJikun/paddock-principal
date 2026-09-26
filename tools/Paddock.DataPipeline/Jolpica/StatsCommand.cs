using System.Globalization;
using System.Text.Json;
using Paddock.Data.Historical;

namespace Paddock.DataPipeline;

public static class StatsCommand
{
    public const int DefaultFrom = 1950;
    public const int DefaultTo = 2025;

    public static int Execute(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (!CommandArgs.TryParse(args, start: 1, ["--cache", "--from", "--to"], stderr, out var options, out _))
        {
            return 1;
        }

        var from = DefaultFrom;
        var to = DefaultTo;
        if (options.ContainsKey("--from") && !TryYear(options, "--from", stderr, out from))
        {
            return 1;
        }

        if (options.ContainsKey("--to") && !TryYear(options, "--to", stderr, out to))
        {
            return 1;
        }

        if (from > to)
        {
            stderr.WriteLine("--from must not be greater than --to.");
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
        var present = data.Races.Races.Select(race => race.Season).ToHashSet();
        var missing = new List<int>();
        for (var year = from; year <= to; year++)
        {
            if (!present.Contains(year))
            {
                missing.Add(year);
            }
        }

        if (missing.Count > 0)
        {
            stderr.WriteLine("Missing seasons: " + string.Join(", ", missing.Select(year => year.ToString(CultureInfo.InvariantCulture))));
            return 1;
        }

        var report = EraStats.Build(data, from, to);
        var directory = ReportsDirectory(cacheRoot);
        var markdownPath = Path.Combine(directory, "era_stats.md");
        var jsonPath = Path.Combine(directory, "era_stats.json");
        var json = JsonSerializer.Serialize(report, HistoricalJson.Options);
        if (!json.EndsWith('\n'))
        {
            json += "\n";
        }

        var markdown = EraStatsText.Markdown(report);
        if (!markdown.EndsWith('\n'))
        {
            markdown += "\n";
        }

        JolpicaCache.WriteAtomic(markdownPath, markdown);
        JolpicaCache.WriteAtomic(jsonPath, json);

        foreach (var line in EraStatsText.SummaryLines(report))
        {
            stdout.WriteLine(line);
        }

        stdout.WriteLine("wrote " + markdownPath);
        stdout.WriteLine("wrote " + jsonPath);
        return 0;
    }

    public static string ReportsDirectory(string cacheRoot)
    {
        var parent = Directory.GetParent(Path.GetFullPath(cacheRoot))
            ?? throw new InvalidOperationException($"Cache path has no parent: {cacheRoot}");
        return Path.Combine(parent.FullName, "reports");
    }

    private static bool TryYear(Dictionary<string, string> options, string flag, TextWriter stderr, out int year)
    {
        if (!int.TryParse(options[flag], NumberStyles.None, CultureInfo.InvariantCulture, out year))
        {
            stderr.WriteLine($"Invalid {flag} value: {options[flag]}");
            return false;
        }

        return true;
    }
}
