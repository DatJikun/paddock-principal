using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using Paddock.Application.Localization;
using Paddock.Application.Racing;
using Paddock.Application.Spy;
using Paddock.Data.Authored;
using Paddock.Domain.Racing;
using Paddock.Domain.Spy;
using Paddock.Simulation.Racing;
using Paddock.Simulation.Racing.Incidents;
using Paddock.Simulation.Racing.Points;
using Paddock.Simulation.Racing.Qualifying;
using Paddock.Simulation.Racing.Weather;
using Paddock.Simulation.Racing.Weekend;

namespace Paddock.SimRunner;

/// <summary>
/// <c>race --year Y --round N [--seed S] [--grid fixture] [--spy] [--lang pl|en] [--verbose] [--log [--full]] [--hash] [--timing]</c>
/// runs one race weekend through <see cref="IRaceSimulator"/> (default <see cref="RaceEngineKind.Lap"/>) on the SYNTHETIC fixture field of the era and prints the readable race
/// report (<see cref="RaceReportBuilder"/>): conditions, qualifying, the race in phases, pit stops, shared drives, the result and
/// the points, through the translation keys of both languages. <c>--verbose</c> adds the lap by lap section. <c>--log</c> prints the
/// old raw event log instead (the same lines as <c>race-replay</c>, through <see cref="RaceEventText"/>, then the classification table).
/// <c>race --season Y [--seed S] [--lang pl|en]</c> runs every round of a season and prints the drivers' table after each round.
/// Read-only: nothing is saved (INV-001). <c>--spy</c> also prints the developer's decision traces and the true weather;
/// the race is the same either way (INV-006). <c>--hash</c> prints the SHA-256 of the tape JSON, the number the golden
/// test pins: <c>dotnet run --project tools/Paddock.SimRunner -- race --year 1955 --round 1 --seed 20261003 --hash</c>.
/// </summary>
public static class RaceCommand
{
    public const string Name = "race";

    /// <summary>Seed used when <c>--seed</c> is not given.</summary>
    public const ulong DefaultSeed = 20_261_003UL;

    private const string Usage =
        "Usage: race --year <int> --round <int> [--seed <ulong>] [--grid fixture] [--spy] [--lang pl|en] [--verbose] [--log [--full]] [--hash] [--timing] | race --season <int> [--seed <ulong>] [--lang pl|en] [--timing]";

    private sealed record Options(
        int? Year,
        int? Round,
        int? Season,
        ulong Seed,
        bool Spy,
        bool Full,
        bool Log,
        bool Verbose,
        bool Hash,
        bool Timing,
        string Language);

    public static int Execute(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        var options = Parse(args, stderr);
        if (options is null)
        {
            return 1;
        }

        var root = FindRoot();
        if (root is null)
        {
            stderr.WriteLine("Could not locate PaddockPrincipal.sln.");
            return 1;
        }

        AuthoredData data;
        IReadOnlyDictionary<string, string> strings;
        TranslationCatalog catalog;
        try
        {
            data = AuthoredDataLoader.Load(Path.Combine(root, "data"));
            strings = StringTable.Load(options.Language);
            catalog = StringTable.LoadCatalog();
        }
        catch (Exception ex) when (ex is AuthoredDataLoadException or InvalidOperationException or IOException or LocalizationLoadException)
        {
            stderr.WriteLine(ex.Message);
            return 1;
        }

        var clock = Stopwatch.StartNew();
        try
        {
            var code = options.Season is { } season
                ? RunSeason(data, strings, season, options, stdout)
                : RunRace(data, strings, catalog, options.Year!.Value, options.Round!.Value, options, stdout);
            if (options.Timing)
            {
                stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"elapsed {clock.ElapsedMilliseconds} ms"));
            }

