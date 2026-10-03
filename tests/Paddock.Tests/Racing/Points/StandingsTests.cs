using Paddock.Simulation.Racing.Points;
using static Paddock.Tests.Racing.Points.PointsTestKit;

namespace Paddock.Tests.Racing.Points;

public class StandingsTests
{
    private static RaceClassification Classify(PointsRules rules, params RaceResultInput[] order) =>
        RaceClassifier.Classify(rules, order, new RaceContext(50));

    private static Standings Season(PointsRules rules, int rounds, params RaceClassification[] races)
    {
        var standings = Standings.Start(rules, rounds);
        foreach (var race in races)
        {
            standings = standings.Apply(race);
        }

        return standings;
    }

    // ---- accumulation ----------------------------------------------------------------------------------------

    [Fact]
    public void MiniSeason2025_DriverTable()
    {
        var rules = Rules(2025);
        var standings = Season(
            rules,
            3,
            Classify(rules, Car("a", 50, constructor: "x"), Car("b", 50, constructor: "y"), Car("c", 50, constructor: "x")),
            Classify(rules, Car("b", 50, constructor: "y"), Car("a", 50, constructor: "x"), Car("c", 50, constructor: "x")),
            Classify(rules, Car("c", 50, constructor: "x"), Car("b", 50, constructor: "y"), Car("a", 50, constructor: "x")));

        // a: 25 + 18 + 15 = 58; b: 18 + 25 + 18 = 61; c: 15 + 15 + 25 = 55.
        var drivers = standings.Drivers();
        Assert.Equal(["b", "a", "c"], drivers.Select(row => row.Id));
        Assert.Equal([61m, 58m, 55m], drivers.Select(row => row.CountedPoints));
        Assert.Equal([1, 2, 3], drivers.Select(row => row.Position));
        Assert.Equal(1, drivers[0].Wins);
        Assert.Equal(3, standings.RoundsCompleted);
        Assert.Equal(0, standings.RoundsRemaining);
    }

    [Fact]
    public void MiniSeason2025_ConstructorTableCountsEveryCar()
    {
        var rules = Rules(2025);
        var standings = Season(
            rules,
            3,
            Classify(rules, Car("a", 50, constructor: "x"), Car("b", 50, constructor: "y"), Car("c", 50, constructor: "x")),
            Classify(rules, Car("b", 50, constructor: "y"), Car("a", 50, constructor: "x"), Car("c", 50, constructor: "x")),
            Classify(rules, Car("c", 50, constructor: "x"), Car("b", 50, constructor: "y"), Car("a", 50, constructor: "x")));

        // x: (25+15) + (18+15) + (25+15) = 113; y: 18 + 25 + 18 = 61.
        var constructors = standings.Constructors();
        Assert.Equal(["x", "y"], constructors.Select(row => row.Id));
        Assert.Equal([113m, 61m], constructors.Select(row => row.CountedPoints));
    }

    [Fact]
    public void EraWithoutConstructorsTitle_HasAnEmptyConstructorTable()
    {
        var rules = Rules(1953);
        var standings = Season(rules, 2, Classify(rules, Car("a", 50)));

        Assert.Empty(standings.Constructors());
        Assert.False(standings.ConstructorsChampionDecision().IsDecided);
    }

    [Fact]
    public void Apply_ReturnsANewTable_AndLeavesTheOldOneUntouched()
    {
        var rules = Rules(2025);
        var empty = Standings.Start(rules, 2);

        var one = empty.Apply(Classify(rules, Car("a", 50), Car("b", 50)));
        var two = one.Apply(Classify(rules, Car("b", 50), Car("a", 50)));

        Assert.Empty(empty.Drivers());
        Assert.Equal(0, empty.RoundsCompleted);
        Assert.Equal([25m, 18m], one.Drivers().Select(row => row.CountedPoints));
        Assert.Equal(1, one.RoundsCompleted);
        Assert.Equal([43m, 43m], two.Drivers().Select(row => row.CountedPoints));
    }

