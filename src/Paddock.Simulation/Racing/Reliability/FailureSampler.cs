using Paddock.Domain.Random;

namespace Paddock.Simulation.Racing.Reliability;

/// <summary>
/// Samples the mechanical failures of one car in one race from the <c>Failures</c> RNG stream.
/// </summary>
/// <remarks>
/// Method: for each part and each of its two processes (failures that retire the car, failures that do not)
/// one exponential variable E is drawn, and the failure comes on the first lap where the cumulative hazard
/// reaches E. This is the exact discrete-time survival model with survival exp(-H).
/// Determinism: the draws come from a sub-stream derived from the race stream and the car id, in a fixed order
/// (every <see cref="MechanicalComponent"/> member, four numbers each, whatever parts the car has and however long the race is).
/// So the draws of car A never depend on car B or on the number of cars.
/// </remarks>
public static class FailureSampler
{
    private static readonly int ComponentCount = Enum.GetValues<MechanicalComponent>().Length;

    /// <summary>The race-level <c>Failures</c> stream: derived from the master seed, the season and the round.</summary>
    public static RngStream DeriveRaceStream(ulong masterSeed, int season, int round) =>
        RngStream.Derive(masterSeed, RngStreamName.Failures, season, round);

    /// <summary>
    /// Samples the first failure and the first retirement of one car.
    /// </summary>
    /// <param name="raceStream">The race's <c>Failures</c> stream, see <see cref="DeriveRaceStream"/>. It is not advanced.</param>
    /// <param name="carId">Stable id of the car; selects the car's sub-stream.</param>
    /// <param name="laps">Laps the race is run over, at least 1.</param>
    /// <param name="components">The parts of the car. Missing parts cannot fail. Each part at most once.</param>
    /// <param name="inputs">Season and stress.</param>
    /// <param name="options">Warning lead; the default when null.</param>
    public static FailureSample Sample(
        RngStream raceStream,
        string carId,
        int laps,
        IReadOnlyList<ComponentState> components,
        FailureInputs inputs,
        FailureSamplerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(raceStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(carId);
        ArgumentNullException.ThrowIfNull(components);
        options ??= FailureSamplerOptions.Default;
        if (laps < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(laps), "A race has at least one lap.");
        }

        if (options.WarningLeadLaps < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "WarningLeadLaps must be at least 1.");
        }

        ValidateInputs(inputs);
        var states = IndexComponents(components);

        var rng = raceStream.DeriveChild("car:" + carId);
        var stress = inputs.Stress;
        MechanicalFailure? first = null;
        var firstOrder = int.MaxValue;
        MechanicalFailure? retirement = null;
        var retirementOrder = int.MaxValue;

        for (var index = 0; index < ComponentCount; index++)
        {
            var component = (MechanicalComponent)index;

            // Fixed draws, taken even for a part the car does not have.
            var retireExponential = -Math.Log(1 - rng.NextDouble());
            var otherExponential = -Math.Log(1 - rng.NextDouble());
            var kindDraw = rng.NextDouble();
            var magnitudeDraw = rng.NextDouble();

            if (states[index] is not ComponentState state)
            {
                continue;
            }

            var split = ReliabilityConstants.EffectSplit(component);
            var retireLap = FirstLap(inputs.Season, component, state, stress, laps, retireExponential, split.Retire);
            var otherLap = FirstLap(inputs.Season, component, state, stress, laps, otherExponential, 1 - split.Retire);

            if (retireLap is int lapR)
            {
                var failure = Build(lapR, component, new FailureEffect.Retire(), options);
                Consider(failure, index * 2, ref first, ref firstOrder);
                Consider(failure, index * 2, ref retirement, ref retirementOrder);
            }

            if (otherLap is int lapO)
            {
                FailureEffect effect = kindDraw < split.PowerLossShareOfRest
                    ? new FailureEffect.LosePower(Lerp(ReliabilityConstants.PowerLossMinPercent, ReliabilityConstants.PowerLossMaxPercent, magnitudeDraw))
                    : new FailureEffect.PitForRepair(Lerp(ReliabilityConstants.RepairMinSeconds, ReliabilityConstants.RepairMaxSeconds, magnitudeDraw));
                Consider(Build(lapO, component, effect, options), index * 2 + 1, ref first, ref firstOrder);
            }
        }

        return first is null ? FailureSample.None : new FailureSample(first, retirement);
    }

    // The earliest lap wins; a tie goes to the lower (component, process) order, so the outcome never depends on iteration accidents.
    private static void Consider(MechanicalFailure candidate, int order, ref MechanicalFailure? best, ref int bestOrder)
    {
        if (best is null || candidate.Lap < best.Lap || (candidate.Lap == best.Lap && order < bestOrder))
        {
            best = candidate;
            bestOrder = order;
        }
    }

    private static MechanicalFailure Build(int lap, MechanicalComponent component, FailureEffect effect, FailureSamplerOptions options)
    {
        var warningLap = lap - options.WarningLeadLaps;
        return warningLap >= 1
            ? new MechanicalFailure(lap, component, effect, warningLap, ReliabilityConstants.DegradedPaceLossFraction)
            : new MechanicalFailure(lap, component, effect, null, 0);
    }

    // First lap on which the cumulative hazard (scaled by the process's share of the component's hazard) reaches the exponential draw.
    private static int? FirstLap(
        int season,
        MechanicalComponent component,
        ComponentState state,
        double stress,
        int laps,
        double exponential,
        double processShare)
    {
        if (processShare <= 0)
        {
            return null;
        }

        var cumulative = 0.0;
        for (var lap = 1; lap <= laps; lap++)
        {
            var mileage = state.MileageLaps + (lap - 1);
            cumulative += processShare * ReliabilityEraProfile.LapHazard(season, component, stress, state.ReliabilityRating, mileage);
            if (cumulative >= exponential)
            {
                return lap;
            }
        }

        return null;
    }

    private static ComponentState?[] IndexComponents(IReadOnlyList<ComponentState> components)
    {
        var states = new ComponentState?[ComponentCount];
        foreach (var state in components)
        {
            var index = (int)state.Component;
            if (index < 0 || index >= ComponentCount)
            {
                throw new ArgumentException($"Unknown component {state.Component}.", nameof(components));
            }

            if (states[index] is not null)
            {
                throw new ArgumentException($"Component {state.Component} is listed twice.", nameof(components));
            }

            if (!InUnitRange(state.ReliabilityRating))
            {
                throw new ArgumentOutOfRangeException(nameof(components), $"Reliability rating of {state.Component} must be in [0, 1].");
            }

            if (!(state.MileageLaps >= 0) || double.IsInfinity(state.MileageLaps))
            {
                throw new ArgumentOutOfRangeException(nameof(components), $"Mileage of {state.Component} must be a finite number, not negative.");
            }

            states[index] = state;
        }

        return states;
    }

    private static void ValidateInputs(FailureInputs inputs)
    {
        if (!InUnitRange(inputs.DriverSympathy) || !InUnitRange(inputs.PaceStress) || !InUnitRange(inputs.Heat))
        {
            throw new ArgumentOutOfRangeException(nameof(inputs), "DriverSympathy, PaceStress and Heat must be in [0, 1].");
        }
    }

    private static bool InUnitRange(double value) => value >= 0 && value <= 1;

    private static double Lerp(double min, double max, double t) => min + (max - min) * t;
}
