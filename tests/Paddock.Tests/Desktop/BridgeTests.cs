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
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), endpoint.Name + " " + exchange.Response);
        }
    }

    [Fact]
    public void UnknownNameReturnsItsReasonKey()
    {
        using var career = Open();
        var exchange = career.Host.Handle(Message("u1", "query", "notAQuery"));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(BridgeKeys.UnknownName, json.RootElement.GetProperty("error").GetProperty("key").GetString());
        Assert.Equal("notAQuery", json.RootElement.GetProperty("error").GetProperty("parameters").GetProperty("name").GetString());
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
    public void OpeningOffersTheSeasonTargetWithItsReason()
    {
        using var career = Open();
        var exchange = career.Host.Handle(Message("in", "query", "inbox"));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), exchange.Response);
        var match = json.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("kind").GetString() == BoardEngine.SeasonTargetKind);
        Assert.Equal(BoardKeys.SeasonTargetSubject, match.GetProperty("subject").GetProperty("key").GetString());
        Assert.Equal(SeasonTarget.Expected, match.GetProperty("defaultOptionId").GetString());
    }

    [Fact]
    public void TheAiPrincipalDirectorLeavesThePlayersTeamAlone()
    {
        // Regression: without seating the player, the T44 director ran the player's team as an AI team
        // (for example it set that team's scouting focus on the first morning).
        using var career = Open();
        AcceptExpectedSeasonTarget(career.Host, "target");
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
        AcceptExpectedSeasonTarget(first.Host, "t1");
        AcceptExpectedSeasonTarget(second.Host, "t2");
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
    public void AFerrariCareerReachesTheFirstRaceAndASaveResumesTheSameWorld()
    {
        using var career = Lobby();
        var started = career.Host.Handle(Message(
            "start",
            "command",
            "newCareer",
            """{"managerId":"human:player","teamId":"ferrari","givenName":"Enzo","familyName":"Test","nationality":"IT","tilt":"none","preset":"Chaos","year":1955,"seed":1}"""));
        using (var json = JsonDocument.Parse(started.Response))
        {
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), started.Response);
            Assert.Equal("1955-01-01", json.RootElement.GetProperty("data").GetProperty("date").GetString());
            Assert.Equal("ferrari", json.RootElement.GetProperty("data").GetProperty("organizationId").GetString());
        }

        var refused = career.Host.Handle(Message(
            "split",
            "command",
            "setDevelopmentSplit",
            """{"managerId":"human:player","organizationId":"mercedes","currentPercent":40,"accountPercent":30,"nextYearPercent":30}"""));
        using (var json = JsonDocument.Parse(refused.Response))
        {
            Assert.False(json.RootElement.GetProperty("ok").GetBoolean(), refused.Response);
            Assert.Equal("development.error.notInControl", json.RootElement.GetProperty("error").GetProperty("key").GetString());
        }

        var raced = false;
        for (var day = 0; day < 90 && !raced; day++)
        {
            var pool = career.Host.Handle(Message("pool" + day, "query", "pool"));
            using (var poolJson = JsonDocument.Parse(pool.Response))
            {
                Assert.Equal(JsonValueKind.Null, poolJson.RootElement.GetProperty("data").GetProperty("focus").ValueKind);
            }

            ClearDecision(career.Host, day);
            var moved = career.Host.Handle(Message("day" + day, "command", "advanceDay"));
            using var movedJson = JsonDocument.Parse(moved.Response);
            Assert.True(movedJson.RootElement.GetProperty("ok").GetBoolean(), moved.Response);
            var table = career.Host.Handle(Message("table" + day, "query", "standings"));
            using var tableJson = JsonDocument.Parse(table.Response);
            Assert.True(tableJson.RootElement.GetProperty("ok").GetBoolean(), table.Response);
            if (tableJson.RootElement.GetProperty("data").GetProperty("roundsCompleted").GetInt32() >= 1)
            {
                raced = true;
            }
        }

        Assert.True(raced, "the first 1955 race did not finish");
        var calendar = career.Host.Handle(Message("calendar", "query", "calendar"));
        using (var json = JsonDocument.Parse(calendar.Response))
        {
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), calendar.Response);
            var rounds = json.RootElement.GetProperty("data").GetProperty("rounds");
            Assert.True(rounds.GetArrayLength() > 1);
            Assert.Contains(rounds.EnumerateArray(), round => round.GetProperty("finished").GetBoolean());
        }

        var next = career.Host.Handle(Message("next", "query", "nextRace"));
        using (var json = JsonDocument.Parse(next.Response))
        {
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), next.Response);
        }

        var result = career.Host.Handle(Message("result", "query", "raceResult"));
        using (var json = JsonDocument.Parse(result.Response))
        {
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), result.Response);
            Assert.True(json.RootElement.GetProperty("data").GetProperty("found").GetBoolean());
            Assert.True(json.RootElement.GetProperty("data").GetProperty("rows").GetArrayLength() > 1);
            Assert.True(json.RootElement.GetProperty("data").GetProperty("sections").GetArrayLength() > 0);
        }

        var hash = career.Host.StateHash;
        var name = "bridge-" + Guid.NewGuid().ToString("N");
        var path = Path.Combine(BridgeHost.RepositoryRoot(), "saves", name + ".paddock");
        try
        {
            var saved = career.Host.Handle(Message(
                "save",
                "command",
                "saveCareer",
                "{\"managerId\":\"human:player\",\"name\":\"" + name + "\"}"));
            using (var json = JsonDocument.Parse(saved.Response))
            {
                Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), saved.Response);
                Assert.Equal(hash, json.RootElement.GetProperty("data").GetProperty("hash").GetString());
            }

            using var loaded = Lobby();
            var back = loaded.Host.Handle(Message(
                "load",
                "command",
                "loadCareer",
                "{\"managerId\":\"human:player\",\"path\":\"" + name + "\"}"));
            using var loadedJson = JsonDocument.Parse(back.Response);
            Assert.True(loadedJson.RootElement.GetProperty("ok").GetBoolean(), back.Response);
            Assert.Equal(hash, loaded.Host.StateHash);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
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

    private static void AcceptExpectedSeasonTarget(BridgeHost host, string id)
    {
        var inbox = host.Handle(Message(id, "query", "inbox"));
        using var json = JsonDocument.Parse(inbox.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), inbox.Response);
        var itemId = json.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("kind").GetString() == BoardEngine.SeasonTargetKind)
            .GetProperty("id").GetString();
        var resolved = host.Handle(Message(
            id + "-answer",
            "command",
            "resolveInbox",
            "{\"managerId\":\"" + CareerBridge.HumanManagerId + "\",\"itemId\":\"" + itemId + "\",\"optionId\":\"" + SeasonTarget.Expected + "\"}"));
        using var answered = JsonDocument.Parse(resolved.Response);
        Assert.True(answered.RootElement.GetProperty("ok").GetBoolean(), resolved.Response);
    }

    private static string Message(string id, string kind, string name, string? args = null) =>
        "{\"id\":\"" + id + "\",\"kind\":\"" + kind + "\",\"name\":\"" + name + "\",\"args\":"
        + (args ?? "{\"managerId\":\"" + CareerBridge.HumanManagerId + "\"}") + "}";

    private static void ClearDecision(BridgeHost host, int day)
    {
        for (var pass = 0; pass < 8; pass++)
        {
            var inbox = host.Handle(Message("inbox" + day + "-" + pass, "query", "inbox"));
            using var json = JsonDocument.Parse(inbox.Response);
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), inbox.Response);
            string? itemId = null;
            string? optionId = null;
            foreach (var item in json.RootElement.GetProperty("data").GetProperty("items").EnumerateArray())
            {
                if (!item.GetProperty("needsDecision").GetBoolean() || item.GetProperty("status").GetString() != "Open")
                {
                    continue;
                }

                var options = item.GetProperty("options");
                if (options.GetArrayLength() == 0)
                {
                    continue;
                }

                itemId = item.GetProperty("id").GetString();
                optionId = options[0].GetProperty("id").GetString();
                break;
            }

            if (itemId is null)
            {
                return;
            }

            var resolved = host.Handle(Message(
                "yes" + day + "-" + pass,
                "command",
                "resolveInbox",
                "{\"managerId\":\"human:player\",\"itemId\":\"" + itemId + "\",\"optionId\":\"" + optionId + "\"}"));
            using var resolvedJson = JsonDocument.Parse(resolved.Response);
            Assert.True(resolvedJson.RootElement.GetProperty("ok").GetBoolean(), resolved.Response);
        }
    }

    private static Opened Open() => new(BridgeHost.Open(Path.Combine(BridgeHost.RepositoryRoot(), "data")));

    private static Opened Lobby() => new(BridgeHost.Lobby(Path.Combine(BridgeHost.RepositoryRoot(), "data")));

    private sealed class Opened : IDisposable
    {
        public Opened(BridgeHost host) => Host = host;

        public BridgeHost Host { get; }

        public void Dispose()
        {
        }
    }
}
