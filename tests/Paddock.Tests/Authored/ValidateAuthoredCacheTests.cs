using Paddock.DataPipeline;

namespace Paddock.Tests.Authored;

public class ValidateAuthoredCacheTests
{
    [Fact]
    public void DefaultCacheDirectory_IsTheNormalizedJolpicaCache()
    {
        Assert.Equal(
            Path.Combine(RepoPaths.Root(), "data", "cache", "jolpica", "normalized"),
            AuthoredCacheCrossCheck.DefaultCacheDirectory());
    }

    [Fact]
    public void WithoutTheFlag_ThePassingFixtureDoesNotReadTheCache()
    {
        using var fixture = CacheCrossCheckFixture.Materialize();
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = ValidateAuthoredCommand.Execute(
            ["validate-authored", "--data-root", fixture.DataRoot],
            stdout,
            stderr);

        Assert.Equal(0, code);
        Assert.Equal(string.Empty, stderr.ToString());
        Assert.DoesNotContain("cache", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PassingFixture_ReportsZeroOnEveryCheck()
    {
        using var fixture = CacheCrossCheckFixture.Materialize();
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = Run(fixture, stdout, stderr);

        Assert.Equal(0, code);
        Assert.Equal(string.Empty, stderr.ToString());
        Assert.Equal(
            [
                "authored data: ok",
                "catalog dimensions: 1",
                "timeline periods: 1",
                "other series ideas: 0",
                "circuits: 1",
                "layouts: 1",
                "race map entries: 2",
                "technologies: 2",
                "engine entries: 3",
                "constructors: 3",
                "lineages: 1",
                "founder organizations: 1",
                "staff: 1",
                "era catalog dimensions: 1",
                "era timeline periods: 1",
                "cpi years: 77",
                "track geometries: 0",
                "banned rules in the default list: 0",
                "cache cross-check: ok",
                "engine seasons: 2 checked, 0 gaps",
                "constructors: 3 checked, 0 unknown",
                "drivers: 2 checked, 0 unknown",
                "races: 2 mapped, 2 in cache, 0 missing",
            ],
            Lines(stdout));
    }

    [Fact]
    public void EngineSeasonGap_ListsTheConstructorAndSeason()
    {
        using var fixture = CacheCrossCheckFixture.Materialize("engine-gap");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = Run(fixture, stdout, stderr);

        Assert.Equal(1, code);
        Assert.Equal(string.Empty, stderr.ToString());
        var lines = Lines(stdout);
        Assert.Contains("authored data: ok", lines);
        Assert.Contains("cache cross-check: 1 error", lines);
        Assert.Contains("engine seasons: 2 checked, 1 gap", lines);
        Assert.Contains("constructors: 2 checked, 0 unknown", lines);
        Assert.Contains("error [engine-season-gap]: ferrari 1950", lines);
    }

    [Fact]
    public void UnknownConstructor_NamesTheIdAndTheFiles()
    {
        using var fixture = CacheCrossCheckFixture.Materialize("unknown-constructor");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = Run(fixture, stdout, stderr);

        Assert.Equal(1, code);
        Assert.Equal(string.Empty, stderr.ToString());
        var lines = Lines(stdout);
        Assert.Contains("authored data: ok", lines);
        Assert.Contains("cache cross-check: 1 error", lines);
        Assert.Contains("constructors: 4 checked, 1 unknown", lines);
        Assert.Contains("engine seasons: 2 checked, 0 gaps", lines);
        Assert.Contains("error [unknown-jolpica-constructor]: ghost (engines.json, staff.json)", lines);
    }

    [Fact]
    public void EmptyRepoAllowList_DoesNotExemptAFixtureConstructor()
    {
        using var fixture = CacheCrossCheckFixture.Materialize();
        File.Copy(
            Path.Combine(RepoPaths.Root(), "data", "authored", "teams", "non_jolpica_constructors.json"),
            Path.Combine(fixture.DataRoot, "authored", "teams", "non_jolpica_constructors.json"),
            overwrite: true);
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = Run(fixture, stdout, stderr);

        Assert.Equal(1, code);
        Assert.Contains("constructors: 3 checked, 1 unknown", Lines(stdout));
        Assert.Contains("error [unknown-jolpica-constructor]: folded_team (engines.json)", Lines(stdout));
    }

    [Fact]
    public void UnknownDriver_SkipsNullDriverIds()
    {
        using var fixture = CacheCrossCheckFixture.Materialize("unknown-driver");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = Run(fixture, stdout, stderr);

        Assert.Equal(1, code);
        Assert.Equal(string.Empty, stderr.ToString());
        var lines = Lines(stdout);
        Assert.Contains("authored data: ok", lines);
        Assert.Contains("drivers: 3 checked, 1 unknown", lines);
        Assert.Contains("error [unknown-jolpica-driver]: ghost (fixture_list)", lines);
    }

    [Fact]
    public void RaceMap_ReportsBothDirections()
    {
        using var fixture = CacheCrossCheckFixture.Materialize("race-map");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = Run(fixture, stdout, stderr);

        Assert.Equal(1, code);
        Assert.Equal(string.Empty, stderr.ToString());
        var lines = Lines(stdout);
        Assert.Contains("authored data: ok", lines);
        Assert.Contains("cache cross-check: 2 errors", lines);
        Assert.Contains("races: 2 mapped, 2 in cache, 2 missing", lines);
        Assert.Contains("error [unknown-jolpica-race]: race 1950 round 9 is not in the Jolpica cache", lines);
        Assert.Contains("error [unmapped-jolpica-race]: Jolpica race 1950 round 2 has no layout", lines);
    }

    [Fact]
    public void TwoChecks_ReportEveryError()
    {
        using var fixture = CacheCrossCheckFixture.Materialize("engine-gap", "unknown-driver");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = Run(fixture, stdout, stderr);

        Assert.Equal(1, code);
        var lines = Lines(stdout);
        Assert.Contains("authored data: ok", lines);
        Assert.Contains("cache cross-check: 2 errors", lines);
        Assert.Contains("error [engine-season-gap]: ferrari 1950", lines);
        Assert.Contains("error [unknown-jolpica-driver]: ghost (fixture_list)", lines);
    }

    [Fact]
    public void MissingCache_ExitsNonZeroWithThePath()
    {
        using var fixture = CacheCrossCheckFixture.Materialize();
        var missing = Path.Combine(fixture.Root, "missing-cache");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = ValidateAuthoredCommand.Execute(
            ["validate-authored", "--data-root", fixture.DataRoot, "--with-cache", missing],
            stdout,
            stderr);

        Assert.Equal(1, code);
        Assert.Equal(string.Empty, stderr.ToString());
        var lines = Lines(stdout);
        Assert.Contains("authored data: ok", lines);
        Assert.Contains("cache: missing " + Path.GetFullPath(missing), lines);
        Assert.DoesNotContain(lines, line => line.StartsWith("engine seasons:", StringComparison.Ordinal));
    }

    [Fact]
    public void CacheDirectoryWithoutTheNormalizedFiles_NamesTheMissingFiles()
    {
        using var fixture = CacheCrossCheckFixture.Materialize();
        var empty = Path.Combine(fixture.Root, "empty-cache");
        Directory.CreateDirectory(empty);
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = ValidateAuthoredCommand.Execute(
            ["validate-authored", "--with-cache", empty, "--data-root", fixture.DataRoot],
            stdout,
            stderr);

        Assert.Equal(1, code);
        Assert.Contains(
            "cache: missing drivers.json, constructors.json, races.json, results.json in " + Path.GetFullPath(empty),
            Lines(stdout));
    }

    [Fact]
    public void WrongSchema_FailsBeforeTheChecks()
    {
        using var fixture = CacheCrossCheckFixture.Materialize("bad-schema");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = Run(fixture, stdout, stderr);

        Assert.Equal(1, code);
        var lines = Lines(stdout);
        Assert.Contains("cache cross-check: load failed", lines);
        Assert.Contains("drivers.json schemaVersion is 2, expected 1", lines);
        Assert.DoesNotContain(lines, line => line.StartsWith("engine seasons:", StringComparison.Ordinal));
    }

    [Fact]
    public void AuthoredErrors_StillRunTheCacheChecks()
    {
        using var fixture = CacheCrossCheckFixture.Materialize("authored-error");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = Run(fixture, stdout, stderr);

        Assert.Equal(1, code);
        var lines = Lines(stdout);
        Assert.Contains(
            "error: dimension 'points_scale' value 'nope' in 1950-open is not in the catalog value list",
            lines);
        Assert.Contains("cache cross-check: ok", lines);
    }

    [Fact]
    public void DuplicateWithCache_FailsWithoutAReport()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = ValidateAuthoredCommand.Execute(
            ["validate-authored", "--with-cache", "a", "--with-cache", "b"],
            stdout,
            stderr);

        Assert.Equal(1, code);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.Contains("Duplicate --with-cache.", stderr.ToString(), StringComparison.Ordinal);
    }

    private static int Run(CacheCrossCheckFixture fixture, StringWriter stdout, StringWriter stderr)
    {
        return ValidateAuthoredCommand.Execute(
            ["validate-authored", "--data-root", fixture.DataRoot, "--with-cache", fixture.CacheDirectory],
            stdout,
            stderr);
    }

    private static string[] Lines(StringWriter writer) =>
        writer.ToString().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
}

internal sealed class CacheCrossCheckFixture : IDisposable
{
    private CacheCrossCheckFixture(string root)
    {
        Root = root;
        DataRoot = Path.Combine(root, "data");
        CacheDirectory = Path.Combine(root, "cache");
    }

    public string Root { get; }

    public string DataRoot { get; }

    public string CacheDirectory { get; }

    public static CacheCrossCheckFixture Materialize(params string[] overlays)
    {
        var root = Path.Combine(Path.GetTempPath(), "paddock-cache-cross-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(RepoPaths.Root(), "tests", "Paddock.Tests", "Fixtures", "cache-cross-check");
        Copy(Path.Combine(source, "pass"), root);
        foreach (var overlay in overlays)
        {
            Copy(Path.Combine(source, overlay), root);
        }

        return new CacheCrossCheckFixture(root);
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);

    private static void Copy(string source, string destination)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            var directory = Path.GetDirectoryName(target);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(file, target, overwrite: true);
        }
    }
}
