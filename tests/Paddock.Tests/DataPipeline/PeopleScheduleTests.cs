using System.Text.Json;
using Paddock.Data.Historical;
using Paddock.DataPipeline;

namespace Paddock.Tests.DataPipeline;

public class PeopleScheduleTests
{
    [Fact]
    public void StintsGroupBySeasonAndConstructorAndKeepAReturnAsOneRow()
    {
        var report = Build(
            races:
            [
                Race(1970, 1), Race(1970, 2), Race(1970, 3), Race(1970, 4),
            ],
            results:
            [
                Row(1970, 1, "returner", "alfa"),
                Row(1970, 3, "returner", "alfa"),
                Row(1970, 2, "returner", "ferrari"),
                Row(1970, 4, "returner", "ferrari"),
                Row(1970, 1, "returner", "ferrari", shared: true),
            ]);

        var driver = Assert.Single(report.Drivers);
        Assert.Equal(
            [
                Stint(1970, "alfa", 1, 3, 2, ScheduleRoles.Substitute),
                Stint(1970, "ferrari", 1, 1, 1, ScheduleRoles.SharedDrive),
                Stint(1970, "ferrari", 2, 4, 2, ScheduleRoles.Substitute),
            ],
            driver.Stints);
    }

    [Fact]
    public void MidSeasonChangeRecordsTheRoundAndANewSeasonDoesNot()
    {
        var report = Build(
            races: [Race(1962, 1), Race(1962, 2), Race(1962, 5), Race(1963, 1)],
            results:
            [
                Row(1962, 1, "clark", "lotus"),
                Row(1962, 2, "clark", "lotus"),
                Row(1962, 5, "clark", "brm"),
                Row(1963, 1, "clark", "lotus"),
                Row(1962, 2, "clark", "cooper", shared: true),
            ]);

        Assert.Equal(
            [
                new TeamChangeEvent(1962, "clark", "lotus", "brm", 5),
                new TeamChangeEvent(1963, "clark", "brm", "lotus", null),
            ],
            report.TeamChanges);
    }

    [Fact]
    public void ThreeStartsAreSubstituteAndFourAreRace()
    {
        var races = new List<HistoricalRace>();
        var results = new List<HistoricalResult>();
        for (var round = 1; round <= 4; round++)
        {
            races.Add(Race(1954, round));
            results.Add(Row(1954, round, "regular", "maserati"));
            if (round <= 3)
            {
                results.Add(Row(1954, round, "guest", "cooper"));
            }
        }

        results.Add(Row(1954, 1, "guest", "cooper", "Did not qualify"));
        results.Add(Row(1954, 2, "guest", "cooper", "Withdrew"));
        var report = Build(races, results);
        var regular = Assert.Single(Assert.Single(report.Drivers, driver => driver.DriverId == "regular").Stints);
        var guest = Assert.Single(Assert.Single(report.Drivers, driver => driver.DriverId == "guest").Stints);
        Assert.Equal(Stint(1954, "maserati", 1, 4, 4, ScheduleRoles.Race), regular);
        Assert.Equal(Stint(1954, "cooper", 1, 3, 3, ScheduleRoles.Substitute), guest);
    }

    [Fact]
    public void PoolEntryUsesTheDebutSplitAndTheAgeFloor()
    {
        Assert.Equal(2, PeopleScheduleRules.ResolveLeadYears(2.5m, 1979));
        Assert.Equal(3, PeopleScheduleRules.ResolveLeadYears(2.5m, 1980));
        Assert.Equal(4, PeopleScheduleRules.ResolveLeadYears(4m, 1950));
        Assert.Equal(4, PeopleScheduleRules.ResolveLeadYears(4m, 1990));
        Assert.False(PeopleScheduleRules.IsValidLead(-1m));
        Assert.False(PeopleScheduleRules.IsValidLead(2.25m));
        Assert.True(PeopleScheduleRules.IsValidLead(0m));
        Assert.True(PeopleScheduleRules.IsValidLead(2.5m));

        Assert.Equal(1977, PeopleScheduleRules.EntryYear(1979, 1950, 2.5m));
        Assert.Equal(1977, PeopleScheduleRules.EntryYear(1980, 1960, 2.5m));
        Assert.Equal(1979, PeopleScheduleRules.EntryYear(1982, null, 2.5m));
        Assert.Equal(2020, PeopleScheduleRules.EntryYear(2018, 2003, 2.5m));
        Assert.Equal(1952, PeopleScheduleRules.EntryYear(1954, 1929, 2.5m));
        Assert.Equal(1975, PeopleScheduleRules.EntryYear(1979, 1950, 4m));
        Assert.Null(PeopleScheduleRules.BornYear(null));
        Assert.Null(PeopleScheduleRules.BornYear("nope"));
        Assert.Equal(1929, PeopleScheduleRules.BornYear("1929-09-17"));

        var report = Build(
            races: [Race(1979, 1), Race(1980, 1), Race(2018, 1)],
            results:
            [
                Row(1979, 1, "early", "ferrari"),
                Row(1980, 1, "late", "williams"),
                Row(2018, 1, "young", "red_bull"),
            ],
            drivers:
            [
                Person("early", "Early", "Driver", "1950-01-01"),
                Person("late", "Late", "Driver", "1960-01-01"),
                Person("young", "Young", "Driver", "2003-06-01"),
                Person("nobirth", "No", "Birth", null),
            ],
            lead: 4m);

        Assert.Equal(1975, One(report, "early").PoolEntryYear);
        Assert.Equal(1977, One(report, "late").PoolEntryYear);
        Assert.Equal(2020, One(report, "young").PoolEntryYear);
        Assert.DoesNotContain(report.Drivers, driver => driver.DriverId == "nobirth");
    }

