using System.Globalization;
using System.Text;
using System.Text.Json;
using Paddock.Data.Authored;
using Paddock.Data.Historical;

namespace Paddock.DataPipeline;

/// <summary>
/// <c>starting-data [--cache &lt;dir&gt;] [--from &lt;year&gt;] [--to &lt;year&gt;]</c> (#271): fits the ratings car effect over the range and
/// writes, for every season, each constructor's starting car strength, the constructors' order and the engine to
/// <c>reports/starting_data.json</c> and <c>.md</c> next to the cache. Local only, never committed (PP-041).
/// </summary>
public static class StartingDataCommand
{
    public const string Name = "starting-data";

    public static int Execute(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (!CommandArgs.TryParse(args, start: 1, ["--cache", "--from", "--to"], stderr, out var options, out _))
        {
            return 1;
        }

        var from = RatingsCommand.DefaultFrom;
        var to = RatingsCommand.DefaultTo;
        if ((options.TryGetValue("--from", out var fromText) && !TryYear(fromText, "--from", stderr, out from))
            || (options.TryGetValue("--to", out var toText) && !TryYear(toText, "--to", stderr, out to)))
        {
            return 1;
        }

        if (from > to)
        {
            stderr.WriteLine("--from must not be greater than --to.");
            return 1;
        }

        var cacheRoot = options.TryGetValue("--cache", out var cache) ? cache : JolpicaCache.DefaultRoot();
        var normalized = JolpicaCache.Normalized(cacheRoot);
        var racesPath = Path.Combine(normalized, "races.json");
        var resultsPath = Path.Combine(normalized, "results.json");
        if (!File.Exists(racesPath) || !File.Exists(resultsPath))
        {
            stderr.WriteLine($"Normalized races and results not found in {normalized}. Run fetch and normalize first.");
            return 1;
        }

        var races = JsonSerializer.Deserialize<HistoricalRacesDocument>(File.ReadAllText(racesPath), HistoricalJson.Options)?.Races
            ?? throw new InvalidDataException(racesPath + " is empty.");
        var results = JsonSerializer.Deserialize<HistoricalResultsDocument>(File.ReadAllText(resultsPath), HistoricalJson.Options)?.Results
            ?? throw new InvalidDataException(resultsPath + " is empty.");

        var dataRoot = Path.Combine(JolpicaCache.FindRepoRoot(), "data");
        var lineage = ConstructorLineageMap.Parse(File.ReadAllText(Path.Combine(dataRoot, "authored", "teams", "lineage.json")));
        IReadOnlyList<EngineEntry> engines;
        try
        {
            engines = AuthoredDataLoader.Load(dataRoot).Engines.Entries;
        }
        catch (AuthoredDataLoadException exception)
        {
            stderr.WriteLine(exception.Message);
            return 1;
        }

        var run = RatingsCommand.RunModel(races, results, from, to, lineage: lineage);
        var report = StartingDataBuilder.Build(races, results, run.CarEffects, engines, lineage, from, to);

        var directory = StatsCommand.ReportsDirectory(cacheRoot);
        var jsonPath = Path.Combine(directory, StartingDataLoader.FileName);
        var markdownPath = Path.Combine(directory, "starting_data.md");
        var json = JsonSerializer.Serialize(report, HistoricalJson.Options);
        JolpicaCache.WriteAtomic(jsonPath, json.EndsWith('\n') ? json : json + "\n");
        JolpicaCache.WriteAtomic(markdownPath, Markdown(report));

        foreach (var line in SummaryLines(report))
        {
            stdout.WriteLine(line);
        }

        stdout.WriteLine("wrote " + markdownPath);
        stdout.WriteLine("wrote " + jsonPath);
        return 0;
    }

    public static IEnumerable<string> SummaryLines(StartingDataReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var constructors = report.Seasons.SelectMany(season => season.Constructors).ToList();
        yield return "starting data " + Number(report.FromYear) + "-" + Number(report.ToYear) + ": "
            + Number(report.Seasons.Count) + " seasons, " + Number(constructors.Count) + " constructor-seasons";
        foreach (var source in new[] { StartingStrengthSources.CarEffect, StartingStrengthSources.EarlierSeason, StartingStrengthSources.NewEntrant })
        {
            yield return "  " + source + ": " + Number(constructors.Count(entry => entry.StrengthSource == source));
        }

        yield return "  without an engine in engines.json: " + Number(constructors.Count(entry => entry.EngineSupplier is null));
    }

    /// <summary>A developer report, one table per season. Not player text.</summary>
    public static string Markdown(StartingDataReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var text = new StringBuilder();
        text.AppendLine("# Starting data (ESTIMATE)");
        text.AppendLine();
        text.AppendLine(report.Notes);
        text.AppendLine();
        foreach (var line in SummaryLines(report))
        {
            text.AppendLine(line.StartsWith("  ", StringComparison.Ordinal) ? "- " + line.Trim() : line);
        }

        foreach (var season in report.Seasons)
        {
            var places = season.Standings.ToDictionary(standing => standing.ConstructorId, standing => standing.Position, StringComparer.Ordinal);
            text.AppendLine();
            text.AppendLine("## " + Number(season.Season));
            text.AppendLine();
            text.AppendLine("New-entrant strength: " + Decimal(season.NewEntrantStrength) + ".");
            text.AppendLine();
            text.AppendLine("| Constructor | Strength | Source | Place | Engine |");
            text.AppendLine("|---|---:|---|---:|---|");
            foreach (var constructor in season.Constructors.OrderByDescending(entry => entry.Strength).ThenBy(entry => entry.ConstructorId, StringComparer.Ordinal))
            {
                var place = places.TryGetValue(constructor.ConstructorId, out var position) ? Number(position) : "-";
                var engine = constructor.EngineSupplier is null ? "-" : constructor.EngineSupplier + " (" + constructor.EngineName + ")";
                text.AppendLine("| " + constructor.ConstructorId + " | " + Decimal(constructor.Strength) + " | " + constructor.StrengthSource + " | " + place + " | " + engine + " |");
            }
        }

        return text.ToString();
    }

    private static bool TryYear(string text, string flag, TextWriter stderr, out int year)
    {
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out year))
        {
            return true;
        }

        stderr.WriteLine($"Invalid {flag} value: {text}");
        return false;
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Decimal(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);
}
