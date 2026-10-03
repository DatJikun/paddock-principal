using Paddock.Domain.Career;
using Paddock.Domain.Racing;
using Paddock.Simulation.Racing.Incidents;
using Paddock.Simulation.Racing.Points;
using Paddock.Simulation.Racing.Weekend;

namespace Paddock.Tests.Racing.Weekend;

/// <summary>
/// Incidents, neutralisation and the pass rule, on a deliberately violent weekend (a very risky era profile and the most
/// dangerous circuit the model allows) so every branch of the race loop is met. ESTIMATE bounds throughout.
/// </summary>
public class WeekendIncidentTests
{
    private static readonly EraSafetyProfile Violent = new(FatalityRiskBand.VeryHigh, NeutralisationRules.SafetyCarAndVsc, RedFlagAvailable: true);

    private static RaceWeekendInput Chaos(int round, ulong seed, FatalityLevel fatality) =>
        WeekendTestKit.Input(1988, round, seed) with { Safety = Violent, TrackDanger = IncidentConstants.MaxTrackDanger, Fatality = fatality };

    private static RaceWeekendResult[] Chaotic(FatalityLevel fatality, int count = 40) =>
        Enumerable.Range(0, count)
            .AsParallel()
            .AsOrdered()
            .Select(i => WeekendTestKit.Run(Chaos((i % 4) + 1, 5_000UL + (ulong)i, fatality)))
            .ToArray();

    private static readonly Lazy<RaceWeekendResult[]> FatalitiesOff = new(() => Chaotic(FatalityLevel.Off));
    private static readonly Lazy<RaceWeekendResult[]> FatalitiesOn = new(() => Chaotic(FatalityLevel.On));

    [Fact]
    public void NobodyDies_WhenFatalitiesAreOff_ButCareersCanEnd()
    {
        var people = FatalitiesOff.Value.SelectMany(r => r.PersonOutcomes).ToList();

        Assert.DoesNotContain(people, p => p.Fatal);
        Assert.Contains(people, p => p.Injury == InjuryGrade.CareerEnding);
        Assert.Contains(people, p => p.Injury == InjuryGrade.Serious);
        Assert.Contains(people, p => p.Injury == InjuryGrade.Light);
    }

    [Fact]
    public void ADeathIsPossible_OnlyWhenFatalitiesAreOn_AndIsAFactNotAnAppliedState()
    {
        var deaths = FatalitiesOn.Value.SelectMany(r => r.PersonOutcomes).Where(p => p.Fatal).ToList();

        Assert.NotEmpty(deaths);
        Assert.All(deaths, p =>
        {
            Assert.True(p.Retired);
            Assert.Equal(RetirementReason.Accident, p.Reason);
            Assert.NotNull(p.Lap);
        });
    }

    [Fact]
    public void EveryNeutralisationKind_IsMet_AndEveryTapeStaysValid()
    {
        var races = FatalitiesOn.Value.Concat(FatalitiesOff.Value).ToList();
        var kinds = races.SelectMany(r => r.Neutralisations).ToList();

        Assert.Contains(kinds, n => n.Kind == NeutralisationKind.SafetyCar);
        Assert.Contains(kinds, n => n.Kind == NeutralisationKind.VirtualSafetyCar);
        Assert.Contains(kinds, n => n is { Kind: NeutralisationKind.RedFlag, RaceResumed: true });
        Assert.Contains(kinds, n => n is { Kind: NeutralisationKind.RedFlag, RaceResumed: false });

        foreach (var race in races)
        {
            // A red flag that ends the race shortens it; the winner is flagged on the last lap that was run.
            Assert.Equal(race.LapsRun, race.CarResults[0].LapsCompleted);
            Assert.True(race.LapsRun <= race.ScheduledLaps);
            var ended = race.Neutralisations.Any(n => n is { Kind: NeutralisationKind.RedFlag, RaceResumed: false });
            Assert.Equal(ended, race.LapsRun < race.ScheduledLaps);
            Assert.Equal(race.Tape.Events.OfType<RedFlag>().Count(), race.Neutralisations.Count(n => n.Kind == NeutralisationKind.RedFlag));

            // Under a safety car the field is bunched: on a lap with no stops, no car is more than the bunching gap behind the next.
            foreach (var n in race.Neutralisations.Where(n => n.Kind == NeutralisationKind.SafetyCar && !race.PitStops.Any(p => p.Lap == n.LastLap)))
            {
                var lap = race.LapRecords.Where(l => l.Lap == n.LastLap && l.Neutralised).OrderBy(l => l.EndSeconds).ToList();
                for (var i = 1; i < lap.Count; i++)
                {
                    var gap = lap[i].EndSeconds - lap[i - 1].EndSeconds;
                    Assert.True(gap <= WeekendConstants.BunchedGapSeconds + 1e-6, $"gap of {gap:R} s behind a safety car on lap {n.LastLap}");
                }
            }
        }
    }

