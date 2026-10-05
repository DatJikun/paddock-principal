using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Paddock.Desktop.Bridge;

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
    public void TheWindowOpensWithNoCareer()
    {
        using var career = Open(start: false);
        var exchange = career.Host.Handle(Message("s", "query", "session"));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), exchange.Response);
        Assert.False(json.RootElement.GetProperty("data").GetProperty("open").GetBoolean());
        var shell = career.Host.Handle(Message("shell", "query", "shell"));
        using var refused = JsonDocument.Parse(shell.Response);
        Assert.False(refused.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(Paddock.Application.Career.PlayKeys.NoCareer, refused.RootElement.GetProperty("error").GetProperty("key").GetString());
    }

    [Fact]
    public void StartOnFerrariSavesAndLoadsTheSameHash()
    {
        using var first = Open();
        var started = first.Host.Handle(Message("dup", "command", "startCareer", StartArgs()));
        using var refused = JsonDocument.Parse(started.Response);
        Assert.False(refused.RootElement.GetProperty("ok").GetBoolean());

        var before = first.Host.StateHash;
        var saved = first.Host.Handle(Message("save", "command", "saveCareer", "{\"managerId\":\"" + CareerBridge.HumanManagerId + "\",\"name\":\"ferrari1955\"}"));
        using var saveJson = JsonDocument.Parse(saved.Response);
        Assert.True(saveJson.RootElement.GetProperty("ok").GetBoolean(), saved.Response);

        using var second = Open(start: false, first.Saves);
        var loaded = second.Host.Handle(Message("load", "command", "loadCareer", "{\"managerId\":\"" + CareerBridge.HumanManagerId + "\",\"name\":\"ferrari1955\"}"));
        using var loadJson = JsonDocument.Parse(loaded.Response);
        Assert.True(loadJson.RootElement.GetProperty("ok").GetBoolean(), loaded.Response);
        Assert.Equal(before, second.Host.StateHash);
        Assert.Equal(CareerBridge.HumanManagerId, loadJson.RootElement.GetProperty("data").GetProperty("managerId").GetString());
    }

    [Fact]
    public void ACommandForAnotherTeamIsRejected()
    {
        using var career = Open();
        var teams = career.Host.Handle(Message("teams", "query", "teams", "{\"managerId\":\"" + CareerBridge.HumanManagerId + "\",\"year\":1955}"));
        using var listed = JsonDocument.Parse(teams.Response);
        string? other = null;
        foreach (var team in listed.RootElement.GetProperty("data").GetProperty("teams").EnumerateArray())
        {
            var id = team.GetProperty("id").GetString();
            if (id is not null && !string.Equals(id, "ferrari", StringComparison.Ordinal))
            {
                other = id;
                break;
            }
        }

        Assert.NotNull(other);
        var before = career.Host.StateHash;
        var exchange = career.Host.Handle(Message(
            "split",
            "command",
            "developmentSplit",
            "{\"managerId\":\"" + CareerBridge.HumanManagerId + "\",\"organizationId\":\"" + other + "\",\"currentPercent\":40,\"accountPercent\":20,\"nextYearPercent\":40}"));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean(), exchange.Response);
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("error").GetProperty("key").GetString()));
        Assert.Equal(before, career.Host.StateHash);
    }

    private static string Message(string id, string kind, string name, string? args = null) =>
        "{\"id\":\"" + id + "\",\"kind\":\"" + kind + "\",\"name\":\"" + name + "\",\"args\":"
        + (args ?? "{\"managerId\":\"" + CareerBridge.HumanManagerId + "\"}") + "}";

    private static string StartArgs() =>
        "{\"managerId\":\"" + CareerBridge.HumanManagerId + "\",\"preset\":\"Balanced\",\"year\":1955,\"fatality\":\"Off\",\"teamId\":\"ferrari\",\"givenName\":\"Enzo\",\"familyName\":\"Ferrari\",\"nationality\":\"IT\",\"tilt\":\"none\",\"seed\":\"1\",\"careerName\":\"career\"}";

    private static Opened Open(bool start = true, string? saves = null)
    {
        var directory = saves ?? Path.Combine(Path.GetTempPath(), "paddock-bridge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var host = BridgeHost.Open(Path.Combine(BridgeHost.RepositoryRoot(), "data"), savesDirectory: directory);
        if (start)
        {
            var exchange = host.Handle(Message("start", "command", "startCareer", StartArgs()));
            using var json = JsonDocument.Parse(exchange.Response);
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), exchange.Response);
            var notice = json.RootElement.GetProperty("data").GetProperty("noticeKey");
            if (notice.ValueKind != JsonValueKind.Null)
            {
                Assert.Equal(BridgeKeys.GeneratedPeople, notice.GetString());
            }
        }

        return new Opened(host, directory, ownsDirectory: saves is null);
    }

    private sealed class Opened : IDisposable
    {
        private readonly bool _ownsDirectory;

        public Opened(BridgeHost host, string saves, bool ownsDirectory)
        {
            Host = host;
            Saves = saves;
            _ownsDirectory = ownsDirectory;
        }

        public BridgeHost Host { get; }

        public string Saves { get; }

        public void Dispose()
        {
            if (_ownsDirectory && Directory.Exists(Saves))
            {
                Directory.Delete(Saves, true);
            }
        }
    }
}
