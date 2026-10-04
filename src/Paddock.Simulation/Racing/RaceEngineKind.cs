namespace Paddock.Simulation.Racing;

/// <summary>
/// Which race engine runs a weekend (PP-052). The lap engine is the default. A later continuous engine must
/// implement <see cref="IRaceSimulator"/> and be added here; callers pass the setting in and never construct an engine.
/// </summary>
public enum RaceEngineKind
{
    /// <summary>The current lap engine (<see cref="LapRaceSimulator"/>).</summary>
    Lap,
}
