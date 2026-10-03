using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Paddock.Data.Authored;
using Paddock.Simulation.Racing.Points;

namespace Paddock.Tests.Racing.Points;

/// <summary>Shared helpers: the real authored regulations, and small builders for races and rounds.</summary>
internal static class PointsTestKit
{
    private static readonly Lazy<AuthoredData> Data = new(
        () => AuthoredDataLoader.Load(Path.Combine(RepoPaths.Root(), "data")));

    public static AuthoredData Real => Data.Value;

    /// <summary>The points rules the real data gives for <paramref name="season"/>.</summary>
    public static PointsRules Rules(int season, PointsRulesOptions? options = null) =>
        PointsRules.For(Real.RuleSetFor(season), options);

    /// <summary>A car that finished (or ran) as a single-driver entry. Id "x" gives constructor "x-team" unless given.</summary>
    public static RaceResultInput Car(
        string driver,
        int laps,
        FinishStatus status = FinishStatus.Classified,
        string? constructor = null,
        bool fastestLap = false,
        params string[] partners) =>
        new(driver, constructor ?? driver + "-team", status, laps, partners, fastestLap);

    /// <summary>
    /// A full field of <paramref name="size"/> classified single-driver cars, all on the lead lap, driver "d01".."dNN",
    /// two cars to a constructor ("c01" has d01 and d02).
    /// </summary>
    public static List<RaceResultInput> Field(int size, int laps = 50, int fastestLapIndex = -1)
    {
        var field = new List<RaceResultInput>();
        for (var i = 0; i < size; i++)
        {
            var driver = "d" + (i + 1).ToString("00", CultureInfo.InvariantCulture);
            var constructor = "c" + (i / 2 + 1).ToString("00", CultureInfo.InvariantCulture);
            field.Add(Car(driver, laps, constructor: constructor, fastestLap: i == fastestLapIndex));
        }

        return field;
    }

    public static decimal PointsOf(RaceClassification race, string driver) =>
        race.DriverScores.Single(score => score.DriverId == driver).Points;

    public static decimal ConstructorPointsOf(RaceClassification race, string constructor) =>
        race.ConstructorScores.Single(score => score.ConstructorId == constructor).Points;

    /// <summary>A round built straight from scores, for tests of the table and tie-breaks that do not need the classifier.</summary>
    public static RaceClassification Round(params (string Driver, decimal Points, int? Position)[] scores) =>
        new(
            [],
            [.. scores.Select(score => new DriverScore(score.Driver, score.Points, score.Position))],
            []);

    /// <summary>As <see cref="Round"/>, with constructor scores too.</summary>
    public static RaceClassification Round(
        (string Driver, decimal Points, int? Position)[] drivers,
        (string Constructor, decimal Points, int[] Positions)[] constructors) =>
        new(
            [],
            [.. drivers.Select(score => new DriverScore(score.Driver, score.Points, score.Position))],
            [.. constructors.Select(score => new ConstructorScore(score.Constructor, score.Points, [.. score.Positions]))]);

    /// <summary>A text form of a classification: equal text means equal classification, compared value by value.</summary>
    public static string Dump(RaceClassification race)
    {
        var text = new StringBuilder();
        foreach (var car in race.Cars)
        {
            text.Append(CultureInfo.InvariantCulture, $"car {car.Position} {car.IsClassified} {car.ConstructorId} ")
                .Append(string.Join('+', car.DriverIds))
                .Append(CultureInfo.InvariantCulture, $" {car.CarPoints} {car.ConstructorPoints}\n");
        }

        foreach (var score in race.DriverScores)
        {
            text.Append(CultureInfo.InvariantCulture, $"drv {score.DriverId} {score.Points} {score.Position}\n");
        }

        foreach (var score in race.ConstructorScores)
        {
            text.Append(CultureInfo.InvariantCulture, $"con {score.ConstructorId} {score.Points} ")
                .Append(string.Join(',', score.Positions))
                .Append('\n');
        }

        return text.ToString();
    }

    public static string Dump(ImmutableArray<StandingsRow> table) =>
        string.Join(
            '\n',
            table.Select(row => string.Create(
                CultureInfo.InvariantCulture,
                $"{row.Position} {row.Id} {row.CountedPoints} {row.TotalPoints} [{string.Join(',', row.FinishCounts)}] {row.TiedOnEverything}")));
}
