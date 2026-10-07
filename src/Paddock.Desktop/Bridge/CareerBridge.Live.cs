using System.Diagnostics;
using System.Text.Json;
using Paddock.Application.Board;
using Paddock.Application.Career;
using Paddock.Application.Commands;
using Paddock.Application.Racing;
using Paddock.Simulation.Career;
using Paddock.Simulation.Racing.Playback;

namespace Paddock.Desktop.Bridge;

/// <summary>Argument of <c>liveFrames</c>: a window of race time in milliseconds.</summary>
public sealed record LiveFramesCall(string ManagerId, long FromMs, long ToMs);

/// <summary>Argument of <c>liveRaceControl</c>: <c>play</c>, <c>pause</c>, or <c>setSpeed</c> (with <see cref="Speed"/>).</summary>
public sealed record LiveRaceControlCall(string ManagerId, string Action, double? Speed);

/// <summary>
/// The race the career just ran, watched live (PP-052). The bridge is the host of TECH §5.1: it owns the one race clock, every
/// viewer reads it, and a viewing order from any viewer moves it for all (the host pushes <c>raceClock</c>). Watching changes
/// nothing in the world, so the orders are not world commands.
/// </summary>
public sealed partial class CareerBridge
{
    private LiveRacePlayback? _live;

    /// <summary>The clock the race playback reads. Tests replace it; the window uses a stopwatch.</summary>
    public IClock LiveClock { get; set; } = new StopwatchClock();

    /// <summary>A race just ran: open its clock at race time 0, paused until a viewer starts it.</summary>
    private void OpenLiveRace(int season, int round)
    {
        _live = Box.TryGet<RaceWatch>() is { Tape: { } tape } && !tape.Events.IsDefaultOrEmpty
            ? new LiveRacePlayback(season, round, tape.Events[^1].RaceTime, LiveClock)
            : null;
    }

    /// <summary>
    /// The race being watched: an open quick race (#280) first, otherwise the career's. Null when there is neither.
    /// </summary>
    private (CareerSession Session, CareerModuleContext Box, LiveRacePlayback? Live)? Watched() =>
        _quick is { } quick ? (quick.Session, quick.Modules, _quickLive)
        : HasCareer ? (Session, Box, _live)
        : null;

    private LiveRaceView ReadLiveRace()
    {
        var watched = Watched() ?? throw new InvalidOperationException("No career is open.");
        return watched.Box.TryGet<RaceWatch>() is { } watch && LiveFor(watch, watched.Live) is not null
            ? LiveRaceRead.Read(watched.Session, watch, watched.Box.TryGet<BoardBook>()?.Section.OrganizationOf(Human.Value), Circuits, Tracks)
            : LiveRaceRead.Read(watched.Session, new RaceWatch(), null, Circuits, Tracks);
    }

    private LiveFramesView ReadLiveFrames(JsonElement args)
    {
        var from = RaceMsOf(args, "fromMs") ?? 0;
        var to = RaceMsOf(args, "toMs") ?? from;
        return Watched() is { } watched && watched.Box.TryGet<RaceWatch>() is { } watch && LiveFor(watch, watched.Live) is not null
            ? LiveRaceRead.Frames(watch, from, to)
            : new LiveFramesView(false, from, to, []);
    }

    private LiveClockView ReadLiveClock() =>
        Watched() is { } watched && watched.Box.TryGet<RaceWatch>() is { } watch && LiveFor(watch, watched.Live) is { } live
            ? live.View()
            : new LiveClockView(false, 0, 0, 0, 0, LiveRacePlayback.DefaultSpeed, true, false, LiveRacePlayback.Speeds);

    /// <summary>Applies one viewing order. A refusal is a key; the clock is unchanged.</summary>
    public (LiveClockView? Clock, TranslationMessage? Error) ControlLiveRace(JsonElement args)
    {
        if (Watched() is not { } watched || watched.Box.TryGet<RaceWatch>() is not { } watch || LiveFor(watch, watched.Live) is not { } live)
        {
            return (null, TranslationMessage.Of(LiveRaceText.NoRace));
        }

        if (!LiveRacePlayback.TryParse(TextOf(args, "action"), out var action))
        {
            return (null, TranslationMessage.Of(LiveRaceText.BadAction));
        }

        var speed = args.TryGetProperty("speed", out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : 0;
        var refusal = live.Apply(Human.Value, action, speed);
        return refusal is null ? (live.View(), null) : (null, TranslationMessage.Of(refusal));
    }

    /// <summary>The playback of the race the watch holds, or null when the watch moved on (a new career, another race).</summary>
    private static LiveRacePlayback? LiveFor(RaceWatch watch, LiveRacePlayback? playback) =>
        playback is { } live && watch.Tape is not null && watch.Season == live.Season && watch.Round == live.Round ? live : null;

    private static long? RaceMsOf(JsonElement args, string name) =>
        args.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
            ? number
            : args.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Number
                ? (long)Math.Round(value.GetDouble(), MidpointRounding.AwayFromZero)
                : null;

    private sealed class StopwatchClock : IClock
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public long NowMs => _stopwatch.ElapsedMilliseconds;
    }
}
