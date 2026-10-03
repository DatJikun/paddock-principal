using System.Collections.Immutable;

namespace Paddock.Domain.Racing;

public enum RaceEventKind
{
    RaceStarted,
    LapCompleted,
    PitStop,
    PositionChange,
    Incident,
    Retirement,
    WeatherChange,
    SafetyCar,
    RedFlag,
    FastestLap,
    Finished,
    RaceEnded,
}

/// <summary>
/// Retirement buckets. Mirrors <c>Paddock.DataPipeline.FinishStatus.Kind</c> (Mechanical, Accident,
/// Other). It is mirrored, not referenced, because Domain must not depend on tools or Data (TECH §3).
/// </summary>
public enum RetirementReason
{
    Mechanical,
    Accident,
    Other,
}

public enum IncidentSeverity
{
    Minor,
    Major,
    Severe,
}

public enum PitLanePhase
{
    In,
    Out,
}

public enum SafetyCarPhase
{
    Deployed,
    Ending,
}

/// <summary>
/// One immutable fact about a race. Envelope: <see cref="Seq"/> (0-based, strictly increasing within a
/// tape), <see cref="Lap"/> (the lap the event belongs to, 0 = before the first lap) and
/// <see cref="RaceTime"/> in milliseconds since the start. Persons and organizations are referred to by
/// their stable ids as plain strings.
/// </summary>
public abstract record RaceEvent(int Seq, int Lap, long RaceTime)
{
    public abstract RaceEventKind Kind { get; }
}

public sealed record RaceStarted(int Seq, int Lap, long RaceTime, int TotalLaps, ImmutableArray<string> DriverIds)
    : RaceEvent(Seq, Lap, RaceTime)
{
    public override RaceEventKind Kind => RaceEventKind.RaceStarted;
}

public sealed record LapCompleted(int Seq, int Lap, long RaceTime, string DriverId, long LapTimeMs, int Position, long GapToLeaderMs)
    : RaceEvent(Seq, Lap, RaceTime)
{
    public override RaceEventKind Kind => RaceEventKind.LapCompleted;
}

public sealed record PitStop(int Seq, int Lap, long RaceTime, string DriverId, PitLanePhase Phase, long DurationMs, string Tyres)
    : RaceEvent(Seq, Lap, RaceTime)
{
    public override RaceEventKind Kind => RaceEventKind.PitStop;
}

public sealed record PositionChange(int Seq, int Lap, long RaceTime, string DriverId, int FromPosition, int ToPosition)
    : RaceEvent(Seq, Lap, RaceTime)
{
    public override RaceEventKind Kind => RaceEventKind.PositionChange;
}

public sealed record Incident(int Seq, int Lap, long RaceTime, ImmutableArray<string> InvolvedIds, IncidentSeverity Severity)
    : RaceEvent(Seq, Lap, RaceTime)
{
    public override RaceEventKind Kind => RaceEventKind.Incident;
}

public sealed record Retirement(int Seq, int Lap, long RaceTime, string DriverId, RetirementReason Reason)
    : RaceEvent(Seq, Lap, RaceTime)
{
    public override RaceEventKind Kind => RaceEventKind.Retirement;
}

public sealed record WeatherChange(int Seq, int Lap, long RaceTime, string Condition)
    : RaceEvent(Seq, Lap, RaceTime)
{
    public override RaceEventKind Kind => RaceEventKind.WeatherChange;
}

public sealed record SafetyCar(int Seq, int Lap, long RaceTime, SafetyCarPhase Phase, bool IsVirtual)
    : RaceEvent(Seq, Lap, RaceTime)
{
    public override RaceEventKind Kind => RaceEventKind.SafetyCar;
}

public sealed record RedFlag(int Seq, int Lap, long RaceTime)
    : RaceEvent(Seq, Lap, RaceTime)
{
    public override RaceEventKind Kind => RaceEventKind.RedFlag;
}

public sealed record FastestLap(int Seq, int Lap, long RaceTime, string DriverId, long LapTimeMs)
    : RaceEvent(Seq, Lap, RaceTime)
{
    public override RaceEventKind Kind => RaceEventKind.FastestLap;
}

public sealed record Finished(int Seq, int Lap, long RaceTime, string DriverId, int Position, int LapsCompleted, long TotalTimeMs)
    : RaceEvent(Seq, Lap, RaceTime)
{
    public override RaceEventKind Kind => RaceEventKind.Finished;
}

public sealed record RaceEnded(int Seq, int Lap, long RaceTime)
    : RaceEvent(Seq, Lap, RaceTime)
{
    public override RaceEventKind Kind => RaceEventKind.RaceEnded;
}