    [Fact]
    public void IndianapolisRoundsAreExcludedFromStintsAndListedSeparately()
    {
        var report = Build(
            races: [Race(1950, 1), Race(1950, 3, indy: true), Race(1961, 1)],
            results:
            [
                Row(1950, 3, "parsons", "kurtis_kraft"),
                Row(1950, 3, "parsons", "kurtis_kraft", shared: true),
                Row(1950, 1, "parsons", "kurtis_kraft", "Did not start"),
                Row(1961, 1, "later", "ferrari"),
            ],
            drivers: [Person("parsons", "Johnnie", "Parsons", "1918-07-04", "American")]);

        var parsons = One(report, "parsons");
        Assert.Empty(parsons.Stints);
        Assert.Null(parsons.FirstSeason);
        Assert.Null(parsons.PoolEntryYear);
        Assert.Equal(1918, parsons.Born);
        Assert.Equal("American", parsons.Nationality);
        var indy = Assert.Single(parsons.Indianapolis500);
        Assert.Equal(new IndianapolisAppearance(1950, 3, "kurtis_kraft"), indy);
        var later = One(report, "later");
        Assert.Equal(Stint(1961, "ferrari", 1, 1, 1, ScheduleRoles.Substitute), Assert.Single(later.Stints));
        Assert.Empty(later.Indianapolis500);
        Assert.Empty(report.TeamChanges);
    }

    [Fact]
    public void DuplicateIdsAreListedAndNotMerged()
    {
        var report = Build(
            races: [Race(1951, 1)],
            results:
            [
                Row(1951, 1, "jose_gonzalez", "ferrari"),
                Row(1951, 1, "jose_dup", "ferrari", number: "2"),
            ],
            drivers:
            [
                Person("jose_gonzalez", "José", "González", "1920-10-05"),
                Person("jose_dup", "Jose", "Gonzalez", "1920-10-05"),
                Person("brian", "Brian", "Shawe-Taylor", "1915-01-28"),
                Person("brian_dup", "Brian", "Shawe Taylor", "1915-01-28"),
                Person("twins_a", "Graham", "Hill", "1929-02-15"),
                Person("twins_b", "Phil", "Hill", "1929-02-15"),
                Person("other", "Damon", "Hill", "1960-09-17"),
            ]);

        Assert.Equal(2, report.Drivers.Count);
        Assert.Equal(
            [
                new DuplicateDriverSuspect("brian", "brian_dup", DuplicateSuspectReasons.SameName),
                new DuplicateDriverSuspect("jose_dup", "jose_gonzalez", DuplicateSuspectReasons.SameName),
                new DuplicateDriverSuspect("twins_a", "twins_b", DuplicateSuspectReasons.SameBirthAndFamily),
            ],
            report.DuplicateSuspects);
        Assert.Equal(["jose_dup", "jose_gonzalez"], report.SingleStartDriverIds);
    }

