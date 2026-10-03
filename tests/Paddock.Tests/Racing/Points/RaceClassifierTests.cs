using Paddock.Simulation.Racing.Points;
using static Paddock.Tests.Racing.Points.PointsTestKit;

namespace Paddock.Tests.Racing.Points;

public class RaceClassifierTests
{
    private static readonly RaceContext Race60 = new(60);

    // ---- classification --------------------------------------------------------------------------------------

    [Fact]
    public void NinetyPercentRule_ClassifiesAtFloorOfNinetyPercentOfTheWinnersLaps_WhateverTheStatus()
    {
        // 2025: winner 50 laps, so 45 are needed. A retirement with 45 laps is classified; a car still running with 44 is not.
        var race = RaceClassifier.Classify(
            Rules(2025),
            [
                Car("a", 50),
                Car("b", 49),
                Car("c", 45, FinishStatus.Mechanical),
                Car("d", 44),
            ],
            new RaceContext(50));

        Assert.Equal([true, true, true, false], race.Cars.Select(car => car.IsClassified));
        Assert.Equal([1, 2, 3, 4], race.Cars.Select(car => car.Position));
        Assert.Equal(25m, PointsOf(race, "a"));
        Assert.Equal(15m, PointsOf(race, "c"));
        Assert.Equal(0m, PointsOf(race, "d"));
        Assert.Null(race.DriverScores.Single(score => score.DriverId == "d").Position);
    }

    [Fact]
    public void NinetyPercentRule_RoundsTheThresholdDown()
    {
        // 51 laps: 0.9 * 51 = 45.9, rounded down to 45, so 45 is classified and 44 is not.
        var race = RaceClassifier.Classify(
            Rules(2025),
            [Car("a", 51), Car("b", 45, FinishStatus.Accident), Car("c", 44, FinishStatus.Accident)],
            new RaceContext(51));

        Assert.Equal([true, true, false], race.Cars.Select(car => car.IsClassified));
    }

    [Fact]
    public void RuleIntroducedIn1998_AppliesFromThatSeasonOn_AndNotBefore()
    {
        var order = new[] { Car("a", 50), Car("b", 46, FinishStatus.Mechanical) };

        var before = RaceClassifier.Classify(Rules(1997), order, new RaceContext(50));
        var from = RaceClassifier.Classify(Rules(1998), order, new RaceContext(50));

        Assert.False(before.Cars[1].IsClassified); // fallback: retired, so not classified
        Assert.True(from.Cars[1].IsClassified); // 46 >= 45
    }

    [Fact]
    public void FallbackWithoutADistanceRule_ClassifiesOnlyCarsRunningAtTheFlag_AheadOfRetirements()
    {
        // The data has no distance rule for 1991. A retirement with more laps than a finisher is still behind him.
        var race = RaceClassifier.Classify(
            Rules(1991),
            [
                Car("w", 60),
                Car("retired", 58, FinishStatus.Mechanical),
                Car("runner", 50),
                Car("crashed", 10, FinishStatus.Accident),
            ],
            Race60);

        Assert.Equal(["w", "runner", "retired", "crashed"], race.Cars.Select(car => car.DriverIds[0]));
        Assert.Equal([1, 2, 3, 4], race.Cars.Select(car => car.Position));
        Assert.Equal([true, true, false, false], race.Cars.Select(car => car.IsClassified));
        Assert.Equal(6m, PointsOf(race, "runner")); // takes second place: 1991 pays 10-6-4-3-2-1
        Assert.Equal(0m, PointsOf(race, "retired"));
    }

    [Fact]
    public void PointsStopAtTheEndOfTheScale()
    {
        // 2025 pays ten places; the eleventh and the twelfth score nothing but are classified.
        var race = RaceClassifier.Classify(Rules(2025), Field(12), new RaceContext(50));

        Assert.Equal(1m, PointsOf(race, "d10"));
        Assert.Equal(0m, PointsOf(race, "d11"));
        Assert.All(race.Cars, car => Assert.True(car.IsClassified));
    }

