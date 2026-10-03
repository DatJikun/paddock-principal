using Paddock.Domain.Random;
using Paddock.Simulation.Racing.Incidents;
using static Paddock.Tests.Racing.Incidents.IncidentTestKit;

namespace Paddock.Tests.Racing.Incidents;

public class IncidentSamplerTests
{
    [Fact]
    public void SameInputs_GiveTheSameResult_AndTheStreamIsNotAdvanced()
    {
        var stream = Stream(1955, 3);
        var stateBefore = stream.State;
        var race = Race(1955, fatalities: true, danger: 3);

        var first = SampleIncidents(200, race, stream, lap: 1).ToList();
        var second = SampleIncidents(200, race, stream, lap: 1).ToList();

        Assert.Equal(first, second);
        Assert.Equal(stateBefore, stream.State);
        Assert.Equal(200, first.Count);
    }

    [Fact]
    public void DifferentSeedsAndRounds_GiveDifferentIncidents()
    {
        var race = Race(1955, danger: 3);

        var baseline = SampleIncidents(100, race, Stream(1955, 1)).ToList();

        Assert.NotEqual(baseline, SampleIncidents(100, race, Stream(1955, 2)).ToList());
        Assert.NotEqual(baseline, SampleIncidents(100, race, Stream(1955, 1, Seed + 1)).ToList());
    }

    [Fact]
    public void RaceStream_IsTheIncidentsStreamOfTheRaceAndSeason()
    {
        var expected = RngStream.Derive(Seed, RngStreamName.Incidents, 1962, 4);

        Assert.Equal(expected.State, IncidentSampler.DeriveRaceStream(Seed, 1962, 4).State);
        Assert.Equal(RngStreamName.Incidents, IncidentSampler.DeriveRaceStream(Seed, 1962, 4).Name);
        Assert.NotEqual(expected.State, IncidentSampler.DeriveRaceStream(Seed, 1962, 5).State);
    }

    [Fact]
    public void CarA_DoesNotDependOnWhichOtherCarsAreInTheRace()
    {
        var stream = Stream(1955, 1);
        var race = Race(1955, danger: 4);
        var carA = new IncidentCar("car-a", 60, 40);

        List<IncidentResult?> RunAlone() =>
            Enumerable.Range(1, 60).Select(lap => IncidentSampler.SampleLap(stream, carA, Lap(lap, 0.3), race)).ToList();

        var alone = RunAlone();

        // Sample 30 other cars first, in between and after: nothing about car A may change.
        var withCompany = new List<IncidentResult?>();
        for (var lap = 1; lap <= 60; lap++)
        {
            for (var other = 0; other < 30; other++)
            {
                _ = IncidentSampler.SampleLap(stream, new IncidentCar("other-" + other, 90, 10), Lap(lap, 0.3), race);
            }

            withCompany.Add(IncidentSampler.SampleLap(stream, carA, Lap(lap, 0.3), race));
        }

        Assert.Equal(alone, withCompany);
        Assert.Contains(alone, r => r is not null);
    }

