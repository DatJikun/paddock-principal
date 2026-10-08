using Paddock.Data.Authored;
using Paddock.Domain.World;
using Paddock.Simulation.Racing.Incidents;
using Paddock.Simulation.Racing.Points;
using Paddock.Simulation.Racing.Weekend;
using Paddock.Simulation.Regulation;
using Paddock.Tests.Racing.Weekend;

namespace Paddock.Tests.Regulation;

/// <summary>
/// Every rule a vote can change measurably changes a race (#275 scope 8): the catalogue holds only dimensions the race engine reads.
/// Each test runs the same weekends with the old value and with the new one and finds a difference in the standings, the grid, the
/// distance or the race itself. A dimension that cannot show one does not belong in <see cref="LiveRules"/>.
/// </summary>
public class RulesTakeEffectTests
{
    private static readonly IReadOnlyDictionary<string, RuleDimensionSpec> Specs =
        RuleCatalog.ToSpecs(WeekendTestKit.Data.Catalog).ToDictionary(spec => spec.Id, StringComparer.Ordinal);

    private static RaceWeekendInput WithRule(RaceWeekendInput input, string dimension, string value)
    {
        var rules = input.Rules.With(Specs, input.Rules.Season, [new RuleChange(dimension, value)]);
        return input with
        {
            Rules = rules,
            Safety = EraSafetyProfile.FromAuthored(input.Season, WeekendTestKit.Data.EraSetFor(input.Season).Value("fatality_risk"), rules.Value("safety_car")),
            TotalLaps = RaceDistance.LapsFor(rules, input.Track),
        };
    }

    private static string CurrentValue(int season, string dimension) => WeekendTestKit.Data.RuleSetFor(season).Value(dimension);

    /// <summary>Runs the same weekends under both values and says whether any of them differs in what <paramref name="measure"/> reads.</summary>
    private static bool Differs(int season, string dimension, string value, Func<RaceWeekendResult, string> measure, int seeds = 6, bool finalRound = false)
    {
        for (var seed = 1; seed <= seeds; seed++)
        {
            var input = WeekendTestKit.Input(season, round: (seed % 3) + 1, seed: (ulong)seed) with { IsFinalRound = finalRound };
            var before = WeekendTestKit.Run(input);
            var after = WeekendTestKit.Run(WithRule(input, dimension, value));
            if (measure(before) != measure(after))
            {
                return true;
            }
        }

        return false;
    }

    private static string Points(RaceWeekendResult result) =>
        string.Join("|", result.Classification.Cars.Select(car => car.Position + ":" + car.CarPoints + ":" + car.ConstructorPoints));

