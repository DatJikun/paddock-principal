using Paddock.Data.Authored;
using Paddock.Data.Historical;
using Paddock.Data.World;
using Paddock.Domain.Career;
using Paddock.SimRunner;
using Paddock.Tests.Desktop;

namespace Paddock.Tests.SimRunner;

/// <summary>
/// The start-year sweep (#271) on a three-season starting-data fixture and a copy of the authored data with no cache, so the result
/// never depends on a local Jolpica cache.
/// </summary>
public class StartSweepTests
{
    private static readonly string FixturePath = Path.Combine(RepoPaths.Root(), "tests", "Paddock.Tests", "Fixtures", "starting-data", "three-seasons.json");

    [Fact]
    [Trait("Category", "Slow")]
    public void EveryFixtureYearBuildsItsWorldFromTheStartingDataAndPlaysItsFirstRace()
    {
        var first = Sweep();
        var second = Sweep();

        Assert.Equal([1964, 1965, 1966], first.Select(row => row.Year));
        Assert.All(first, row =>
        {
            Assert.True(row.Passed, row.Year + ": " + row.Error);
            Assert.True(row.GeneratedStartData);
            Assert.True(row.Teams > 0);
            Assert.True(row.Starters > 0);
            Assert.NotNull(row.FirstRaceDate);
        });
        Assert.Equal(first, second);
    }

    [Fact]
    public void AYearWithoutRacesFailsWithAReasonAndTheOthersStillRun()
    {
        var data = AuthoredDataLoader.Load(BridgeTestData.DataRoot);
        var lines = new List<string>();

        var rows = StartSweepCommand.Run(BridgeTestData.DataRoot, data, EmptyPeopleProvider.Instance, null, null, CareerPreset.MostHistorical, 3UL, 2030, 2030, row => lines.Add(StartSweepCommand.Line(row)));

        var row = Assert.Single(rows);
        Assert.False(row.Passed);
        Assert.Contains("FAIL", Assert.Single(lines), StringComparison.Ordinal);
    }

    [Fact]
    public void TheCommandRefusesBadArguments()
    {
        Assert.Equal(1, Run(["start-sweep", "--from", "1964", "--seed", "1"]));
        Assert.Equal(1, Run(["start-sweep", "--from", "1949", "--to", "1950", "--seed", "1"]));
        Assert.Equal(1, Run(["start-sweep", "--from", "1964", "--to", "1964", "--seed", "1", "--preset", "Custom"]));
        Assert.Equal(1, Run(["start-sweep", "--from", "1964", "--to", "1964", "--seed", "1", "--schedule", "a.json"]));
        Assert.Equal(1, Run(["start-sweep", "--from", "1964", "--to", "1964", "--seed", "1", "--bogus", "x"]));

        static int Run(string[] args) => StartSweepCommand.Execute(args, new StringWriter(), new StringWriter());
    }

    private static IReadOnlyList<StartSweepRow> Sweep()
    {
        var data = AuthoredDataLoader.Load(BridgeTestData.DataRoot);
        return StartSweepCommand.Run(
            BridgeTestData.DataRoot,
            data,
            EmptyPeopleProvider.Instance,
            StartingDataLoader.Load(FixturePath),
            null,
            CareerPreset.MostHistorical,
            7UL,
            1964,
            1966);
    }
}
