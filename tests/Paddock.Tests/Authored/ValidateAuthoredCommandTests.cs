using Paddock.DataPipeline;

namespace Paddock.Tests.Authored;

public class ValidateAuthoredCommandTests
{
    [Fact]
    public void RepoData_PrintsAnOkReport()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var dataRoot = Path.Combine(RepoPaths.Root(), "data");

        var code = ValidateAuthoredCommand.Execute(
            ["validate-authored", "--data-root", dataRoot],
            stdout,
            stderr);

        Assert.Equal(0, code);
        Assert.Equal(string.Empty, stderr.ToString());
        Assert.Equal(
            [
                "authored data: ok",
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
                "founder organizations: 51",
                "staff: 182",
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