    [Fact]
    public void ADriverWhoJoinsLateAndOneWhoSkipsARoundScoreZeroThere()
    {
        var rules = Rules(1991);
        var standings = Season(
            rules,
            3,
            Classify(rules, Car("a", 50)),
            Classify(rules, Car("b", 50)),
            Classify(rules, Car("a", 50), Car("b", 50)));

        var drivers = standings.Drivers();
        Assert.Equal(["a", "b"], drivers.Select(row => row.Id));
        Assert.Equal([20m, 16m], drivers.Select(row => row.CountedPoints)); // a: 10 + 0 + 10; b: 0 + 10 + 6
    }

    [Fact]
    public void AnAppliedSeasonCannotTakeAnotherRound()
    {
        var rules = Rules(2025);
        var standings = Season(rules, 1, Classify(rules, Car("a", 50)));

        Assert.Throws<InvalidOperationException>(() => standings.Apply(Classify(rules, Car("a", 50))));
    }

    [Fact]
    public void ContextForNextRound_FlagsTheFinaleForDoublePoints()
    {
        var rules = Rules(2014);
        var standings = Standings.Start(rules, 2);

        Assert.False(standings.ContextForNextRound(50).IsFinalRound);

        standings = standings.Apply(Classify(rules, Car("a", 50)));
        var finale = standings.ContextForNextRound(50);
        Assert.True(finale.IsFinalRound);

        standings = standings.Apply(RaceClassifier.Classify(rules, [Car("a", 50)], finale));
        Assert.Equal(25m + 50m, standings.Drivers()[0].CountedPoints);
    }

    // ---- dropped results -------------------------------------------------------------------------------------

    [Fact]
    public void BestSixOfEight_1963_DropsTheTwoWorstResults_ButKeepsTheTotal()
    {
        var rules = Rules(1963); // best 6 results, 9-6-4-3-2-1
        var races = new List<RaceClassification>();
        for (var round = 1; round <= 8; round++)
        {
            // "a" wins the first six and is fifth (2 points) in the last two; "b" is always second.
            races.Add(round <= 6
                ? Classify(rules, Car("a", 50), Car("b", 50))
                : Classify(rules, Car("x", 50), Car("b", 50), Car("y", 50), Car("z", 50), Car("a", 50)));
        }

        var standings = Season(rules, 8, [.. races]);
        var a = standings.Drivers().Single(row => row.Id == "a");

        Assert.Equal(54m, a.CountedPoints); // six wins; the two fifth places are dropped
        Assert.Equal(58m, a.TotalPoints);
        Assert.Equal(6, a.Wins);
        var b = standings.Drivers().Single(row => row.Id == "b");
        Assert.Equal(36m, b.CountedPoints); // eight times 6, best six
        Assert.Equal(48m, b.TotalPoints);
    }

    [Fact]
    public void ADropRuleCanChangeWhoLeadsComparedWithTheRawTotal()
    {
        // 1963: best 6 of 8 results, 9 for a win, 6 for second.
        // "y" wins the first five rounds and is absent after: 45 raw, 45 counted.
        // "x" is second in all eight rounds: 48 raw, but only the best six count: 36.
        var rules = Rules(1963);
        var races = new List<RaceClassification>();
        for (var round = 1; round <= 8; round++)
        {
            races.Add(round <= 5
                ? Classify(rules, Car("y", 50), Car("x", 50))
                : Classify(rules, Car("z", 50), Car("x", 50)));
        }

        var rows = Season(rules, 8, [.. races]).Drivers();

        Assert.Equal(["y", "x", "z"], rows.Select(row => row.Id));
        Assert.Equal(45m, rows[0].CountedPoints);
        Assert.Equal(36m, rows[1].CountedPoints);
        Assert.Equal(48m, rows[1].TotalPoints); // the raw total would have put x first
        Assert.Equal(27m, rows[2].CountedPoints);
    }

    // ---- tie-breaks ------------------------------------------------------------------------------------------

