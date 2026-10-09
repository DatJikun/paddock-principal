using System.Globalization;
using Paddock.Simulation.Racing.Playback;

namespace Paddock.Application.Racing;

/// <summary>What a viewer asks of the race clock.</summary>
public enum LiveRaceAction
{
    Play,
    Pause,
    SetSpeed,
}

/// <summary>
/// One viewing order, kept as data with who gave it and when (PP-045): the host applies orders in the order they arrive, so a
/// log of them replays the same watching for every guest. An order changes how the race is shown, never the race: the tape is
/// already written when the race day ends (INV-007), so this is not a world command and needs no command queue.
/// </summary>
/// <param name="ManagerId">Who gave the order.</param>
/// <param name="Action">What was asked.</param>
/// <param name="Speed">The new speed for <see cref="LiveRaceAction.SetSpeed"/>; ignored otherwise.</param>
/// <param name="AtRaceMs">Race time when the host applied it.</param>
public sealed record LiveRaceControl(string ManagerId, LiveRaceAction Action, double Speed, long AtRaceMs);

/// <summary>
/// The clock every viewer of one race follows. <see cref="RaceTimeMs"/> is the race time now; a viewer draws the race at that
/// time and moves its own picture forward at <see cref="Speed"/> until the next reading. <see cref="Revision"/> grows when a pit
/// wall order re-ran the race (#286): a viewer whose copy of the race is older reads it again.
/// </summary>
public sealed record LiveClockView(
    bool Active,
    int Season,
    int Round,
    long RaceTimeMs,
    long DurationMs,
    double Speed,
    bool Paused,
    bool Finished,
    IReadOnlyList<double> Speeds,
    int Revision = 0);

/// <summary>
/// The host's playback of one race (multiplayer foundation, TECH §5.1): a single race clock, owned by the host, that every viewer
/// reads. A single player is a host with no guests. Speeds are the watching speeds of the race screen's speed control (#322, ×1 to
/// ×30, one step at a time). There is no jump to the flag: the race is watched as it runs (owner's call, #276). It reads only the
/// injected <see cref="IClock"/>, so tests drive it by hand.
/// </summary>
public sealed class LiveRacePlayback
{
    public static IReadOnlyList<double> Speeds { get; } = [1, 2, 5, 10, 20, 30];

    /// <summary>The speed a race opens at (#322: the slowest, so nothing is missed by default). A choice of the screen, not a number of the world.</summary>
    public const double DefaultSpeed = 1;

    private readonly IClock _clock;
    private readonly List<LiveRaceControl> _log = [];
    private long _anchorRaceMs;
    private long _anchorClockMs;

    /// <summary>A race clock at race time 0, paused until a viewer starts it.</summary>
    public LiveRacePlayback(int season, int round, long durationMs, IClock clock)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(durationMs);
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        Season = season;
        Round = round;
        DurationMs = durationMs;
        Speed = DefaultSpeed;
        Paused = true;
        _anchorClockMs = clock.NowMs;
    }

    public int Season { get; }

    public int Round { get; }

    public long DurationMs { get; private set; }

    public double Speed { get; private set; }

    public bool Paused { get; private set; }

    /// <summary>Every order the host applied, oldest first.</summary>
    public IReadOnlyList<LiveRaceControl> Log => _log;

    public long RaceTimeMs
    {
        get
        {
            if (Paused)
            {
                return _anchorRaceMs;
            }

            var elapsed = Math.Max(0, _clock.NowMs - _anchorClockMs);
            return Math.Min(DurationMs, _anchorRaceMs + (long)Math.Floor(elapsed * Speed));
        }
    }

    public bool Finished => RaceTimeMs >= DurationMs;

    /// <summary>How many times a pit wall order re-ran the race being played (#286).</summary>
    public int Revision { get; private set; }

    public LiveClockView View() => new(true, Season, Round, RaceTimeMs, DurationMs, Speed, Paused || Finished, Finished, Speeds, Revision);

    /// <summary>
    /// The race was re-run by a pit wall order (#286): it now lasts <paramref name="durationMs"/>. The race time stays where it is,
    /// so every viewer goes on from the same instant.
    /// </summary>
    public void Retime(long durationMs, int revision)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(durationMs);
        var now = RaceTimeMs;
        _anchorRaceMs = Math.Min(now, durationMs);
        _anchorClockMs = _clock.NowMs;
        DurationMs = durationMs;
        Revision = revision;
    }

    /// <summary>
    /// Applies one order. A speed outside <see cref="Speeds"/> is refused with a reason key and changes nothing. Starting a
    /// finished race changes nothing either: there is no rewind (a guest who joins late sees the race from where the host is).
    /// </summary>
    public string? Apply(string managerId, LiveRaceAction action, double speed = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managerId);
        if (action == LiveRaceAction.SetSpeed && !Speeds.Contains(speed))
        {
            return LiveRaceText.BadSpeed;
        }

        var now = RaceTimeMs;
        _anchorRaceMs = now;
        _anchorClockMs = _clock.NowMs;
        switch (action)
        {
            case LiveRaceAction.Play:
                Paused = false;
                break;
            case LiveRaceAction.Pause:
                Paused = true;
                break;
            case LiveRaceAction.SetSpeed:
                Speed = speed;
                Paused = false;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown race action.");
        }

        _log.Add(new LiveRaceControl(managerId, action, action == LiveRaceAction.SetSpeed ? speed : Speed, now));
        return null;
    }

    public static bool TryParse(string? text, out LiveRaceAction action)
    {
        action = default;
        return text is not null
            && Enum.TryParse(text, ignoreCase: true, out action)
            && Enum.IsDefined(action)
            && !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }
}

/// <summary>Refusal keys of the race clock.</summary>
public static class LiveRaceText
{
    public const string NoRace = "live.error.noRace";
    public const string BadSpeed = "live.error.badSpeed";
    public const string BadAction = "live.error.badAction";
}