    [Fact]
    public void AResumedRedFlag_StopsTheClockOfTheRace()
    {
        var race = FatalitiesOn.Value.First(r => r.Neutralisations.Any(n => n is { Kind: NeutralisationKind.RedFlag, RaceResumed: true }));
        var flag = race.Neutralisations.First(n => n is { Kind: NeutralisationKind.RedFlag, RaceResumed: true });

        var before = race.LapRecords.Where(l => l.Lap == flag.IncidentLap).Min(l => l.EndSeconds);
        var after = race.LapRecords.Where(l => l.Lap == flag.IncidentLap + 1).Min(l => l.EndSeconds);

        // The stoppage is a number of reference laps: the next lap ends much later than a lap of racing would.
        Assert.True(after - before > 3 * 60, $"the leader needed {after - before:F0} s for the lap after the red flag");
    }

    // ---- The pass rule -----------------------------------------------------------------------------------------

    private sealed class Never : IPassModel
    {
        public double OvertakeMarginSeconds => WeekendConstants.OvertakeMarginSeconds;

        public bool Passes(in PassSituation situation) => false;
    }

    private sealed class Always : IPassModel
    {
        public double OvertakeMarginSeconds => WeekendConstants.OvertakeMarginSeconds;

        public bool Passes(in PassSituation situation) => true;
    }

    private static int HeldLaps(RaceWeekendResult result) => result.LapRecords.Count(l => l.Held);

    [Fact]
    public void ThePassModel_IsReplaceable_AndDecidesHowManyCarsAreHeldUp()
    {
        var input = WeekendTestKit.Input(2012, seed: 11);

        var never = WeekendTestKit.Run(input with { PassModel = new Never() });
        var always = WeekendTestKit.Run(input with { PassModel = new Always() });

        Assert.Equal(0, HeldLaps(always));
        Assert.True(HeldLaps(never) > 50, "a field that can never pass is held up all the time");
        Assert.True(
            always.Tape.Events.OfType<PositionChange>().Count() > never.Tape.Events.OfType<PositionChange>().Count(),
            "passing makes more position changes than not passing");
    }
}

public class MarginPassModelTests
{
    private static PassSituation Situation(double advantage, double overtaking = 50, double defending = 50) =>
        new(GapSeconds: 0.4, ChaserLapSeconds: 90 - advantage, AheadLapSeconds: 90, overtaking, defending);

    [Fact]
    public void ACarPasses_OnlyWithMoreThanTheMargin()
    {
        var model = MarginPassModel.Default;

        Assert.False(model.Passes(Situation(0.5)));
        Assert.False(model.Passes(Situation(WeekendConstants.OvertakeMarginSeconds)));
        Assert.True(model.Passes(Situation(WeekendConstants.OvertakeMarginSeconds + 0.01)));
    }

    [Fact]
    public void OvertakingVersusDefending_MovesTheMargin()
    {
        var model = MarginPassModel.Default;
        var even = model.RequiredAdvantageSeconds(50, 50);

        Assert.Equal(WeekendConstants.OvertakeMarginSeconds, even, 12);
        Assert.True(model.RequiredAdvantageSeconds(90, 30) < even, "an artist of overtaking needs less");
        Assert.True(model.RequiredAdvantageSeconds(30, 90) > even, "a hard defender needs more");
        Assert.True(model.Passes(Situation(even * 0.9, overtaking: 100, defending: 0)));
        Assert.False(model.Passes(Situation(even * 1.1, overtaking: 0, defending: 100)));
    }

    [Fact]
    public void TheMargin_StaysPositive_ForAnyRatings()
    {
        var model = MarginPassModel.Default;

        foreach (var over in new[] { 0d, 50d, 100d })
        {
            foreach (var def in new[] { 0d, 50d, 100d })
            {
                Assert.True(model.RequiredAdvantageSeconds(over, def) > 0);
            }
        }
    }
}
