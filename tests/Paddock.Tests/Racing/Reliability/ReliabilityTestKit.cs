using Paddock.Simulation.Racing.Reliability;

namespace Paddock.Tests.Racing.Reliability;

internal static class ReliabilityTestKit
{
    public const ulong Seed = 20_261_003UL;

    public static IReadOnlyList<ComponentState> AllComponents(double rating, double mileage = 0) =>
        Enum.GetValues<MechanicalComponent>().Select(c => new ComponentState(c, rating, mileage)).ToArray();

    public static IReadOnlyList<ComponentState> ReferenceComponents() =>
        AllComponents(ReliabilityConstants.ReferenceReliabilityRating, ReliabilityConstants.ReferenceMileageLaps);

    public static FailureInputs ReferenceInputs(int season) => new(
        season,
        ReliabilityConstants.ReferenceSympathy,
        ReliabilityConstants.ReferencePaceStress,
        ReliabilityConstants.ReferenceHeat);

    public static IEnumerable<FailureSample> SampleField(
        int count,
        int laps,
        IReadOnlyList<ComponentState> components,
        FailureInputs inputs,
        int round = 1,
        FailureSamplerOptions? options = null)
    {
        var stream = FailureSampler.DeriveRaceStream(Seed, inputs.Season, round);
        for (var i = 0; i < count; i++)
        {
            yield return FailureSampler.Sample(stream, "car-" + i, laps, components, inputs, options);
        }
    }
}