    [Fact]
    public void BuilderOutputIsByteIdenticalWhenInputOrderChanges()
    {
        var races = new List<HistoricalRace> { Race(1954, 1), Race(1954, 2), Race(1955, 1), Race(1950, 2, indy: true) };
        var results = new List<HistoricalResult>
        {
            Row(1954, 1, "moss", "maserati"),
            Row(1954, 2, "moss", "maserati"),
            Row(1955, 1, "moss", "mercedes"),
            Row(1950, 2, "parsons", "kurtis_kraft"),
            Row(1954, 1, "fry", "maserati", shared: true),
            Row(1954, 1, "shawe", "maserati", shared: true, number: "10"),
        };
        var drivers = new List<HistoricalDriver>
        {
            Person("moss", "Stirling", "Moss", "1929-09-17"),
            Person("parsons", "Johnnie", "Parsons", "1918-07-04"),
            Person("fry", "Joe", "Fry", "1915-10-26"),
            Person("shawe", "Brian", "Shawe-Taylor", "1915-01-28"),
        };

        var forward = Json(Build(races, results, drivers));
        races.Reverse();
        results.Reverse();
        drivers.Reverse();
        var backward = Json(Build(races, results, drivers));
        Assert.Equal(forward, backward);
    }

    [Fact]
    public void ScheduleOnTheFixtureCoversTheEdgeCases()
    {
        var report = LoadFixture();
        var moss = One(report, "moss");
        Assert.Equal(1929, moss.Born);
        Assert.Equal("British", moss.Nationality);
        Assert.Equal(1954, moss.FirstSeason);
        Assert.Equal(1955, moss.LastSeason);
        Assert.Equal(1952, moss.PoolEntryYear);
        Assert.Equal(
            [
                Stint(1954, "maserati", 1, 4, 4, ScheduleRoles.Race),
                Stint(1955, "mercedes", 1, 1, 1, ScheduleRoles.Substitute),
            ],
            moss.Stints);

        Assert.Equal(Stint(1956, "maserati", 1, 3, 3, ScheduleRoles.Substitute), Assert.Single(One(report, "three").Stints));
        Assert.Equal(ScheduleRoles.SharedDrive, Assert.Single(One(report, "fry").Stints).Role);
        Assert.Equal(ScheduleRoles.SharedDrive, Assert.Single(One(report, "shawe_taylor").Stints).Role);

        var returner = One(report, "returner");
        Assert.Equal(
            [
                Stint(1970, "alfa", 1, 3, 2, ScheduleRoles.Substitute),
                Stint(1970, "ferrari", 2, 2, 1, ScheduleRoles.Substitute),
            ],
            returner.Stints);
        Assert.Equal(
            [
                new TeamChangeEvent(1955, "moss", "maserati", "mercedes", null),
                new TeamChangeEvent(1962, "clark", "lotus", "brm", 3),
                new TeamChangeEvent(1970, "returner", "alfa", "ferrari", 2),
                new TeamChangeEvent(1970, "returner", "ferrari", "alfa", 3),
            ],
            report.TeamChanges);

        Assert.Equal(1977, One(report, "early").PoolEntryYear);
        Assert.Equal(1977, One(report, "late").PoolEntryYear);
        Assert.Equal(1979, One(report, "nobirth").PoolEntryYear);
        Assert.Null(One(report, "nobirth").Born);
        Assert.Equal(2020, One(report, "young").PoolEntryYear);
        Assert.Equal(1960, One(report, "clark").PoolEntryYear);

        var parsons = One(report, "parsons");
        Assert.Empty(parsons.Stints);
        Assert.Null(parsons.PoolEntryYear);
        Assert.Equal(new IndianapolisAppearance(1950, 3, "kurtis_kraft"), Assert.Single(parsons.Indianapolis500));
        Assert.DoesNotContain(report.Drivers, driver => driver.DriverId == "dnq_driver");
        Assert.DoesNotContain(report.Drivers, driver => driver.DriverId == "jose_gonzalez");
        Assert.Equal(
            [new DuplicateDriverSuspect("jose_dup", "jose_gonzalez", DuplicateSuspectReasons.SameName)],
            report.DuplicateSuspects);
        Assert.Equal(
            ["early", "fry", "guest", "late", "nobirth", "shawe_taylor", "young"],
            report.SingleStartDriverIds);
        Assert.Equal(2.5m, report.PoolLeadYears);
        Assert.Equal(PeopleScheduleRules.PoolDebutSplitSeason, report.DebutSplitSeason);
    }

