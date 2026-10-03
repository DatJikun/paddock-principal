using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Paddock.Application.Career;
using Paddock.Data.Authored;
using Paddock.Data.Historical;
using Paddock.Data.World;
using Paddock.Domain.Career;
using Paddock.Simulation.Career;

namespace Paddock.SimRunner;

/// <summary>
/// <c>run --preset &lt;name&gt; --from &lt;Y&gt; --to &lt;Y&gt; --seed &lt;N&gt; [--save path] [--lang en|pl]</c>:
/// builds the opening world, then lives every day through 31 December of <c>--to</c> with AI managers only.
/// Prints one line per season (alive, retired, pool, contracts, world state hash).
/// <c>--save</c> writes that world through the T19 repository.
/// The stored determinism hash is updated by editing the test constant after a reviewed change.
/// The test does not rewrite it.
/// </summary>
public static class RunCommand
{
    public const string Name = "run";

    public static int Execute(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (args.Length == 0 || !string.Equals(args[0], Name, StringComparison.Ordinal))
        {
            stderr.WriteLine("Unknown command. Expected: run --preset <name> --from <Y> --to <Y> --seed <N>");
            return 1;
        }

        string? preset = null;
        int? from = null;
        int? to = null;
        ulong? seed = null;
        string? language = null;
        string? dataRoot = null;
        string? schedulePath = null;
        string? driversPath = null;
        string? savePath = null;
        for (var i = 1; i < args.Length; i++)
        {
            var flag = args[i];
            if (flag is not ("--preset" or "--from" or "--to" or "--seed" or "--lang" or "--data-root" or "--schedule" or "--drivers" or "--save"))
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
                "--save" => Take(ref savePath, value),
                "--from" => TakeYear(ref from, value, stderr),
                "--to" => TakeYear(ref to, value, stderr),
                _ => TakeSeed(ref seed, value, stderr),
            };
            if (duplicate)
            {
                stderr.WriteLine("Duplicate or invalid option: " + flag);
                return 1;
            }
        }

        if (preset is null || from is null || to is null || seed is null)
        {
            stderr.WriteLine("Missing required option. Expected --preset, --from, --to and --seed.");
            return 1;
        }

        if (to.Value < from.Value)
        {
            stderr.WriteLine("--to must be the same year as --from, or a later year.");
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

        if (!TryPreset(preset, out var selected))
        {
            stderr.WriteLine("Invalid --preset value: " + preset);
            return 1;
        }

        var strings = StringTable.Load(language ?? "en");
        var config = CareerConfig.FromPreset(selected).WithStartYear(from.Value);
        try
        {
            var root = dataRoot ?? FindDataRoot();
            if (root is null)
            {
                stderr.WriteLine("Could not find data/authored. Pass --data-root pointing at the data directory.");
                return 1;
            }

            var data = AuthoredDataLoader.Load(root);
            var provider = LoadProvider(root, schedulePath, driversPath);
            var created = WorldInitializer.Create(config, data, provider, seed.Value);
            var arrivals = TalentIntakeSchedule.AfterStart(config, provider, created.World, seed.Value);
            var session = new CareerSession(
                created.World,
                seed.Value,
                created.TalentPool,
                arrivals,
                new CareerSessionOptions { LastSeasons = LastSeasons.From(provider) });
            var result = CareerHost.Run(session, to.Value);
            Print(result, strings, preset, config.PeopleSource.ToString(), from.Value, to.Value, seed.Value, stdout);
            if (savePath is not null)
            {
                var careerName = preset + " " + Number(from.Value) + "-" + Number(to.Value);
                CareerSaveWriter.Write(
                    savePath,
                    result.Session,
                    config,
                    created.PlayerOrganization.Value,
                    HashAuthored(root),
                    careerName);
                stdout.WriteLine(Fill(strings.Required(CareerRunText.Saved), ("path", savePath)));
            }

            return 0;
        }
        catch (WorldInitException ex)
        {
            stderr.WriteLine(FormatInit(strings.Required(ex.Code), ex.Arguments, config));
            return 1;
        }
        catch (Exception ex) when (ex is IOException or JsonException or AuthoredDataLoadException or ArgumentException or InvalidOperationException)
        {
            stderr.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void Print(
        CareerRunResult result,
        IReadOnlyDictionary<string, string> strings,
        string preset,
        string source,
        int from,
        int to,
        ulong seed,
        TextWriter stdout)
    {
        stdout.WriteLine(Fill(
            strings.Required(CareerRunText.Title),
            ("preset", preset),
            ("from", Number(from)),
            ("to", Number(to)),
            ("seed", seed.ToString(CultureInfo.InvariantCulture)),
            ("source", source)));
        stdout.WriteLine(Fill(
            strings.Required(CareerRunText.Estimates),
            ("driverFrom", Number(CareerDayEstimates.DriverRetirementFromAge)),
            ("driverCertain", Number(CareerDayEstimates.DriverRetirementCertainAge)),
            ("staffFrom", Number(CareerDayEstimates.StaffRetirementFromAge)),
            ("staffCertain", Number(CareerDayEstimates.StaffRetirementCertainAge)),
            ("entryDate", CareerDayEstimates.PoolEntryDateText()),
            ("intake", Number(CareerDayEstimates.GeneratedIntakePerSeason))));
        stdout.WriteLine(strings.Required(CareerRunText.HashNote));
        stdout.WriteLine(Fill(
            strings.Required(CareerRunText.Ai),
            ("ai", Number(result.AiManagers)),
            ("humans", Number(result.HumanManagers))));
        foreach (var year in result.Session.Years)
        {
            stdout.WriteLine(Fill(
                strings.Required(CareerRunText.Year),
                ("year", Number(year.Year)),
                ("alive", Number(year.Alive)),
                ("retired", Number(year.Retired)),
                ("pool", Number(year.Pool)),
                ("contracts", Number(year.Contracts)),
                ("hash", year.StateHash)));
        }
    }

    private static string HashAuthored(string dataRoot)
    {
        var authored = Path.Combine(Path.GetFullPath(dataRoot), "authored");
        var files = Directory.EnumerateFiles(authored, "*.json", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(authored, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var relative in files)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(relative));
            hash.AppendData("\n"u8);
            hash.AppendData(File.ReadAllBytes(Path.Combine(authored, relative.Replace('/', Path.DirectorySeparatorChar))));
            hash.AppendData("\n"u8);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static IPeopleProvider LoadProvider(string dataRoot, string? schedulePath, string? driversPath)
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
        return new ScheduleBackedPeopleProvider(schedule, drivers.Drivers);
    }

    private static string FormatInit(string template, IReadOnlyList<string> arguments, CareerConfig config)
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
            stderr.WriteLine("Invalid year value: " + value);
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

    private static string Fill(string template, params (string Name, string Value)[] pairs)
    {
        foreach (var (name, value) in pairs)
        {
            template = template.Replace("{" + name + "}", value, StringComparison.Ordinal);
        }

        return template;
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