            return code;
        }
        catch (InvalidOperationException ex)
        {
            stderr.WriteLine(ex.Message);
            return 1;
        }
    }

    /// <summary>Builds the input of a weekend from the authored data and the fixture field (the tool's only source of entries until T20).</summary>
    public static RaceWeekendInput BuildInput(AuthoredData data, int season, int round, ulong seed, ImmutableArray<RaceEntry> entries, int? month = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        var track = data.LayoutFor(season, round);
        var rules = data.RuleSetFor(season);
        var era = data.EraSetFor(season);
        var rounds = data.RaceAssignments.Count(a => a.Season == season);
        return new RaceWeekendInput
        {
            MasterSeed = seed,
            Season = season,
            Round = round,
            IsFinalRound = round == rounds,
            Track = track,
            Rules = rules,
            Safety = EraSafetyProfile.FromAuthored(season, era.Value("fatality_risk"), rules.Value("safety_car")),
            Climate = new DefaultClimateSource(),
            Month = month ?? MonthOf(round, rounds),
            TotalLaps = RaceDistance.LapsFor(rules, track),
            Entries = entries,
        };
    }

    // ESTIMATE: the calendar has no dates yet, so rounds are spread over March to October.
    private static int MonthOf(int round, int rounds) => 3 + ((round - 1) * 8 / Math.Max(1, rounds));

    /// <summary>Runs one weekend through the given simulator. The career and the tool share <see cref="RaceSession"/>.</summary>
    public static RaceTape Simulate(IRaceSimulator simulator, RaceWeekendInput input, ITraceSink sink)
    {
        ArgumentNullException.ThrowIfNull(simulator);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(sink);
        return RaceSession.Run(simulator, RaceSimulationRequest.ForWeekend(input, sink));
    }

    private static int RunRace(AuthoredData data, IReadOnlyDictionary<string, string> strings, TranslationCatalog catalog, int season, int round, Options options, TextWriter stdout)
    {
        var input = BuildInput(data, season, round, options.Seed, SyntheticField.For(season));
        var collector = options.Spy ? new CollectingSink() : null;
        ITraceSink sink = collector ?? (ITraceSink)NullSink.Instance;
        var request = RaceSimulationRequest.ForWeekend(input, sink);
        var tape = RaceSession.Run(request, RaceEngineKind.Lap);
        var published = RequirePublished(request);

        if (options.Hash)
        {
            stdout.WriteLine(tape.Hash);
            return 0;
        }

        if (options.Log)
        {
            PrintLog(stdout, strings, input, published, options);
        }
        else
        {
            PrintNarrative(stdout, strings, catalog, input, published, options);
        }

        if (collector is not null)
        {
            PrintSpy(stdout, strings, published, collector);
        }

        return 0;
    }

    private static RacePublishedFacts RequirePublished(RaceSimulationRequest request) =>
        request.Published ?? throw new InvalidOperationException("The race engine did not publish the facts the report reads.");

    /// <summary>The readable report: the title, the synthetic note and the seed, then the sections of <see cref="RaceReportBuilder"/>.</summary>
    private static void PrintNarrative(TextWriter stdout, IReadOnlyDictionary<string, string> strings, TranslationCatalog catalog, RaceWeekendInput input, RacePublishedFacts published, Options options)
    {
        var sink = new CollectingMissingKeySink();
        var localizer = new Localizer(catalog, options.Language == "pl" ? Language.Pl : Language.En, sink);
        var report = RaceReportBuilder.Build(
            RaceReportInput.From(published, input.Season, input.Round, input.Track.Id),
            new RaceReportOptions(options.Verbose));
        var lines = RaceReportRenderer.Render(report, localizer);
        if (sink.Reports.Count > 0)
        {
            // A fallback is never silent: a report that quotes a key instead of text is a bug.
            throw new InvalidOperationException("The race report used missing or incomplete translations: " + string.Join("; ", sink.Reports.Select(r => $"{r.Kind} {r.Key} {r.Detail}".TrimEnd())));
        }

        stdout.WriteLine(lines[0]);
        stdout.WriteLine(strings.Required("race.synthetic"));
        stdout.WriteLine(Fill(strings.Required("race.seed"), ("seed", options.Seed)));
        foreach (var line in lines.Skip(1))
        {
            stdout.WriteLine(line);
        }
    }

    /// <summary>The old output (<c>--log</c>): the raw event log, then the classification table.</summary>
    private static void PrintLog(TextWriter stdout, IReadOnlyDictionary<string, string> strings, RaceWeekendInput input, RacePublishedFacts published, Options options)
    {
        stdout.WriteLine(Fill(strings.Required("race.title"), ("year", input.Season), ("round", input.Round), ("layout", input.Track.Id), ("laps", input.TotalLaps)));
        stdout.WriteLine(strings.Required("race.synthetic"));
        stdout.WriteLine(Fill(strings.Required("race.seed"), ("seed", options.Seed)));
        stdout.WriteLine();

        stdout.WriteLine(strings.Required("race.section.qualifying"));
        foreach (var line in published.Qualifying.Summary)
        {
            stdout.WriteLine(Fill(strings.Required(line.Key), [.. line.Args.Select(a => (a.Key, a.Value ?? string.Empty))]));
        }

        stdout.WriteLine();
        stdout.WriteLine(strings.Required("race.section.race"));
        var lastFastest = published.Tape.Events.OfType<FastestLap>().LastOrDefault();
        foreach (var raceEvent in published.Tape.Events)
        {
            if (options.Full || IsHeadline(raceEvent, lastFastest))
            {
                stdout.WriteLine(RaceEventText.Format(raceEvent));
            }
        }

        stdout.WriteLine();
        stdout.WriteLine(strings.Required("race.section.classification"));
        stdout.WriteLine(strings.Required("race.classification.header"));
        var cars = published.CarResults.ToDictionary(c => c.DriverId, StringComparer.Ordinal);
        var leader = published.CarResults[0];
        foreach (var classified in published.Classification.Cars)
        {
            var car = cars[classified.DriverIds[0]];
            var status = strings.Required(StatusKey(car.Status, classified.IsClassified));
            var drivers = string.Join(" / ", car.DriversWhoDrove);
            var gap = car == leader
                ? Clock(car.TotalSeconds)
                : car.Status == FinishStatus.Classified && car.LapsCompleted < leader.LapsCompleted
                    ? "+" + (leader.LapsCompleted - car.LapsCompleted).ToString(CultureInfo.InvariantCulture) + " L"
                    : car.Status == FinishStatus.Classified ? "+" + (car.TotalSeconds - leader.TotalSeconds).ToString("0.000", CultureInfo.InvariantCulture) : string.Empty;
            stdout.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{classified.Position,3}  {drivers,-24} {car.ConstructorId,-8} {car.LapsCompleted,3}  {gap,-11} {status,-14} {classified.CarPoints,5}"));
        }

        stdout.WriteLine();
        stdout.WriteLine(strings.Required("race.section.points"));
        foreach (var score in published.Classification.DriverScores.Where(s => s.Points > 0).OrderByDescending(s => s.Points).ThenBy(s => s.DriverId, StringComparer.Ordinal))
        {
            stdout.WriteLine(Fill(strings.Required("race.points.driver"), ("driver", score.DriverId), ("points", score.Points)));
        }

        foreach (var score in published.Classification.ConstructorScores.Where(s => s.Points > 0).OrderByDescending(s => s.Points).ThenBy(s => s.ConstructorId, StringComparer.Ordinal))
        {
            stdout.WriteLine(Fill(strings.Required("race.points.constructor"), ("team", score.ConstructorId), ("points", score.Points)));
        }

        var injured = published.PersonOutcomes.Where(p => p.Injury != InjuryGrade.None || p.Fatal).ToList();
        if (injured.Count > 0)
        {
            stdout.WriteLine();
            foreach (var person in injured)
            {
                stdout.WriteLine(Fill(
                    strings.Required(person.Fatal ? "race.outcome.fatal" : "race.outcome.injury"),
                    ("driver", person.DriverId),
                    ("grade", person.Injury.ToString()),
                    ("lap", person.Lap ?? 0)));
            }
        }
    }

    private static void PrintSpy(TextWriter stdout, IReadOnlyDictionary<string, string> strings, RacePublishedFacts published, CollectingSink collector)
    {
        stdout.WriteLine();
        stdout.WriteLine(strings.Required("race.section.spy"));
        var truth = published.TruthWeather;
        stdout.WriteLine(Fill(
            strings.Required("race.spy.weather"),
            ("showery", truth.IsShowery ? "yes" : "no"),
            ("onset", truth.OnsetMinute?.ToString(CultureInfo.InvariantCulture) ?? "-"),
            ("peak", truth.Samples.Max(s => s.TrackWetness).ToString("0.00", CultureInfo.InvariantCulture))));
        foreach (var trace in collector.Traces)
        {
            stdout.WriteLine(TraceJson.Serialize(trace));
        }
    }

    private static int RunSeason(AuthoredData data, IReadOnlyDictionary<string, string> strings, int season, Options options, TextWriter stdout)
    {
        var rounds = data.RaceAssignments.Where(a => a.Season == season).Select(a => a.Round).Order().ToList();
        if (rounds.Count == 0)
        {
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"Season {season} has no rounds in the calendar."));
        }

        var entries = SyntheticField.For(season);
        stdout.WriteLine(Fill(strings.Required("race.season.title"), ("year", season), ("rounds", rounds.Count)));
        stdout.WriteLine(strings.Required("race.synthetic"));
        stdout.WriteLine(Fill(strings.Required("race.seed"), ("seed", options.Seed)));
        stdout.WriteLine();

        var rules = PointsRules.For(data.RuleSetFor(season));
        var standings = Standings.Start(rules, rounds.Count);
        foreach (var round in rounds)
        {
            var input = BuildInput(data, season, round, options.Seed, entries);
            var request = RaceSimulationRequest.ForWeekend(input, NullSink.Instance);
            _ = RaceSession.Run(request, RaceEngineKind.Lap);
            var published = RequirePublished(request);
            standings = standings.Apply(published.Classification);
            var winner = published.CarResults[0];
            stdout.WriteLine(Fill(
                strings.Required("race.season.round"),
                ("round", round),
                ("layout", input.Track.Id),
                ("laps", input.TotalLaps),
                ("winner", string.Join(" / ", winner.DriversWhoDrove)),
                ("finishers", published.CarResults.Count(c => c.Status == FinishStatus.Classified)),
                ("starters", published.CarResults.Length)));
            foreach (var row in standings.Drivers().Take(5))
            {
                stdout.WriteLine(Fill(
                    strings.Required("race.season.row"),
                    ("position", row.Position),
                    ("driver", row.Id),
                    ("points", row.CountedPoints),
                    ("wins", row.Wins)));
            }
        }

        stdout.WriteLine();
        stdout.WriteLine(strings.Required("race.season.final.drivers"));
        foreach (var row in standings.Drivers().Take(10))
        {
            stdout.WriteLine(Fill(strings.Required("race.season.row"), ("position", row.Position), ("driver", row.Id), ("points", row.CountedPoints), ("wins", row.Wins)));
        }

        stdout.WriteLine(strings.Required("race.season.final.constructors"));
        foreach (var row in standings.Constructors().Take(10))
        {
            stdout.WriteLine(Fill(strings.Required("race.season.row"), ("position", row.Position), ("driver", row.Id), ("points", row.CountedPoints), ("wins", row.Wins)));
        }

        return 0;
    }

    private static bool IsHeadline(RaceEvent e, FastestLap? lastFastest) => e switch
    {
        // Only the final fastest lap: the lap record improves nearly every lap while the tanks empty.
        FastestLap x => x == lastFastest,
        // The lap chart is the leader's laps; the rest of the field is in --full.
        LapCompleted x => x.Position == 1,
        PositionChange x => x.ToPosition <= 3 || x.FromPosition <= 3,
        _ => true,
    };

    private static string StatusKey(FinishStatus status, bool classified) => status switch
    {
        FinishStatus.Classified => "race.status.finished",
        FinishStatus.Mechanical => classified ? "race.status.mechanical.classified" : "race.status.mechanical",
        FinishStatus.Accident => classified ? "race.status.accident.classified" : "race.status.accident",
        _ => classified ? "race.status.other.classified" : "race.status.other",
    };

    private static string Clock(double seconds)
    {
        var ms = (long)Math.Round(seconds * 1000d);
        return string.Create(CultureInfo.InvariantCulture, $"{ms / 3_600_000}:{ms / 60_000 % 60:00}:{ms / 1000 % 60:00}.{ms % 1000:000}");
    }

    private static string Fill(string template, params (string Name, object? Value)[] values)
    {
        var text = template;
        foreach (var (name, value) in values)
        {
            text = text.Replace("{" + name + "}", Format(value), StringComparison.Ordinal);
        }

        return text;
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static Options? Parse(string[] args, TextWriter stderr)
    {
        int? year = null;
        int? round = null;
        int? season = null;
        ulong seed = DefaultSeed;
        var seedSet = false;
        string? language = null;
        var spy = false;
        var full = false;
        var log = false;
        var verbose = false;
        var hash = false;
        var timing = false;

        for (var i = 1; i < args.Length; i++)
        {
            var flag = args[i];
            switch (flag)
            {
                case "--spy":
                    spy = true;
                    continue;
                case "--full":
                    full = true;
                    continue;
                case "--log":
                    log = true;
                    continue;
                case "--verbose":
                    verbose = true;
                    continue;
                case "--hash":
                    hash = true;
                    continue;
                case "--timing":
                    timing = true;
                    continue;
                case "--year" or "--round" or "--season" or "--seed" or "--grid" or "--lang":
                    break;
                default:
                    stderr.WriteLine($"Unknown argument: {flag}");
                    return null;
            }

            if (i + 1 >= args.Length)
            {
                stderr.WriteLine($"Missing value for {flag}.");
                return null;
            }

            var value = args[++i];
            switch (flag)
            {
                case "--year" when year is null:
                    if (!TryInt(value, out var y))
                    {
                        stderr.WriteLine($"Invalid --year value: {value}");
                        return null;
                    }

                    year = y;
                    break;
                case "--round" when round is null:
                    if (!TryInt(value, out var r))
                    {
                        stderr.WriteLine($"Invalid --round value: {value}");
                        return null;
                    }

                    round = r;
                    break;
                case "--season" when season is null:
                    if (!TryInt(value, out var s))
                    {
                        stderr.WriteLine($"Invalid --season value: {value}");
                        return null;
                    }

                    season = s;
                    break;
                case "--seed" when !seedSet:
                    if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out seed))
                    {
                        stderr.WriteLine($"Invalid --seed value: {value}");
                        return null;
                    }

                    seedSet = true;
                    break;
                case "--grid" when value == SyntheticField.FixtureName:
                    break;
                case "--lang" when language is null && value is "pl" or "en":
                    language = value;
                    break;
                default:
                    stderr.WriteLine($"Invalid or duplicate option: {flag} {value}");
                    return null;
            }
        }

        var raceMode = year is not null || round is not null;
        if (season is not null && (raceMode || hash || spy || full || log || verbose))
        {
            stderr.WriteLine($"--season cannot be combined with --year, --round, --spy, --full, --log, --verbose or --hash. {Usage}");
            return null;
        }

        if (full && !log)
        {
            stderr.WriteLine($"--full belongs to --log. {Usage}");
            return null;
        }

        if (verbose && log)
        {
            stderr.WriteLine($"--verbose belongs to the report and --log prints the raw log; give one of them. {Usage}");
            return null;
        }

        if (season is null && (year is null || round is null))
        {
            stderr.WriteLine($"Give --year and --round, or --season. {Usage}");
            return null;
        }

        return new Options(year, round, season, seed, spy, full, log, verbose, hash, timing, language ?? "en");
    }

    private static bool TryInt(string value, out int result) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result) && result > 0;

    internal static string? FindRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "PaddockPrincipal.sln")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }
        }

        return null;
    }

    private sealed class CollectingSink : ITraceSink
    {
        public List<DecisionTrace> Traces { get; } = [];

        public bool IsEnabled => true;

        public void Record(DecisionTrace trace)
        {
            ArgumentNullException.ThrowIfNull(trace);
            Traces.Add(trace);
        }
    }
}
