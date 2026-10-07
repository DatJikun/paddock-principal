using System.Text.Json;
using Paddock.Application.Racing;
using Paddock.Desktop.Bridge;
using Paddock.Simulation.Racing.Playback;

namespace Paddock.Tests.Desktop;

/// <summary>Pit wall orders in a quick race (#286): the host re-runs the race, keeps what was seen and tells every viewer.</summary>
public class PitWallOrderBridgeTests
{
    private const string Ferrari1976 = """{"managerId":"human:player","year":1976,"teamId":"ferrari","round":1,"seed":7}""";

    [Fact]
    public void AStopCalledMidRaceIsMadeAtTheEndOfTheLapAndNothingSeenBeforeChanges()
    {
        var clock = new ManualClock();
        var host = QuickRace(clock);
        var before = Data(host, "query", "liveRace");
        var pitWall = before.GetProperty("pitWall");
        Assert.True(pitWall.GetProperty("canOrder").GetBoolean());
        Assert.True(pitWall.GetProperty("tyreChange").GetBoolean());
        var compounds = pitWall.GetProperty("compounds").EnumerateArray().Select(c => c.GetString()!).ToArray();
        Assert.Contains(pitWall.GetProperty("startTyres").GetString(), compounds);
        var own = OwnRunner(before);
        Assert.All(pitWall.GetProperty("laps").EnumerateArray(), lap => Assert.Contains(lap.GetProperty("carId").GetString(), OwnCars(before)));

        Control(host, "play");
        clock.Now += 30_000;
        var now = Data(host, "query", "liveClock").GetProperty("raceTimeMs").GetInt64();
        var hard = compounds[^2];

        var ordered = host.Handle(Message("o", "command", "liveRaceOrder", Order(own, "pit", tyres: hard)));
        using (var json = JsonDocument.Parse(ordered.Response))
        {
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), ordered.Response);
            Assert.Equal(1, json.RootElement.GetProperty("data").GetProperty("revision").GetInt32());
            Assert.Equal(now, json.RootElement.GetProperty("data").GetProperty("raceTimeMs").GetInt64());
        }

        Assert.Contains(ordered.Events, item => item.Contains("raceTape", StringComparison.Ordinal));
        var after = Data(host, "query", "liveRace");
        Assert.Equal(1, after.GetProperty("pitWall").GetProperty("revision").GetInt32());
        var order = Assert.Single(after.GetProperty("pitWall").GetProperty("orders").EnumerateArray());
        var lap = order.GetProperty("lap").GetInt32();
        Assert.Contains(
            after.GetProperty("events").EnumerateArray(),
            e => e.GetProperty("kind").GetString() == "pitOut" && e.GetProperty("carId").GetString() == own
                && e.GetProperty("lap").GetInt32() == lap && e.GetProperty("tyres").GetString() == hard);
        Assert.Contains(after.GetProperty("events").EnumerateArray(), e => e.GetProperty("kind").GetString() == "order" && e.GetProperty("timeMs").GetInt64() == now);
        Assert.Equal(Seen(before, now), Seen(after, now));
    }

    [Fact]
    public void PaceIsTakenFromTheNextLapRivalsCannotBeOrderedAndTheSameOrdersGiveTheSameRace()
    {
        string Run(bool rival)
        {
            var clock = new ManualClock();
            var host = QuickRace(clock);
            var race = Data(host, "query", "liveRace");
            var own = OwnRunner(race);
            Control(host, "play");
            clock.Now += 20_000;
            if (rival)
            {
                var other = race.GetProperty("cars").EnumerateArray().First(c => !c.GetProperty("own").GetBoolean()).GetProperty("carId").GetString()!;
                Assert.Equal(LiveOrderKeys.NotOwn, Error(host, "liveRaceOrder", Order(other, "pace", pace: "push")));
            }

            Data(host, "command", "liveRaceOrder", Order(own, "pace", pace: "push"));
            var after = Data(host, "query", "liveRace");
            var pushed = after.GetProperty("pitWall").GetProperty("laps").EnumerateArray()
                .Where(l => l.GetProperty("carId").GetString() == own && l.GetProperty("manual").GetBoolean()).ToArray();
            Assert.NotEmpty(pushed);
            Assert.All(pushed, l => Assert.Equal("push", l.GetProperty("pace").GetString()));
            Assert.All(pushed, l => Assert.True(l.GetProperty("startMs").GetInt64() >= 200_000));
            return after.GetProperty("events").GetRawText();
        }

        Assert.Equal(Run(rival: false), Run(rival: true));
    }

    private static string OwnRunner(JsonElement race)
    {
        // An own car that is still running at the flag, so every lap of the orders exists.
        var finishers = race.GetProperty("events").EnumerateArray()
            .Where(e => e.GetProperty("kind").GetString() == "finish")
            .Select(e => e.GetProperty("carId").GetString()!)
            .ToHashSet();
        return OwnCars(race).First(finishers.Contains);
    }

    private static string[] OwnCars(JsonElement race) =>
        [.. race.GetProperty("cars").EnumerateArray().Where(c => c.GetProperty("own").GetBoolean()).Select(c => c.GetProperty("carId").GetString()!)];

    // What the screen showed before the order: the tape's events up to now (the radio and the answers are the pit wall's own).
    private static string[] Seen(JsonElement race, long nowMs) =>
        [.. race.GetProperty("events").EnumerateArray()
            .Where(e => e.GetProperty("timeMs").GetInt64() < nowMs && e.GetProperty("kind").GetString() is not ("order" or "driver" or "call"))
            .Select(e => e.GetRawText())];

    private static string Order(string car, string action, string? pace = null, string? tyres = null) =>
        "{\"managerId\":\"human:player\",\"carId\":\"" + car + "\",\"action\":\"" + action + "\""
        + (pace is null ? "" : ",\"pace\":\"" + pace + "\"")
        + (tyres is null ? "" : ",\"tyres\":\"" + tyres + "\"") + "}";

    private static BridgeHost QuickRace(IClock clock)
    {
        var career = CareerBridge.Lobby(BridgeTestData.DataRoot);
        career.LiveClock = clock;
        var host = new BridgeHost(career);
        Data(host, "command", "startQuickRace", Ferrari1976);
        return host;
    }

    private static void Control(BridgeHost host, string action) =>
        Data(host, "command", "liveRaceControl", "{\"managerId\":\"human:player\",\"action\":\"" + action + "\"}");

    private static JsonElement Data(BridgeHost host, string kind, string name, string? args = null)
    {
        var exchange = host.Handle(Message(name, kind, name, args));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), exchange.Response);
        return json.RootElement.GetProperty("data").Clone();
    }

    private static string? Error(BridgeHost host, string name, string args)
    {
        var exchange = host.Handle(Message(name, "command", name, args));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean(), exchange.Response);
        return json.RootElement.GetProperty("error").GetProperty("key").GetString();
    }

    private static string Message(string id, string kind, string name, string? args = null) =>
        "{\"id\":\"" + id + "\",\"kind\":\"" + kind + "\",\"name\":\"" + name + "\",\"args\":" + (args ?? "{\"managerId\":\"human:player\"}") + "}";

    private sealed class ManualClock : IClock
    {
        public long Now { get; set; }

        public long NowMs => Now;
    }
}
