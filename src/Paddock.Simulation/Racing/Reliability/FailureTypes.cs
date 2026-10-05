namespace Paddock.Simulation.Racing.Reliability;

/// <summary>The state of one part of one car at the start of the race.</summary>
/// <param name="Component">Which part.</param>
/// <param name="ReliabilityRating">The part's reliability rating in [0, 1]; higher means fewer failures.</param>
/// <param name="MileageLaps">Laps the part has already run (engine mileage and so on). Raises the hazard. Not negative.</param>
public readonly record struct ComponentState(MechanicalComponent Component, double ReliabilityRating, double MileageLaps = 0);

/// <summary>
/// What makes the parts of a car work harder than usual, plus the season that sets the base hazard.
/// </summary>
/// <param name="Season">The season, which selects the era's base hazard.</param>
/// <param name="DriverSympathy">The driver's mechanical sympathy in [0, 1]; higher is gentler on the car.</param>
/// <param name="PaceStress">How hard the car is being pushed in [0, 1].</param>
/// <param name="Heat">Weather heat in [0, 1].</param>
public readonly record struct FailureInputs(int Season, double DriverSympathy, double PaceStress, double Heat)
{
    /// <summary>
    /// The stress in [0, 1] used in <c>(1 + stress)</c>: a weighted sum of the pace, the heat and the lack of sympathy
    /// (weights in <see cref="ReliabilityConstants"/>, ESTIMATE).
    /// </summary>
    public double Stress => ComputeStress(DriverSympathy, PaceStress, Heat);

    public static double ComputeStress(double driverSympathy, double paceStress, double heat) =>
        ReliabilityConstants.PaceStressWeight * paceStress
        + ReliabilityConstants.HeatStressWeight * heat
        + ReliabilityConstants.SympathyStressWeight * (1 - driverSympathy);
}

/// <summary>Options of <see cref="FailureSampler"/>.</summary>
/// <param name="WarningLeadLaps">How many laps before a failure the warning flag is raised. At least 1.</param>
public sealed record FailureSamplerOptions(int WarningLeadLaps = ReliabilityConstants.DefaultWarningLeadLaps)
{
    public static FailureSamplerOptions Default { get; } = new();
}

/// <summary>
/// What a failure does to the car. Punctures are not here: they are handled elsewhere.
/// A retirement maps to <c>FinishStatus.Mechanical</c> in the points rules.
/// </summary>
public abstract record FailureEffect
{
    private FailureEffect()
    {
    }

    /// <summary>The car is out of the race.</summary>
    public sealed record Retire : FailureEffect;

    /// <summary>The car loses a percentage of its power for the rest of the race.</summary>
    /// <param name="Percent">Percent of full power lost, in (0, 100).</param>
    public sealed record LosePower(double Percent) : FailureEffect;

    /// <summary>The car stops in the pits for a repair and rejoins.</summary>
    /// <param name="TimeCostSeconds">Time the repair costs, in seconds.</param>
    public sealed record PitForRepair(double TimeCostSeconds) : FailureEffect;
}

/// <summary>
/// One mechanical failure. This is simulation truth (TECH §3, truth versus knowledge): the warning is what a
/// strategist or the Spy may be shown, subject to the access rules, not something every actor knows.
/// </summary>
/// <param name="Lap">The lap (1-based) on which the failure happens.</param>
/// <param name="Component">The part that fails.</param>
/// <param name="Effect">What the failure does.</param>
/// <param name="WarningLap">
/// The lap from which the warning flag is up: <c>Lap - WarningLeadLaps</c>. Null when the failure comes too early
/// to be preceded by a full lead, or when it is a sudden failure (PP-057).
/// </param>
/// <param name="DegradedPaceLossFraction">Share of lap time lost while the warning is active. 0 when there is no warning.</param>
/// <param name="Sudden">True when the failure was drawn as sudden: no warning lap and no degraded pace, even when the lead would have fitted.</param>
public sealed record MechanicalFailure(
    int Lap,
    MechanicalComponent Component,
    FailureEffect Effect,
    int? WarningLap,
    double DegradedPaceLossFraction,
    bool Sudden = false)
{
    public bool IsRetirement => Effect is FailureEffect.Retire;

    /// <summary>True on laps from the warning lap up to, not including, the failure lap.</summary>
    public bool WarningActiveOnLap(int lap) => WarningLap is int from && lap >= from && lap < Lap;

    /// <summary>The share of lap time the car loses on the given lap because of the developing failure.</summary>
    public double DegradedPaceLossOnLap(int lap) => WarningActiveOnLap(lap) ? DegradedPaceLossFraction : 0;
}

/// <summary>
/// The result of sampling one car's race.
/// Failures arrive as two independent processes per part (one that ends the race, one that does not), so the
/// race-ending lap is known even when a non-retiring failure comes first.
/// </summary>
/// <param name="First">The earliest failure of any kind, or null when nothing fails.</param>
/// <param name="Retirement">The earliest failure that retires the car, or null when none does. Equals <see cref="First"/> when the first failure retires.</param>
public sealed record FailureSample(MechanicalFailure? First, MechanicalFailure? Retirement)
{
    public static FailureSample None { get; } = new(null, null);
}
