using System.Globalization;
using Paddock.Data.Authored;

namespace Paddock.DataPipeline;

public static class ValidateAuthoredCommand
{
    public static int Execute(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (args.Length == 0 || !string.Equals(args[0], "validate-authored", StringComparison.Ordinal))
        {
            stderr.WriteLine("Unknown command. Expected: validate-authored [--data-root <path>]");
            return 1;
        }

        string? dataRoot = null;
        for (var i = 1; i < args.Length; i++)
        {
            var flag = args[i];
            if (!string.Equals(flag, "--data-root", StringComparison.Ordinal))
            {
                stderr.WriteLine($"Unknown argument: {flag}");
                return 1;
            }

            if (dataRoot is not null)
            {
                stderr.WriteLine("Duplicate --data-root.");
                return 1;
            }

            if (i + 1 >= args.Length)
            {
                stderr.WriteLine("Missing value for --data-root.");
                return 1;
            }

            dataRoot = args[++i];
            if (string.IsNullOrWhiteSpace(dataRoot))
            {
                stderr.WriteLine("Missing value for --data-root.");
                return 1;
            }
        }

        try
        {
            var root = dataRoot ?? FindDataRoot();
            var data = AuthoredDataLoader.Load(root);
            var errors = AuthoredDataValidator.Validate(data);
            WriteReport(stdout, data, errors);
            return errors.Count == 0 ? 0 : 1;
        }
        catch (AuthoredDataLoadException ex)
        {
            stdout.WriteLine("authored data: load failed");
            stdout.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string FindDataRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var asDataDirectory = Path.Combine(dir.FullName, "authored", "regulations", "catalog.json");
            if (File.Exists(asDataDirectory))
            {
                return dir.FullName;
            }

            var asRepoRoot = Path.Combine(dir.FullName, "data", "authored", "regulations", "catalog.json");
            if (File.Exists(asRepoRoot))
            {
                return Path.Combine(dir.FullName, "data");
            }

            dir = dir.Parent;
        }

        throw new AuthoredDataLoadException(
            "Could not find data/authored. Pass --data-root pointing at the data directory.");
    }

    private static void WriteReport(TextWriter stdout, AuthoredData data, IReadOnlyList<AuthoredDataError> errors)
    {
        var layoutCount = 0;
        foreach (var circuit in data.Circuits.Circuits)
        {
            layoutCount += circuit.Layouts.Count;
        }

        stdout.WriteLine(errors.Count == 0
            ? "authored data: ok"
            : "authored data: " + errors.Count.ToString(CultureInfo.InvariantCulture) + (errors.Count == 1 ? " error" : " errors"));
        stdout.WriteLine("catalog dimensions: " + data.Catalog.Count.ToString(CultureInfo.InvariantCulture));
        stdout.WriteLine("timeline periods: " + data.Timeline.Count.ToString(CultureInfo.InvariantCulture));
        stdout.WriteLine("other series ideas: " + data.OtherSeriesIdeas.Count.ToString(CultureInfo.InvariantCulture));
        stdout.WriteLine("circuits: " + data.Circuits.Circuits.Count.ToString(CultureInfo.InvariantCulture));
        stdout.WriteLine("layouts: " + layoutCount.ToString(CultureInfo.InvariantCulture));
        stdout.WriteLine("race map entries: " + data.RaceLayoutMap.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var error in errors)
        {
            stdout.WriteLine("error: " + error.Message);
        }
    }
}