    [Fact]
    public void EmptyRaceClassifiesToNothing()
    {
        var race = RaceClassifier.Classify(Rules(2025), [], new RaceContext(50));

        Assert.Empty(race.Cars);
        Assert.Empty(race.DriverScores);
        Assert.Empty(race.ConstructorScores);
    }

    // ---- fastest-lap point -----------------------------------------------------------------------------------

    [Fact]
    public void FastestLap1950_OnePointWhereverTheCarFinished_EvenAfterARetirement()
    {
        var race = RaceClassifier.Classify(
            Rules(1950),
            [Car("a", 50), Car("b", 49), Car("c", 10, FinishStatus.Mechanical, fastestLap: true)],
            new RaceContext(50));

        Assert.Equal(1m, PointsOf(race, "c"));
        Assert.False(race.Cars.Single(car => car.DriverIds[0] == "c").IsClassified);
        Assert.Equal(8m, PointsOf(race, "a"));
    }

    [Fact]
    public void FastestLap1950_TiedLapSplitsTheOnePoint()
    {
        var race = RaceClassifier.Classify(
            Rules(1950),
            [Car("a", 50, fastestLap: true), Car("b", 50, fastestLap: true), Car("c", 50)],
            new RaceContext(50));

        Assert.Equal(8.5m, PointsOf(race, "a")); // 8 + 1/2
        Assert.Equal(6.5m, PointsOf(race, "b")); // 6 + 1/2
        Assert.Equal(4m, PointsOf(race, "c"));
    }

    [Fact]
    public void FastestLap2019_PaidOnlyInTheTopTen_AndAlsoCountsForTheConstructor()
    {
        var tenth = RaceClassifier.Classify(Rules(2019), Field(20, fastestLapIndex: 9), new RaceContext(50));
        var eleventh = RaceClassifier.Classify(Rules(2019), Field(20, fastestLapIndex: 10), new RaceContext(50));

        Assert.Equal(2m, PointsOf(tenth, "d10")); // 1 for tenth + 1 fastest lap
        Assert.Equal(0m, PointsOf(eleventh, "d11"));
        Assert.Equal(4m, ConstructorPointsOf(tenth, "c05")); // d09 (2) + d10 (1 + 1), every car scores
        Assert.Equal(0m, ConstructorPointsOf(eleventh, "c06"));
    }

    [Fact]
    public void FastestLap2022_NeedsHalfTheScheduledDistance()
    {
        var oneShort = RaceClassifier.Classify(Rules(2022), Field(20, laps: 29, fastestLapIndex: 1), new RaceContext(60));
        var half = RaceClassifier.Classify(Rules(2022), Field(20, laps: 30, fastestLapIndex: 1), new RaceContext(60));

        Assert.Equal(18m, PointsOf(oneShort, "d02")); // the winner covered 29 of 60 laps: no point
        Assert.Equal(19m, PointsOf(half, "d02")); // exactly half the distance: point paid
    }

    [Fact]
    public void FastestLap2019_StillNeedsNoHalfDistance()
    {
        var race = RaceClassifier.Classify(Rules(2019), Field(20, laps: 5, fastestLapIndex: 1), new RaceContext(60));

        Assert.Equal(19m, PointsOf(race, "d02"));
    }

    [Fact]
    public void FastestLap2025_NoPointEvenWhenFlagged()
    {
        var race = RaceClassifier.Classify(Rules(2025), Field(20, fastestLapIndex: 0), new RaceContext(50));

        Assert.Equal(25m, PointsOf(race, "d01"));
    }

    [Fact]
    public void FastestLap1959_GoesToTheDriverButNotToTheConstructor()
    {
        // 1958-59: a constructors' title exists and the fastest-lap point did not go to constructors.
        var race = RaceClassifier.Classify(
            Rules(1959),
            [
                Car("a", 50, constructor: "x"),
                Car("b", 50, constructor: "y", fastestLap: true),
            ],
            new RaceContext(50));

        Assert.Equal(7m, PointsOf(race, "b")); // 6 for second + 1
        Assert.Equal(6m, ConstructorPointsOf(race, "y"));
        Assert.Equal(8m, ConstructorPointsOf(race, "x"));
    }

    // ---- shared drives ---------------------------------------------------------------------------------------

