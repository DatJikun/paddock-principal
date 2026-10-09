using System.Globalization;
using System.Text.Json;
using Paddock.Career;
using Paddock.Data.Authored;
using Paddock.Data.Historical;
using Paddock.Data.World;
using Paddock.Domain.Career;
using Paddock.Domain.World;

namespace Paddock.SimRunner;

/// <summary>
/// <c>init-world --preset &lt;name&gt; --year &lt;Y&gt; --seed &lt;N&gt; [--lang en|pl] [--data-root &lt;dir&gt;]
/// [--schedule &lt;people_schedule.json&gt; --drivers &lt;drivers.json&gt; [--ratings &lt;ratings.json&gt;]]</c>:
/// builds the starting world and prints the report (counts per kind, gaps, state hash). Read-only; it writes nothing.
/// Real drivers come from the local Jolpica cache outputs when they exist; without them the provider is empty.
/// </summary>
public static class InitWorldCommand
{
    public const string Name = "init-world";

    private const int SubjectsShown = 12;
    private const int TopDriversShown = 10;

    public static int Execute(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (args.Length == 0 || !string.Equals(args[0], Name, StringComparison.Ordinal))
        {
            stderr.WriteLine("Unknown command. Expected: init-world --preset <name> --year <Y> --seed <N>");
            return 1;
        }

        string? preset = null;
        int? year = null;
        ulong? seed = null;
        string? language = null;
        string? dataRoot = null;
        string? schedulePath = null;
        string? driversPath = null;
        string? ratingsPath = null;
        for (var i = 1; i < args.Length; i++)
        {
            var flag = args[i];
            if (flag is not ("--preset" or "--year" or "--seed" or "--lang" or "--data-root" or "--schedule" or "--drivers" or "--ratings"))
            {
                stderr.WriteLine("Unknown argument: " + flag);
                return 1;
            }

            if (i + 1 >= args.Length)
            {
                stderr.WriteLine("Missing value for " + flag + ".");
                return 1;
            }

            var value = args[++i];
            var duplicate = flag switch
            {
                "--preset" => Take(ref preset, value),
                "--lang" => Take(ref language, value),
                "--data-root" => Take(ref dataRoot, value),
                "--schedule" => Take(ref schedulePath, value),
                "--drivers" => Take(ref driversPath, value),
                "--ratings" => Take(ref ratingsPath, value),
                "--year" => TakeYear(ref year, value, stderr),
                _ => TakeSeed(ref seed, value, stderr),
            };
            if (duplicate)
            {
                stderr.WriteLine("Duplicate or invalid option: " + flag);
                return 1;
            }
        }

        if (preset is null || year is null || seed is null)
        {
            stderr.WriteLine("Missing required option. Expected --preset, --year and --seed.");
            return 1;
        }

        if (language is not null and not ("en" or "pl"))
        {
            stderr.WriteLine("--lang must be en or pl.");
            return 1;
        }

        if ((schedulePath is null) != (driversPath is null))
        {
            stderr.WriteLine("--schedule and --drivers go together.");
            return 1;
        }

        if (ratingsPath is not null && schedulePath is null)
        {
            stderr.WriteLine("--ratings goes with --schedule and --drivers.");
            return 1;
        }

        if (!TryPreset(preset, out var selected))
        {
            stderr.WriteLine("Invalid --preset value: " + preset);
            return 1;
        }

        var strings = StringTable.Load(language ?? "en");
        var config = CareerConfig.FromPreset(selected).WithStartYear(year.Value);
        try
        {
            var root = dataRoot ?? FindDataRoot();
            if (root is null)
            {
                stderr.WriteLine("Could not find data/authored. Pass --data-root pointing at the data directory.");
                return 1;
            }

            var data = AuthoredDataLoader.Load(root);
            var provider = LoadProvider(root, schedulePath, driversPath, ratingsPath);
            var starting = CareerData.LoadStartingSources(root, data, config.StartYear);
            var result = WorldInitializer.Create(config, data, provider, seed.Value, new WorldInitOptions(CarStrength: starting.CarStrength, Tiers: starting.Tiers));
            Print(result, ReferenceEquals(provider, EmptyPeopleProvider.Instance), strings, stdout);
            return 0;
        }
        catch (WorldInitException ex)
        {
            stderr.WriteLine(Format(strings.Required(ex.Code), ex.Arguments, config));
            return 1;
        }
        catch (Exception ex) when (ex is IOException or JsonException or AuthoredDataLoadException)
        {
            stderr.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void Print(WorldInitResult result, bool noProvider, IReadOnlyDictionary<string, string> strings, TextWriter stdout)
    {
        var report = result.Report;
        stdout.WriteLine(strings.Required("world.init.report.title")
            .Replace("{year}", Number(report.StartYear), StringComparison.Ordinal)
            .Replace("{reference}", Number(report.ReferenceSeason), StringComparison.Ordinal)
            .Replace("{source}", report.PeopleSource.ToString(), StringComparison.Ordinal));
        if (noProvider)
        {
            stdout.WriteLine(strings.Required("world.init.provider.none"));
        }

        var counts = report.Counts;
        foreach (var (key, value) in new (string, int)[]
        {
            ("world.init.count.teams", counts.Teams),
            ("world.init.count.dissolved_teams", counts.DissolvedTeams),
            ("world.init.count.engine_suppliers", counts.EngineSuppliers),
            ("world.init.count.lineage_links", counts.LineageLinks),
            ("world.init.count.racing_drivers", counts.RacingDrivers),
            ("world.init.count.pool_drivers", counts.PoolDrivers),
            ("world.init.count.staff", counts.StaffPeople),
            ("world.init.count.contracts", counts.Contracts),
            ("world.init.count.real_persons", counts.RealPersons),
            ("world.init.count.generated_persons", counts.GeneratedPersons),
        })
        {
            stdout.WriteLine(strings.Required(key) + ": " + Number(value));
        }

        if (report.Gaps.Count == 0)
        {
            stdout.WriteLine(strings.Required("world.init.gaps.none"));
        }
        else
        {
            stdout.WriteLine(strings.Required("world.init.gaps.header"));
            foreach (var gap in report.Gaps)
            {
                var shown = string.Join(", ", gap.Subjects.Take(SubjectsShown));
                var more = gap.Subjects.Count > SubjectsShown ? ", ..." : string.Empty;
                stdout.WriteLine("  " + strings.Required(gap.Code) + ": " + Number(gap.Subjects.Count) + " [" + shown + more + "]");
            }
        }

        var top = result.World.Persons
            .Where(person => person.IsReal && person.Roles.Contains(PersonRole.Driver))
            .Select(person => (Person: person, Mean: Mean(person.Truth)))
            .OrderByDescending(entry => entry.Mean)
            .ThenBy(entry => entry.Person.Id.Value, StringComparer.Ordinal)
            .Take(TopDriversShown)
            .ToArray();
        if (top.Length > 0)
        {
            stdout.WriteLine(strings.Required("world.init.top_drivers") + ":");
            foreach (var (person, mean) in top)
            {
                stdout.WriteLine("  " + person.GivenName + " " + person.FamilyName + ": " + mean.ToString("0.0", CultureInfo.InvariantCulture));
            }
        }

        stdout.WriteLine(strings.Required("world.init.report.hash").Replace("{hash}", result.World.StateHash(), StringComparison.Ordinal));
    }

    private static IPeopleProvider LoadProvider(
        string dataRoot,
        string? schedulePath,
        string? driversPath,
        string? ratingsPath)
    {
        if (schedulePath is null || driversPath is null)
        {
            var cache = Path.Combine(Path.GetFullPath(dataRoot), "cache");
            schedulePath = Path.Combine(cache, "reports", "people_schedule.json");
            driversPath = Path.Combine(cache, "jolpica", "normalized", "drivers.json");
            if (!File.Exists(schedulePath) || !File.Exists(driversPath))
            {
                return EmptyPeopleProvider.Instance;
            }
        }

        var schedule = JsonSerializer.Deserialize<PeopleScheduleReport>(File.ReadAllText(schedulePath), HistoricalJson.Options)
            ?? throw new JsonException("The people schedule file is empty.");
        var drivers = JsonSerializer.Deserialize<HistoricalDriversDocument>(File.ReadAllText(driversPath), HistoricalJson.Options)
            ?? throw new JsonException("The drivers file is empty.");
        ratingsPath ??= CareerData.RatingsPathFor(schedulePath);
        var ratings = ratingsPath is null ? null : FittedDriverRatings.Load(ratingsPath);
        return new ScheduleBackedPeopleProvider(
            schedule,
            drivers.Drivers,
            ratings is null ? null : ratings.RatingFor,
            CareerData.ConstructorIdsFor(driversPath),
            RealPersonOverridesLoader.Load(dataRoot));
    }

    private static double Mean(PersonTruth truth) => truth.Attributes.Average(attribute => attribute.Value);

    private static string Format(string template, IReadOnlyList<string> arguments, CareerConfig config)
    {
        return template
            .Replace("{codes}", string.Join(", ", arguments), StringComparison.Ordinal)
            .Replace("{team}", arguments.Count > 0 ? arguments[0] : config.PlayerTeam, StringComparison.Ordinal)
            .Replace("{year}", arguments.Count > 1 ? arguments[1] : Number(config.StartYear), StringComparison.Ordinal);
    }

    private static string? FindDataRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "data", "authored")))
                {
                    return Path.Combine(dir.FullName, "data");
                }

                dir = dir.Parent;
            }
        }

        return null;
    }

    private static bool Take(ref string? slot, string value)
    {
        if (slot is not null)
        {
            return true;
        }

        slot = value;
        return false;
    }

    private static bool TakeYear(ref int? slot, string value, TextWriter stderr)
    {
        if (slot is not null)
        {
            return true;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            stderr.WriteLine("Invalid --year value: " + value);
            return true;
        }

        slot = parsed;
        return false;
    }

    private static bool TakeSeed(ref ulong? slot, string value, TextWriter stderr)
    {
        if (slot is not null)
        {
            return true;
        }

        if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            stderr.WriteLine("Invalid --seed value: " + value);
            return true;
        }

        slot = parsed;
        return false;
    }

    private static bool TryPreset(string name, out CareerPreset preset)
    {
        foreach (var candidate in Enum.GetValues<CareerPreset>())
        {
            if (candidate != CareerPreset.Custom && string.Equals(candidate.ToString(), name, StringComparison.Ordinal))
            {
                preset = candidate;
                return true;
            }
        }

        preset = default;
        return false;
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
