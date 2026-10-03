using System.Text.Json;
using Paddock.Simulation.Racing.Qualifying;

namespace Paddock.Tests.Racing.Qualifying;

/// <summary>
/// All entrants below are synthetic fixtures (not real drivers). Expected sizes follow the hand-set rules passed
/// to each test; the lap-time constants are ESTIMATES (<see cref="QualifyingConstants"/>), so the tests check
/// direction and bookkeeping, not calibrated values.
/// </summary>
public class QualifyingSimulatorTests
{
    private const double Base = 90d;

    private static QualifyingContext Ctx(ulong seed = 42, double wet = 1d, bool declaredWet = false) =>
        new(seed, 1950, 3, Base, wet, declaredWet);

    private static List<QualifyingEntrant> Field(int count, double paceStep = 0.15, double consistency = 0.5) =>
        Enumerable.Range(0, count)
            .Select(i => new QualifyingEntrant($"d{i:00}", $"c{i / 2:00}", i * paceStep, consistency))
            .ToList();

    private static string Fingerprint(QualifyingResult r) =>
        string.Join(
            "|",
            r.Grid.Select(s => $"{s.GridPosition}:{s.DriverId}:{s.ScoreSeconds:F3}:{s.BestLapSeconds:F3}")
                .Concat(r.NonQualifiers.Select(n => $"{n.Reason}:{n.DriverId}:{n.BestLapSeconds:F3}"))
                .Concat(r.PreQualifying.Select(p => $"pq:{p.DriverId}:{p.BestLapSeconds:F3}:{p.Advanced}")));

    // ---- Determinism ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(QualifyingFormat.SingleSession)]
    [InlineData(QualifyingFormat.TwoDay)]
    [InlineData(QualifyingFormat.OneLapShootout)]
    [InlineData(QualifyingFormat.Aggregate)]
    [InlineData(QualifyingFormat.Knockout)]
    public void SameSeedGivesTheSameResultAndAnotherSeedDoesNot(QualifyingFormat format)
    {
        var rules = RulesFor(format, 20);
        var field = Field(20, paceStep: 0.02, consistency: 0.2);

        var a = QualifyingSimulator.Run(Ctx(7), field, rules);
        var b = QualifyingSimulator.Run(Ctx(7), field, rules);
        var c = QualifyingSimulator.Run(Ctx(8), field, rules);

        Assert.Equal(Fingerprint(a), Fingerprint(b));
        Assert.NotEqual(Fingerprint(a), Fingerprint(c));
    }

    [Fact]
    public void RoundAndSeasonAreAPartOfTheStreamKey()
    {
        var rules = QualifyingRules.SingleSession(3);
        var field = Field(10, 0.01, 0.1);
        var a = QualifyingSimulator.Run(new QualifyingContext(1, 1950, 1, Base), field, rules);
        var b = QualifyingSimulator.Run(new QualifyingContext(1, 1950, 2, Base), field, rules);
        var c = QualifyingSimulator.Run(new QualifyingContext(1, 1951, 1, Base), field, rules);
        Assert.NotEqual(Fingerprint(a), Fingerprint(b));
        Assert.NotEqual(Fingerprint(a), Fingerprint(c));
    }

    private static QualifyingRules RulesFor(QualifyingFormat format, int field) => format switch
    {
        QualifyingFormat.SingleSession => QualifyingRules.SingleSession(),
        QualifyingFormat.TwoDay => QualifyingRules.TwoDay(),
        QualifyingFormat.OneLapShootout => QualifyingRules.OneLapShootout(),
        QualifyingFormat.Aggregate => QualifyingRules.Aggregate(),
        _ => QualifyingRules.Knockout(field),
    };

    // ---- Grid size and 1950s style -----------------------------------------------------------------------------

