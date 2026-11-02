using System.Text.Json;
using Paddock.Desktop.Bridge;

namespace Paddock.Tests.Desktop;

/// <summary>#320 (playtest 2): the championship tables list every driver and constructor that raced this season, not the top six.</summary>
public class StandingsBridgeTests
{
    [Fact]
    public void AfterARaceTheStandingsListEveryDriverAndConstructorThatRaced()
    {
        var host = BridgeHost.Lobby(BridgeTestData.DataRoot);
        Send(host, "start", "command", "newCareer", """{"managerId":"human:player","teamId":"ferrari","givenName":"Enzo","familyName":"Test","nationality":"IT","tilt":"none","preset":"Balanced","year":1955,"seed":3}""");

        for (var day = 0; day < 120 && Rounds(host) == 0; day++)
        {
            AnswerDecisions(host, day);
            Send(host, "day" + day, "command", "advanceDay");
        }

        Assert.True(Rounds(host) >= 1, "the first race did not finish");
        using var result = JsonDocument.Parse(host.Handle(Message("res", "query", "raceResult", """{"managerId":"human:player","season":null,"round":null}""")).Response);
        var rows = result.RootElement.GetProperty("data").GetProperty("rows").EnumerateArray().ToArray();
        var drivers = rows.Select(row => row.GetProperty("driverId").GetString()!).ToHashSet();
        var teams = rows.Select(row => row.GetProperty("teamId").GetString()!).ToHashSet();

        using var standings = JsonDocument.Parse(host.Handle(Message("st", "query", "standings")).Response);
        var data = standings.RootElement.GetProperty("data");
        var tableDrivers = data.GetProperty("drivers").EnumerateArray().Select(row => row.GetProperty("id").GetString()!).ToArray();
        var tableTeams = data.GetProperty("constructors").EnumerateArray().Select(row => row.GetProperty("id").GetString()!).ToArray();

        Assert.True(drivers.Count > 6, "the race had too few cars to tell");
        Assert.Subset(tableDrivers.ToHashSet(), drivers);
        Assert.Subset(tableTeams.ToHashSet(), teams);
        Assert.Equal(Enumerable.Range(1, tableDrivers.Length), data.GetProperty("drivers").EnumerateArray().Select(row => row.GetProperty("position").GetInt32()));
    }

    private static int Rounds(BridgeHost host)
    {
        using var json = JsonDocument.Parse(host.Handle(Message("rounds", "query", "standings")).Response);
        return json.RootElement.GetProperty("data").GetProperty("roundsCompleted").GetInt32();
    }

    private static void AnswerDecisions(BridgeHost host, int day)
    {
        using var json = JsonDocument.Parse(host.Handle(Message("inbox" + day, "query", "inbox")).Response);
        foreach (var item in json.RootElement.GetProperty("data").GetProperty("items").EnumerateArray())
        {
            if (item.GetProperty("status").GetString() != "Open" || !item.GetProperty("needsDecision").GetBoolean())
            {
                continue;
            }

            var option = item.GetProperty("options")[0].GetProperty("id").GetString();
            host.Handle(Message("answer" + day, "command", "resolveInbox", "{\"managerId\":\"human:player\",\"itemId\":\"" + item.GetProperty("id").GetString() + "\",\"optionId\":\"" + option + "\"}"));
        }
    }

    private static void Send(BridgeHost host, string id, string kind, string name, string? args = null)
    {
        var response = host.Handle(Message(id, kind, name, args)).Response;
        using var json = JsonDocument.Parse(response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), response);
    }

    private static string Message(string id, string kind, string name, string? args = null) =>
        "{\"id\":\"" + id + "\",\"kind\":\"" + kind + "\",\"name\":\"" + name + "\",\"args\":"
        + (args ?? "{\"managerId\":\"" + CareerBridge.HumanManagerId + "\"}") + "}";
}
