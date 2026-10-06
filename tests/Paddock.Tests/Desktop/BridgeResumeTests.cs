using System.Text.Json;
using Paddock.Desktop.Bridge;

namespace Paddock.Tests.Desktop;

/// <summary>
/// #252 (INV-002): a save written on the first morning of a season loads to the same world and then lives exactly the future the
/// uninterrupted run lives, including every inbox item and its id (the notices of the AI managers included).
/// </summary>
public class BridgeResumeTests
{
    private static readonly string[] Queries = ["inbox", "shell"];

    [Theory]
    [InlineData("1956-01-01", 40, 2)]
    [InlineData("1955-06-01", 40, 2)]
    public void AResumedSaveContinuesLikeTheLiveRun(string saveOn, int days, int seed)
    {
        var live = BridgeHost.Lobby(BridgeTestData.DataRoot);
        Send(live, "start", "command", "newCareer", """{"managerId":"human:player","teamId":"ferrari","givenName":"Enzo","familyName":"Test","nationality":"IT","tilt":"none","preset":"Balanced","year":1955,"seed":""" + seed + "}");
        AdvanceTo(live, saveOn);

        var name = "resume-" + Guid.NewGuid().ToString("N");
        var path = Path.Combine(BridgeTestData.SavesDirectory, name + ".paddock");
        try
        {
            Send(live, "save", "command", "saveCareer", "{\"managerId\":\"human:player\",\"name\":\"" + name + "\"}");
            var resumed = BridgeHost.Lobby(BridgeTestData.DataRoot);
            Send(resumed, "load", "command", "loadCareer", "{\"managerId\":\"human:player\",\"path\":\"" + name + "\"}");
            Assert.Equal(live.StateHash, resumed.StateHash);
            Assert.Equal(Snapshot(live), Snapshot(resumed));

            for (var day = 0; day < days; day++)
            {
                Step(live, day);
                Step(resumed, day);
                Assert.True(live.StateHash == resumed.StateHash, "hash differs after day " + day);
                Assert.Equal(Snapshot(live), Snapshot(resumed));
            }
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static string Snapshot(BridgeHost host)
    {
        var parts = new List<string>();
        foreach (var query in Queries)
        {
            parts.Add(host.Handle(Message("snap-" + query, "query", query)).Response);
        }

        return string.Join("\n", parts);
    }

    private static void AdvanceTo(BridgeHost host, string date)
    {
        for (var day = 0; day < 400; day++)
        {
            if (DateOf(host) == date)
            {
                return;
            }

            Step(host, day);
        }

        Assert.Fail("never reached " + date);
    }

    private static string DateOf(BridgeHost host)
    {
        using var json = JsonDocument.Parse(host.Handle(Message("date", "query", "shell")).Response);
        return json.RootElement.GetProperty("data").GetProperty("date").GetString()!;
    }

    private static void Step(BridgeHost host, int day)
    {
        ClearDecisions(host, day);
        var exchange = host.Handle(Message("day" + day, "command", "advanceDay"));
        Assert.True(JsonDocument.Parse(exchange.Response).RootElement.GetProperty("ok").GetBoolean(), exchange.Response + host.Handle(Message("s", "query", "shell")).Response + host.Handle(Message("i", "query", "inbox")).Response);
    }

    private static void ClearDecisions(BridgeHost host, int day)
    {
        for (var pass = 0; pass < 60; pass++)
        {
            using var json = JsonDocument.Parse(host.Handle(Message("inbox" + day + "-" + pass, "query", "inbox")).Response);
            string? itemId = null;
            var optionIds = new List<string>();
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
                foreach (var option in options.EnumerateArray())
                {
                    optionIds.Add(option.GetProperty("id").GetString()!);
                }

                break;
            }

            if (itemId is null)
            {
                return;
            }

            // The last option first: it is the one that opens no negotiation (the 1 July renewal flood hits the 5-negotiation cap, #253).
            optionIds.Reverse();
            var resolved = false;
            foreach (var optionId in optionIds)
            {
                var exchange = host.Handle(Message("yes" + day + "-" + pass, "command", "resolveInbox", "{\"managerId\":\"human:player\",\"itemId\":\"" + itemId + "\",\"optionId\":\"" + optionId + "\"}"));
                using var answer = JsonDocument.Parse(exchange.Response);
                if (answer.RootElement.GetProperty("ok").GetBoolean())
                {
                    resolved = true;
                    break;
                }
            }

            Assert.True(resolved, "no option of " + itemId + " could be taken on day " + day);
        }
    }

    private static void Send(BridgeHost host, string id, string kind, string name, string? args = null)
    {
        var exchange = host.Handle(Message(id, kind, name, args));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), exchange.Response);
    }

    private static string Message(string id, string kind, string name, string? args = null) =>
        "{\"id\":\"" + id + "\",\"kind\":\"" + kind + "\",\"name\":\"" + name + "\",\"args\":"
        + (args ?? "{\"managerId\":\"" + CareerBridge.HumanManagerId + "\"}") + "}";
}
