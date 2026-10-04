using System.Text.Json;
using Paddock.Data.Historical;
using Paddock.DataPipeline;
using Paddock.SimRunner;

namespace Paddock.Tests.SimRunner;

/// <summary>
/// The ratings model writes <c>reports/ratings.json</c>; a career must read it. Before the fix nothing did, so every real
/// driver was reported as "no rating" and got a flat stand-in. These tests use a synthetic cache in a temp directory.
/// </summary>
public sealed class InitWorldRatingsTests : IDisposable
{
    private const string GapPrefix = "Real drivers with no rating";

    private readonly string _root = Directory.CreateTempSubdirectory("paddock-init-ratings-").FullName;

    public InitWorldRatingsTests()
    {
        CopyDirectory(Path.Combine(JolpicaCache.FindRepoRoot(), "data", "authored"), Path.Combine(_root, "authored"));
        Write(
            Path.Combine("cache", "jolpica", "normalized", "drivers.json"),
            new HistoricalDriversDocument(
                HistoricalSchema.Version,
                [
                    Driver("rated_racer", "1920-01-01"),
                    Driver("unrated_racer", "1921-01-01"),
                    Driver("rated_rookie", "1932-01-01"),
                ]));
        Write(
            Path.Combine("cache", "reports", "people_schedule.json"),
            new PeopleScheduleReport(
                PeopleScheduleRules.DefaultPoolLeadYears,
                PeopleScheduleRules.PoolDebutSplitSeason,
                [
                    Scheduled("rated_racer", 1920, firstSeason: 1950, poolEntry: 1948, seats: [new DriverStint(1950, "ferrari", 1, 7, 7, ScheduleRoles.Race)]),
                    Scheduled("unrated_racer", 1921, firstSeason: 1950, poolEntry: 1948, seats: [new DriverStint(1950, "ferrari", 1, 7, 7, ScheduleRoles.Race)]),
                    Scheduled("rated_rookie", 1932, firstSeason: 1953, poolEntry: 1950, seats: []),
                ],
                [],
                [],
                []));
        Write(
            Path.Combine("cache", "reports", "ratings.json"),
            Ratings(
                Entry("rated_racer", (1950, 90), (1951, 95)),
                Entry("rated_rookie", (1953, 60), (1954, 70))));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void TheDefaultCacheFilesGiveTheRatedDriversTheirRatings()
    {
        var lines = Run("init-world", "--preset", "MostHistorical", "--year", "1950", "--seed", "7", "--data-root", _root);

        var gap = Assert.Single(lines, line => line.Contains(GapPrefix, StringComparison.Ordinal));
        Assert.Contains("1 [unrated_racer]", gap, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutARatingsFileEveryRealDriverStaysUnrated()
    {
        File.Delete(Path.Combine(_root, "cache", "reports", "ratings.json"));

        var lines = Run("init-world", "--preset", "MostHistorical", "--year", "1950", "--seed", "7", "--data-root", _root);

        var gap = Assert.Single(lines, line => line.Contains(GapPrefix, StringComparison.Ordinal));
        Assert.Contains("[rated_racer, rated_rookie, unrated_racer]", gap, StringComparison.Ordinal);
    }

    [Fact]
    public void RatingsChangeTheWorldTheCareerStartsFrom()
    {
        var args = new[] { "init-world", "--preset", "MostHistorical", "--year", "1950", "--seed", "7", "--data-root", _root };
        var rated = Run(args);
        File.Delete(Path.Combine(_root, "cache", "reports", "ratings.json"));
        var unrated = Run(args);

        Assert.NotEqual(rated[^1], unrated[^1]);
    }

    [Fact]
    public void AnExplicitPairUsesOnlyTheRatingsNamedByRatings()
    {
        var reports = Path.Combine(_root, "cache", "reports");
        var drivers = Path.Combine(_root, "cache", "jolpica", "normalized", "drivers.json");
        string[] pair = ["--schedule", Path.Combine(reports, "people_schedule.json"), "--drivers", drivers];

        var implicitRatings = Run(["init-world", "--preset", "MostHistorical", "--year", "1950", "--seed", "7", "--data-root", _root, .. pair]);
        var named = Run(["init-world", "--preset", "MostHistorical", "--year", "1950", "--seed", "7", "--data-root", _root, .. pair, "--ratings", Path.Combine(reports, "ratings.json")]);

        Assert.Contains("[rated_racer, rated_rookie, unrated_racer]", Assert.Single(implicitRatings, line => line.Contains(GapPrefix, StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.Contains("1 [unrated_racer]", Assert.Single(named, line => line.Contains(GapPrefix, StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    [Fact]
    public void ANamedRatingsFileThatIsMissingIsAnError()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = InitWorldCommand.Execute(
            ["init-world", "--preset", "MostHistorical", "--year", "1950", "--seed", "7", "--data-root", _root, "--ratings", Path.Combine(_root, "nope.json")],
            stdout,
            stderr);

        Assert.Equal(1, code);
        Assert.NotEqual(string.Empty, stderr.ToString());
    }

    [Fact]
    public void ASaveCannotBeResumedAfterTheRatingsChanged()
    {
        var save = Path.Combine(_root, "career.paddock");
        var run = new StringWriter();
        Assert.Equal(0, RunCommand.Execute(["run", "--preset", "MostHistorical", "--from", "1950", "--to", "1950", "--seed", "7", "--data-root", _root, "--save", save], run, new StringWriter()));

        var again = new StringWriter();
        Assert.Equal(0, RunCommand.Execute(["run", "--resume", save, "--to", "1951", "--data-root", _root], again, new StringWriter()));

        Write(Path.Combine("cache", "reports", "ratings.json"), Ratings(Entry("rated_racer", (1950, 40))));
        var stderr = new StringWriter();
        var code = RunCommand.Execute(["run", "--resume", save, "--to", "1951", "--data-root", _root], new StringWriter(), stderr);

        Assert.Equal(1, code);
        Assert.Contains("changed since this save was written", stderr.ToString(), StringComparison.Ordinal);
    }

    private static HistoricalDriver Driver(string id, string born) =>
        new(id, id, "Test", born, "Italian", null, null, null);

    private static ScheduledDriver Scheduled(string id, int born, int firstSeason, int poolEntry, DriverStint[] seats) =>
        new(id, born, "Italian", firstSeason, firstSeason + 5, poolEntry, seats, []);

    private static DriverRatingEntry Entry(string id, params (int Season, int Overall)[] seasons) =>
        new(1, id, id, seasons[^1].Overall, 3.0, 0.5, 0.0, null, seasons.Select(s => new SeasonRating(s.Season, 0.0, s.Overall)).ToArray());

    private static RatingsReportDocument Ratings(params DriverRatingEntry[] entries) =>
        new(
            new RatingsFitSummary(0, 0, 0, 0, 0, 0, true, 0, 0, 0, 1950, 1950, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, 0),
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            entries);

    private void Write<T>(string relative, T document)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(document, HistoricalJson.Options));
    }

    private static string[] Run(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = InitWorldCommand.Execute(args, stdout, stderr);
        Assert.Equal(string.Empty, stderr.ToString());
        Assert.Equal(0, code);
        return stdout.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static void CopyDirectory(string from, string to)
    {
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}
