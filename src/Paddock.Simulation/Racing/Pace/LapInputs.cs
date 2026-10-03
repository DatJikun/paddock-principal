using Paddock.Domain.World;

namespace Paddock.Simulation.Racing.Pace;

/// <summary>A car's performance vector (DESIGN 5.4), each on 0..100.</summary>
public readonly record struct CarPerformance(
    double Power,
    double Downforce,
    double MechanicalGrip,
    double Braking,
    double Reliability);

/// <summary>A driver's pace-relevant ratings, each 0..100.</summary>
/// <param name="Pace">Raw speed.</param>
/// <param name="Consistency">Lap-to-lap repeatability; drives the size of the noise layer.</param>
/// <param name="Composure">Calm under difficult conditions; drives the wet penalty.</param>
/// <param name="AffinitySeconds">
/// Hidden track-specific affinity, in seconds GAINED (positive = faster). Small; clamped to
/// +/- <see cref="PaceConstants.MaxAffinitySeconds"/>. Default 0.
/// </param>
public readonly record struct DriverPace(double Pace, double Consistency, double Composure, double AffinitySeconds = 0d);

/// <summary>
/// Everything the lap-time model needs for one lap of one car. Aggregated by the (future) race orchestrator.
/// The model never reads RNG: <see cref="NoiseDraw"/> is a N(0,1) sample drawn by the caller from its own stream.
/// </summary>
public sealed record LapInputs
{
    public required TrackLayout Track { get; init; }

    public required int Season { get; init; }

    /// <summary>Era limits; the downforce cap lives here.</summary>
    public required EraPerformanceLimits Limits { get; init; }

    public required CarPerformance Car { get; init; }

    public required DriverPace Driver { get; init; }

    /// <summary>Fuel on board, kg (not negative).</summary>
    public double FuelMassKg { get; init; }

    /// <summary>Seconds lost to tyre wear this lap, as computed by the tyre model (not negative).</summary>
    public double TyreWearFactor { get; init; }

    /// <summary>Gap to the car ahead in seconds; <see cref="double.PositiveInfinity"/> (default) means clear track.</summary>
    public double TrafficGapAheadSeconds { get; init; } = double.PositiveInfinity;

    /// <summary>0 = clean air, 1 = fully in dirty air.</summary>
    public double DirtyAirLevel { get; init; }

    /// <summary>0 = dry, 1 = soaked.</summary>
    public double WetnessLevel { get; init; }

    /// <summary>0 = tyres fit the conditions, 1 = completely wrong tyres. Only matters when it is wet.</summary>
    public double TyreFitMismatch { get; init; }

    /// <summary>A N(0,1) sample supplied by the caller (isolated RNG stream, INV-004).</summary>
    public double NoiseDraw { get; init; }
}
