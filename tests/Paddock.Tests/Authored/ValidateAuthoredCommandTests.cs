using Paddock.DataPipeline;

namespace Paddock.Tests.Authored;

public class ValidateAuthoredCommandTests
{
    [Fact]
    public void RepoData_ReportsTheKnownAuthorFailures()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var dataRoot = Path.Combine(RepoPaths.Root(), "data");

        var code = ValidateAuthoredCommand.Execute(
            ["validate-authored", "--data-root", dataRoot],
            stdout,
            stderr);

        Assert.Equal(1, code);
        Assert.Equal(string.Empty, stderr.ToString());
        Assert.Equal(
            [
                "authored data: 2 errors",
                "catalog dimensions: 41",
                "timeline periods: 232",
                "other series ideas: 23",
                "circuits: 78",
                "layouts: 156",
                "race map entries: 1172",
                "technologies: 40",
                "engine entries: 1287",
                "constructors: 213",
                "lineages: 10",
                "founder organizations: 50",
                "staff: 182",
                "error: lineage 'jordan-aston-martin' entries 'mf1' (2006) and 'spyker_mf1' (2006) overlap",
                "error: organization 'williams' founded 1977 is after constructor 'williams' entry 1975-1976",
            ],
            Lines(stdout));
    }

    [Fact]
    public void BrokenFixture_PrintsTheReportAndExitsNonZero()
    {
        using var fixture = new TempAuthoredData(
            catalog: AuthoredSamples.ValueListCatalog,
            timeline: AuthoredSamples.BadEnumTimeline);
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = ValidateAuthoredCommand.Execute(
            ["validate-authored", "--data-root", fixture.Root],
            stdout,
            stderr);

        Assert.Equal(1, code);
        Assert.Equal(string.Empty, stderr.ToString());
        Assert.Contains(
            "error: dimension 'points_scale' value 'nope' in 1950-open is not in the catalog value list",
            Lines(stdout));
    }

    [Fact]
    public void UnknownCommand_FailsWithoutAReport()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = ValidateAuthoredCommand.Execute(["fetch"], stdout, stderr);

        Assert.Equal(1, code);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.Contains("validate-authored", stderr.ToString(), StringComparison.Ordinal);
    }

    private static string[] Lines(StringWriter writer) =>
        writer.ToString().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
}
