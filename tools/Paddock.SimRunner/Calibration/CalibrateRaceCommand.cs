using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Paddock.Data.Authored;
using Paddock.Data.Historical;
using Paddock.DataPipeline;
using Paddock.Domain.Spy;
using Paddock.Simulation.Racing.Weekend;

namespace Paddock.SimRunner.Calibration;

/// <summary>An era band of seasons the calibration pools together, with the pit stop reference range for it.</summary>
/// <param name="From">First season.</param>
/// <param name="To">Last season.</param>
/// <param name="StopsLow">Lower end of the stops-per-car sanity range.</param>
/// <param name="StopsHigh">Upper end of the stops-per-car sanity range.</param>
public sealed record EraBand(int From, int To, double StopsLow, double StopsHigh)
{
    public string Label => string.Create(CultureInfo.InvariantCulture, $"{From}-{To}");
}

/// <summary>
/// Tolerance bands of the calibration (every number here is an ESTIMATE of what counts as close enough, not a measured fact).
/// Where the Jolpica cache has the historical figure, the band is around it. It has no pit stop, lap-led or safety car data,
/// so for those the report checks a sanity range written from general knowledge of the sport (stops) and says so.
/// </summary>
public static class CalibrationTargets
{
    /// <summary>ESTIMATE: absolute tolerance on the finish rate.</summary>
    public const double FinishRateTolerance = 0.08;

    /// <summary>ESTIMATE: absolute tolerance on the mechanical and accident shares of retirements.</summary>
    public const double ShareTolerance = 0.15;

    /// <summary>ESTIMATE: absolute tolerance on the lapped share of finishers.</summary>
    public const double LappedTolerance = 0.15;

    /// <summary>ESTIMATE: absolute tolerance on the pole-to-win rate (races per band are few, so wide).</summary>
    public const double PoleToWinTolerance = 0.15;

    /// <summary>ESTIMATE: the simulated median margin must lie within this factor of the historical one, either way.</summary>
    public const double MarginFactor = 2.5;

    /// <summary>ESTIMATE: absolute tolerance on the share of races that see rain (R9 data).</summary>
    public const double RainTolerance = 0.07;

    /// <summary>Design target: at most this share of cars may fit wet tyres on a dry day.</summary>
    public const double WetTyresOnDryDayMax = 0.02;

    /// <summary>ESTIMATE: lead changes per race must reach this mean, or the racing is static (no history in the cache).</summary>
    public const double LeadChangesMin = 0.8;

    /// <summary>ESTIMATE: the pole-to-win rate of a sane era is below this (no history needed: it is not 1.0).</summary>
    public const double AbsurdPoleToWin = 0.85;

    /// <summary>ESTIMATE: tolerance on the mean start air temperature against R9 (C).</summary>
    public const double AirTempBiasMaxC = 3.0;

    /// <summary>ESTIMATE: fewer R9 air temperatures than this in a band are shown but not judged.</summary>
    public const int AirTempMinSamples = 3;

    /// <summary>
    /// Era bands. The stops-per-car ranges are an ESTIMATE from general knowledge, NOT from the cache: stops were rare before
    /// 1980 (tyres lasted a race), about one tyre stop in the no-refuelling years 1984-1993, two to three with refuelling
    /// (1994-2009), then one to two on tyres alone.
    /// </summary>
    public static readonly IReadOnlyList<EraBand> Bands =
    [
        new(1950, 1959, 0.0, 0.6),
        new(1960, 1969, 0.0, 0.5),
        new(1970, 1979, 0.0, 0.6),
        new(1980, 1993, 0.4, 1.4),
        new(1994, 2009, 1.8, 3.2),
        new(2010, 2025, 1.2, 3.0),
    ];
}

/// <summary>
/// <c>calibrate-race --from Y --to Y [--stride N] [--seeds N] [--cache path]</c> simulates the races of the seasons (every
/// <c>stride</c>th season, <c>seeds</c> seeds each) on the synthetic fixture field, pools them per era band and compares the
/// figures with the owner's LOCAL Jolpica cache and the R9 weather data. The report goes to stdout and to
/// <c>data/cache/reports/calibrate_race_FROM_TO.md</c> next to the cache (not committed, PP-041). Read-only (INV-001).
/// </summary>
public static class CalibrateRaceCommand
{
    public const string Name = "calibrate-race";