    [Fact]
    public async Task ScheduleCommandWritesDeterministicReports()
    {
        var cache = Materialize();
        var root = Directory.GetParent(cache)!.FullName;
        try
        {
            var stderr = new StringWriter();
            var stdout = new StringWriter();
            var code = await PipelineCommands.ExecuteAsync(["schedule", "--cache", cache], stdout, stderr);
            Assert.Equal(0, code);
            Assert.Equal(string.Empty, stderr.ToString());
            var lines = JolpicaFixtures.Lines(stdout);
            Assert.Equal(
            [
                "people schedule",
                "drivers: 12",
                "stints: 14",
                "shared-drive stints: 2",
                "team changes: 4",
                "mid-season changes: 3",
                "duplicate suspects: 1",
                "single-start drivers: 7",
                "indianapolis 500 starts: 1",
                "indianapolis-only drivers: 1",
                "pool lead years: 2.5 (2 before 1980, 3 from 1980)",
                "decade 1950s: drivers 4, stints 5",
                "decade 1960s: drivers 2, stints 3",
                "decade 1970s: drivers 2, stints 3",
                "decade 1980s: drivers 2, stints 2",
                "decade 2010s: drivers 1, stints 1",
            ],
            lines[..^2]);
            Assert.EndsWith("people_schedule.md", lines[^2], StringComparison.Ordinal);
            Assert.EndsWith("people_schedule.json", lines[^1], StringComparison.Ordinal);

            var jsonPath = Path.Combine(root, "reports", "people_schedule.json");
            var markdownPath = Path.Combine(root, "reports", "people_schedule.md");
            var firstJson = File.ReadAllBytes(jsonPath);
            var firstMarkdown = File.ReadAllBytes(markdownPath);
            var again = await PipelineCommands.ExecuteAsync(["schedule", "--cache", cache], new StringWriter(), stderr);
            Assert.Equal(0, again);
            Assert.Equal(firstJson, File.ReadAllBytes(jsonPath));
            Assert.Equal(firstMarkdown, File.ReadAllBytes(markdownPath));

            var report = JsonSerializer.Deserialize<PeopleScheduleReport>(File.ReadAllText(jsonPath), HistoricalJson.Options);
            Assert.NotNull(report);
            Assert.Equal(12, report.Drivers.Count);
            Assert.Contains("### moss", File.ReadAllText(markdownPath), StringComparison.Ordinal);
            Assert.Contains("shared_drive", File.ReadAllText(jsonPath), StringComparison.Ordinal);
            Assert.Contains("ROADMAP open question 4", File.ReadAllText(markdownPath), StringComparison.Ordinal);
            Assert.Contains("jose_dup", File.ReadAllText(markdownPath), StringComparison.Ordinal);
            Assert.Contains("indianapolis", File.ReadAllText(markdownPath), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExplicitWholeYearLeadReplacesTheDebutSplit()
    {
        var cache = Materialize();
        var root = Directory.GetParent(cache)!.FullName;
        try
        {
            var stderr = new StringWriter();
            var code = await PipelineCommands.ExecuteAsync(
                ["schedule", "--cache", cache, "--pool-lead-years", "4"],
                new StringWriter(),
                stderr);
            Assert.Equal(0, code);
            Assert.Equal(string.Empty, stderr.ToString());
            var report = JsonSerializer.Deserialize<PeopleScheduleReport>(
                File.ReadAllText(Path.Combine(root, "reports", "people_schedule.json")),
                HistoricalJson.Options);
            Assert.NotNull(report);
            Assert.Equal(4m, report.PoolLeadYears);
            Assert.Equal(1950, One(report, "moss").PoolEntryYear);
            Assert.Equal(1975, One(report, "early").PoolEntryYear);
            Assert.Equal(1978, One(report, "nobirth").PoolEntryYear);
            Assert.Equal(2020, One(report, "young").PoolEntryYear);
            Assert.Contains("pool lead years: 4 (whole years for every debut)", File.ReadAllText(Path.Combine(root, "reports", "people_schedule.md")), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ScheduleRejectsAMissingCacheAndABadLead()
    {
        var missingRoot = Path.Combine(Path.GetTempPath(), "paddock-schedule-missing-" + Guid.NewGuid().ToString("N"));
        var missing = Path.Combine(missingRoot, "jolpica");
        Directory.CreateDirectory(missingRoot);
        try
        {
            var stderr = new StringWriter();
            var missingCode = await PipelineCommands.ExecuteAsync(["schedule", "--cache", missing], new StringWriter(), stderr);
            Assert.Equal(1, missingCode);
            Assert.Contains("Raw cache not found", stderr.ToString(), StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(missingRoot, "reports")));
        }
        finally
        {
            Directory.Delete(missingRoot, recursive: true);
        }

        var cache = Materialize();
        var root = Directory.GetParent(cache)!.FullName;
        try
        {
            var bad = new StringWriter();
            var code = await PipelineCommands.ExecuteAsync(
                ["schedule", "--cache", cache, "--pool-lead-years", "2.25"],
                new StringWriter(),
                bad);
            Assert.Equal(1, code);
            Assert.Contains("Invalid --pool-lead-years", bad.ToString(), StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(root, "reports")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AResultWithoutARaceFails()
    {
        var error = Assert.Throws<InvalidDataException>(() => Build([Race(1950, 1)], [Row(1950, 2, "moss", "alfa")]));
        Assert.Contains("1950/2 moss", error.Message, StringComparison.Ordinal);
    }

    private static PeopleScheduleReport LoadFixture()
    {
        var cache = Materialize();
        try
        {
            return PeopleScheduleBuilder.Build(
                JolpicaNormalizer.ReadRaw(JolpicaCache.Raw(cache)),
                PeopleScheduleRules.DefaultPoolLeadYears);
        }
        finally
        {
            Directory.Delete(Directory.GetParent(cache)!.FullName, recursive: true);
        }
    }

    private static string Materialize()
    {
        var root = Path.Combine(Path.GetTempPath(), "paddock-schedule-" + Guid.NewGuid().ToString("N"));
        var cache = Path.Combine(root, "jolpica");
        Copy(cache, "drivers.json", "raw", "drivers", "offset-0.json");
        Copy(cache, "races.json", "raw", "races", "offset-0.json");
        Copy(cache, "results.json", "raw", "results", "offset-0.json");
        return cache;
    }

    private static void Copy(string cache, string fixture, params string[] relative)
    {
        var source = Path.Combine(RepoPaths.Root(), "tests", "Paddock.Tests", "Fixtures", "schedule", fixture);
        var destination = Path.Combine(new[] { cache }.Concat(relative).ToArray());
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination);
    }

    private static PeopleScheduleReport Build(
        IReadOnlyList<HistoricalRace> races,
        IReadOnlyList<HistoricalResult> results,
        IReadOnlyList<HistoricalDriver>? drivers = null,
        decimal? lead = null)
    {
        return PeopleScheduleBuilder.Build(
            new NormalizedHistoricalData(
                new HistoricalDriversDocument(HistoricalSchema.Version, drivers ?? []),
                new HistoricalConstructorsDocument(HistoricalSchema.Version, []),
                new HistoricalCircuitsDocument(HistoricalSchema.Version, []),
                new HistoricalRacesDocument(HistoricalSchema.Version, races),
                new HistoricalResultsDocument(HistoricalSchema.Version, results),
                new HistoricalQualifyingDocument(HistoricalSchema.Version, [])),
            lead ?? PeopleScheduleRules.DefaultPoolLeadYears);
    }

    private static string Json(PeopleScheduleReport report)
    {
        var json = JsonSerializer.Serialize(report, HistoricalJson.Options);
        if (!json.EndsWith('\n'))
        {
            json += "\n";
        }

        return json;
    }

    private static ScheduledDriver One(PeopleScheduleReport report, string driverId)
    {
        return Assert.Single(report.Drivers, driver => driver.DriverId == driverId);
    }

    private static DriverStint Stint(int season, string constructor, int first, int last, int starts, string role)
    {
        return new DriverStint(season, constructor, first, last, starts, role);
    }

    private static HistoricalDriver Person(string id, string given, string family, string? born, string? nationality = "Test")
    {
        return new HistoricalDriver(id, given, family, born, nationality, null, null, "http://example.test/" + id);
    }

    private static HistoricalRace Race(int season, int round, bool indy = false)
    {
        return new HistoricalRace(
            season,
            round,
            indy ? "Indianapolis 500" : "Grand Prix",
            indy ? "indianapolis" : "silverstone",
            "1950-01-01",
            null,
            "http://example.test",
            indy);
    }

    private static HistoricalResult Row(
        int season,
        int round,
        string driver,
        string constructor,
        string status = "Finished",
        bool shared = false,
        string number = "1")
    {
        var classified = status == "Finished";
        return new HistoricalResult(
            season,
            round,
            driver,
            constructor,
            shared ? "10" : number,
            classified ? 1 : 20,
            classified ? "1" : "R",
            status,
            0m,
            1,
            1,
            null,
            null,
            classified,
            false,
            shared);
    }
}
