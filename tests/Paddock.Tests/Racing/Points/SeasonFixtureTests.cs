using Paddock.Simulation.Racing.Points;
using static Paddock.Tests.Racing.Points.PointsTestKit;

namespace Paddock.Tests.Racing.Points;

/// <summary>
/// End-to-end seasons run through the classifier and the standings under the REAL rules of 1976 and 2008 from
/// data/authored/regulations.
///
/// These fixtures are SYNTHETIC: they are not the 1976 or 2008 race results. The task asked for the real championship
/// outcomes (Hunt 69, Lauda 68; Hamilton 98, Massa 97) to be reproduced from hand-typed top-3 results, but no verified
/// source was reachable from the build session (Wikipedia and Ergast are blocked by the network proxy, there is no committed
/// results data, and Jolpica data must not be committed), and race results typed from memory would be unverified history.
/// What the fixtures do check is that the rules of those two seasons, as the data states them, give the expected
/// totals, drops and tie-breaks.
/// </summary>
public class SeasonFixtureTests
{
    private static RaceClassification Race(PointsRules rules, params string[] drivers) =>
        RaceClassifier.Classify(
            rules,
            [.. drivers.Select(driver => Car(driver, 60, constructor: ConstructorOf(driver)))],
            new RaceContext(60));

    private static string ConstructorOf(string driver) => driver switch
    {
        "ham" or "kov" => "mclaren",
        "mas" or "rai" => "ferrari",
        _ => driver + "-team",
    };

    [Fact]
    public void Rules2008_ThreeRaceFixture_TotalsAndConstructorsFollowTheScale()
    {
        // 2008: 10-8-6-5-4-3-2-1, every result counts, every car scores for its constructor, no fastest-lap point.
        var rules = Rules(2008);
        var standings = Standings.Start(rules, 3)
            .Apply(Race(rules, "ham", "mas", "rai", "kov"))
            .Apply(Race(rules, "mas", "ham", "kov", "rai"))
            .Apply(Race(rules, "ham", "rai", "mas", "kov"));

        var drivers = standings.Drivers();
        Assert.Equal(["ham", "mas", "rai", "kov"], drivers.Select(row => row.Id));
        Assert.Equal([28m, 24m, 19m, 16m], drivers.Select(row => row.CountedPoints));

        var constructors = standings.Constructors();
        Assert.Equal(["mclaren", "ferrari"], constructors.Select(row => row.Id));
        Assert.Equal([44m, 43m], constructors.Select(row => row.CountedPoints)); // 15 + 14 + 15 and 14 + 15 + 14

        var decision = standings.DriversChampionDecision();
        Assert.True(decision.IsDecided);
        Assert.Equal("ham", decision.LeaderId);
    }

    [Fact]
    public void Rules2008_ThreeRaceFixture_IsIdenticalWhenRunTwice()
    {
        var rules = Rules(2008);

        Standings Run() => Standings.Start(rules, 3)
            .Apply(Race(rules, "ham", "mas", "rai", "kov"))
            .Apply(Race(rules, "mas", "ham", "kov", "rai"))
            .Apply(Race(rules, "ham", "rai", "mas", "kov"));

        Assert.Equal(Dump(Run().Drivers()), Dump(Run().Drivers()));
        Assert.Equal(Dump(Run().Constructors()), Dump(Run().Constructors()));
    }

    [Fact]
    public void Rules1976_SixteenRounds_DropsAndCountbackDecideAnExactTie()
    {
        // 1976 rules from the data: 9-6-4-3-2-1, best 7 results of each block of 8 rounds, no fastest-lap point.
        // "a" wins rounds 1-8, is second in 9-15 and third in 16. "b" is second in 1-8 and wins 9-16. "c" is third or second.
        var rules = Rules(1976);
        Assert.Equal("best_14_split_7_and_7", Real.RuleSetFor(1976).Value("results_counted"));

        var standings = Standings.Start(rules, 16);
        for (var round = 1; round <= 16; round++)
        {
            standings = standings.Apply(round switch
            {
                <= 8 => Race(rules, "a", "b", "c"),
                <= 15 => Race(rules, "b", "a", "c"),
                _ => Race(rules, "b", "c", "a"),
            });
        }

        var rows = standings.Drivers();
        var a = rows.Single(row => row.Id == "a");
        var b = rows.Single(row => row.Id == "b");

        // a: block one 8 wins, best seven = 63; block two seven seconds and a third, best seven = 42 -> 105.
        // b: block one eight seconds, best seven = 42; block two eight wins, best seven = 63 -> 105.
        Assert.Equal(105m, a.CountedPoints);
        Assert.Equal(105m, b.CountedPoints);
        Assert.Equal(118m, a.TotalPoints); // 72 + 42 + 4
        Assert.Equal(120m, b.TotalPoints); // 48 + 72
        // Level on counted points and on wins (8 each); b has eight seconds to a's seven.
        Assert.Equal(["b", "a", "c"], rows.Select(row => row.Id));
        Assert.Equal(8, a.Wins);
        Assert.Equal(8, b.Wins);
        Assert.False(a.TiedOnEverything);
        Assert.True(standings.DriversChampionDecision().IsDecided);
        Assert.Equal("b", standings.DriversChampionDecision().LeaderId);
    }
}
