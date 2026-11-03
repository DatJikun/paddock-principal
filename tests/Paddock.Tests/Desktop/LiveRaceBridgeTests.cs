using System.Text.Json;
using Paddock.Application.Racing;
using Paddock.Desktop.Bridge;
using Paddock.Simulation.Racing.Playback;

namespace Paddock.Tests.Desktop;

/// <summary>The career's race of today, watched live through the bridge before the day books it (PP-052, TECH §5.1, #316).</summary>
public class LiveRaceBridgeTests
{
    private const string Manager = "{\"managerId\":\"human:player\"}";

    [Fact]
    public void BeforeAnyRaceThereIsNothingToWatchAndAnOrderIsRefusedWithAReason()
    {
        var host = StartFerrariCareer(new ManualClock());

        Assert.False(Data(host, "query", "liveClock").GetProperty("active").GetBoolean());
        Assert.False(Data(host, "query", "liveRace").GetProperty("found").GetBoolean());
        var refused = host.Handle(Message("c", "command", "liveRaceControl", "{\"managerId\":\"human:player\",\"action\":\"play\"}"));
        using var json = JsonDocument.Parse(refused.Response);
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(LiveRaceText.NoRace, json.RootElement.GetProperty("error").GetProperty("key").GetString());
    }

    [Fact]
    public void TheFirstRaceOpensPausedEveryViewerFollowsOneClockAndOnlyOurStrategistIsHeard()
    {
        var clock = new ManualClock();
        var host = StartFerrariCareer(clock);
        AdvanceToRaceMorning(host);
        Data(host, "command", "watchRace");
        // Opening the race records the tape being watched (#316); playing it changes nothing more.
        var hashBefore = host.StateHash;

        var opened = Data(host, "query", "liveClock");
        Assert.True(opened.GetProperty("active").GetBoolean());
        Assert.True(opened.GetProperty("paused").GetBoolean());
        Assert.Equal(0, opened.GetProperty("raceTimeMs").GetInt64());
        Assert.Equal(1, opened.GetProperty("speed").GetDouble());
        Assert.Equal(new double[] { 1, 2, 5, 10, 20, 30 }, opened.GetProperty("speeds").EnumerateArray().Select(item => item.GetDouble()));
        var duration = opened.GetProperty("durationMs").GetInt64();
        Assert.True(duration > 0);

        var race = Data(host, "query", "liveRace");
        Assert.True(race.GetProperty("found").GetBoolean());
        Assert.True(race.GetProperty("totalLaps").GetInt32() > 0);
        Assert.True(race.GetProperty("lapLengthM").GetDouble() > 0);
        Assert.Equal(duration, race.GetProperty("durationMs").GetInt64());
        var cars = race.GetProperty("cars").EnumerateArray().ToArray();
        var own = cars.Where(car => car.GetProperty("own").GetBoolean()).Select(car => car.GetProperty("carId").GetString()).ToHashSet();
        Assert.NotEmpty(own);
        Assert.All(cars.Where(car => car.GetProperty("own").GetBoolean()), car => Assert.Equal("ferrari", car.GetProperty("teamId").GetString()));

        var events = race.GetProperty("events").EnumerateArray().ToArray();
        Assert.Equal("start", events[0].GetProperty("kind").GetString());
        Assert.Contains(events, item => item.GetProperty("kind").GetString() == "finish" && item.GetProperty("key").GetString() == LiveRaceKeys.Winner);
        var times = events.Select(item => item.GetProperty("timeMs").GetInt64()).ToArray();
        Assert.Equal(times.OrderBy(time => time), times);
        foreach (var call in events.Where(item => item.GetProperty("kind").GetString() == "call"))
        {
            Assert.True(call.GetProperty("own").GetBoolean());
            Assert.Contains(call.GetProperty("carId").GetString(), own);
        }

        var narrated = events.Where(item => item.GetProperty("key").ValueKind == JsonValueKind.String).ToArray();
        Assert.NotEmpty(narrated);
        Assert.DoesNotContain(events, item => item.GetProperty("kind").GetString() == "lap" && item.GetProperty("key").ValueKind != JsonValueKind.Null);

        // One clock: the sender gets the reply and every viewer gets the push.
        var play = host.Handle(Message("p", "command", "liveRaceControl", "{\"managerId\":\"human:player\",\"action\":\"setSpeed\",\"speed\":20}"));
        Assert.Contains(play.Events, item => item.Contains("raceClock", StringComparison.Ordinal));
        clock.Now += 3_000;
        Assert.Equal(60_000, Data(host, "query", "liveClock").GetProperty("raceTimeMs").GetInt64());
        Control(host, "pause");
        clock.Now += 3_000;
        Assert.Equal(60_000, Data(host, "query", "liveClock").GetProperty("raceTimeMs").GetInt64());

        var badSpeed = host.Handle(Message("b", "command", "liveRaceControl", "{\"managerId\":\"human:player\",\"action\":\"setSpeed\",\"speed\":3}"));
        using (var bad = JsonDocument.Parse(badSpeed.Response))
        {
            Assert.Equal(LiveRaceText.BadSpeed, bad.RootElement.GetProperty("error").GetProperty("key").GetString());
        }

        // #322: the top step is offered and accepted; the speed never changes what the tape holds (the hash is checked below).
        var top = host.Handle(Message("t", "command", "liveRaceControl", "{\"managerId\":\"human:player\",\"action\":\"setSpeed\",\"speed\":30}"));
        using (var topJson = JsonDocument.Parse(top.Response))
        {
            Assert.False(topJson.RootElement.TryGetProperty("error", out _));
        }

        // The career's race of today is watched before the day books it, so its pit wall takes orders as in a quick race (#316).
        Assert.True(race.GetProperty("pitWall").GetProperty("canOrder").GetBoolean());
        Assert.Equal(JsonValueKind.Null, race.GetProperty("pitWall").GetProperty("locked").ValueKind);

        var frames = Data(host, "query", "liveFrames", "{\"managerId\":\"human:player\",\"fromMs\":30000,\"toMs\":40000}");
        Assert.True(frames.GetProperty("found").GetBoolean());
        Assert.NotEmpty(frames.GetProperty("cars").EnumerateArray());

        Control(host, "play");
        clock.Now += duration;
        var end = Data(host, "query", "liveClock");
        Assert.True(end.GetProperty("finished").GetBoolean());
        Assert.Equal(duration, end.GetProperty("raceTimeMs").GetInt64());

        // Watching is not playing: nothing is booked until the day is lived.
        Assert.Equal(hashBefore, host.StateHash);
        Assert.Equal(0, Data(host, "query", "raceResult", "{\"managerId\":\"human:player\"}").GetProperty("rows").GetArrayLength());
    }

