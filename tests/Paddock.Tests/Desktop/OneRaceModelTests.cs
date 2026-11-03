using System.Text.Json;
using Paddock.Application.Racing;
using Paddock.Desktop.Bridge;
using static Paddock.Tests.Desktop.LiveRaceBridgeTests;

namespace Paddock.Tests.Desktop;

/// <summary>
/// One race model (#316, PP-058): the career's race of today is watched before the day books it, its pit wall takes orders as in a
/// quick race, and the day books the race with those orders. Watching without orders and simulating are the same race.
/// </summary>
public class OneRaceModelTests
{
    [Fact]
    public void WatchingWithoutOrdersBooksTheSameRaceAsSimulating()
    {
        var watched = RaceMorning(out var clock);
        LiveRaceBridgeTests.Data(watched, "command", "watchRace");
        var seen = Events(watched);
        LiveRaceBridgeTests.Data(watched, "command", "liveRaceControl", "{\"managerId\":\"human:player\",\"action\":\"setSpeed\",\"speed\":30}");
        LiveRaceBridgeTests.Data(watched, "command", "liveRaceControl", "{\"managerId\":\"human:player\",\"action\":\"play\"}");
        clock.Now += 10_000_000;
        Assert.True(LiveRaceBridgeTests.Data(watched, "query", "liveClock").GetProperty("finished").GetBoolean());
        Assert.True(Booked(watched));

        var simulated = RaceMorning(out _);
        Assert.True(Booked(simulated));

        Assert.Equal(simulated.StateHash, watched.StateHash);
        Assert.Equal(Result(simulated), Result(watched));
        // What was watched is what was booked: the clock stays where it finished, nothing is replayed from the start.
        Assert.Equal(seen, Events(watched));
        Assert.True(LiveRaceBridgeTests.Data(watched, "query", "liveClock").GetProperty("finished").GetBoolean());
    }

    [Fact]
    public void AnOrderInAWatchedCareerRaceChangesTheResultTheDayBooks()
    {
        var watched = RaceMorning(out var clock);
        LiveRaceBridgeTests.Data(watched, "command", "watchRace");
        var race = LiveRaceBridgeTests.Data(watched, "query", "liveRace");
        Assert.True(race.GetProperty("pitWall").GetProperty("canOrder").GetBoolean());
        var own = OwnRunner(race);
        LiveRaceBridgeTests.Data(watched, "command", "liveRaceControl", "{\"managerId\":\"human:player\",\"action\":\"setSpeed\",\"speed\":10}");
        LiveRaceBridgeTests.Data(watched, "command", "liveRaceControl", "{\"managerId\":\"human:player\",\"action\":\"play\"}");
        clock.Now += 20_000;
        LiveRaceBridgeTests.Data(watched, "command", "liveRaceOrder", "{\"managerId\":\"human:player\",\"carId\":\"" + own + "\",\"action\":\"pace\",\"pace\":\"push\"}");
        var ordered = Events(watched);
        Assert.NotEqual(Events(race), ordered);


        Assert.True(Booked(watched));
        Assert.Equal(ordered, Events(watched));
        Assert.False(LiveRaceBridgeTests.Data(watched, "query", "liveRace").GetProperty("pitWall").GetProperty("canOrder").GetBoolean());
        Assert.Equal(LiveOrderKeys.Closed, LiveRaceBridgeTests.Data(watched, "query", "liveRace").GetProperty("pitWall").GetProperty("locked").GetString());

        var simulated = RaceMorning(out _);
        Assert.True(Booked(simulated));
        Assert.NotEqual(simulated.StateHash, watched.StateHash);
    }

    [Fact]
    public void ThereIsNoRaceToWatchOnADayWithoutOne()
    {
        var host = StartFerrariCareer(new ManualClock());
        Assert.Equal(CareerRaceKeys.NoRace, Error(host, "watchRace", "{\"managerId\":\"human:player\"}"));
    }

    private static BridgeHost RaceMorning(out ManualClock clock)
    {
        clock = new ManualClock();
        var host = StartFerrariCareer(clock);
        AdvanceToRaceMorning(host);
        return host;
    }

    /// <summary>Lives the race day; true when the day booked the race.</summary>
    private static bool Booked(BridgeHost host)
    {
        var moved = host.Handle(Message("book", "command", "advanceDay"));
        using var json = JsonDocument.Parse(moved.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), moved.Response);
        return moved.Events.Any(item => item.Contains("raceFinished", StringComparison.Ordinal));
    }

    private static string Result(BridgeHost host) =>
        LiveRaceBridgeTests.Data(host, "query", "raceResult", "{\"managerId\":\"human:player\",\"round\":1}").GetRawText();

    private static string Events(BridgeHost host) => Events(LiveRaceBridgeTests.Data(host, "query", "liveRace"));

    // The race itself: the pit wall's own radio and answers are left out, they follow the orders.
    private static string Events(JsonElement race) =>
        string.Join('\n', race.GetProperty("events").EnumerateArray()
            .Where(e => e.GetProperty("kind").GetString() is not ("order" or "driver" or "call"))
            .Select(e => e.GetRawText()));

    private static string OwnRunner(JsonElement race)
    {
        var finishers = race.GetProperty("events").EnumerateArray()
            .Where(e => e.GetProperty("kind").GetString() == "finish")
            .Select(e => e.GetProperty("carId").GetString()!)
            .ToHashSet();
        return race.GetProperty("cars").EnumerateArray()
            .Where(c => c.GetProperty("own").GetBoolean())
            .Select(c => c.GetProperty("carId").GetString()!)
            .First(finishers.Contains);
    }

    private static string? Error(BridgeHost host, string name, string args)
    {
        var exchange = host.Handle(Message(name, "command", name, args));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean(), exchange.Response);
        return json.RootElement.GetProperty("error").GetProperty("key").GetString();
    }
}