    [Fact]
    public void EqualPoints_AreSeparatedByTheMostWins_ThenSeconds()
    {
        var rules = Rules(1991);
        // 10 points each: "winner" has a win; "steady" has a second and a third (6 + 4).
        var standings = Standings.Start(rules, 2)
            .Apply(Round(("winner", 10m, 1), ("steady", 6m, 2)))
            .Apply(Round(("winner", 0m, null), ("steady", 4m, 3)));

        var rows = standings.Drivers();

        Assert.Equal(["winner", "steady"], rows.Select(row => row.Id));
        Assert.Equal([10m, 10m], rows.Select(row => row.CountedPoints));
        Assert.False(rows[0].TiedOnEverything);
        Assert.Equal(1, rows[0].Wins);
        Assert.Equal([1, 0, 0], rows[0].FinishCounts.ToArray());
        Assert.Equal([0, 1, 1], rows[1].FinishCounts.ToArray());
    }

    [Fact]
    public void EqualPointsAndWins_AreSeparatedBySecondPlaces()
    {
        var rules = Rules(1991);
        // p: two seconds (12). q: three thirds (12). Neither wins.
        var standings = Standings.Start(rules, 3)
            .Apply(Round(("r", 10m, 1), ("p", 6m, 2), ("q", 4m, 3)))
            .Apply(Round(("r", 10m, 1), ("p", 6m, 2), ("q", 4m, 3)))
            .Apply(Round(("r", 10m, 1), ("p", 0m, null), ("q", 4m, 3)));

        var rows = standings.Drivers();

        Assert.Equal(["r", "p", "q"], rows.Select(row => row.Id));
        Assert.Equal([30m, 12m, 12m], rows.Select(row => row.CountedPoints));
        Assert.Equal([0, 2, 0], rows[1].FinishCounts.ToArray());
        Assert.Equal([0, 0, 3], rows[2].FinishCounts.ToArray());
        Assert.All(rows, row => Assert.False(row.TiedOnEverything));
    }

    [Fact]
    public void ATieOnPointsAndEveryFinish_IsOrderedByIdAndFlagged()
    {
        var rules = Rules(1991);
        var standings = Standings.Start(rules, 1)
            .Apply(Round(("zed", 4m, 3), ("amy", 4m, 3)));

        var rows = standings.Drivers();

        Assert.Equal(["amy", "zed"], rows.Select(row => row.Id));
        Assert.All(rows, row => Assert.True(row.TiedOnEverything));
    }

    [Fact]
    public void CountbackIncludesDroppedResults()
    {
        var rules = Rules(1963); // best 6 of 7 results
        var standings = Standings.Start(rules, 7);
        for (var round = 0; round < 6; round++)
        {
            standings = standings.Apply(Round(("w", 6m, 2), ("v", 6m, 2)));
        }

        // w's seventh result (a third place, 4 points) is dropped, v scored nothing; both count 36.
        standings = standings.Apply(Round(("w", 4m, 3), ("v", 0m, null)));

        var rows = standings.Drivers();
        Assert.Equal([36m, 36m], rows.Select(row => row.CountedPoints));
        Assert.Equal(["w", "v"], rows.Select(row => row.Id)); // the dropped third place still counts back
        Assert.Equal(40m, rows[0].TotalPoints);
        Assert.False(rows[0].TiedOnEverything);
    }

    // ---- champion decided ------------------------------------------------------------------------------------

    private static Standings TwoDriverSeason(PointsRules rules, int rounds, decimal leader, decimal challenger, int played)
    {
        // Spread the points over `played` rounds as one score in the first round and zero afterwards.
        var standings = Standings.Start(rules, rounds);
        for (var round = 0; round < played; round++)
        {
            standings = standings.Apply(
                round == 0
                    ? Round(("lead", leader, 1), ("chal", challenger, 2))
                    : Round(("lead", 0m, null), ("chal", 0m, null)));
        }

        return standings;
    }

