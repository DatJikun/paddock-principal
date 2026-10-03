using System.Collections.Immutable;
using Paddock.Domain.Random;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Tyres;

namespace Paddock.Tests.Racing.Pits;

/// <summary>
/// A tiny toy race, only for these tests: one car alone on the track, a 30-lap race in 2012 rules (no refuelling, two dry
/// compounds required), the T30 tyre and fuel models as the truth, and a hidden tyre-wear multiplier per race that no
/// strategist is told. It is not the race loop (T35); it only needs the calculators and the pit-stop model.
/// All numbers are ESTIMATE, so the bounds are generous.
/// </summary>
public class ToyRaceTests
{
    private const int Season = 2012;
    private const int Laps = 30;
    private const double Burn = 1.5;
    private static readonly string[] Compounds = ["C4", "C3", "C2"];

    private sealed record ToyResult(double LossSeconds, int Stops, bool Legal);

    private static ToyResult Run(int skill, ulong raceSeed, StrategistOptions options)
    {
        var rules = PitTestKit.Rules(Season);
        var catalog = TyreCompoundCatalog.Default.RaceCompounds(Season).ToDictionary(c => c.Id);
        var calc = PitTestKit.T30Calculators(Season);
        var strategist = new RuleBasedStrategist(skill, calc, RuleBasedStrategist.DeriveRaceStream(raceSeed, Season, 1), options);
        var pitStream = PitStopModel.DeriveRaceStream(raceSeed, Season, 1);
        var lane = rules.PitLaneTimeLossSeconds();
        var crew = new PitCrew(50);

        // The truth the strategist does not know: how hard this track is on tyres.
        var truth = RngStream.Derive(raceSeed, RngStreamName.LapNoise, Season, 1).DeriveChild("toy-wear");
        var hiddenWear = 0.8 + (0.5 * truth.NextDouble());

        var compound = "C3";
        var wear = 0.0;
        var age = 0;
        var fuel = Burn * Laps * 1.03;
        var mode = PaceMode.Standard;
        var used = new List<string> { compound };
        var loss = 0.0;
        var stops = 0;
        var info = Compounds.Select(id => new CompoundInfo(id, false)).ToImmutableArray();

        for (var lap = 1; lap <= Laps; lap++)
        {
            var snapshot = new KnowledgeSnapshot(
                Season,
                lap,
                Laps,
                90,
                (lap - 1) * 1.5,
                rules,
                lane,
                null,
                info,
                new OwnCarKnowledge("toy", compound, age, fuel, Burn, mode, [.. used], stops, false, age, crew),
                new VisibleGaps(1, null, null),
                null);
            var decision = strategist.Decide(snapshot);
            mode = decision.Pace;

            var (paceDelta, wearFactor, burnFactor) = mode switch
            {
                PaceMode.Push => (-PitConstants.PushPaceGainSeconds, PitConstants.PushWearFactor, PitConstants.PushBurnFactor),
                PaceMode.Save => (FuelModel.FuelSavingPaceLossSeconds, PitConstants.SaveWearFactor, 1 - TyreFuelConstants.FuelSavingBurnReduction),
                _ => (0.0, 1.0, 1.0),
            };
            var conditions = new TyreConditions(0.5, 0.5, FuelModel.CarWeightFactor(fuel));
            loss += TyreWear.LapLoss(catalog[compound], (int)Math.Round(wear * hiddenWear), conditions)
                + FuelModel.MassPenaltySeconds(fuel)
                + paceDelta;
            if (fuel + 1e-9 < Burn * burnFactor)
            {
                loss += PitConstants.InfeasiblePlanPenaltySeconds;
                fuel = 0;
            }
            else
            {
                fuel -= Burn * burnFactor;
            }

            wear += wearFactor;
            age++;

            if (decision.Action == StrategyAction.PitNow && lap < Laps)
            {
                stops++;
                var stop = PitStopModel.Sample(pitStream, "toy", stops, rules, lane, new PitStopRequest(decision.CompoundId is not null), crew);
                loss += stop.TotalSeconds;
                if (decision.CompoundId is { } next)
                {
                    compound = next;
                    used.Add(next);
                    wear = 0;
                    age = 0;
                }
            }
        }

        var legal = rules.MixSatisfied(used, _ => false, wetRace: false);
        return new ToyResult(loss + (legal ? 0 : PitConstants.InfeasiblePlanPenaltySeconds), stops, legal);
    }

    [Fact]
    public void AStrongStrategist_BeatsAWeakOne_Over500Races()
    {
        // Smaller search than the default so 1000 races stay fast; both strategists use the same one.
        var options = new StrategistOptions(MaxStops: 2, StopShiftVariants: 1);
        const int races = 500;
        var strong = new ToyResult[races];
        var weak = new ToyResult[races];
        for (var i = 0; i < races; i++)
        {
            strong[i] = Run(skill: 95, raceSeed: (ulong)(1000 + i), options);
            weak[i] = Run(skill: 5, raceSeed: (ulong)(1000 + i), options);
        }

        var strongMean = strong.Average(r => r.LossSeconds);
        var weakMean = weak.Average(r => r.LossSeconds);
        var strongWins = Enumerable.Range(0, races).Count(i => strong[i].LossSeconds < weak[i].LossSeconds);

        // Generous bounds: the strong strategist is clearly better on average and in most races, and obeys the mix rule.
        Assert.True(weakMean - strongMean > 5, $"mean loss strong {strongMean:F1} s, weak {weakMean:F1} s");
        Assert.True(strongWins > 0.6 * races, $"strong won {strongWins} of {races}");
        Assert.True(strong.All(r => r.Legal), "the strong strategist broke the mandatory mix");
    }

    [Fact]
    public void ToyRace_IsDeterministic()
    {
        var options = new StrategistOptions(MaxStops: 2, StopShiftVariants: 1);
        var a = Run(40, 7, options);
        var b = Run(40, 7, options);

        Assert.Equal(a, b);
    }
}