    [Fact]
    public void EntriesAboveTheGridLimitLeaveTwentyOrMoreNonQualifiers()
    {
        // Hand-set grid size of 24 for 46 entries, a 1950s style session with no cutoff.
        var rules = QualifyingRules.SingleSession() with { MaxGridSize = 24 };
        var result = QualifyingSimulator.Run(Ctx(), Field(46, 0.1), rules);

        Assert.Equal(24, result.Grid.Count);
        Assert.Equal(22, result.NonQualifiers.Count);
        Assert.All(result.NonQualifiers, n => Assert.Equal(NonQualifierReason.GridLimit, n.Reason));
        Assert.Equal(Enumerable.Range(1, 24), result.Grid.Select(s => s.GridPosition));

        // The slowest cars are the ones left out: every grid car is at least as fast as every non-qualifier.
        double slowestOnGrid = result.Grid.Max(s => s.ScoreSeconds);
        Assert.All(result.NonQualifiers, n => Assert.True(n.BestLapSeconds >= slowestOnGrid));
    }

    [Fact]
    public void NoGridLimitMeansEveryRunningCarStarts()
    {
        var result = QualifyingSimulator.Run(Ctx(), Field(30), QualifyingRules.TwoDay());
        Assert.Equal(30, result.Grid.Count);
        Assert.Empty(result.NonQualifiers);
    }

    [Fact]
    public void GridIsSortedByScoreAndPoleIsTheFastest()
    {
        var result = QualifyingSimulator.Run(Ctx(), Field(20, 0.1), QualifyingRules.SingleSession());
        Assert.Equal(result.Grid.OrderBy(s => s.ScoreSeconds).Select(s => s.DriverId).Count(), result.Grid.Count);
        for (int i = 1; i < result.Grid.Count; i++)
        {
            Assert.True(result.Grid[i - 1].ScoreSeconds <= result.Grid[i].ScoreSeconds);
        }

        Assert.Equal(result.Grid.Min(s => s.ScoreSeconds), result.Pole!.ScoreSeconds);
        Assert.Equal(1, result.Pole.GridPosition);
    }

    // ---- Knockout ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(20, 5, 5, 10)]
    [InlineData(22, 6, 6, 10)]
    public void KnockoutEliminationCountsFollowTheFieldSize(int field, int q1Out, int q2Out, int q3Size)
    {
        var rules = QualifyingRules.Knockout(field);
        Assert.Equal([q1Out, q2Out], rules.KnockoutEliminations);

        var result = QualifyingSimulator.Run(Ctx(), Field(field, 0.03, 0.3), rules);

        Assert.Equal(field, result.Grid.Count);
        int Reached(string segment) => result.Grid.Count(s => s.SegmentTimes.Any(t => t.SegmentId == segment));
        Assert.Equal(field, Reached("Q1"));
        Assert.Equal(field - q1Out, Reached("Q2"));
        Assert.Equal(q3Size, Reached("Q3"));

        // Q3 cars hold the front places by Q3 time, Q2 leavers the next ones by Q2 time, Q1 leavers the back by Q1 time.
        var slots = result.Grid;
        Assert.All(slots.Take(q3Size), s => Assert.Contains(s.SegmentTimes, t => t.SegmentId == "Q3"));
        Assert.All(slots.Skip(q3Size).Take(q2Out), s => Assert.Equal("Q2", s.SegmentTimes[^1].SegmentId));
        Assert.All(slots.Skip(q3Size + q2Out), s => Assert.Equal("Q1", s.SegmentTimes[^1].SegmentId));
        AssertAscending(slots.Take(q3Size).Select(s => s.ScoreSeconds));
        AssertAscending(slots.Skip(q3Size).Take(q2Out).Select(s => s.ScoreSeconds));
        AssertAscending(slots.Skip(q3Size + q2Out).Select(s => s.ScoreSeconds));
    }

    [Fact]
    public void KnockoutCutsAreDerivedForOtherFieldSizes()
    {
        Assert.Equal([7, 7], QualifyingRules.KnockoutEliminationsFor(24));
        Assert.Equal([3, 2], QualifyingRules.KnockoutEliminationsFor(15));
        Assert.Equal([0, 0], QualifyingRules.KnockoutEliminationsFor(8));
    }

