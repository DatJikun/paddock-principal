using Paddock.Data.Historical;
using Paddock.DataPipeline;

namespace Paddock.Tests.DataPipeline;

public class JolpicaNormalizerTests
{
    private static readonly string[] SummaryWhen1950IsComplete =
    [
        "drivers: 9",
        "constructors: 6",
        "circuits: 4",
        "races: 5",
        "results: 7",
        "qualifying: 1",
        "sprint result rows: 1",
        "non-classified results: 3",
        "disqualifications: 1",
        "shared-drive rows: 2",
        "shared-drive cars: 1",
        "indianapolis 500 races (1950-1960): 1",
        "indianapolis 500 result rows: 1",
        "seasons with no qualifying rows: 1950, 1952, 2021",
        "decade 1950s: races 3, results 7, qualifying 0, sprint rows 0, shared-drive rows 2, indianapolis 500 races 1",
        "decade 1990s: races 1, results 0, qualifying 1, sprint rows 0, shared-drive rows 0, indianapolis 500 races 0",
        "decade 2020s: races 1, results 0, qualifying 0, sprint rows 1, shared-drive rows 0, indianapolis 500 races 0",
        "raw pages: seasons 0, circuits 1, drivers 1, constructors 1, races 4, results 4, qualifying 2, sprint 1, driver-standings 0, constructor-standings 0",
        "seasons 1950-1950: present 1, missing 0",
        "reference errors: 0",
    ];

    [Fact]
    public void IndianapolisWindowAndClassificationMatchTheApiQuirks()
    {
        Assert.True(HistoricalEdges.IsIndianapolis500(1950, "indianapolis"));
        Assert.True(HistoricalEdges.IsIndianapolis500(1960, "indianapolis"));
        Assert.False(HistoricalEdges.IsIndianapolis500(1961, "indianapolis"));
        Assert.False(HistoricalEdges.IsIndianapolis500(1950, "silverstone"));
        Assert.True(HistoricalEdges.IsClassified("10"));
        Assert.False(HistoricalEdges.IsClassified("R"));
        Assert.False(HistoricalEdges.IsClassified("D"));
        Assert.True(HistoricalEdges.IsDisqualified("D", "Finished"));
        Assert.True(HistoricalEdges.IsDisqualified("12", "Disqualified"));
        Assert.False(HistoricalEdges.IsDisqualified("R", "Oil leak"));
    }

