using System.Text.Json;
using Paddock.Desktop.Bridge;

namespace Paddock.Tests.Desktop;

/// <summary>#324: the newspaper of the main screen, read through the bridge from a real career.</summary>
public class NewspaperBridgeTests
{
    private const string Ferrari = """{"managerId":"human:player","teamId":"ferrari","givenName":"Enzo","familyName":"Test","nationality":"ITA","tilt":"none","preset":"Chaos","year":1955,"seed":1}""";

    [Fact]
    public void AfterTheFirstRaceThePaperHasTheWinnerAndTheResultOfTheOwnTeamAndChangesNothing()
    {
        var host = StartCareer();
        AdvanceUntil(host, () => Rounds(host) >= 1, 90);
        var hash = host.StateHash;
        var tiles = Paper(host);
        Assert.Equal(hash, host.StateHash);

        var result = Json(host, "result", "raceResult");
        var rows = result.GetProperty("rows").EnumerateArray().ToArray();
        var winner = rows.First(row => row.GetProperty("position").GetInt32() == 1);
        var win = Assert.Single(tiles, tile => tile.GetProperty("kind").GetString() == "race");
        Assert.Equal("paper.race.win", win.GetProperty("title").GetProperty("key").GetString());
        Assert.Equal(winner.GetProperty("driverName").GetString(), win.GetProperty("title").GetProperty("parameters").GetProperty("driver").GetString());
        Assert.Equal("race", win.GetProperty("link").GetProperty("kind").GetString());
        Assert.Equal("1", win.GetProperty("link").GetProperty("id").GetString());

        var ownRows = rows.Where(row => row.GetProperty("teamId").GetString() == "ferrari").ToArray();
        Assert.NotEmpty(ownRows);
        var own = Assert.Single(tiles, tile => tile.GetProperty("kind").GetString() == "own");
        Assert.True(own.GetProperty("mine").GetBoolean());
        Assert.StartsWith("paper.race.own", own.GetProperty("title").GetProperty("key").GetString(), StringComparison.Ordinal);
        Assert.Contains(
            ownRows.Select(row => row.GetProperty("driverName").GetString()),
            name => name == own.GetProperty("title").GetProperty("parameters").GetProperty("driver").GetString());
    }

    [Fact]
    public void AfterASigningThePaperHasASigningHeadlineThatOpensTheDriver()
    {
        var host = StartCareer();
        var market = Json(host, "market", "market");
        var free = market.GetProperty("freeAgents").EnumerateArray().First(person => person.GetProperty("kind").GetString() == "driver");
        var personId = free.GetProperty("personId").GetString()!;
        var opened = host.Handle(Message("open", "command", "openNegotiation", "{\"managerId\":\"human:player\",\"organizationId\":\"ferrari\",\"personId\":\"" + personId + "\",\"subject\":\"driver\",\"deadline\":null}"));
        Assert.True(Ok(opened.Response), opened.Response);
        var id = Json(host, "neg", "negotiations").GetProperty("items")[0].GetProperty("id").GetString()!;
        var offered = host.Handle(Message(
            "offer",
            "command",
            "submitOffer",
            "{\"managerId\":\"human:player\",\"negotiationId\":\"" + id + "\",\"salary\":900000,\"winBonus\":0,\"titleBonus\":0,\"years\":2,\"seat\":\"Equal\",\"optionHolder\":null,\"optionYears\":null,\"exitWorseThan\":null}"));
        Assert.True(Ok(offered.Response), offered.Response);

        Assert.DoesNotContain(Paper(host), tile => tile.GetProperty("kind").GetString() == "signing");
        JsonElement? signing = null;
        for (var day = 0; day < 40 && signing is null; day++)
        {
            Advance(host, day);
            signing = Paper(host).Cast<JsonElement?>().FirstOrDefault(tile => tile!.Value.GetProperty("kind").GetString() == "signing");
        }

        Assert.True(signing is not null, "The driver did not sign in 40 days.");
        var tile = signing!.Value;
        Assert.True(tile.GetProperty("mine").GetBoolean());
        Assert.Equal("paper.signing.driver", tile.GetProperty("title").GetProperty("key").GetString());
        Assert.Equal("driver", tile.GetProperty("link").GetProperty("kind").GetString());
        Assert.Equal(personId, tile.GetProperty("link").GetProperty("id").GetString());
    }

    private static BridgeHost StartCareer()
    {
        var host = BridgeHost.Lobby(BridgeTestData.DataRoot);
        var started = host.Handle(Message("start", "command", "newCareer", Ferrari));
        Assert.True(Ok(started.Response), started.Response);
        return host;
    }

    private static JsonElement[] Paper(BridgeHost host) =>
        Json(host, "paper", "newspaper").GetProperty("headlines").EnumerateArray().ToArray();

    private static int Rounds(BridgeHost host) => Json(host, "table", "standings").GetProperty("roundsCompleted").GetInt32();

    private static void AdvanceUntil(BridgeHost host, Func<bool> done, int days)
    {
        for (var day = 0; day < days && !done(); day++)
        {
            Advance(host, day);
        }

        Assert.True(done(), "The condition did not come true in " + days + " days.");
    }

    /// <summary>One day on. A decision that holds the clock is answered with its sign or accept option, else its first.</summary>
    private static void Advance(BridgeHost host, int day)
    {
        for (var pass = 0; pass < 12; pass++)
        {
            var moved = host.Handle(Message("day" + day + "-" + pass, "command", "advanceDay"));
            if (Ok(moved.Response))
            {
                return;
            }

            Answer(host, day + "-" + pass);
        }

        Assert.Fail("The clock stayed held on day " + day + ".");
    }

    private static void Answer(BridgeHost host, string tag)
    {
        var inbox = Json(host, "inbox" + tag, "inbox");
        foreach (var item in inbox.GetProperty("items").EnumerateArray())
        {
            if (!item.GetProperty("needsDecision").GetBoolean() || item.GetProperty("status").GetString() != "Open")
            {
                continue;
            }

            var options = item.GetProperty("options").EnumerateArray().Select(option => option.GetProperty("id").GetString()!).ToArray();
            if (options.Length == 0)
            {
                continue;
            }

            var pick = options.FirstOrDefault(option => option is "sign" or "accept") ?? options[0];
            var resolved = host.Handle(Message(
                "yes" + tag + item.GetProperty("id").GetString(),
                "command",
                "resolveInbox",
                "{\"managerId\":\"human:player\",\"itemId\":\"" + item.GetProperty("id").GetString() + "\",\"optionId\":\"" + pick + "\"}"));
            Assert.True(Ok(resolved.Response), resolved.Response);
            return;
        }
    }

    private static JsonElement Json(BridgeHost host, string id, string query)
    {
        var exchange = host.Handle(Message(id, "query", query));
        using var document = JsonDocument.Parse(exchange.Response);
        Assert.True(document.RootElement.GetProperty("ok").GetBoolean(), exchange.Response);
        return document.RootElement.GetProperty("data").Clone();
    }

    private static bool Ok(string response)
    {
        using var document = JsonDocument.Parse(response);
        return document.RootElement.GetProperty("ok").GetBoolean();
    }

    private static string Message(string id, string kind, string name, string? args = null) =>
        "{\"id\":\"" + id + "\",\"kind\":\"" + kind + "\",\"name\":\"" + name + "\",\"args\":"
        + (args ?? "{\"managerId\":\"" + CareerBridge.HumanManagerId + "\"}") + "}";
}