    [Fact]
    public void KnockoutNeverEliminatesEverybodyInASmallField()
    {
        var rules = QualifyingRules.Knockout(20); // cuts of 5 and 5 against a field of 4
        var result = QualifyingSimulator.Run(Ctx(), Field(4), rules);
        Assert.Equal(4, result.Grid.Count);
        Assert.Contains(result.Grid, s => s.SegmentTimes.Any(t => t.SegmentId == "Q3"));
    }

    [Fact]
    public void SprintShootoutIsAShorterKnockoutOverSprintSegments()
    {
        var rules = QualifyingRules.SprintShootout(20);
        var result = QualifyingSimulator.Run(Ctx(), Field(20, 0.03), rules);

        Assert.Equal(["SQ1", "SQ2", "SQ3"], rules.Sessions.Select(s => s.Id));
        Assert.True(rules.MaxLapsPerDriver < QualifyingRules.Knockout(20).MaxLapsPerDriver);
        Assert.Equal(10, result.Grid.Count(s => s.SegmentTimes.Any(t => t.SegmentId == "SQ3")));
    }

    // ---- Cutoff -----------------------------------------------------------------------------------------------

    private static List<QualifyingEntrant> FieldWithBackmarker()
    {
        var field = Field(12, 0.05, 0.9);
        field.Add(new QualifyingEntrant("slow", "cx", 15d, 0.9)); // about 117 percent of the base lap
        return field;
    }

    [Theory]
    [InlineData(QualifyingFormat.SingleSession)]
    [InlineData(QualifyingFormat.TwoDay)]
    [InlineData(QualifyingFormat.OneLapShootout)]
    [InlineData(QualifyingFormat.Aggregate)]
    [InlineData(QualifyingFormat.Knockout)]
    public void TheCutoffKeepsAHopelesslySlowCarOffTheGrid(QualifyingFormat format)
    {
        var rules = RulesFor(format, 13) with { Cutoff = QualifyingCutoff.WithinOfPole };
        var result = QualifyingSimulator.Run(Ctx(), FieldWithBackmarker(), rules);

        var nq = Assert.Single(result.NonQualifiers);
        Assert.Equal("slow", nq.DriverId);
        Assert.Equal(NonQualifierReason.OutsideCutoff, nq.Reason);
        Assert.Equal(12, result.Grid.Count);
        Assert.DoesNotContain(result.Grid, s => s.DriverId == "slow");
    }

    [Fact]
    public void NoCutoffLetsTheSlowCarStart()
    {
        var result = QualifyingSimulator.Run(Ctx(), FieldWithBackmarker(), QualifyingRules.SingleSession());
        Assert.Equal(13, result.Grid.Count);
        Assert.Equal("slow", result.Grid[^1].DriverId);
    }

    [Fact]
    public void CutoffIsExactlyOneHundredAndSevenPercentOfPole()
    {
        var rules = QualifyingRules.SingleSession() with { Cutoff = QualifyingCutoff.WithinOfPole };
        var field = Field(10, 0.5, 0.5);
        field.Add(new QualifyingEntrant("edge", "cx", 6.0, 0.5)); // base 90 + 6 = 96, about 106.7 percent of a 90 s pole
        field.Add(new QualifyingEntrant("out", "cy", 8.5, 0.5));  // about 109.4 percent
        var result = QualifyingSimulator.Run(Ctx(), field, rules);

        double limit = result.Pole!.ScoreSeconds * 1.07;
        Assert.All(result.Grid, s => Assert.True(s.ScoreSeconds <= limit));
        Assert.All(result.NonQualifiers.Where(n => n.Reason == NonQualifierReason.OutsideCutoff), n => Assert.True(n.BestLapSeconds > limit));
        Assert.Contains(result.NonQualifiers, n => n.DriverId == "out");
    }