    [Fact]
    public async Task NormalizeKeepsEdgeCasesAndSummaryHasNoReferenceErrors()
    {
        var cache = JolpicaFixtures.Materialize();
        try
        {
            var stderr = new StringWriter();
            var normalizeOut = new StringWriter();
            var normalizeCode = await PipelineCommands.ExecuteAsync(
                ["normalize", "--cache", cache],
                normalizeOut,
                stderr);
            Assert.Equal(0, normalizeCode);
            Assert.Equal(string.Empty, stderr.ToString());

            var data = JolpicaNormalizer.ReadRaw(Path.Combine(cache, "raw"));
            var farina = Assert.Single(data.Drivers.Drivers, driver => driver.DriverId == "farina");
            Assert.Null(farina.Code);
            Assert.Null(farina.PermanentNumber);
            var verstappen = Assert.Single(data.Drivers.Drivers, driver => driver.DriverId == "max_verstappen");
            Assert.Equal("VER", verstappen.Code);
            Assert.Equal("33", verstappen.PermanentNumber);

            var british = Assert.Single(data.Races.Races, race => race.Season == 1950 && race.Round == 1);
            Assert.False(british.IsIndianapolis500);
            Assert.Equal("silverstone", british.CircuitId);
            var indy = Assert.Single(data.Races.Races, race => race.Season == 1950 && race.Round == 3);
            Assert.True(indy.IsIndianapolis500);

            var roundOne = data.Results.Results.Where(row => row.Season == 1950 && row.Round == 1).ToList();
            Assert.Equal(["farina", "fry", "shawe_taylor", "fangio", "kelly"], roundOne.Select(row => row.DriverId).ToArray());

            var winner = roundOne[0];
            Assert.True(winner.IsClassified);
            Assert.False(winner.IsSharedDrive);
            Assert.False(winner.IsDisqualified);
            Assert.Equal(9m, winner.Points);
            Assert.Equal(8003600L, winner.TimeMillis);

            var fangio = roundOne.Single(row => row.DriverId == "fangio");
            Assert.False(fangio.IsClassified);
            Assert.Equal("R", fangio.PositionText);
            Assert.Equal("Oil leak", fangio.Status);
            Assert.Equal(12, fangio.Position);

            var kelly = roundOne.Single(row => row.DriverId == "kelly");
            Assert.False(kelly.IsClassified);
            Assert.Equal("R", kelly.PositionText);
            Assert.Equal("Not classified", kelly.Status);

            Assert.All(roundOne.Where(row => row.CarNumber == "10"), row => Assert.True(row.IsSharedDrive));
            Assert.Equal(2, roundOne.Count(row => row.IsSharedDrive));

            var bonetto = Assert.Single(data.Results.Results, row => row.DriverId == "bonetto");
            Assert.True(bonetto.IsDisqualified);
            Assert.False(bonetto.IsClassified);
            Assert.Equal("D", bonetto.PositionText);

            var senna = Assert.Single(data.Qualifying.Qualifying);
            Assert.Equal("senna", senna.DriverId);
            Assert.Equal("1:15.962", senna.Q1);
            Assert.Null(senna.Q2);
            Assert.Null(senna.Q3);

            var driversJson = File.ReadAllText(Path.Combine(cache, "normalized", "drivers.json"));
            var circuitsJson = File.ReadAllText(Path.Combine(cache, "normalized", "circuits.json"));
            Assert.Contains("\"code\": null", driversJson, StringComparison.Ordinal);
            Assert.Contains("Nürburgring", circuitsJson, StringComparison.Ordinal);
            Assert.Contains("Autódromo José Carlos Pace", circuitsJson, StringComparison.Ordinal);
            Assert.DoesNotContain("\\u00fc", circuitsJson, StringComparison.Ordinal);

            var again = File.ReadAllText(Path.Combine(cache, "normalized", "results.json"));
            var second = await PipelineCommands.ExecuteAsync(["normalize", "--cache", cache], new StringWriter(), stderr);
            Assert.Equal(0, second);
            Assert.Equal(again, File.ReadAllText(Path.Combine(cache, "normalized", "results.json")));

            var summaryOut = new StringWriter();
            var summaryCode = await PipelineCommands.ExecuteAsync(
                ["summary", "--cache", cache, "--from", "1950", "--to", "1950"],
                summaryOut,
                stderr);
            Assert.Equal(0, summaryCode);
            Assert.Equal(SummaryWhen1950IsComplete, JolpicaFixtures.Lines(summaryOut));
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }

    [Fact]
    public async Task SummaryListsMissingSeasonsAsReferenceErrors()
    {
        var cache = JolpicaFixtures.Materialize();
        try
        {
            var stderr = new StringWriter();
            Assert.Equal(0, await PipelineCommands.ExecuteAsync(["normalize", "--cache", cache], new StringWriter(), stderr));

            var stdout = new StringWriter();
            var code = await PipelineCommands.ExecuteAsync(
                ["summary", "--cache", cache, "--from", "1950", "--to", "1951"],
                stdout,
                stderr);

            Assert.Equal(1, code);
            Assert.Equal(string.Empty, stderr.ToString());
            var lines = JolpicaFixtures.Lines(stdout);
            Assert.Contains("seasons 1950-1951: present 1, missing 1", lines);
            Assert.Contains("missing seasons: 1951", lines);
            Assert.Contains("reference errors: 1", lines);
            Assert.Contains("- season 1951 has no races", lines);
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }

    [Fact]
    public async Task SummaryReportsAResultWhoseDriverIsNotInTheDriverList()
    {
        var cache = Path.Combine(Path.GetTempPath(), "paddock-jolpica-" + Guid.NewGuid().ToString("N"));
        try
        {
            Write(cache, "raw/drivers/offset-0.json", DriverPage("farina"));
            Write(cache, "raw/constructors/offset-0.json", ConstructorPage());
            Write(cache, "raw/circuits/offset-0.json", CircuitPage());
            Write(cache, "raw/races/1950/offset-0.json", RacePage());
            Write(cache, "raw/results/1950/offset-0.json", ResultPage("ghost"));

            var stderr = new StringWriter();
            Assert.Equal(0, await PipelineCommands.ExecuteAsync(["normalize", "--cache", cache], new StringWriter(), stderr));
            var stdout = new StringWriter();
            var code = await PipelineCommands.ExecuteAsync(
                ["summary", "--cache", cache, "--from", "1950", "--to", "1950"],
                stdout,
                stderr);

            Assert.Equal(1, code);
            Assert.Contains("- result 1950/1 ghost references missing driver", JolpicaFixtures.Lines(stdout));
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }

    [Fact]
    public void HalfPointsAndMissingDriverIdAreHandled()
    {
        var cache = Path.Combine(Path.GetTempPath(), "paddock-jolpica-" + Guid.NewGuid().ToString("N"));
        try
        {
            Write(cache, "raw/results/1950/offset-0.json", ResultPage("farina", "4.5"));
            var data = JolpicaNormalizer.ReadRaw(Path.Combine(cache, "raw"));
            Assert.Equal(4.5m, Assert.Single(data.Results.Results).Points);

            Write(cache, "raw/results/1950/offset-0.json", ResultPage("farina").Replace("\"driverId\":\"farina\"", "\"driverId\":\"\"", StringComparison.Ordinal));
            var error = Assert.Throws<InvalidDataException>(() => JolpicaNormalizer.ReadRaw(Path.Combine(cache, "raw")));
            Assert.Contains("driverId", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }

    [Fact]
    public async Task UnknownCommandFailsWithoutWritingData()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = await PipelineCommands.ExecuteAsync(["build"], stdout, stderr);
        Assert.Equal(1, code);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.Contains("Unknown command", stderr.ToString(), StringComparison.Ordinal);
    }

    private static void Write(string root, string relative, string json)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    private static string DriverPage(string driverId)
    {
        return "{\"MRData\":{\"limit\":\"1\",\"offset\":\"0\",\"total\":\"1\",\"DriverTable\":{\"Drivers\":[{\"driverId\":\""
            + driverId
            + "\",\"url\":\"http://example.test/"
            + driverId
            + "\",\"givenName\":\"Nino\",\"familyName\":\"Farina\",\"dateOfBirth\":\"1906-10-30\",\"nationality\":\"Italian\"}]}}}";
    }

    private static string ConstructorPage()
    {
        return """
            {"MRData":{"limit":"1","offset":"0","total":"1","ConstructorTable":{"Constructors":[{"constructorId":"alfa","url":"http://example.test/alfa","name":"Alfa Romeo","nationality":"Swiss"}]}}}
            """;
    }

    private static string CircuitPage()
    {
        return """
            {"MRData":{"limit":"1","offset":"0","total":"1","CircuitTable":{"Circuits":[{"circuitId":"silverstone","url":"http://example.test/silverstone","circuitName":"Silverstone Circuit","Location":{"lat":"52.0786","long":"-1.01694","locality":"Silverstone","country":"UK"}}]}}}
            """;
    }

    private static string RacePage()
    {
        return """
            {"MRData":{"limit":"1","offset":"0","total":"1","RaceTable":{"Races":[{"season":"1950","round":"1","url":"http://example.test/1950-1","raceName":"British Grand Prix","date":"1950-05-13","Circuit":{"circuitId":"silverstone"}}]}}}
            """;
    }

    private static string ResultPage(string driverId, string points = "9")
    {
        return "{\"MRData\":{\"limit\":\"1\",\"offset\":\"0\",\"total\":\"1\",\"RaceTable\":{\"Races\":[{\"season\":\"1950\",\"round\":\"1\",\"Results\":[{\"number\":\"2\",\"position\":\"1\",\"positionText\":\"1\",\"points\":\""
            + points
            + "\",\"Driver\":{\"driverId\":\""
            + driverId
            + "\"},\"Constructor\":{\"constructorId\":\"alfa\"},\"grid\":\"1\",\"laps\":\"70\",\"status\":\"Finished\"}]}]}}}";
    }
}
