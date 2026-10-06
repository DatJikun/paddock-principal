using System.Text.Json;
using Paddock.Career;
using Paddock.Data.Historical;
using Paddock.Data.World;

namespace Paddock.Tests.World;

public class FittedDriverRatingsTests
{
    private const string Json = """
        {
          "fitSummary": { "racesCount": 1 },
          "driverRatings": [
            { "rank": 1, "driverId": "star", "name": "Star", "overall": 99,
              "ratingBySeason": [ { "season": 1950, "value": 1, "overall": 60 }, { "season": 1952, "value": 2, "overall": 90 } ],
              "arc": { "peakLevel": 19.8 } },
            { "rank": 2, "driverId": "plain", "name": "Plain", "overall": 40,
              "ratingBySeason": [ { "season": 1951, "value": 0, "overall": 40 }, { "season": 1953, "value": 0, "overall": 50 } ],
              "arc": null }
          ]
        }
        """;

    [Fact]
    public void CurrentIsTheNearestRatedSeasonAndPotentialIsTheCareerPeak()
    {
        var ratings = FittedDriverRatings.Parse(Json);

        var inFit = ratings.RatingFor("star", 1952)!;
        Assert.Equal(18, inFit.Current.Cornering);
        Assert.Equal(20, inFit.Potential.Cornering);

        var gap = ratings.RatingFor("star", 1951)!;
        Assert.Equal(12, gap.Current.Braking);

        var noArc = ratings.RatingFor("plain", 1953)!;
        Assert.Equal(10, noArc.Current.Composure);
        Assert.Equal(10, noArc.Potential.Composure);
    }

    [Fact]
    public void BeforeTheFirstSeasonTheDriverHasNotGrownYetAndUnknownDriversStayUnrated()
    {
        var ratings = FittedDriverRatings.Parse(Json);

        Assert.Equal(6, ratings.RatingFor("plain", 1949)!.Current.Fitness);
        Assert.Equal(4, ratings.RatingFor("plain", 1930)!.Current.Fitness);
        Assert.Null(ratings.RatingFor("nobody", 1950));
        Assert.Equal(2, ratings.Count);
    }

    [Fact]
    public void ProviderLoadsRatingsNextToTheScheduleAndWithoutTheFileKeepsTheOldBehaviour()
    {
        var dir = Directory.CreateTempSubdirectory("ratings-test").FullName;
        try
        {
            var schedulePath = Path.Combine(dir, "people_schedule.json");
            var driversPath = Path.Combine(dir, "drivers.json");
            var schedule = new PeopleScheduleReport(
                2.5m,
                1955,
                [new ScheduledDriver("star", 1920, "Argentine", 1950, 1955, 1949, [new DriverStint(1955, "alpha", 1, 10, 8, "race")], [])],
                [],
                [],
                []);
            var table = new HistoricalDriversDocument(1, [new HistoricalDriver("star", "S", "Tar", "1920-01-01", "Argentine", null, null, null)]);
            File.WriteAllText(schedulePath, JsonSerializer.Serialize(schedule, HistoricalJson.Options));
            File.WriteAllText(driversPath, JsonSerializer.Serialize(table, HistoricalJson.Options));

            var without = CareerData.LoadProvider((schedulePath, driversPath));
            var hashWithout = CareerData.HashWorldData(AuthoredRoot(dir), (schedulePath, driversPath));
            Assert.Null(without.RatingFor("star", 1952));

            File.WriteAllText(Path.Combine(dir, "ratings.json"), Json);
            var with = CareerData.LoadProvider((schedulePath, driversPath));
            var hashWith = CareerData.HashWorldData(AuthoredRoot(dir), (schedulePath, driversPath));
            Assert.Equal(18, with.RatingFor("star", 1952)!.Current.Cornering);
            Assert.NotEqual(hashWithout, hashWith);
            Assert.Equal(hashWith, CareerData.HashWorldData(AuthoredRoot(dir), (schedulePath, driversPath)));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static string AuthoredRoot(string dir)
    {
        Directory.CreateDirectory(Path.Combine(dir, "authored"));
        File.WriteAllText(Path.Combine(dir, "authored", "a.json"), "{}");
        return dir;
    }
}