    [Fact]
    public void KnockoutCutoffIsMeasuredInQ1AndSkippedInTheWet()
    {
        var rules = QualifyingRules.Knockout(13) with { Cutoff = QualifyingCutoff.WithinOfFastestQ1UnlessWet };

        var dry = QualifyingSimulator.Run(Ctx(), FieldWithBackmarker(), rules);
        Assert.Contains(dry.NonQualifiers, n => n.DriverId == "slow" && n.Reason == NonQualifierReason.OutsideCutoff);

        var wet = QualifyingSimulator.Run(Ctx(declaredWet: true), FieldWithBackmarker(), rules);
        Assert.Empty(wet.NonQualifiers);
        Assert.Equal(13, wet.Grid.Count);

        var stillCut = rules with { Cutoff = QualifyingCutoff.WithinOfFastestQ1 };
        var wetButNoExemption = QualifyingSimulator.Run(Ctx(declaredWet: true), FieldWithBackmarker(), stillCut);
        Assert.Single(wetButNoExemption.NonQualifiers);
    }

    // ---- Ties -------------------------------------------------------------------------------------------------

    [Fact]
    public void EqualTimesAreBrokenByTheEarlierSetTime()
    {
        // 300 identical cars on one lap each: equal millisecond times are certain, and in a one-lap session
        // running in entry order the earlier lap is the one with the lower entry index.
        var field = Enumerable.Range(0, 300).Select(i => new QualifyingEntrant($"d{i:000}", "c", 0d, 1d)).ToList();
        var result = QualifyingSimulator.Run(Ctx(), field, QualifyingRules.SingleSession(1));

        int ties = 0;
        for (int i = 1; i < result.Grid.Count; i++)
        {
            var (a, b) = (result.Grid[i - 1], result.Grid[i]);
            Assert.True(a.ScoreSeconds <= b.ScoreSeconds);
            if (a.ScoreSeconds == b.ScoreSeconds)
            {
                ties++;
                Assert.True(string.CompareOrdinal(a.DriverId, b.DriverId) < 0, $"{a.DriverId} should be ahead of {b.DriverId}");
            }
        }

        Assert.True(ties > 20, "the fixture must actually produce ties");
    }

    // ---- Failed cars, pit lane, pre-qualifying -------------------------------------------------------------------

    [Fact]
    public void ACarThatIsNotRunningSetsNoTimeAndDoesNotQualify()
    {
        var field = Field(10);
        field[4] = field[4] with { CarNotRunning = true };
        var result = QualifyingSimulator.Run(Ctx(), field, QualifyingRules.TwoDay());

        var nq = Assert.Single(result.NonQualifiers);
        Assert.Equal("d04", nq.DriverId);
        Assert.Equal(NonQualifierReason.CarNotRunning, nq.Reason);
        Assert.Null(nq.BestLapSeconds);
        Assert.Empty(nq.SegmentTimes);
        Assert.Equal(9, result.Grid.Count);
        Assert.Equal(9 * 24, result.SimulatedLaps);
    }

    [Fact]
    public void ACarNotRunningDoesNotReRollTheOthersLuck()
    {
        // Same entrants except for one flagged car: for a single-lap session at progress ~0 the others' lap-time
        // loss is the same draw, so their times may differ only through the (tiny) evolution shift.
        var field = Field(10, 0.5, 0.5);
        var flagged = field.ToList();
        flagged[9] = flagged[9] with { CarNotRunning = true };
        var a = QualifyingSimulator.Run(Ctx(), field, QualifyingRules.SingleSession(1));
        var b = QualifyingSimulator.Run(Ctx(), flagged, QualifyingRules.SingleSession(1));
        foreach (var slot in b.Grid)
        {
            var same = a.Grid.Single(s => s.DriverId == slot.DriverId);
            Assert.InRange(Math.Abs(same.ScoreSeconds - slot.ScoreSeconds), 0d, 0.06);
        }
    }

    [Fact]
    public void PitLaneStartersGetGridPositionZeroAndTheRestCloseUp()
    {
        var field = Field(8, 0.3, 0.5);
        field[0] = field[0] with { StartsFromPitLane = true };
        field[3] = field[3] with { StartsFromPitLane = true };
        var result = QualifyingSimulator.Run(Ctx(), field, QualifyingRules.SingleSession());

        Assert.Equal(["d00", "d03"], result.PitLaneStarters.Select(s => s.DriverId).OrderBy(x => x).ToArray());
        Assert.All(result.PitLaneStarters, s => Assert.Equal(0, s.GridPosition));
        Assert.Equal(Enumerable.Range(1, 6), result.Grid.Where(s => !s.StartsFromPitLane).Select(s => s.GridPosition));
        Assert.Equal(8, result.Grid.Count);
        Assert.True(result.Grid.Skip(6).All(s => s.StartsFromPitLane), "pit-lane starters come last in grid order");
    }

