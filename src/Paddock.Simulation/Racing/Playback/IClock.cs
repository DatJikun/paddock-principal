namespace Paddock.Simulation.Racing.Playback;

/// <summary>Monotonic millisecond clock. Playback never reads wall-clock time; it only asks this.</summary>
public interface IClock
{
    long NowMs { get; }
}
