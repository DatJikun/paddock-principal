using System.Collections.Immutable;
using Paddock.Domain.World;
using Paddock.Simulation.Racing.Pits;
using Paddock.Simulation.Racing.Tyres;
using Paddock.Tests.Racing.Points;

namespace Paddock.Tests.Racing.Pits;

/// <summary>Shared helpers: the real regulations, calculators built on T30, and snapshot builders. Every number is ESTIMATE.</summary>
internal static class PitTestKit
{
    public const ulong Seed = 20_26;

    public static RuleSet RuleSetFor(int season) => PointsTestKit.Real.RuleSetFor(season);

    public static PitRules Rules(int season) => PitRules.For(RuleSetFor(season));

    /// <summary>The T30 models behind the strategist's calculators: the real compound catalog, reference conditions, fuel mass by T30.</summary>
    public static StrategyCalculators T30Calculators(int season)
    {
        var compounds = TyreCompoundCatalog.Default.RaceCompounds(season).Append(TyreCompoundCatalog.Default.WetCompound(season)).ToDictionary(c => c.Id);
        return new StrategyCalculators(
            (id, age, fuel) => TyreWear.LapLoss(compounds[id], age, new TyreConditions(0.5, 0.5, FuelModel.CarWeightFactor(fuel))),
            FuelModel.MassPenaltySeconds);
    }

    public static ImmutableArray<CompoundInfo> Compounds(int season, bool withWet = false)
    {
        var dry = TyreCompoundCatalog.Default.RaceCompounds(season).Select(c => new CompoundInfo(c.Id, false));
        return [.. withWet ? dry.Append(new CompoundInfo(TyreCompoundCatalog.Default.WetCompound(season).Id, true)) : dry];
    }

    public static OwnCarKnowledge Car(
        string compound,
        int age = 0,
        double fuel = 50,
        double burn = 1.5,
        PaceMode pace = PaceMode.Standard,
        string[]? used = null,
        bool canSwap = false,
        int driverStint = 0,
        double crewQuality = 50) =>
        new("car-1", compound, age, fuel, burn, pace, [.. used ?? [compound]], 0, canSwap, driverStint, new PitCrew(crewQuality));

    public static KnowledgeSnapshot Snapshot(
        int season,
        int lap,
        int totalLaps,
        OwnCarKnowledge car,
        ImmutableArray<CompoundInfo>? compounds = null,
        VisibleGaps? gaps = null,
        Simulation.Racing.Weather.WeatherForecast? forecast = null,
        double? tankCapacityKg = null)
    {
        var rules = Rules(season);
        return new KnowledgeSnapshot(
            season,
            lap,
            totalLaps,
            90,
            (lap - 1) * 90 / 60.0,
            rules,
            rules.PitLaneTimeLossSeconds(),
            tankCapacityKg,
            compounds ?? Compounds(season),
            car,
            gaps ?? new VisibleGaps(5, null, null),
            forecast);
    }
}