    [Fact]
    public void PitLaneStartersStillCountAgainstTheGridLimit()
    {
        var field = Field(8, 0.3);
        field[0] = field[0] with { StartsFromPitLane = true };
        var result = QualifyingSimulator.Run(Ctx(), field, QualifyingRules.SingleSession() with { MaxGridSize = 5 });
        Assert.Equal(5, result.Grid.Count);
        Assert.Equal(3, result.NonQualifiers.Count);
    }

    [Fact]
    public void OnlyTheFastestFlaggedPreQualifiersAdvance()
    {
        var field = Field(14, 0.2, 0.9);
        for (int i = 8; i < 14; i++)
        {
            field[i] = field[i] with { InPreQualifying = true };
        }

        var rules = QualifyingRules.SingleSession() with { PreQualifying = new PreQualifyingOptions(6, 4) };
        var result = QualifyingSimulator.Run(Ctx(), field, rules);

        Assert.Equal(6, result.PreQualifying.Count);
        Assert.Equal(4, result.PreQualifying.Count(p => p.Advanced));
        Assert.Equal(2, result.NonQualifiers.Count(n => n.Reason == NonQualifierReason.FailedPreQualifying));
        Assert.Equal(12, result.Grid.Count);
        // Those who failed never reach the main session, those who advanced have a main-session time.
        foreach (var nq in result.NonQualifiers)
        {
            Assert.Equal(["PQ"], nq.SegmentTimes.Select(t => t.SegmentId));
        }

        Assert.All(result.Grid, s => Assert.DoesNotContain(s.SegmentTimes, t => t.SegmentId == "PQ"));
        var order = result.PreQualifying.Select(p => p.BestLapSeconds).ToArray();
        AssertAscending(order);
    }

    [Fact]
    public void PreQualifyingIsSkippedWhenNobodyIsFlagged()
    {
        var rules = QualifyingRules.SingleSession() with { PreQualifying = new PreQualifyingOptions(6, 4) };
        var withRules = QualifyingSimulator.Run(Ctx(), Field(10), rules);
        var without = QualifyingSimulator.Run(Ctx(), Field(10), QualifyingRules.SingleSession());
        Assert.Empty(withRules.PreQualifying);
        Assert.Equal(Fingerprint(without), Fingerprint(withRules));
    }

    // ---- Formats ----------------------------------------------------------------------------------------------

    [Fact]
    public void TwoDayCountsTheBestLapOfBothSessions()
    {
        var result = QualifyingSimulator.Run(Ctx(), Field(12, 0.1), QualifyingRules.TwoDay());
        Assert.All(result.Grid, s =>
        {
            Assert.Equal(["Day1", "Day2"], s.SegmentTimes.Select(t => t.SegmentId));
            Assert.Equal(s.SegmentTimes.Min(t => t.BestLapSeconds), s.ScoreSeconds);
            Assert.Equal(s.ScoreSeconds, s.BestLapSeconds);
        });
    }

    [Fact]
    public void AggregateAddsTheCountingTimes()
    {
        var result = QualifyingSimulator.Run(Ctx(), Field(12, 0.1), QualifyingRules.Aggregate());
        Assert.All(result.Grid, s => Assert.Equal(s.SegmentTimes.Sum(t => t.BestLapSeconds), s.ScoreSeconds, 6));
        AssertAscending(result.Grid.Select(s => s.ScoreSeconds));
    }

    [Fact]
    public void ShootoutFirstRunOnlySetsTheRunningOrder()
    {
        var result = QualifyingSimulator.Run(Ctx(), Field(12, 0.1), QualifyingRules.OneLapShootout());
        Assert.All(result.Grid, s =>
        {
            Assert.Equal(["Run1", "Run2"], s.SegmentTimes.Select(t => t.SegmentId));
            Assert.Equal(s.SegmentTimes[1].BestLapSeconds, s.ScoreSeconds);
        });
        Assert.Equal(24, result.SimulatedLaps);
    }

