using System.Globalization;
using System.Text.Json;
using Paddock.Data.Historical;

namespace Paddock.DataPipeline;

public static class ScheduleCommand
{
    public static int Execute(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (!CommandArgs.TryParse(args, start: 1, ["--cache", "--pool-lead-years"], stderr, out var options, out _))
        {
            return 1;
        }

        if (!TryLead(options, stderr, out var lead))
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

        var report = PeopleScheduleBuilder.Build(JolpicaNormalizer.ReadRaw(rawRoot), lead);
        var directory = StatsCommand.ReportsDirectory(cacheRoot);
        var markdownPath = Path.Combine(directory, "people_schedule.md");
        var jsonPath = Path.Combine(directory, "people_schedule.json");
        var json = JsonSerializer.Serialize(report, HistoricalJson.Options);
        if (!json.EndsWith('\n'))
        {
            json += "\n";
        }

        var markdown = PeopleScheduleText.Markdown(report);
        if (!markdown.EndsWith('\n'))
        {
            markdown += "\n";
        }

        JolpicaCache.WriteAtomic(markdownPath, markdown);
        JolpicaCache.WriteAtomic(jsonPath, json);

        foreach (var line in PeopleScheduleText.SummaryLines(report))
        {
            stdout.WriteLine(line);
        }

        stdout.WriteLine("wrote " + markdownPath);
        stdout.WriteLine("wrote " + jsonPath);
        return 0;
    }

    private static bool TryLead(Dictionary<string, string> options, TextWriter stderr, out decimal lead)
    {
        lead = PeopleScheduleRules.DefaultPoolLeadYears;
        if (!options.TryGetValue("--pool-lead-years", out var text))
        {
            return true;
        }

        if (!decimal.TryParse(
                text,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out lead)
            || !PeopleScheduleRules.IsValidLead(lead))
        {
            stderr.WriteLine(
                "Invalid --pool-lead-years value: "
                + text
                + ". Use a non-negative whole or half year, for example 2.5.");
            return false;
        }

        return true;
    }
}
