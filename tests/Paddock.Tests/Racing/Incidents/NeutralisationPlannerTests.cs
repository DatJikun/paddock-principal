using Paddock.Simulation.Racing.Incidents;
using static Paddock.Tests.Racing.Incidents.IncidentTestKit;

namespace Paddock.Tests.Racing.Incidents;

public class NeutralisationPlannerTests
{
    private const int Laps = 60;

    private static Neutralisation Plan(int season, IncidentResult incident, int laps = Laps) =>
        NeutralisationPlanner.Plan(incident, EraOf(season), laps);

    [Fact]
    public void EraProfiles_FollowTheAuthoredData()
    {
        Assert.Equal(FatalityRiskBand.VeryHigh, EraOf(1955).Risk);
        Assert.Equal(FatalityRiskBand.VeryLow, EraOf(2015).Risk);
        Assert.Equal(NeutralisationRules.None, EraOf(1955).Rules);
        Assert.Equal(NeutralisationRules.MarshalsAndYellowFlags, EraOf(1965).Rules);
        Assert.Equal(NeutralisationRules.MarshalsAndYellowFlags, EraOf(1992).Rules);
        Assert.Equal(NeutralisationRules.SafetyCar, EraOf(1993).Rules);
        Assert.Equal(NeutralisationRules.SafetyCar, EraOf(2014).Rules);
        Assert.Equal(NeutralisationRules.SafetyCarAndVsc, EraOf(2015).Rules);
        Assert.False(EraOf(1955).RedFlagAvailable);
        Assert.True(EraOf(1975).RedFlagAvailable);

        // Every season of the game parses.
        for (var season = 1950; season <= 2026; season++)
        {
            _ = EraOf(season);
        }
    }

    [Fact]
    public void Authored_ValuesAreValidated()
    {
        Assert.Throws<ArgumentException>(() => FatalityRiskBands.Parse("extreme"));
        Assert.Throws<ArgumentException>(() => EraSafetyProfile.FromAuthored(2000, "low", "hovercraft"));
    }

    [Theory]
    [InlineData(1955, NeutralisationKind.None)]
    [InlineData(1975, NeutralisationKind.LocalYellow)]
    [InlineData(1995, NeutralisationKind.SafetyCar)]
    [InlineData(2005, NeutralisationKind.SafetyCar)]
    [InlineData(2020, NeutralisationKind.VirtualSafetyCar)]
    public void ASingleCarRetirement_IsNeutralisedAccordingToTheYear(int season, NeutralisationKind expected)
    {
        var plan = Plan(season, Single(OutcomeSeverity.Retire, kind: IncidentKind.Spin));

        Assert.Equal(expected, plan.Kind);
        Assert.Equal(10, plan.StartLap);
    }

    [Fact]
    public void SafetyCarAvailability_ByYear()
    {
        var retire = Single(OutcomeSeverity.Retire, kind: IncidentKind.Collision, other: IncidentOutcome.Retire);
        var kinds = new[] { 1955, 1975, 1992, 1993, 2014, 2015, 2025 }
            .ToDictionary(season => season, season => Plan(season, retire).Kind);

        Assert.Equal(NeutralisationKind.None, kinds[1955]);
        Assert.Equal(NeutralisationKind.LocalYellow, kinds[1975]);
        Assert.Equal(NeutralisationKind.LocalYellow, kinds[1992]);
        Assert.Equal(NeutralisationKind.SafetyCar, kinds[1993]);
        Assert.Equal(NeutralisationKind.SafetyCar, kinds[2014]);
        Assert.Equal(NeutralisationKind.SafetyCar, kinds[2015]);
        Assert.Equal(NeutralisationKind.SafetyCar, kinds[2025]);
    }

    [Fact]
    public void Durations_FollowTheKindOfNeutralisation()
    {
        Assert.Equal(
            IncidentConstants.VscLaps,
            Plan(2020, Single(OutcomeSeverity.Retire, kind: IncidentKind.Barrier)).DurationLaps);
        Assert.Equal(
            IncidentConstants.SafetyCarRetirementLaps,
            Plan(1995, Single(OutcomeSeverity.Retire, kind: IncidentKind.Barrier)).DurationLaps);
        Assert.Equal(
            IncidentConstants.SafetyCarMultiCarLaps,
            Plan(1995, Single(OutcomeSeverity.Retire, kind: IncidentKind.Collision, other: IncidentOutcome.Minor)).DurationLaps);
        Assert.Equal(0, Plan(1975, Single(OutcomeSeverity.Retire)).DurationLaps);
    }