    [Fact]
    public void TrackEvolutionMakesLaterSessionsQuicker()
    {
        var result = QualifyingSimulator.Run(Ctx(), Field(20, 0.0, 0.5), QualifyingRules.TwoDay());
        double day1 = result.Grid.Average(s => s.SegmentTimes[0].BestLapSeconds);
        double day2 = result.Grid.Average(s => s.SegmentTimes[1].BestLapSeconds);
        Assert.True(day2 < day1, $"day 2 ({day2}) should be quicker than day 1 ({day1})");
    }

    [Fact]
    public void NoLapBeatsTheIdealLapMinusAllTrackEvolution()
    {
        var field = Field(15, 0.2, 0.0);
        var result = QualifyingSimulator.Run(Ctx(), field, QualifyingRules.TwoDay());
        foreach (var slot in result.Grid)
        {
            double ideal = Base + field.Single(e => e.DriverId == slot.DriverId).PaceSeconds;
            Assert.True(slot.BestLapSeconds >= ideal - QualifyingConstants.TrackEvolutionSeconds - 0.001);
        }
    }

    [Fact]
    public void ConsistentDriversLoseLessToNoise()
    {
        var consistent = Enumerable.Range(0, 60).Select(i => new QualifyingEntrant($"a{i}", "c", 0d, 1d)).ToList();
        var erratic = Enumerable.Range(0, 60).Select(i => new QualifyingEntrant($"b{i}", "c", 0d, 0d)).ToList();
        var result = QualifyingSimulator.Run(Ctx(), consistent.Concat(erratic).ToList(), QualifyingRules.SingleSession(2));

        double Mean(string prefix) => result.Grid.Where(s => s.DriverId.StartsWith(prefix)).Average(s => s.ScoreSeconds);
        Assert.True(Mean("a") < Mean("b"));
    }

    [Fact]
    public void WetnessMultiplierSlowsEveryLap()
    {
        var dry = QualifyingSimulator.Run(Ctx(), Field(10), QualifyingRules.SingleSession());
        var wet = QualifyingSimulator.Run(Ctx(wet: 1.15), Field(10), QualifyingRules.SingleSession());
        Assert.True(wet.Pole!.ScoreSeconds > dry.Pole!.ScoreSeconds + 10d);
    }

    // ---- Bounds, input checks, summary ---------------------------------------------------------------------------

    [Theory]
    [InlineData(QualifyingFormat.SingleSession)]
    [InlineData(QualifyingFormat.TwoDay)]
    [InlineData(QualifyingFormat.OneLapShootout)]
    [InlineData(QualifyingFormat.Aggregate)]
    [InlineData(QualifyingFormat.Knockout)]
    public void SimulatedLapsStayWithinTheRulesBound(QualifyingFormat format)
    {
        var rules = RulesFor(format, 24);
        var field = Field(24);
        var result = QualifyingSimulator.Run(Ctx(), field, rules);
        Assert.InRange(result.SimulatedLaps, 1, field.Count * rules.MaxLapsPerDriver);
    }

    [Fact]
    public void ALargeEntryStaysBounded()
    {
        var rules = QualifyingRules.SingleSession() with { PreQualifying = new PreQualifyingOptions(8, 4) };
        var field = Field(60).Select(e => e with { InPreQualifying = true }).ToList();
        var result = QualifyingSimulator.Run(Ctx(), field, rules);
        Assert.True(result.SimulatedLaps <= 60 * rules.MaxLapsPerDriver);
        Assert.Equal(60 * 8 + 4 * 12, result.SimulatedLaps);
    }

    [Fact]
    public void EmptyEntryGivesAnEmptyResult()
    {
        var result = QualifyingSimulator.Run(Ctx(), [], QualifyingRules.Knockout(20));
        Assert.Empty(result.Grid);
        Assert.Empty(result.NonQualifiers);
        Assert.Null(result.Pole);
        Assert.Equal(0, result.SimulatedLaps);
    }