    [Fact]
    public void Decided_OnlyWhenTheLeaderIsStrictlyBeyondTheRunnerUpsBestCase_2025()
    {
        var rules = Rules(2025); // 25 for a win, no fastest-lap point, nothing doubled
        // Two rounds left: the runner-up can add at most 50.
        var tied = TwoDriverSeason(rules, 4, leader: 150m, challenger: 100m, played: 2);
        var ahead = TwoDriverSeason(rules, 4, leader: 151m, challenger: 100m, played: 2);

        var tiedDecision = tied.DriversChampionDecision();
        var aheadDecision = ahead.DriversChampionDecision();

        Assert.False(tiedDecision.IsDecided);
        Assert.Equal(150m, tiedDecision.MaxChallengerPoints);
        Assert.Equal("chal", tiedDecision.ChallengerId);
        Assert.True(aheadDecision.IsDecided);
        Assert.Equal("lead", aheadDecision.LeaderId);
        Assert.Equal(2, aheadDecision.RoundsRemaining);
    }

    [Fact]
    public void Decided_CountsTheFastestLapPoint_2019()
    {
        var rules = Rules(2019); // 26 per round with the fastest-lap point
        var open = TwoDriverSeason(rules, 3, leader: 52m, challenger: 0m, played: 1); // two rounds left: 52 -> a tie, not decided
        var shut = TwoDriverSeason(rules, 3, leader: 53m, challenger: 0m, played: 1);

        Assert.False(open.DriversChampionDecision().IsDecided);
        Assert.Equal(52m, open.DriversChampionDecision().MaxChallengerPoints);
        Assert.True(shut.DriversChampionDecision().IsDecided);
    }

    [Fact]
    public void Decided_CountsTheDoubleFinale_2014()
    {
        var rules = Rules(2014); // 25, and 50 in the last round
        var clear = TwoDriverSeason(rules, 2, leader: 75m, challenger: 0m, played: 1); // one round left, worth up to 50 (double points)
        var tie = TwoDriverSeason(rules, 2, leader: 50m, challenger: 0m, played: 1);
        var shut = TwoDriverSeason(rules, 2, leader: 51m, challenger: 0m, played: 1);

        Assert.True(clear.DriversChampionDecision().IsDecided);
        Assert.False(tie.DriversChampionDecision().IsDecided);
        Assert.Equal(50m, tie.DriversChampionDecision().MaxChallengerPoints);
        Assert.True(shut.DriversChampionDecision().IsDecided);
    }

    [Fact]
    public void Decided_AfterTheLastRound_TheTableIsFinal()
    {
        var rules = Rules(2025);
        var done = TwoDriverSeason(rules, 1, leader: 25m, challenger: 18m, played: 1);

        var decision = done.DriversChampionDecision();

        Assert.True(decision.IsDecided);
        Assert.Equal("lead", decision.LeaderId);
        Assert.Equal("chal", decision.ChallengerId);
        Assert.Equal(18m, decision.MaxChallengerPoints);
        Assert.Equal(0, decision.RoundsRemaining);
    }

    private static Standings Rounds1976(PointsRules rules, decimal[] leader, decimal[] challenger)
    {
        var standings = Standings.Start(rules, 16);
        for (var round = 0; round < leader.Length; round++)
        {
            standings = standings.Apply(Round(("lead", leader[round], null), ("chal", challenger[round], null)));
        }

        return standings;
    }

    // 1976: 16 rounds, best 7 of each block of 8. After 15 rounds the challenger has six points in each of the first
    // eight rounds (best seven count: 42) and nine in each of the next seven (63): 105 counted. In the last round a
    // win is worth nine, but the second block then has eight results of nine and only seven count: his best case stays 105.
    private static readonly decimal[] Challenger = [6, 6, 6, 6, 6, 6, 6, 6, 9, 9, 9, 9, 9, 9, 9];

    [Fact]
    public void Decided_RespectsDroppedResults_1976()
    {
        decimal[] leader = [9, 9, 9, 9, 9, 9, 9, 0, 9, 9, 9, 9, 4, 3, 0]; // 63 + 43 = 106

        var decision = Rounds1976(Rules(1976), leader, Challenger).DriversChampionDecision();

        Assert.Equal(106m, decision.LeaderPoints);
        Assert.Equal(105m, decision.MaxChallengerPoints);
        Assert.Equal("chal", decision.ChallengerId);
        Assert.True(decision.IsDecided);
    }