    [Fact]
    public void ANoConsequenceIncident_NeverNeutralises_AndAMinorOneOnlyWavesYellowFlags()
    {
        foreach (var season in new[] { 1955, 1975, 1995, 2020 })
        {
            Assert.Equal(NeutralisationKind.None, Plan(season, Single(OutcomeSeverity.None)).Kind);
        }

        Assert.Equal(NeutralisationKind.None, Plan(1955, Single(OutcomeSeverity.Minor)).Kind);
        Assert.Equal(NeutralisationKind.LocalYellow, Plan(1975, Single(OutcomeSeverity.Minor)).Kind);
        Assert.Equal(NeutralisationKind.LocalYellow, Plan(2020, Single(OutcomeSeverity.Minor)).Kind);
    }

    [Fact]
    public void SevereInjuries_CallARedFlag_WhereOneIsAvailable()
    {
        var career = Single(OutcomeSeverity.Injury, InjuryGrade.CareerEnding);
        var serious = Single(OutcomeSeverity.Injury, InjuryGrade.Serious);

        Assert.Equal(NeutralisationKind.None, Plan(1955, career).Kind);
        foreach (var season in new[] { 1975, 1995, 2020 })
        {
            var plan = Plan(season, career);
            Assert.Equal(NeutralisationKind.RedFlag, plan.Kind);
            Assert.Equal(IncidentConstants.RedFlagSevereLaps, plan.DurationLaps);
            Assert.True(plan.ResumesRace);
            Assert.Equal(IncidentConstants.RedFlagSeriousLaps, Plan(season, serious).DurationLaps);
        }
    }

    [Fact]
    public void AFatalIncident_IsNeutralisedLikeACareerEndingOne()
    {
        foreach (var season in new[] { 1955, 1975, 1995, 2020 })
        {
            Assert.Equal(
                Plan(season, Single(OutcomeSeverity.Injury, InjuryGrade.CareerEnding)),
                Plan(season, Single(OutcomeSeverity.Fatal)));
        }
    }

    [Fact]
    public void ARedFlagLateInTheRace_DoesNotResume()
    {
        var early = Plan(2020, Single(OutcomeSeverity.Injury, InjuryGrade.Serious, lap: 20));
        var late = Plan(2020, Single(OutcomeSeverity.Injury, InjuryGrade.Serious, lap: 50));

        Assert.True(early.ResumesRace);
        Assert.False(late.ResumesRace);
        Assert.Equal(NeutralisationKind.RedFlag, late.Kind);
    }

    [Fact]
    public void NeutralisationIsCutOffAtTheEndOfTheRace()
    {
        var retire = Single(OutcomeSeverity.Retire, kind: IncidentKind.Collision, lap: 59, other: IncidentOutcome.Retire);

        Assert.Equal(2, Plan(1995, retire).DurationLaps);
        Assert.Equal(1, Plan(1995, Single(OutcomeSeverity.Retire, lap: 60)).DurationLaps);
    }

    [Fact]
    public void ASeriousInjuryWithoutARedFlag_GetsALongSafetyCar()
    {
        var era = new EraSafetyProfile(FatalityRiskBand.Moderate, NeutralisationRules.SafetyCar, RedFlagAvailable: false);

        var plan = NeutralisationPlanner.Plan(Single(OutcomeSeverity.Injury, InjuryGrade.Serious), era, Laps);

        Assert.Equal(NeutralisationKind.SafetyCar, plan.Kind);
        Assert.Equal(IncidentConstants.SafetyCarSeriousLaps, plan.DurationLaps);
    }

    [Fact]
    public void BadInputs_AreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Plan(2020, Single(OutcomeSeverity.Retire, lap: 10), laps: 9));
        Assert.Throws<ArgumentNullException>(() => NeutralisationPlanner.Plan(null!, EraOf(2020), Laps));
    }

    [Fact]
    public void SampledIncidents_AlwaysGetAPlan_ThatMatchesTheEra()
    {
        foreach (var season in new[] { 1955, 1975, 1995, 2020 })
        {
            var era = EraOf(season);
            foreach (var incident in SampleIncidents(2000, Race(era, fatalities: true, danger: 3), Stream(season, 1)))
            {
                var plan = NeutralisationPlanner.Plan(incident, era, Laps);
                Assert.Equal(incident.Lap, plan.StartLap);
                Assert.True(plan.Kind != NeutralisationKind.SafetyCar || era.HasSafetyCar);
                Assert.True(plan.Kind != NeutralisationKind.VirtualSafetyCar || era.HasVsc);
                Assert.True(plan.Kind != NeutralisationKind.RedFlag || era.RedFlagAvailable);
                Assert.True(plan.Kind != NeutralisationKind.LocalYellow || era.Rules != NeutralisationRules.None);
                Assert.True(era.Rules != NeutralisationRules.None || plan.Kind == NeutralisationKind.None);
            }
        }
    }
}
