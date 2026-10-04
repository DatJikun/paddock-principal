namespace Paddock.Simulation.Racing;

/// <summary>Turns the engine setting into the simulator that runs the race. The default is the lap engine.</summary>
public static class RaceSimulators
{
    public static IRaceSimulator Resolve(RaceEngineKind engine = RaceEngineKind.Lap) => engine switch
    {
        RaceEngineKind.Lap => LapRaceSimulator.Instance,
        _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, "No race engine is registered for this setting."),
    };
}