    private static string Grid(RaceWeekendResult result) =>
        string.Join("|", result.Qualifying.Grid.Select(slot => slot.DriverId + "@" + slot.ScoreSeconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void ADifferentPointsTableGivesDifferentPoints()
    {
        Assert.True(Differs(1955, "points_scale", "top10_25_18_15_12_10_8_6_4_2_1", Points));
        Assert.True(Differs(1955, "points_scale", "top6_10_6_4_3_2_1", Points));
    }

    [Fact]
    public void AFastestLapPointChangesWhoScores()
    {
        Assert.True(Differs(1988, "fastest_lap_point", "one_point_if_top_10", Points, seeds: 12));
    }

    [Fact]
    public void DoublePointsInTheFinalChangeTheFinalOnly()
    {
        Assert.True(Differs(1955, "double_points_finale", "yes", Points, finalRound: true));
        Assert.False(Differs(1955, "double_points_finale", "yes", Points, finalRound: false));
    }

    [Fact]
    public void CountingBothCarsOrOnlyTheBetterOneChangesTheConstructorsPoints()
    {
        Assert.True(Differs(1988, "constructors_points_counting", "best_finishing_car_only", Points));
    }

    [Fact]
    public void CountingFewerResultsChangesTheStandings()
    {
        const int season = 1955;
        var rounds = WeekendTestKit.Data.RaceAssignments.Count(a => a.Season == season);
        var all = CountedStandings(season, "all", rounds);
        var four = CountedStandings(season, "best_4", rounds);

        Assert.NotEqual(all, four);
    }

    private static string CountedStandings(int season, string counted, int rounds)
    {
        var results = new List<RaceWeekendResult>();
        for (var round = 1; round <= rounds; round++)
        {
            results.Add(WeekendTestKit.Run(WeekendTestKit.Input(season, round, seed: (ulong)round)));
        }

        var baseRules = WeekendTestKit.Data.RuleSetFor(season);
        var rules = PointsRules.For(baseRules.With(Specs, season, [new RuleChange("results_counted", counted)]));
        var standings = Standings.Start(rules, rounds);
        foreach (var result in results)
        {
            standings = standings.Apply(result.Classification);
        }

        return string.Join("|", standings.Drivers().Select(row => row.Id + ":" + row.CountedPoints));
    }

    [Theory]
    [InlineData("one_lap_friday_and_saturday")]
    [InlineData("knockout_q1_q2_q3")]
    [InlineData("saturday_twelve_lap")]
    public void ADifferentQualifyingFormatGivesADifferentGrid(string format)
    {
        Assert.NotEqual(format, CurrentValue(1955, "qualifying_format"));
        Assert.True(Differs(1955, "qualifying_format", format, Grid, seeds: 3));
    }

    [Theory]
    [InlineData(1988, "physical")]
    [InlineData(1988, "physical_and_vsc")]
    public void ASafetyCarRuleChangesTheRace(int season, string value)
    {
        Assert.NotEqual(value, CurrentValue(season, "safety_car"));
        Assert.True(Differs(season, "safety_car", value, result => result.Digest(), seeds: 30));
    }

    [Fact]
    public void RefuellingChangesTheRaceWhereTheEraCanDoIt()
    {
        var season = 2000;
        Assert.Equal("allowed", CurrentValue(season, "refuelling"));
        Assert.True(Differs(season, "refuelling", "banned", result => result.Digest(), seeds: 3));
    }

    [Fact]
    public void ADifferentRaceDistanceChangesTheLapsOfARace()
    {
        var season = 1955;
        var changed = 0;
        foreach (var assignment in WeekendTestKit.Data.RaceAssignments.Where(a => a.Season == season))
        {
            var track = WeekendTestKit.Data.LayoutFor(season, assignment.Round);
            var input = WeekendTestKit.Input(season, assignment.Round);
            var shorter = WithRule(input, "race_distance_rule", "min_300km_or_two_hours");
            var standard = WithRule(input, "race_distance_rule", "standard_305km");
            if (shorter.TotalLaps != standard.TotalLaps)
            {
                changed++;
                Assert.Equal(shorter.TotalLaps, WeekendTestKit.Run(shorter).ScheduledLaps);
                Assert.Equal(standard.TotalLaps, WeekendTestKit.Run(standard).ScheduledLaps);
            }
        }

        Assert.True(changed > 0, "300 km and 305 km are a different number of laps on some circuit");
        _ = Specs["race_distance_rule"];
    }

    [Fact]
    public void EveryLiveRuleHasATestHere()
    {
        // When a dimension joins LiveRules it needs a test above that shows an adopted value changes a race; this list is the reminder.
        var tested = new[]
        {
            "points_scale", "fastest_lap_point", "double_points_finale", "results_counted", "constructors_points_counting",
            "qualifying_format", "safety_car", "race_distance_rule", "refuelling",
        };

        Assert.Equal(tested.Order(StringComparer.Ordinal), LiveRules.All.Select(rule => rule.DimensionId).Order(StringComparer.Ordinal));
        Assert.All(LiveRules.All, rule =>
        {
            Assert.True(Specs.ContainsKey(rule.DimensionId), rule.DimensionId + " is in the catalog");
            Assert.All(rule.Values, value => Assert.True(Specs[rule.DimensionId].Values.Contains(value), rule.DimensionId + "=" + value + " is a catalog value"));
        });
    }
}