    [Fact]
    public void BadInputIsRejected()
    {
        var ok = Field(3);
        var rules = QualifyingRules.SingleSession();
        Assert.Throws<ArgumentException>(() => QualifyingSimulator.Run(Ctx(), [ok[0], ok[0]], rules));
        Assert.Throws<ArgumentException>(() => QualifyingSimulator.Run(Ctx(), [ok[0] with { Consistency = 1.5 }], rules));
        Assert.Throws<ArgumentException>(() => QualifyingSimulator.Run(Ctx(), [ok[0] with { PaceSeconds = double.NaN }], rules));
        Assert.Throws<ArgumentException>(() => QualifyingSimulator.Run(new QualifyingContext(1, 1950, 1, 0d), ok, rules));
        Assert.Throws<ArgumentException>(() => QualifyingSimulator.Run(Ctx(wet: 0.9), ok, rules));
        Assert.Throws<ArgumentException>(() => QualifyingSimulator.Run(Ctx(), ok, rules with { MaxGridSize = 0 }));
        Assert.Throws<ArgumentException>(() => QualifyingSimulator.Run(Ctx(), ok, rules with { KnockoutEliminations = [1] }));
        Assert.Throws<ArgumentException>(() => QualifyingSimulator.Run(Ctx(), ok, QualifyingRules.Knockout(20) with { KnockoutEliminations = [1] }));
    }

