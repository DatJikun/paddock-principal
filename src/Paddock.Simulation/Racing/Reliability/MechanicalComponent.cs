namespace Paddock.Simulation.Racing.Reliability;

/// <summary>
/// The parts that can fail mechanically during a race. The order of the members is part of the contract:
/// <see cref="FailureSampler"/> draws random numbers in this order, so reordering or inserting a member
/// changes every sampled failure.
/// Tyre-related failures are deliberately excluded (punctures are handled elsewhere), as are collisions.
/// The names mirror the "Mechanical" bucket of the Jolpica finish statuses (Engine, Gearbox, Suspension,
/// Brakes, Electrical/Hydraulics, Overheating/Cooling system); Simulation does not reference the data pipeline.
/// </summary>
public enum MechanicalComponent
{
    Engine,
    Gearbox,
    Suspension,
    Brakes,
    ElectricsHydraulics,
    Cooling,
}