    private static void Control(BridgeHost host, string action)
    {
        var exchange = host.Handle(Message(action, "command", "liveRaceControl", "{\"managerId\":\"human:player\",\"action\":\"" + action + "\"}"));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), exchange.Response);
    }

    internal static JsonElement Data(BridgeHost host, string kind, string name, string? args = null)
    {
        var exchange = host.Handle(Message(name, kind, name, args));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), exchange.Response);
        return json.RootElement.GetProperty("data").Clone();
    }

    internal static BridgeHost StartFerrariCareer(IClock clock)
    {
        var career = CareerBridge.Lobby(BridgeTestData.DataRoot);
        career.LiveClock = clock;
        var host = new BridgeHost(career);
        var started = host.Handle(Message(
            "start",
            "command",
            "newCareer",
            """{"managerId":"human:player","teamId":"ferrari","givenName":"Enzo","familyName":"Test","nationality":"IT","tilt":"none","preset":"Chaos","year":1955,"seed":1}"""));
        using var json = JsonDocument.Parse(started.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), started.Response);
        return host;
    }

    /// <summary>Lives days until the morning of the first race: the race is today and not run yet.</summary>
    internal static void AdvanceToRaceMorning(BridgeHost host)
    {
        for (var day = 0; day < 90; day++)
        {
            var next = Data(host, "query", "nextRace");
            if (next.TryGetProperty("date", out var date) && date.ValueKind == JsonValueKind.String
                && date.GetString() == Data(host, "query", "shell").GetProperty("date").GetString())
            {
                return;
            }

            AnswerDecisions(host, day);
            var moved = host.Handle(Message("day" + day, "command", "advanceDay"));
            using var movedJson = JsonDocument.Parse(moved.Response);
            Assert.True(movedJson.RootElement.GetProperty("ok").GetBoolean(), moved.Response);
            Assert.DoesNotContain(moved.Events, item => item.Contains("raceFinished", StringComparison.Ordinal));
        }

        Assert.Fail("the first 1955 race day did not come");
    }

    internal static void AnswerDecisions(BridgeHost host, int day)
    {
        for (var pass = 0; pass < 8; pass++)
        {
            var inbox = Data(host, "query", "inbox");
            var open = inbox.GetProperty("items").EnumerateArray().FirstOrDefault(item =>
                item.GetProperty("needsDecision").GetBoolean()
                && item.GetProperty("status").GetString() == "Open"
                && item.GetProperty("options").GetArrayLength() > 0);
            if (open.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            var resolved = host.Handle(Message(
                "yes" + day + "-" + pass,
                "command",
                "resolveInbox",
                "{\"managerId\":\"human:player\",\"itemId\":\"" + open.GetProperty("id").GetString()
                + "\",\"optionId\":\"" + open.GetProperty("options")[0].GetProperty("id").GetString() + "\"}"));
            using var json = JsonDocument.Parse(resolved.Response);
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), resolved.Response);
        }
    }

    internal static string Message(string id, string kind, string name, string? args = null) =>
        "{\"id\":\"" + id + "\",\"kind\":\"" + kind + "\",\"name\":\"" + name + "\",\"args\":" + (args ?? Manager) + "}";

    internal sealed class ManualClock : IClock
    {
        public long Now { get; set; }

        public long NowMs => Now;
    }
}
