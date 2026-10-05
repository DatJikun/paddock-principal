using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Paddock.Application.Board;
using Paddock.Desktop.Bridge;
using Paddock.Domain.Board;

namespace Paddock.Tests.Desktop;

public class BridgeTests
{
    [Fact]
    public void QueryRoundTripReturnsTheOpeningDate()
    {
        using var career = Open();
        var exchange = career.Host.Handle(Message("q1", "query", "shell"));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("1955-01-01", json.RootElement.GetProperty("data").GetProperty("date").GetString());
        Assert.Equal(CareerBridge.HumanManagerId, json.RootElement.GetProperty("data").GetProperty("managerId").GetString());
        Assert.Empty(exchange.Events);
    }

    [Fact]
    public void EveryRegisteredQueryReturnsOk()
    {
        using var career = Open();
        foreach (var endpoint in BridgeRegistry.Endpoints)
        {
            if (endpoint.Kind != BridgeRegistry.Query)
            {
                continue;
            }

            var exchange = career.Host.Handle(Message("q-" + endpoint.Name, "query", endpoint.Name));
            using var json = JsonDocument.Parse(exchange.Response);
            if (endpoint.Name is "raceResult" or "raceReport")
            {
                Assert.False(json.RootElement.GetProperty("ok").GetBoolean(), exchange.Response);
                Assert.Equal(BridgeKeys.NoRace, json.RootElement.GetProperty("error").GetProperty("key").GetString());
                continue;
            }

            Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), endpoint.Name + " " + exchange.Response);
        }
    }

    [Fact]
    public void UnknownNameReturnsItsReasonKey()
    {
        using var career = Open();
        var exchange = career.Host.Handle(Message("u1", "query", "missing"));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(BridgeKeys.UnknownName, json.RootElement.GetProperty("error").GetProperty("key").GetString());
        Assert.Equal("missing", json.RootElement.GetProperty("error").GetProperty("parameters").GetProperty("name").GetString());
    }

    [Fact]
    public void RejectedCommandReturnsTheReasonKeyAndDoesNotChangeTheWorld()
    {
        using var career = Open();
        var before = career.Host.StateHash;
        var exchange = career.Host.Handle(Message(
            "c1",
            "command",
            "resolveInbox",
            """{"managerId":"human:player","itemId":"missing","optionId":"yes"}"""));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("error").GetProperty("key").GetString()));
        Assert.Equal(before, career.Host.StateHash);
        Assert.Empty(exchange.Events);
    }

    [Fact]
    public void TheAiPrincipalDirectorLeavesThePlayersTeamAlone()
    {
        // Regression: without seating the player, the T44 director ran the player's team as an AI team
        // (for example it set that team's scouting focus on the first morning).
        using var career = Open();
        for (var day = 0; day < 30; day++)
        {
            var pool = career.Host.Handle(Message("p" + day, "query", "pool"));
            using var poolJson = JsonDocument.Parse(pool.Response);
            Assert.True(poolJson.RootElement.GetProperty("ok").GetBoolean(), poolJson.RootElement.ToString());
            Assert.Equal(JsonValueKind.Null, poolJson.RootElement.GetProperty("data").GetProperty("focus").ValueKind);

            var moved = career.Host.Handle(Message("a" + day, "command", "advanceDay"));
            using var movedJson = JsonDocument.Parse(moved.Response);
            if (!movedJson.RootElement.GetProperty("ok").GetBoolean())
            {
                break;
            }
        }
    }

    [Fact]
    public void AdvanceDayMovesTheDateAndASecondCareerAgrees()
    {
        using var first = Open();
        using var second = Open();
        var moved = first.Host.Handle(Message("a1", "command", "advanceDay"));
        using var json = JsonDocument.Parse(moved.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), json.RootElement.ToString());
        Assert.Equal("1955-01-02", json.RootElement.GetProperty("data").GetProperty("date").GetString());
        Assert.Contains(moved.Events, item => item.Contains("dayAdvanced", StringComparison.Ordinal));

        second.Host.Handle(Message("a1", "command", "advanceDay"));
        Assert.Equal(first.Host.StateHash, second.Host.StateHash);

        var shell = first.Host.Handle(Message("q2", "query", "shell"));
        using var shellJson = JsonDocument.Parse(shell.Response);
        Assert.Equal("1955-01-02", shellJson.RootElement.GetProperty("data").GetProperty("date").GetString());
    }

    [Fact]
    public async Task DevSocketRoundTripsAQuery()
    {
        using var career = Open();
        using var server = new DevServer(career.Host, Path.Combine(Path.GetTempPath(), "paddock-ui-missing"), DevServer.FindPort(4731));
        server.Start();
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(server.WebSocketUrl), CancellationToken.None);
        var bytes = Encoding.UTF8.GetBytes(Message("s1", "query", "shell"));
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        var buffer = new byte[64 * 1024];
        var received = await socket.ReceiveAsync(buffer, CancellationToken.None);
        using var json = JsonDocument.Parse(Encoding.UTF8.GetString(buffer, 0, received.Count));
        Assert.Equal("1955-01-01", json.RootElement.GetProperty("data").GetProperty("date").GetString());
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
    }

    [Fact]
    public void TheFirstRaceCanBeReadAsAResultAReportAndTheStandings()
    {
        using var career = Open();
        string? raceEvent = null;
        for (var day = 0; day < 120; day++)
        {
            var moved = career.Host.Handle(Message("d" + day, "command", "advanceDay"));
            using var json = JsonDocument.Parse(moved.Response);
            if (!json.RootElement.GetProperty("ok").GetBoolean())
            {
                Assert.Equal("ready.blockingItem", json.RootElement.GetProperty("error").GetProperty("key").GetString());
                AnswerSeasonTarget(career.Host, "ans" + day);
                continue;
            }

            raceEvent = moved.Events.FirstOrDefault(item => item.Contains("raceFinished", StringComparison.Ordinal));
            if (raceEvent is not null)
            {
                break;
            }
        }

        Assert.NotNull(raceEvent);
        var result = career.Host.Handle(Message("rr", "query", "raceResult"));
        using var resultJson = JsonDocument.Parse(result.Response);
        Assert.True(resultJson.RootElement.GetProperty("ok").GetBoolean(), result.Response);
        Assert.True(resultJson.RootElement.GetProperty("data").GetProperty("rows").GetArrayLength() > 0);

        var report = career.Host.Handle(Message("rp", "query", "raceReport"));
        using var reportJson = JsonDocument.Parse(report.Response);
        Assert.True(reportJson.RootElement.GetProperty("ok").GetBoolean(), report.Response);
        Assert.False(string.IsNullOrWhiteSpace(reportJson.RootElement.GetProperty("data").GetProperty("title").GetProperty("key").GetString()));

        var standings = career.Host.Handle(Message("st", "query", "standings"));
        using var standingsJson = JsonDocument.Parse(standings.Response);
        Assert.True(standingsJson.RootElement.GetProperty("ok").GetBoolean(), standings.Response);
        Assert.True(standingsJson.RootElement.GetProperty("data").GetProperty("roundsCompleted").GetInt32() >= 1);
    }

    private static void AnswerSeasonTarget(BridgeHost host, string id)
    {
        var inbox = host.Handle(Message(id, "query", "inbox"));
        using var json = JsonDocument.Parse(inbox.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), inbox.Response);
        var itemId = json.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("kind").GetString() == BoardEngine.SeasonTargetKind && item.GetProperty("status").GetString() == "Open")
            .GetProperty("id").GetString();
        var resolved = host.Handle(Message(
            id + "-r",
            "command",
            "resolveInbox",
            "{\"managerId\":\"" + CareerBridge.HumanManagerId + "\",\"itemId\":\"" + itemId + "\",\"optionId\":\"" + SeasonTarget.Expected + "\"}"));
        using var answered = JsonDocument.Parse(resolved.Response);
        Assert.True(answered.RootElement.GetProperty("ok").GetBoolean(), resolved.Response);
    }

    private static string Message(string id, string kind, string name, string? args = null) =>
        "{\"id\":\"" + id + "\",\"kind\":\"" + kind + "\",\"name\":\"" + name + "\",\"args\":"
        + (args ?? "{\"managerId\":\"" + CareerBridge.HumanManagerId + "\"}") + "}";

    private static Opened Open() => new(BridgeHost.Open(Path.Combine(BridgeHost.RepositoryRoot(), "data")));

    private sealed class Opened : IDisposable
    {
        public Opened(BridgeHost host) => Host = host;

        public BridgeHost Host { get; }

        public void Dispose()
        {
        }
    }
}
