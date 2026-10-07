using System.Globalization;
using System.Text.Json;
using Paddock.Application.Career;
using Paddock.Application.Pool;
using Paddock.Career;
using Paddock.Data.Authored;
using Paddock.Data.Historical;
using Paddock.Data.World;
using Paddock.Domain.Career;
using Paddock.Domain.Cars;
using Paddock.Domain.Racing;
using Paddock.Domain.World;
using Paddock.Simulation.Career;
using Paddock.Simulation.Time;

namespace Paddock.SimRunner;

/// <summary>One start year of the sweep: what the opening world had, and how its first race went.</summary>
public sealed record StartSweepRow(
    int Year,
    int Teams,
    int Cars,
    bool GeneratedStartData,
    string? FirstRaceDate,
    int? FirstRaceRound,
    int Classified,
    int Starters,
    string? Error)
{
    public bool Passed => Error is null;
}

/// <summary>
/// <c>start-sweep --from &lt;Y&gt; --to &lt;Y&gt; --seed &lt;N&gt; [--preset &lt;name&gt;] [--data-root &lt;dir&gt;] [--schedule &lt;file&gt; --drivers &lt;file&gt;]</c>
/// (#271): for every start year, builds the opening world the way <c>run</c> does (authored data layered over the local starting
/// data, real race dates and drivers when the cache has them) and lives every day up to the morning after the first race. Prints a
/// Markdown table, one row per year; a year fails when the world cannot be built, a day throws, or the first race stored no result.
/// Read-only; it writes nothing. Developer output, not player text.
/// </summary>
public static class StartSweepCommand
{
    public const string Name = "start-sweep";

    public static int Execute(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (args.Length == 0 || !string.Equals(args[0], Name, StringComparison.Ordinal))
        {
            stderr.WriteLine("Unknown command. Expected: start-sweep --from <Y> --to <Y> --seed <N>");
            return 1;
        }

        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i++)
        {
            var flag = args[i];
            if (flag is not ("--from" or "--to" or "--seed" or "--preset" or "--data-root" or "--schedule" or "--drivers"))
            {
                stderr.WriteLine("Unknown argument: " + flag);
                return 1;
            }

            if (i + 1 >= args.Length || !options.TryAdd(flag, args[++i]))
            {
                stderr.WriteLine("Missing or duplicate value for " + flag + ".");
                return 1;
            }
        }

        if (!TryInt(options, "--from", stderr, out var from) || !TryInt(options, "--to", stderr, out var to))
        {
            return 1;
        }

        if (!options.TryGetValue("--seed", out var seedText)
            || !ulong.TryParse(seedText, NumberStyles.None, CultureInfo.InvariantCulture, out var seed))
        {
            stderr.WriteLine("Missing or invalid --seed.");
            return 1;
        }

        if (from < CareerConfig.MinStartYear || to < from)
        {
            stderr.WriteLine("--from must be 1950 or later and --to must not be before --from.");
            return 1;
        }

        var presetName = options.GetValueOrDefault("--preset", nameof(CareerPreset.MostHistorical));
        if (!Enum.TryParse<CareerPreset>(presetName, ignoreCase: false, out var preset) || preset == CareerPreset.Custom)
        {
            stderr.WriteLine("Invalid --preset value: " + presetName);
            return 1;
        }

        options.TryGetValue("--schedule", out var schedulePath);
        options.TryGetValue("--drivers", out var driversPath);
        if ((schedulePath is null) != (driversPath is null))
        {
            stderr.WriteLine("--schedule and --drivers go together.");
            return 1;
        }

