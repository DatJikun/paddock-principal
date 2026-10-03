namespace Paddock.Simulation.Racing.Points;

/// <summary>How a car's race ended, in the buckets the points rules care about.</summary>
public enum FinishStatus
{
    /// <summary>Running at the flag.</summary>
    Classified,

    /// <summary>Stopped by a mechanical failure.</summary>
    Mechanical,

    /// <summary>Stopped by an accident or collision.</summary>
    Accident,

    /// <summary>Any other retirement or exclusion.</summary>
    Other,
}