    [Fact]
    public void SharedDrive1952_SplitsTheCarsPointsEqually()
    {
        var race = RaceClassifier.Classify(
            Rules(1952),
            [Car("a", 50), Car("b", 50, partners: "b2"), Car("c", 48)],
            new RaceContext(50));

        Assert.Equal(3m, PointsOf(race, "b")); // second place pays 6
        Assert.Equal(3m, PointsOf(race, "b2"));
        Assert.Equal(2, race.DriverScores.Single(score => score.DriverId == "b2").Position);
        Assert.Equal(6m, race.Cars[1].CarPoints);
    }

    [Fact]
    public void SharedDrive1952_FastestLapPointJoinsTheCarsPointsBeforeTheSplit()
    {
        var race = RaceClassifier.Classify(
            Rules(1952),
            [Car("a", 50, fastestLap: true, partners: "a2")],
            new RaceContext(50));

        Assert.Equal(4.5m, PointsOf(race, "a")); // (8 + 1) / 2
        Assert.Equal(4.5m, PointsOf(race, "a2"));
    }

    [Fact]
    public void SharedDrive1957_StillSplits_And1958_ScoresNoPointsForAnyDriverOfTheCar()
    {
        var order = new[] { Car("a", 50, partners: "a2"), Car("b", 50) };

        var split = RaceClassifier.Classify(Rules(1957), order, new RaceContext(50));
        var none = RaceClassifier.Classify(Rules(1958), order, new RaceContext(50));

        Assert.Equal(4m, PointsOf(split, "a2"));
        Assert.Equal(0m, PointsOf(none, "a"));
        Assert.Equal(0m, PointsOf(none, "a2"));
        Assert.All(["a", "a2"], id => Assert.Null(none.DriverScores.Single(score => score.DriverId == id).Position));
        Assert.Equal(6m, PointsOf(none, "b")); // second place still pays: the shared car keeps its position
        Assert.Equal(8m, ConstructorPointsOf(none, "a-team")); // constructor points are left as they are (to confirm)
    }

    [Fact]
    public void SharedDrive1958_ASoloCarOfTheSameDriverStillScores()
    {
        // "x" drives his own car and also takes over a teammate's: only the solo car pays him.
        var race = RaceClassifier.Classify(
            Rules(1958),
            [Car("a", 50, partners: "x"), Car("x", 50)],
            new RaceContext(50));

        Assert.Equal(0m, PointsOf(race, "a"));
        Assert.Equal(6m, PointsOf(race, "x"));
    }

    [Fact]
    public void SharedDrive_ADriverInTwoCarsKeepsOnlyHisBestShare()
    {
        // 1952: "x" starts the fifth car (2 points) and then takes over the winner's car (8 / 2 = 4).
        var race = RaceClassifier.Classify(
            Rules(1952),
            [
                Car("a", 50, partners: "x"),
                Car("b", 50),
                Car("c", 50),
                Car("d", 50),
                Car("x", 50),
            ],
            new RaceContext(50));

        Assert.Equal(4m, PointsOf(race, "x"));
        Assert.Equal(1, race.DriverScores.Single(score => score.DriverId == "x").Position);
        Assert.Equal(1, race.DriverScores.Count(score => score.DriverId == "x"));
    }

    [Fact]
    public void SharedDrive_ConstructorsTitleStillScoresTheWholeCar()
    {
        var race = RaceClassifier.Classify(
            Rules(1960),
            [Car("a", 50, constructor: "x", partners: "a2")],
            new RaceContext(50));

        Assert.Equal(8m, ConstructorPointsOf(race, "x"));
    }

    // ---- constructors ----------------------------------------------------------------------------------------

    [Fact]
    public void Constructors1955_HaveNoChampionshipPoints()
    {
        var race = RaceClassifier.Classify(Rules(1955), Field(4), new RaceContext(50));

        Assert.All(race.ConstructorScores, score => Assert.Equal(0m, score.Points));
    }