    [Fact]
    public void SummaryIsTranslatableAndMatchesTheResult()
    {
        var field = Field(6, 0.3);
        field[1] = field[1] with { CarNotRunning = true };
        field[2] = field[2] with { StartsFromPitLane = true };
        var rules = QualifyingRules.SingleSession() with { MaxGridSize = 4 };
        var result = QualifyingSimulator.Run(Ctx(), field, rules);

        var keys = result.Summary.Select(l => l.Key).ToList();
        Assert.Equal("qualifying.summary.pole", keys[0]);
        Assert.Contains("qualifying.summary.gridSize", keys);
        Assert.Contains("qualifying.summary.pitLane", keys);
        Assert.Contains("qualifying.summary.dnq.carNotRunning", keys);
        Assert.Contains("qualifying.summary.dnq.gridLimit", keys);

        var pole = result.Summary[0];
        Assert.Equal(result.Pole!.DriverId, pole.Args["driver"]);
        Assert.Equal(QualifyingResult.FormatLapTime(result.Pole.ScoreSeconds), pole.Args["time"]);

        var strings = Path.Combine(RepoPaths.Root(), "strings");
        foreach (var file in new[] { "pl.json", "en.json" })
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(strings, file)));
            foreach (var key in QualifyingSummaryKeys())
            {
                Assert.True(doc.RootElement.TryGetProperty(key, out _), $"{file} is missing {key}");
            }
        }
    }

    private static IEnumerable<string> QualifyingSummaryKeys() =>
    [
        "qualifying.summary.pole",
        "qualifying.summary.gridSize",
        "qualifying.summary.pitLane",
        "qualifying.summary.dnq.carNotRunning",
        "qualifying.summary.dnq.failedPreQualifying",
        "qualifying.summary.dnq.outsideCutoff",
        "qualifying.summary.dnq.gridLimit",
    ];

    [Theory]
    [InlineData(90.0, "1:30.000")]
    [InlineData(59.9996, "1:00.000")]
    [InlineData(83.4567, "1:23.457")]
    [InlineData(125.05, "2:05.050")]
    public void LapTimesAreFormattedWithoutCulture(double seconds, string expected) =>
        Assert.Equal(expected, QualifyingResult.FormatLapTime(seconds));

    // ---- Catalog ----------------------------------------------------------------------------------------------

    [Fact]
    public void EveryCatalogValueMapsToRules()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RepoPaths.Root(), "data", "authored", "regulations", "catalog.json")));
        string[] Values(string id) => doc.RootElement.EnumerateArray()
            .Single(d => d.GetProperty("id").GetString() == id)
            .GetProperty("values").EnumerateArray().Select(v => v.GetString()!).ToArray();

        foreach (var format in Values("qualifying_format"))
        {
            QualifyingRules.FromCatalog(format, "none", "not_held", "no_single_cap_confirmed", 20).Validate();
        }

        foreach (var cutoff in Values("qualifying_time_cutoff"))
        {
            QualifyingRules.FromCatalog("knockout_q1_q2_q3", cutoff, "not_held", "twenty_six", 20).Validate();
        }

        foreach (var pre in Values("pre_qualifying_session"))
        {
            QualifyingRules.FromCatalog("two_session_best_time", "none", pre, "twenty_six", 30).Validate();
        }

        foreach (var cap in Values("maximum_grid"))
        {
            QualifyingRules.FromCatalog("two_session_best_time", "none", "not_held", cap, 30).Validate();
        }

        foreach (var sprint in Values("sprint_format"))
        {
            QualifyingRules.SprintShootoutFromCatalog(sprint, 20)?.Validate();
        }
    }

    [Fact]
    public void CatalogValuesGiveTheExpectedEraRules()
    {
        var y1950 = QualifyingRules.FromCatalog("two_session_best_time", "none", "no_regular_session", "no_single_cap_confirmed", 20);
        Assert.Equal(QualifyingFormat.TwoDay, y1950.Format);
        Assert.Null(y1950.MaxGridSize);
        Assert.Null(y1950.PreQualifying);

        var y1989 = QualifyingRules.FromCatalog("two_session_best_time", "none", "four_fastest_advance", "twenty_six", 30);
        Assert.Equal(26, y1989.MaxGridSize);
        Assert.Equal(4, y1989.PreQualifying!.AdvancingCount);

        var y1996 = QualifyingRules.FromCatalog("saturday_twelve_lap", "within_107_of_pole", "not_held", "twenty_six", 26);
        Assert.Equal(QualifyingFormat.SingleSession, y1996.Format);
        Assert.Equal(QualifyingCutoff.WithinOfPole, y1996.Cutoff);

        var y1993Early = QualifyingRules.FromCatalog("two_session_best_time", "none", "not_held", "twenty_five_then_twenty_six", 26);
        var y1993Late = QualifyingRules.FromCatalog("two_session_best_time", "none", "not_held", "twenty_five_then_twenty_six", 26, lateInSeason: true);
        Assert.Equal(25, y1993Early.MaxGridSize);
        Assert.Equal(26, y1993Late.MaxGridSize);

        var y2026 = QualifyingRules.FromCatalog("knockout_q1_q2_q3", "within_107_of_fastest_q1_unless_wet", "not_held", "twenty_six", 22);
        Assert.Equal([6, 6], y2026.KnockoutEliminations);

        Assert.Null(QualifyingRules.SprintShootoutFromCatalog("sprint_points_top_8_sets_grid", 20));
        Assert.NotNull(QualifyingRules.SprintShootoutFromCatalog("friday_sprint_qualifying", 22));
    }

    [Fact]
    public void UnknownCatalogValuesThrow()
    {
        Assert.Throws<ArgumentException>(() => QualifyingRules.FromCatalog("nope", "none", "not_held", "twenty_six", 20));
        Assert.Throws<ArgumentException>(() => QualifyingRules.FromCatalog("knockout_q1_q2_q3", "nope", "not_held", "twenty_six", 20));
        Assert.Throws<ArgumentException>(() => QualifyingRules.FromCatalog("knockout_q1_q2_q3", "none", "nope", "twenty_six", 20));
        Assert.Throws<ArgumentException>(() => QualifyingRules.FromCatalog("knockout_q1_q2_q3", "none", "not_held", "nope", 20));
        Assert.Throws<ArgumentException>(() => QualifyingRules.SprintShootoutFromCatalog("nope", 20));
    }

    private static void AssertAscending(IEnumerable<double> values)
    {
        double? previous = null;
        foreach (double v in values)
        {
            Assert.True(previous is null || previous <= v, $"{previous} should not be above {v}");
            previous = v;
        }
    }
}