        try
        {
            var root = options.GetValueOrDefault("--data-root") ?? RunCommand.FindDataRoot();
            if (root is null)
            {
                stderr.WriteLine("Could not find data/authored. Pass --data-root pointing at the data directory.");
                return 1;
            }

            var data = AuthoredDataLoader.Load(root);
            var files = CareerData.ResolveProviderFiles(root, schedulePath, driversPath);
            var provider = CareerData.LoadProvider(files);
            var starting = StartingDataLoader.TryLoad(root);
            var dates = CareerData.LoadRaceDates(root);
            stdout.WriteLine("Start sweep " + Number(from) + "-" + Number(to) + ", preset " + preset + ", seed " + seed.ToString(CultureInfo.InvariantCulture) + ".");
            stdout.WriteLine("Real drivers: " + (files is null ? "no" : "yes") + ". Starting data: " + (starting is null ? "no (authored files only)" : "yes") + ".");
            var rows = Run(root, data, provider, starting, dates, preset, seed, from, to, row => stdout.WriteLine(Line(row)), stdout);
            var failed = rows.Count(row => !row.Passed);
            stdout.WriteLine();
            stdout.WriteLine(Number(rows.Count - failed) + " of " + Number(rows.Count) + " years passed.");
            return failed == 0 ? 0 : 2;
        }
        catch (Exception ex) when (ex is IOException or JsonException or AuthoredDataLoadException)
        {
            stderr.WriteLine(ex.Message);
            return 1;
        }
    }

    /// <summary>
    /// Runs the sweep and returns one row per year. <paramref name="onRow"/> sees each row as soon as it is done, and the table header
    /// goes to <paramref name="header"/> first when it is given. Every year is independent: one failing year does not stop the rest.
    /// </summary>
    public static IReadOnlyList<StartSweepRow> Run(
        string dataRoot,
        AuthoredData data,
        IPeopleProvider provider,
        StartingDataReport? starting,
        RaceDateBook? dates,
        CareerPreset preset,
        ulong seed,
        int from,
        int to,
        Action<StartSweepRow>? onRow = null,
        TextWriter? header = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(provider);
        if (header is not null)
        {
            header.WriteLine();
            header.WriteLine("| Year | Teams | Cars | Start data | First race | Classified / starters | Result |");
            header.WriteLine("|---:|---:|---:|---|---|---:|---|");
        }

        var rows = new List<StartSweepRow>();
        for (var year = from; year <= to; year++)
        {
            var row = One(dataRoot, data, provider, starting, dates, preset, seed, year);
            rows.Add(row);
            onRow?.Invoke(row);
        }

        return rows;
    }

    public static string Line(StartSweepRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var race = row.FirstRaceDate is null ? "-" : row.FirstRaceDate + " (round " + Number(row.FirstRaceRound ?? 0) + ")";
        var result = row.Passed ? "ok" : "FAIL: " + row.Error!.Replace("|", "/", StringComparison.Ordinal);
        return "| " + Number(row.Year) + " | " + Number(row.Teams) + " | " + Number(row.Cars) + " | "
            + (row.GeneratedStartData ? "generated + authored" : "authored") + " | " + race + " | "
            + Number(row.Classified) + " / " + Number(row.Starters) + " | " + result + " |";
    }

    private static StartSweepRow One(
        string dataRoot,
        AuthoredData data,
        IPeopleProvider provider,
        StartingDataReport? starting,
        RaceDateBook? dates,
        CareerPreset preset,
        ulong seed,
        int year)
    {
        var teams = 0;
        var cars = 0;
        var generated = false;
        string? raceDate = null;
        int? round = null;
        try
        {
            var config = CareerConfig.FromPreset(preset).WithStartYear(year);
            var sources = StartingSources.For(year, starting, data.CarStrength, data.TeamTiers);
            generated = sources.FromGeneratedData;
            var created = WorldInitializer.Create(config, data, provider, seed, new WorldInitOptions(CarStrength: sources.CarStrength, Tiers: sources.Tiers));
            teams = created.Report.Counts.Teams;
            cars = created.World.Section<CarsSection>(CarsSection.SectionName)?.Cars.Count ?? 0;

            var plan = SeasonCalendar.Plan(year, data.Layouts, data.RaceAssignments, dates);
            var first = plan.Where(session => session.TypeId == ScheduledEventType.Race).OrderBy(session => session.Date).FirstOrDefault();
            if (first.TypeId is null)
            {
                return new StartSweepRow(year, teams, cars, generated, null, null, 0, 0, "the season has no race in race_layout_map.json");
            }

            raceDate = first.Date.ToString();
            round = first.Round;
            var arrivals = TalentIntakeSchedule.AfterStart(config, provider, created.World, seed);
            var session = new CareerSession(
                created.World,
                seed,
                created.TalentPool,
                arrivals,
                new CareerSessionOptions { LastSeasons = LastSeasons.From(provider) });
            var inputs = CareerInputsLoader.Load(dataRoot, data, created.EngineSupplies, config, dates, sources);
            var result = CareerHost.RunUntil(session, first.Date.AddDays(1), null, new CareerRunOptions { Inputs = inputs });
            var stored = result.Session.World.Section<RaceResultsSection>(RaceResultsSection.SectionName)?.Find(year, first.Round);
            if (stored is null || stored.Rows.Count == 0)
            {
                return new StartSweepRow(year, teams, cars, generated, raceDate, round, 0, 0, "the first race stored no result");
            }

            return new StartSweepRow(year, teams, cars, generated, raceDate, round, stored.Rows.Count(row => row.Classified), stored.Rows.Count, null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            var message = ex.Message.Split('\n')[0].Trim();
            return new StartSweepRow(year, teams, cars, generated, raceDate, round, 0, 0, ex.GetType().Name + ": " + message);
        }
    }

    private static bool TryInt(Dictionary<string, string> options, string flag, TextWriter stderr, out int value)
    {
        value = 0;
        if (options.TryGetValue(flag, out var text) && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        stderr.WriteLine("Missing or invalid " + flag + ".");
        return false;
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