    [Fact]
    public void CarsAndLapsHaveTheirOwnDraws()
    {
        var stream = Stream(1955, 1);
        var race = Race(1955, danger: 5);
        var rows = new List<IncidentResult?>();
        for (var lap = 1; lap <= 3; lap++)
        {
            for (var car = 0; car < 400; car++)
            {
                rows.Add(IncidentSampler.SampleLap(stream, new IncidentCar("c" + car, 50, 50), Lap(lap), race));
            }
        }

        Assert.True(rows.Count(r => r is not null) > 30);
        // A different car id or lap never reuses another's draws.
        var a = IncidentSampler.SampleLap(stream, new IncidentCar("x", 50, 50), Lap(1), Race(1955, danger: 5));
        var b = IncidentSampler.SampleLap(stream, new IncidentCar("y", 50, 50), Lap(1), Race(1955, danger: 5));
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void TheOrderOfTheNearbyCarsDoesNotMatter()
    {
        var stream = Stream(1955, 1);
        var race = Race(1955, danger: 5);
        string[] forward = ["n1", "n2", "n3", "n4"];
        string[] backward = ["n4", "n3", "n2", "n1"];

        for (var i = 0; i < 300; i++)
        {
            var car = new IncidentCar("c" + i, 50, 50);
            Assert.Equal(
                IncidentSampler.SampleLap(stream, car, Lap(1, 0, forward), race),
                IncidentSampler.SampleLap(stream, car, Lap(1, 0, backward), race));
        }
    }

    [Fact]
    public void FatalitiesDisabled_NoFatalOutcomeEverOccurs_OverOneHundredThousandIncidents()
    {
        // The most dangerous setting there is: the deadliest era, a dangerous circuit, wet.
        var race = Race(new EraSafetyProfile(FatalityRiskBand.VeryHigh, NeutralisationRules.None, false), fatalities: false, danger: 5);

        var fatal = 0;
        var careerEnding = 0;
        var total = 0;
        foreach (var incident in SampleIncidents(100_000, race, Stream(1950, 1), lap: 1, wetness: 1))
        {
            total++;
            foreach (var participant in incident.Participants)
            {
                fatal += participant.Outcome.IsFatal ? 1 : 0;
                careerEnding += participant.Outcome.Injury == InjuryGrade.CareerEnding ? 1 : 0;
            }

            Assert.NotEqual(OutcomeSeverity.Fatal, incident.WorstSeverity);
        }

        Assert.Equal(100_000, total);
        Assert.Equal(0, fatal);
        Assert.True(careerEnding > 0);
    }

    [Fact]
    public void FatalitiesEnabled_OnlyChangesTheMappingOfTheFatalClass()
    {
        var era = new EraSafetyProfile(FatalityRiskBand.VeryHigh, NeutralisationRules.None, false);
        var off = SampleIncidents(20_000, Race(era, fatalities: false, danger: 5), Stream(1950, 1), wetness: 1).ToList();
        var on = SampleIncidents(20_000, Race(era, fatalities: true, danger: 5), Stream(1950, 1), wetness: 1).ToList();

        Assert.Equal(off.Count, on.Count);
        var fatalSeen = 0;
        for (var i = 0; i < off.Count; i++)
        {
            Assert.Equal(off[i].Kind, on[i].Kind);
            Assert.Equal(off[i].Lap, on[i].Lap);
            foreach (var (without, with) in off[i].Participants.Zip(on[i].Participants, (o, n) => (o, n)))
            {
                Assert.Equal(without.CarId, with.CarId);
                if (with.Outcome.IsFatal)
                {
                    fatalSeen++;
                    Assert.Equal(IncidentOutcome.Injured(InjuryGrade.CareerEnding), without.Outcome);
                }
                else
                {
                    Assert.Equal(without.Outcome, with.Outcome);
                }
            }
        }

        Assert.True(fatalSeen > 0);
    }

    [Fact]
    public void FatalShare_IsHigherIn1955Than2015()
    {
        static double FatalShare(int season)
        {
            var race = Race(season, fatalities: true);
            var incidents = SampleIncidents(60_000, race, Stream(season, 1)).ToList();
            var participants = incidents.SelectMany(i => i.Participants).ToList();
            return (double)participants.Count(p => p.Outcome.IsFatal) / participants.Count;
        }

        var in1955 = FatalShare(1955);
        var in2015 = FatalShare(2015);

        Assert.True(in1955 > 0);
        Assert.True(in1955 > 10 * in2015, $"1955: {in1955}, 2015: {in2015}");
    }

    [Fact]
    public void InjuriesAreRarerInTheModernEra()
    {
        static double InjuryShare(int season)
        {
            var participants = SampleIncidents(30_000, Race(season), Stream(season, 1)).SelectMany(i => i.Participants).ToList();
            return (double)participants.Count(p => p.Outcome.Severity == OutcomeSeverity.Injury) / participants.Count;
        }

        Assert.True(InjuryShare(1955) > 3 * InjuryShare(2015));
    }

    [Fact]
    public void AggressiveDrivers_CauseMoreIncidents_ThanComposedOnes()
    {
        var race = Race(1990);
        var stream = Stream(1990, 5);

        int Count(double aggression, double composure) =>
            Enumerable.Range(0, 20_000)
                .Count(i => IncidentSampler.SampleLap(stream, new IncidentCar("d" + i, aggression, composure), Lap(3), race) is not null);

        var aggressive = Count(90, 20);
        var composed = Count(20, 90);
        var middle = Count(50, 50);

        Assert.True(aggressive > 1.5 * composed, $"aggressive {aggressive}, composed {composed}");
        Assert.True(aggressive > middle && middle > composed);
    }

    [Fact]
    public void TheFirstLap_IsMoreChaoticThanLaterLaps()
    {
        var race = Race(1990);
        var stream = Stream(1990, 1);

        int Count(int lap) =>
            Enumerable.Range(0, 20_000)
                .Count(i => IncidentSampler.SampleLap(stream, new IncidentCar("d" + i, 50, 50), Lap(lap), race) is not null);

        var first = Count(1);
        var tenth = Count(10);

        Assert.True(first > 3 * tenth, $"lap 1: {first}, lap 10: {tenth}");
        Assert.Equal(
            IncidentConstants.FirstLapFactor,
            IncidentSampler.OccurrenceProbability(Typical, Lap(1), race) / IncidentSampler.OccurrenceProbability(Typical, Lap(10), race),
            6);
        Assert.True(
            IncidentSampler.OccurrenceProbability(Typical, Lap(2), race) > IncidentSampler.OccurrenceProbability(Typical, Lap(3), race));
    }

    [Fact]
    public void OccurrenceProbability_RisesWithEachFactor_AndIsCapped()
    {
        var race = Race(1990);
        var baseline = IncidentSampler.OccurrenceProbability(Typical, Lap(10, 0, []), race);

        Assert.True(IncidentSampler.OccurrenceProbability(Typical, Lap(10, 0, ThreeNear), race) > baseline);
        Assert.True(IncidentSampler.OccurrenceProbability(Typical, Lap(10, 1, []), race) > baseline);
        Assert.True(IncidentSampler.OccurrenceProbability(Typical, Lap(10, 0, []), Race(1990, danger: 2)) > baseline);
        Assert.True(IncidentSampler.OccurrenceProbability(Typical, Lap(10, 0, []), Race(1955)) > baseline);
        Assert.True(IncidentSampler.OccurrenceProbability(Typical, Lap(10, 0, []), Race(2020)) < baseline);

        var capped = IncidentSampler.OccurrenceProbability(
            new IncidentCar("x", 100, 0),
            Lap(1, 1, ["a", "b", "c", "d", "e", "f"]),
            Race(1950, danger: IncidentConstants.MaxTrackDanger));
        Assert.Equal(IncidentConstants.MaxLapProbability, capped);
    }

    [Fact]
    public void ContactAndCollision_NeedACarNearby_AndTheOtherCarComesFromThoseNearby()
    {
        var race = Race(1955, danger: 5);
        var stream = Stream(1955, 2);

        var alone = Enumerable.Range(0, 5000)
            .Select(i => IncidentSampler.SampleLap(stream, new IncidentCar("c" + i, 50, 50), Lap(1, 0.5, []), race))
            .OfType<IncidentResult>()
            .ToList();
        Assert.True(alone.Count > 300);
        Assert.All(alone, r =>
        {
            Assert.DoesNotContain(r.Kind, new[] { IncidentKind.Contact, IncidentKind.Collision });
            Assert.Null(r.Other);
        });

        var crowded = SampleIncidents(3000, race, stream).ToList();
        Assert.Contains(crowded, r => r.Kind == IncidentKind.Collision);
        Assert.Contains(crowded, r => r.Kind == IncidentKind.Contact);
        foreach (var incident in crowded)
        {
            var twoCars = incident.Kind is IncidentKind.Contact or IncidentKind.Collision;
            Assert.Equal(twoCars, incident.Other is not null);
            if (incident.Other is { } other)
            {
                Assert.Contains(other.CarId, ThreeNear);
                Assert.Equal(IncidentRole.Other, other.Role);
                Assert.Equal(IncidentRole.Instigator, incident.Instigator.Role);
            }
        }

        // Every kind turns up.
        Assert.Equal(Enum.GetValues<IncidentKind>().Length, crowded.Select(r => r.Kind).Distinct().Count());
    }

    [Fact]
    public void Rain_ShiftsIncidentsTowardsSpinsAndBarriers()
    {
        var race = Race(1990, danger: 3);
        double SingleCarShare(double wetness) =>
            SampleIncidents(20_000, race, Stream(1990, 9), lap: 5, wetness: wetness)
                .Count(r => r.Kind is IncidentKind.Spin or IncidentKind.Barrier) / 20_000.0;

        Assert.True(SingleCarShare(1) > SingleCarShare(0));
    }

    [Fact]
    public void Outcomes_CoverEverySeverity_AndInjuryGradesOnlyGoWithInjury()
    {
        var race = Race(1955, fatalities: true, danger: 5);
        var outcomes = SampleIncidents(40_000, race, Stream(1955, 7)).SelectMany(i => i.Participants).Select(p => p.Outcome).ToList();

        foreach (var severity in Enum.GetValues<OutcomeSeverity>())
        {
            Assert.Contains(outcomes, o => o.Severity == severity);
        }

        foreach (var grade in new[] { InjuryGrade.Light, InjuryGrade.Serious, InjuryGrade.CareerEnding })
        {
            Assert.Contains(outcomes, o => o.Injury == grade);
        }

        Assert.All(outcomes, o =>
        {
            Assert.Equal(o.Severity == OutcomeSeverity.Injury, o.Injury != InjuryGrade.None);
            Assert.Equal(o.Severity >= OutcomeSeverity.Retire, o.RetiresCar);
        });
    }

    [Fact]
    public void ConstantsAreConsistent()
    {
        foreach (var band in Enum.GetValues<FatalityRiskBand>())
        {
            var split = IncidentConstants.InjurySplit(band);
            Assert.Equal(1.0, split.Light + split.Serious + split.CareerEnding + split.FatalClass, 9);
            Assert.InRange(IncidentConstants.InjuryShareOfHardCrash(band), 0, 1);
            Assert.True(IncidentConstants.EraRateMultiplier(band) > 0);
        }

        foreach (var kind in Enum.GetValues<IncidentKind>())
        {
            Assert.True(IncidentConstants.HardCrashShare(kind) + IncidentConstants.MinorShare(kind) <= 1.0);
            Assert.True(IncidentConstants.KindWeight(kind) > 0);
        }

        // Safer eras are safer: the fatal-class share and the injury share never rise as the band improves.
        var bands = Enum.GetValues<FatalityRiskBand>().OrderBy(b => (int)b).ToList();
        for (var i = 1; i < bands.Count; i++)
        {
            Assert.True(IncidentConstants.InjurySplit(bands[i]).FatalClass <= IncidentConstants.InjurySplit(bands[i - 1]).FatalClass);
            Assert.True(IncidentConstants.InjuryShareOfHardCrash(bands[i]) <= IncidentConstants.InjuryShareOfHardCrash(bands[i - 1]));
        }
    }

    [Fact]
    public void BadInputs_AreRejected()
    {
        var stream = Stream();
        var race = Race(1955);

        Assert.Throws<ArgumentOutOfRangeException>(() => IncidentSampler.SampleLap(stream, new IncidentCar("a", 101, 50), Lap(1), race));
        Assert.Throws<ArgumentOutOfRangeException>(() => IncidentSampler.SampleLap(stream, new IncidentCar("a", 50, -1), Lap(1), race));
        Assert.Throws<ArgumentOutOfRangeException>(() => IncidentSampler.SampleLap(stream, Typical, Lap(0), race));
        Assert.Throws<ArgumentOutOfRangeException>(() => IncidentSampler.SampleLap(stream, Typical, Lap(1, 1.5), race));
        Assert.Throws<ArgumentOutOfRangeException>(() => IncidentSampler.SampleLap(stream, Typical, Lap(1), Race(1955, danger: 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => IncidentSampler.SampleLap(stream, Typical, Lap(1), Race(1955, danger: double.NaN)));
        Assert.Throws<ArgumentException>(() => IncidentSampler.SampleLap(stream, new IncidentCar(" ", 50, 50), Lap(1), race));
        Assert.Throws<ArgumentException>(() => IncidentSampler.SampleLap(stream, Typical, Lap(1, 0, ["car-0"]), race));
        Assert.Throws<ArgumentException>(() => IncidentSampler.SampleLap(stream, Typical, Lap(1, 0, ["x", "x"]), race));
        Assert.Throws<ArgumentNullException>(() => IncidentSampler.SampleLap(null!, Typical, Lap(1), race));
        Assert.Throws<ArgumentOutOfRangeException>(() => IncidentOutcome.Injured(InjuryGrade.None));
    }
}