    [Fact]
    public void Constructors1970_OnlyTheBestPlacedCarScores()
    {
        // c01 has the winner and second place; c02 has third and fourth.
        var race = RaceClassifier.Classify(Rules(1970), Field(4), new RaceContext(50));

        Assert.Equal(9m, ConstructorPointsOf(race, "c01"));
        Assert.Equal(4m, ConstructorPointsOf(race, "c02"));
        Assert.Equal(15m, PointsOf(race, "d01") + PointsOf(race, "d02"));
    }

    [Fact]
    public void Constructors1970_BestCarIsTheBestPlacedClassifiedCar_NotTheFirstListed()
    {
        // The leading car of "x" retired and is not classified; its teammate finished and scores for the constructor.
        var race = RaceClassifier.Classify(
            Rules(1970),
            [
                Car("w", 60, constructor: "y"),
                Car("x1", 59, FinishStatus.Mechanical, constructor: "x"),
                Car("x2", 55, constructor: "x"),
            ],
            Race60);

        Assert.Equal(6m, ConstructorPointsOf(race, "x"));
    }

    [Fact]
    public void Constructors1985_EveryClassifiedCarScores()
    {
        var race = RaceClassifier.Classify(Rules(1985), Field(4), new RaceContext(50));

        Assert.Equal(15m, ConstructorPointsOf(race, "c01")); // 9 + 6
        Assert.Equal(7m, ConstructorPointsOf(race, "c02")); // 4 + 3
        Assert.Equal([1, 2], race.ConstructorScores[0].Positions.ToArray());
    }

    [Fact]
    public void Constructors1985_UnclassifiedCarsScoreNothing()
    {
        var race = RaceClassifier.Classify(
            Rules(1985),
            [Car("a", 60, constructor: "x"), Car("b", 40, FinishStatus.Accident, constructor: "x")],
            Race60);

        Assert.Equal(9m, ConstructorPointsOf(race, "x"));
        Assert.Equal([1], race.ConstructorScores[0].Positions.ToArray());
    }

    // ---- double points ---------------------------------------------------------------------------------------

    [Fact]
    public void DoublePointsFinale2014_PaysDoubleInTheLastRoundOnly()
    {
        var order = Field(4);

        var normal = RaceClassifier.Classify(Rules(2014), order, new RaceContext(50, IsFinalRound: false));
        var finale = RaceClassifier.Classify(Rules(2014), order, new RaceContext(50, IsFinalRound: true));
        var finale2015 = RaceClassifier.Classify(Rules(2015), order, new RaceContext(50, IsFinalRound: true));

        Assert.Equal(25m, PointsOf(normal, "d01"));
        Assert.Equal(50m, PointsOf(finale, "d01"));
        Assert.Equal(86m, ConstructorPointsOf(finale, "c01"));
        Assert.Equal(25m, PointsOf(finale2015, "d01"));
    }

    // ---- determinism and input checks ------------------------------------------------------------------------

    [Fact]
    public void SameInputGivesTheSameClassification()
    {
        RaceResultInput[] Order() =>
        [
            Car("a", 50, partners: "a2"),
            Car("b", 50, fastestLap: true),
            Car("c", 30, FinishStatus.Accident),
            Car("d", 50),
        ];

        var first = RaceClassifier.Classify(Rules(1952), Order(), new RaceContext(50));
        var second = RaceClassifier.Classify(Rules(1952), Order(), new RaceContext(50));

        Assert.Equal(Dump(first), Dump(second));
    }

    [Fact]
    public void BadInputIsRejected()
    {
        var rules = Rules(2025);

        Assert.Throws<ArgumentOutOfRangeException>(() => RaceClassifier.Classify(rules, Field(2), new RaceContext(0)));
        Assert.Throws<ArgumentException>(() => RaceClassifier.Classify(rules, [Car("a", 5), Car("a", 5)], Race60));
        Assert.Throws<ArgumentException>(() => RaceClassifier.Classify(rules, [Car("a", 5, partners: "a")], Race60));
        Assert.Throws<ArgumentOutOfRangeException>(() => RaceClassifier.Classify(rules, [Car("a", -1)], Race60));
        Assert.Throws<ArgumentException>(() => RaceClassifier.Classify(rules, [Car(" ", 5)], Race60));
    }
}