    [Fact]
    public void Decided_NotWhenTheLeaderOnlyTiesTheChallengersBestCase_1976()
    {
        decimal[] leader = [9, 9, 9, 9, 9, 9, 9, 0, 9, 9, 9, 9, 4, 2, 0]; // 63 + 42 = 105

        var decision = Rounds1976(Rules(1976), leader, Challenger).DriversChampionDecision();

        Assert.Equal(105m, decision.LeaderPoints);
        Assert.False(decision.IsDecided);
    }

    [Fact]
    public void TheSameResultsRankDifferentlyUnderTheDropRuleAndWithoutIt()
    {
        decimal[] leader = [9, 9, 9, 9, 9, 9, 9, 0, 9, 9, 9, 9, 4, 3, 0];

        var dropped = Rounds1976(Rules(1976), leader, Challenger).Drivers(); // lead 106, chal 105
        var everything = Rounds1976(Rules(1991), leader, Challenger).Drivers(); // lead 106, chal 111

        Assert.Equal(["lead", "chal"], dropped.Select(row => row.Id));
        Assert.Equal(["chal", "lead"], everything.Select(row => row.Id));
        Assert.Equal(111m, everything[0].CountedPoints);
    }

    [Fact]
    public void Decided_ConstructorsTitle_UsesTheCarsPerConstructor()
    {
        var rules = Rules(2025); // all cars: 25 + 18 = 43 per round at most
        var standings = Standings.Start(rules, 2)
            .Apply(Round(
                [("a", 25m, 1), ("b", 18m, 2)],
                [("lead", 86m, [1]), ("chal", 0m, [])]));

        var decision = standings.ConstructorsChampionDecision();

        Assert.Equal(43m, decision.MaxChallengerPoints);
        Assert.True(decision.IsDecided); // 86 > 43
    }

    [Fact]
    public void Decided_ConstructorsTitle_NotYetWhenTheGapIsWithinOneRound()
    {
        var rules = Rules(2025);
        var standings = Standings.Start(rules, 2)
            .Apply(Round(
                [("a", 25m, 1), ("b", 18m, 2)],
                [("lead", 43m, [1]), ("chal", 0m, [])]));

        Assert.False(standings.ConstructorsChampionDecision().IsDecided); // 43 vs 0 + 43: a tie is open
    }

    [Fact]
    public void Decided_EmptyTableIsNeverDecided()
    {
        var decision = Standings.Start(Rules(2025), 5).DriversChampionDecision();

        Assert.False(decision.IsDecided);
        Assert.Null(decision.LeaderId);
    }

    [Fact]
    public void Decided_ALoneDriverCannotBeCaught()
    {
        var standings = Standings.Start(Rules(2025), 5).Apply(Round(("only", 25m, 1)));

        Assert.True(standings.DriversChampionDecision().IsDecided);
    }

    // ---- determinism -----------------------------------------------------------------------------------------

    [Fact]
    public void SameRoundsGiveTheSameTables_AndInputOrderOfDriversDoesNotMatterForTheRanking()
    {
        var rules = Rules(1976);
        RaceClassification[] Races() =>
        [
            Classify(rules, Car("a", 50), Car("b", 50), Car("c", 50)),
            Classify(rules, Car("c", 50), Car("a", 50), Car("b", 50)),
            Classify(rules, Car("b", 50), Car("c", 50), Car("a", 50)),
        ];

        var first = Season(rules, 16, Races());
        var second = Season(rules, 16, Races());

        Assert.Equal(Dump(first.Drivers()), Dump(second.Drivers()));
        Assert.Equal(Dump(first.Constructors()), Dump(second.Constructors()));
        // Three drivers, each with a win, a second and a third: dead level, ordered by id.
        Assert.Equal(["a", "b", "c"], first.Drivers().Select(row => row.Id));
        Assert.All(first.Drivers(), row => Assert.True(row.TiedOnEverything));
    }
}
