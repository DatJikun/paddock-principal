using System.Text.Json;
using Paddock.Application.Racing;
using Paddock.Desktop.Bridge;
using Paddock.Simulation.Racing.Playback;

namespace Paddock.Tests.Desktop;

/// <summary>The quick race (#280): one round outside the career, watched in the race mode.</summary>
public class QuickRaceBridgeTests
{
    private const string Manager = "{\"managerId\":\"human:player\"}";

    private const string Ferrari1955 = """{"managerId":"human:player","year":1955,"teamId":"ferrari","round":1,"seed":7}""";

    [Fact]
    public void TheMenuListsTheSeasonsRoundsInOrderWithTheirTracks()
    {
        var host = new BridgeHost(CareerBridge.Lobby(BridgeTestData.DataRoot));

        var rounds = Data(host, "query", "quickRounds", """{"managerId":"human:player","year":1955}""");

        Assert.Equal(1955, rounds.GetProperty("season").GetInt32());
        var list = rounds.GetProperty("rounds").EnumerateArray().ToArray();
        Assert.NotEmpty(list);
        Assert.Equal(Enumerable.Range(1, list.Length), list.Select(item => item.GetProperty("round").GetInt32()));
        Assert.All(list, item => Assert.False(string.IsNullOrEmpty(item.GetProperty("circuitName").GetString())));
        Assert.All(list, item => Assert.StartsWith("1955-", item.GetProperty("date").GetString(), StringComparison.Ordinal));
        Assert.Contains(list, item => item.GetProperty("points").GetArrayLength() > 0);
    }

    [Fact]
    public void AQuickRaceOpensPausedForTheChosenTeamWithoutACareerAndClosesCleanly()
    {
        var host = Lobby(new ManualClock());

        var started = Data(host, "command", "startQuickRace", Ferrari1955);
        Assert.Equal(1955, started.GetProperty("season").GetInt32());
        Assert.Equal(1, started.GetProperty("round").GetInt32());
        Assert.Equal("ferrari", started.GetProperty("organizationId").GetString());
        Assert.False(Data(host, "query", "session").GetProperty("started").GetBoolean());

        var clock = Data(host, "query", "liveClock");
        Assert.True(clock.GetProperty("active").GetBoolean());
        Assert.True(clock.GetProperty("paused").GetBoolean());
        Assert.Equal(0, clock.GetProperty("raceTimeMs").GetInt64());

        var race = Data(host, "query", "liveRace");
        Assert.True(race.GetProperty("found").GetBoolean());
        Assert.Equal(1, race.GetProperty("round").GetInt32());
        var cars = race.GetProperty("cars").EnumerateArray().ToArray();
        Assert.Contains(cars, car => car.GetProperty("own").GetBoolean());
        Assert.All(cars.Where(car => car.GetProperty("own").GetBoolean()), car => Assert.Equal("ferrari", car.GetProperty("teamId").GetString()));
        Assert.All(cars, car => Assert.True(car.GetProperty("grid").ValueKind == JsonValueKind.Number));
        var layout = race.GetProperty("layoutId").GetString();
        Assert.True(Data(host, "query", "track", "{\"managerId\":\"human:player\",\"layoutId\":\"" + layout + "\"}").GetProperty("found").GetBoolean());
        Control(host, "play");

        Assert.True(Data(host, "command", "closeQuickRace").GetProperty("accepted").GetBoolean());
        Assert.Equal(BridgeKeys.NoCareer, Error(host, "query", "liveClock"));
        Assert.Equal(BridgeKeys.NoCareer, Error(host, "query", "liveRace"));
    }

    [Fact]
    public void TheSameChoiceAndSeedGiveTheSameRaceAndAnotherSeedAnotherOne()
    {
        string Events(string args)
        {
            var host = Lobby(new ManualClock());
            Data(host, "command", "startQuickRace", args);
            return Data(host, "query", "liveRace").GetProperty("events").GetRawText();
        }

        var first = Events(Ferrari1955);
        Assert.Equal(first, Events(Ferrari1955));
        Assert.NotEqual(first, Events(Ferrari1955.Replace("\"seed\":7", "\"seed\":8", StringComparison.Ordinal)));
    }

    [Fact]
    public void AQuickRaceLeavesTheCareerInMemoryAsItWasAndGivesItTheRaceModeBack()
    {
        var host = Lobby(new ManualClock());
        Data(host, "command", "newCareer", """{"managerId":"human:player","teamId":"maserati","givenName":"Enzo","familyName":"Test","nationality":"IT","tilt":"none","preset":"Chaos","year":1955,"seed":1}""");
        var hash = host.StateHash;
        var date = Data(host, "query", "shell").GetProperty("date").GetString();
        Assert.False(Data(host, "query", "liveClock").GetProperty("active").GetBoolean());

        Data(host, "command", "startQuickRace", Ferrari1955);
        Assert.True(Data(host, "query", "liveClock").GetProperty("active").GetBoolean());
        var race = Data(host, "query", "liveRace");
        Assert.All(race.GetProperty("cars").EnumerateArray().Where(car => car.GetProperty("own").GetBoolean()), car => Assert.Equal("ferrari", car.GetProperty("teamId").GetString()));
        Assert.Equal(hash, host.StateHash);
        Assert.Equal(date, Data(host, "query", "shell").GetProperty("date").GetString());

        Data(host, "command", "closeQuickRace");
        Assert.False(Data(host, "query", "liveClock").GetProperty("active").GetBoolean());
        Assert.Equal(hash, host.StateHash);
    }

    [Fact]
    public void ARoundOutsideTheSeasonIsRefusedWithAReasonAndOpensNothing()
    {
        var host = Lobby(new ManualClock());

        var key = Error(host, "command", "startQuickRace", Ferrari1955.Replace("\"round\":1", "\"round\":99", StringComparison.Ordinal));

        Assert.Equal(QuickRaceKeys.NoRound, key);
        Assert.Equal(BridgeKeys.NoCareer, Error(host, "query", "liveClock"));
    }

    private static BridgeHost Lobby(IClock clock)
    {
        var career = CareerBridge.Lobby(BridgeTestData.DataRoot);
        career.LiveClock = clock;
        return new BridgeHost(career);
    }

    private static void Control(BridgeHost host, string action)
    {
        var data = Data(host, "command", "liveRaceControl", "{\"managerId\":\"human:player\",\"action\":\"" + action + "\"}");
        Assert.False(data.GetProperty("paused").GetBoolean());
    }

    private static JsonElement Data(BridgeHost host, string kind, string name, string? args = null)
    {
        var exchange = host.Handle(Message(name, kind, name, args));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), exchange.Response);
        return json.RootElement.GetProperty("data").Clone();
    }

    private static string? Error(BridgeHost host, string kind, string name, string? args = null)
    {
        var exchange = host.Handle(Message(name, kind, name, args));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean(), exchange.Response);
        return json.RootElement.GetProperty("error").GetProperty("key").GetString();
    }

    private static string Message(string id, string kind, string name, string? args = null) =>
        "{\"id\":\"" + id + "\",\"kind\":\"" + kind + "\",\"name\":\"" + name + "\",\"args\":" + (args ?? Manager) + "}";

    private sealed class ManualClock : IClock
    {
        public long Now { get; set; }

        public long NowMs => Now;
    }
}