    private const string Usage = "Usage: calibrate-race --from <year> --to <year> [--stride <n>] [--seeds <n>] [--seed <ulong>] [--cache <jolpica cache dir>] [--out <file>]";

    public static int Execute(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i += 2)
        {
            if (args[i] is not ("--from" or "--to" or "--stride" or "--seeds" or "--seed" or "--cache" or "--out") || i + 1 >= args.Length)
            {
                stderr.WriteLine(Usage);
                return 1;
            }

            options[args[i]] = args[i + 1];
        }

        if (!options.TryGetValue("--from", out var fromText) || !options.TryGetValue("--to", out var toText)
            || !int.TryParse(fromText, NumberStyles.None, CultureInfo.InvariantCulture, out var from)
            || !int.TryParse(toText, NumberStyles.None, CultureInfo.InvariantCulture, out var to) || from > to)
        {
            stderr.WriteLine(Usage);
            return 1;
        }

        var stride = IntOption(options, "--stride", 1);
        var seeds = IntOption(options, "--seeds", 1);
        var baseSeed = options.TryGetValue("--seed", out var seedText) && ulong.TryParse(seedText, NumberStyles.None, CultureInfo.InvariantCulture, out var s)
            ? s
            : RaceCommand.DefaultSeed;
        if (stride < 1 || seeds < 1)
        {
            stderr.WriteLine(Usage);
            return 1;
        }

        var root = FindRoot();
        if (root is null)
        {
            stderr.WriteLine("Could not locate PaddockPrincipal.sln.");
            return 1;
        }

        var cacheRoot = options.TryGetValue("--cache", out var cache) ? cache : Path.Combine(root, "data", "cache", "jolpica");
        var normalized = JolpicaCache.Normalized(cacheRoot);
        if (!File.Exists(Path.Combine(normalized, "results.json")))
        {
            stderr.WriteLine($"Normalized Jolpica cache not found: {normalized} (run DataPipeline fetch and normalize, or pass --cache).");
            return 1;
        }

        var data = AuthoredDataLoader.Load(Path.Combine(root, "data"));
        var history = new NormalizedHistoricalData(
            Read<HistoricalDriversDocument>(normalized, "drivers.json"),
            Read<HistoricalConstructorsDocument>(normalized, "constructors.json"),
            Read<HistoricalCircuitsDocument>(normalized, "circuits.json"),
            Read<HistoricalRacesDocument>(normalized, "races.json"),
            Read<HistoricalResultsDocument>(normalized, "results.json"),
            Read<HistoricalQualifyingDocument>(normalized, "qualifying.json"));
        var r9 = LoadR9(Path.Combine(root, "data", "authored", "weather", "races.json"));

        var text = new StringBuilder();
        text.Append("# calibrate-race ").Append(from).Append('-').Append(to)
            .Append(" (stride ").Append(stride).Append(", seeds ").Append(seeds).Append(", base seed ").Append(baseSeed).Append(")\n\n");
        text.Append("Simulated: synthetic fixture field, one run per race and seed. Historical: local Jolpica cache (not committed, PP-041), R9 weather for rain and air temperature. ")
            .Append("Tolerances are ESTIMATES (see CalibrationTargets). Stops, lead changes and safety cars have no historical data in the cache.\n");

        var failures = 0;
        foreach (var band in CalibrationTargets.Bands)
        {
            var seasons = Enumerable.Range(Math.Max(from, band.From), Math.Max(0, Math.Min(to, band.To) - Math.Max(from, band.From) + 1))
                .Where(y => (y - from) % stride == 0)
                .ToList();
            if (seasons.Count == 0)
            {
                continue;
            }

            var samples = Simulate(data, history, r9, seasons, seeds, baseSeed);
            var sim = EraFigures.FromSamples([.. samples.Select(x => x.Sample)]);
            var hist = HistoricalFigures.For(history, seasons.ToHashSet(), (y, r) => r9.Rain.GetValueOrDefault((y, r)));
            var temps = samples.Where(x => x.R9AirTempC is not null).Select(x => x.Sample.StartAirTempC - x.R9AirTempC!.Value).ToList();
            failures += AppendBand(text, band, seasons, sim, hist, temps);
        }

        text.Append("\nOut of band: ").Append(failures).Append('\n');
        stdout.Write(text.ToString());

