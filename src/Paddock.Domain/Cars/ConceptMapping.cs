using Paddock.Domain.Random;

namespace Paddock.Domain.Cars;

/// <summary>
/// What a concept is good at, at a fixed ceiling. Higher is better for every score except tyre wear and supplier cost.
/// ESTIMATE proposal: no pole wins every situation.
/// </summary>
public readonly record struct ConceptEffects(
    PerformanceLevels Full,
    double StartFraction,
    double TyreWearMultiplier,
    double Peak,
    double Stability,
    double SingleLapAdvantage,
    double LongStintAdvantage,
    double MatchedEngineBonus,
    int SupplierChangeCost);

/// <summary>
/// Maps concept axes onto the performance vector and the situations those axes trade (T41, proposal).
/// Pure: no clock and no RNG. The ceiling draw is <see cref="DrawPotential"/>.
/// </summary>
public static class ConceptMapping
{
    public static ConceptEffects Effects(CarConcept concept, double ceiling)
    {
        var full = PerformanceLevels.Of(
            ceiling * (0.55d - (0.12d * concept.Aero) - (0.10d * concept.Cooling)),
            ceiling * (0.50d + (0.18d * concept.Aero)),
            ceiling * (0.48d + (0.06d * concept.Window)),
            ceiling * 0.50d,
            ceiling * (0.48d + (0.16d * concept.Cooling)));
        var wear = CarEstimates.Quantize(1d + (0.25d * concept.Aero) - (0.20d * concept.TyreKindness));
        var integration = (concept.Integration + 1d) / 2d;
        return new ConceptEffects(
            full,
            StartFraction(concept.Philosophy),
            wear,
            CarEstimates.Quantize(0.5d + (0.2d * concept.Window)),
            CarEstimates.Quantize(0.5d - (0.2d * concept.Window)),
            CarEstimates.Quantize(-0.15d * concept.TyreKindness),
            CarEstimates.Quantize(-wear),
            CarEstimates.Quantize(0.08d * (1d - integration)),
            (int)Math.Round(100d + ((20d - 100d) * integration), MidpointRounding.AwayFromZero));
    }

    /// <summary>The levels a newly approved car actually has: the full vector times the philosophy's start fraction.</summary>
    public static PerformanceLevels StartingLevels(CarConcept concept, double ceiling) =>
        Effects(concept, ceiling).Full.Scale(StartFraction(concept.Philosophy));

    /// <summary>0 for pure evolution, 1 for pure revolution.</summary>
    public static double PhilosophyUnit(double philosophy) => (philosophy + 1d) / 2d;

    public static double StartFraction(double philosophy) =>
        Lerp(CarEstimates.EvolutionStartFraction, CarEstimates.RevolutionStartFraction, PhilosophyUnit(philosophy));

    /// <summary>
    /// Expected ceiling before the random spread, so a test can name the direction without a seed.
    /// <paramref name="executionQuality"/> is 0..1 from the chief designer's precision and the technical director's vision.
    /// </summary>
    public static double ExpectedCeiling(double philosophy, double executionQuality)
    {
        var quality = Math.Clamp(executionQuality, 0d, 1d);
        var mean = Lerp(CarEstimates.EvolutionCeilingMean, CarEstimates.RevolutionCeilingMean, PhilosophyUnit(philosophy));
        return mean * Lerp(CarEstimates.ExecutionFloor, 1d, quality);
    }

    /// <summary>
    /// One ceiling from the caller's <c>Development</c> child (INV-004). Evolution starts higher and ends lower
    /// than revolution: the end is this ceiling, which in-season development (T42) would approach.
    /// </summary>
    public static (double Ceiling, double StartLevel) DrawPotential(Xoshiro256StarStar rng, double philosophy, double executionQuality)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var mean = ExpectedCeiling(philosophy, executionQuality);
        var spread = Lerp(CarEstimates.EvolutionCeilingSd, CarEstimates.RevolutionCeilingSd, PhilosophyUnit(philosophy));
        var ceiling = CarEstimates.ClampRating(mean + (spread * StandardNormal(rng)));
        var start = CarEstimates.Quantize(ceiling * StartFraction(philosophy));
        return (ceiling, start);
    }

    private static double Lerp(double from, double to, double t) => from + ((to - from) * t);

    /// <summary>Irwin–Hall of <see cref="CarEstimates.NormalSampleCount"/> uniforms, shifted to mean 0.</summary>
    private static double StandardNormal(Xoshiro256StarStar rng)
    {
        double sum = 0;
        for (var i = 0; i < CarEstimates.NormalSampleCount; i++)
        {
            sum += rng.NextDouble();
        }

        return sum - (CarEstimates.NormalSampleCount / 2d);
    }
}