        var outPath = options.TryGetValue("--out", out var o)
            ? o
            : Path.Combine(Directory.GetParent(Path.GetFullPath(cacheRoot))!.FullName, "reports", $"calibrate_race_{from}_{to}.md");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
        File.WriteAllText(outPath, text.ToString());
        stderr.WriteLine("wrote " + outPath);
        return 0;
    }

    private sealed record Run(RaceSample Sample, double? R9AirTempC);

    private static List<Run> Simulate(AuthoredData data, NormalizedHistoricalData history, R9Weather r9, List<int> seasons, int seeds, ulong baseSeed)
    {
        var jobs = new List<(int Season, int Round, int Month, ulong Seed)>();
        foreach (var race in history.Races.Races.Where(r => seasons.Contains(r.Season) && !r.IsIndianapolis500))
        {
            if (data.RaceAssignments.All(a => a.Season != race.Season || a.Round != race.Round)
                || !DateTime.TryParse(race.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                continue;
            }

            for (var k = 0; k < seeds; k++)
            {
                jobs.Add((race.Season, race.Round, date.Month, baseSeed + (ulong)k));
            }
        }

        var bag = new ConcurrentBag<(int Season, int Round, ulong Seed, Run Run)>();
        Parallel.ForEach(jobs, job =>
        {
            var input = RaceCommand.BuildInput(data, job.Season, job.Round, job.Seed, SyntheticField.For(job.Season), job.Month);
            var result = RaceWeekend.Run(input, NullSink.Instance);
            bag.Add((job.Season, job.Round, job.Seed, new Run(RaceSample.From(result, job.Season), r9.AirTemp.GetValueOrDefault((job.Season, job.Round)))));
        });
        return [.. bag.OrderBy(b => b.Season).ThenBy(b => b.Round).ThenBy(b => b.Seed).Select(b => b.Run)];
    }

    private static int AppendBand(StringBuilder text, EraBand band, List<int> seasons, EraFigures sim, EraFigures hist, List<double> airTempErrors)
    {
        var rows = new List<(string Metric, string Hist, string Sim, bool? Ok, string Rule)>();

        void Row(string metric, string historical, string simulated, bool? ok, string rule) => rows.Add((metric, historical, simulated, ok, rule));

        Row("finish rate", P(hist.FinishRate), P(sim.FinishRate), Math.Abs(sim.FinishRate - hist.FinishRate) <= CalibrationTargets.FinishRateTolerance, $"+-{CalibrationTargets.FinishRateTolerance:0.00} abs");
        Row("mechanical share of retirements", P(hist.MechanicalShareOfRetirements), P(sim.MechanicalShareOfRetirements), Math.Abs(sim.MechanicalShareOfRetirements - hist.MechanicalShareOfRetirements) <= CalibrationTargets.ShareTolerance, $"+-{CalibrationTargets.ShareTolerance:0.00} abs");
        Row("accident share of retirements", P(hist.AccidentShareOfRetirements), P(sim.AccidentShareOfRetirements), Math.Abs(sim.AccidentShareOfRetirements - hist.AccidentShareOfRetirements) <= CalibrationTargets.ShareTolerance, $"+-{CalibrationTargets.ShareTolerance:0.00} abs");
        Row("stops per car", "n/a (not in cache)", N(sim.StopsPerCar), sim.StopsPerCar is { } st && st >= band.StopsLow && st <= band.StopsHigh, $"sanity {band.StopsLow:0.0}-{band.StopsHigh:0.0} (ESTIMATE)");
        var marginOk = hist.MedianMarginSeconds is { } hm && sim.MedianMarginSeconds is { } sm && sm >= hm / CalibrationTargets.MarginFactor && sm <= hm * CalibrationTargets.MarginFactor;
        Row("median winner margin (s)", N(hist.MedianMarginSeconds), N(sim.MedianMarginSeconds), marginOk, $"within x{CalibrationTargets.MarginFactor:0.0}");
        Row("lapped share of finishers", P(hist.LappedShareOfFinishers), P(sim.LappedShareOfFinishers), Math.Abs(sim.LappedShareOfFinishers - hist.LappedShareOfFinishers) <= CalibrationTargets.LappedTolerance, $"+-{CalibrationTargets.LappedTolerance:0.00} abs");
        Row("pole-to-win rate", P(hist.PoleToWinRate), P(sim.PoleToWinRate), Math.Abs(sim.PoleToWinRate - hist.PoleToWinRate) <= CalibrationTargets.PoleToWinTolerance, $"+-{CalibrationTargets.PoleToWinTolerance:0.00} abs");
        Row("lead changes per race", "n/a (not in cache)", N(sim.LeadChangesPerRace), sim.LeadChangesPerRace >= CalibrationTargets.LeadChangesMin, $">= {CalibrationTargets.LeadChangesMin:0.0} (ESTIMATE)");
        Row("races with a safety car", "n/a", P(sim.SafetyCarRaceShare), null, "info");
        Row("cars on wet tyres, dry days", "n/a", P(sim.WetTyreShareOnDryDays), sim.WetTyreShareOnDryDays is null or <= CalibrationTargets.WetTyresOnDryDayMax, $"<= {CalibrationTargets.WetTyresOnDryDayMax:0.00}");
        Row("races with rain (R9)", P(hist.RainRaceShare), P(sim.RainRaceShare), Math.Abs(sim.RainRaceShare - hist.RainRaceShare) <= CalibrationTargets.RainTolerance, $"+-{CalibrationTargets.RainTolerance:0.00} abs");
        if (airTempErrors.Count > 0)
        {
            var bias = airTempErrors.Average();
            var mae = airTempErrors.Average(Math.Abs);
            Row($"start air temp error vs R9 (n={airTempErrors.Count})", "mean abs " + mae.ToString("0.0", CultureInfo.InvariantCulture) + " C", "bias " + bias.ToString("+0.0;-0.0", CultureInfo.InvariantCulture) + " C", airTempErrors.Count < CalibrationTargets.AirTempMinSamples || Math.Abs(bias) <= CalibrationTargets.AirTempBiasMaxC, $"|bias| <= {CalibrationTargets.AirTempBiasMaxC:0.0} C (judged from n={CalibrationTargets.AirTempMinSamples})");
        }

        text.Append("\n## ").Append(band.Label).Append(" (seasons ").Append(seasons.Count).Append(", simulated races ").Append(sim.Races)
            .Append(", simulated starters ").Append(sim.Starters).Append(", historical races ").Append(hist.Races).Append(")\n\n");
        text.Append("| metric | historical | simulated | rule | in band |\n|---|---|---|---|---|\n");
        foreach (var (metric, historical, simulated, ok, rule) in rows)
        {
            text.Append("| ").Append(metric).Append(" | ").Append(historical).Append(" | ").Append(simulated).Append(" | ").Append(rule)
                .Append(" | ").Append(ok is null ? "-" : ok.Value ? "yes" : "NO").Append(" |\n");
        }

        return rows.Count(r => r.Ok == false);
    }

    private static string P(double? v) => v is null ? "n/a" : (v.Value * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";

    private static string N(double? v) => v is null ? "n/a" : v.Value.ToString("0.00", CultureInfo.InvariantCulture);

    private static int IntOption(Dictionary<string, string> options, string name, int fallback) =>
        options.TryGetValue(name, out var v) && int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : fallback;

    private static T Read<T>(string directory, string file) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(directory, file)), HistoricalJson.Options)
        ?? throw new InvalidDataException($"{file} is empty.");

    /// <summary>R9 race weather (<c>data/authored/weather/races.json</c>): did it rain during the race, and the air temperature, by (season, round).</summary>
    public sealed record R9Weather(IReadOnlyDictionary<(int, int), bool?> Rain, IReadOnlyDictionary<(int, int), double?> AirTemp);

    private static R9Weather LoadR9(string path)
    {
        var rain = new Dictionary<(int, int), bool?>();
        var air = new Dictionary<(int, int), double?>();
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var key = (item.GetProperty("season").GetInt32(), item.GetProperty("round").GetInt32());
            rain[key] = item.GetProperty("rain_during_race") is { ValueKind: not JsonValueKind.Null } r ? r.GetBoolean() : null;
            air[key] = item.GetProperty("air_temp_c") is { ValueKind: not JsonValueKind.Null } a ? a.GetDouble() : null;
        }

        return new R9Weather(rain, air);
    }

    private static string? FindRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "PaddockPrincipal.sln")))
                {
                    return dir.FullName;
                }
            }
        }

        return null;
    }
}
