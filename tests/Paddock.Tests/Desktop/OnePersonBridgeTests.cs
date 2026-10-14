using System.Text.Json;
using Paddock.Desktop.Bridge;

namespace Paddock.Tests.Desktop;

/// <summary>
/// One person everywhere (#326): the academy, the market and the team show the same person with the same age, and a row of the academy
/// opens the same profile as a row of the market. Fixture data only (<see cref="BridgeTestData"/>).
/// </summary>
public class OnePersonBridgeTests
{
    [Fact]
    public void ThePoolTheMarketAndTheTeamAgreeOnTheAgeOfEveryoneTheyShare()
    {
        var host = Started();
        var pool = Read(host, "pool").GetProperty("items").EnumerateArray().ToArray();
        Assert.NotEmpty(pool);

        var market = Read(host, "market");
        // The pool names nobody by id, so only a name that is unique on both lists is the same person for certain.
        var listed = market.GetProperty("freeAgents").EnumerateArray().Concat(market.GetProperty("contracted").EnumerateArray())
            .Where(item => item.GetProperty("kind").GetString() == "driver")
            .GroupBy(item => item.GetProperty("name").GetString()!)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single().GetProperty("age").GetInt32());
        var names = pool.Select(junior => junior.GetProperty("givenName").GetString() + " " + junior.GetProperty("familyName").GetString()).ToArray();
        var shared = 0;
        for (var index = 0; index < pool.Length; index++)
        {
            if (names.Count(name => name == names[index]) != 1 || !listed.TryGetValue(names[index], out var onTheMarket))
            {
                continue;
            }

            shared++;
            Assert.Equal(onTheMarket, pool[index].GetProperty("age").GetInt32());
        }

        Assert.True(shared > 0, "The fixture must put somebody in both the pool and the market.");

        // The team's own list and the market list the staff of the rivals both: the same person has the same age in either.
        var staff = Read(host, "staff").GetProperty("people").EnumerateArray()
            .Where(item => !item.GetProperty("ownTeam").GetBoolean())
            .ToDictionary(item => item.GetProperty("personId").GetString()!, item => item.GetProperty("age").GetInt32());
        var staffOnTheMarket = market.GetProperty("contracted").EnumerateArray().Where(item => staff.ContainsKey(item.GetProperty("personId").GetString()!)).ToArray();
        Assert.NotEmpty(staffOnTheMarket);
        Assert.All(staffOnTheMarket, item => Assert.Equal(staff[item.GetProperty("personId").GetString()!], item.GetProperty("age").GetInt32()));
    }

    [Fact]
    public void ARowOfTheAcademyOpensTheSameProfileAsARowOfTheMarket()
    {
        var host = Started();
        var junior = Read(host, "pool").GetProperty("items").EnumerateArray().First();
        var handle = junior.GetProperty("handle").GetString()!;

        var profile = Read(host, "driver", "{\"managerId\":\"human:player\",\"personId\":\"" + handle + "\"}");

        Assert.True(profile.GetProperty("found").GetBoolean(), "A pool handle must open a profile.");
        Assert.Equal(junior.GetProperty("givenName").GetString() + " " + junior.GetProperty("familyName").GetString(), profile.GetProperty("name").GetString());
        Assert.Equal(junior.GetProperty("age").GetInt32(), profile.GetProperty("age").GetInt32());
        // The handle is the only name the pool gives him, so the profile does not hand out the real id (PP-018).
        Assert.Equal(handle, profile.GetProperty("personId").GetString());
    }

    [Fact]
    public void AHandleOfNobodyOpensNoProfile()
    {
        var host = Started();

        var profile = Read(host, "driver", "{\"managerId\":\"human:player\",\"personId\":\"talent-999999\"}");

        Assert.False(profile.GetProperty("found").GetBoolean());
    }

    private static BridgeHost Started()
    {
        var host = BridgeHost.Lobby(BridgeTestData.DataRoot);
        var started = host.Handle(Message(
            "start",
            "command",
            "newCareer",
            """{"managerId":"human:player","teamId":"ferrari","givenName":"Enzo","familyName":"Test","nationality":"ITA","tilt":"none","preset":"Chaos","year":1955,"seed":1}"""));
        using var json = JsonDocument.Parse(started.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), started.Response);
        return host;
    }

    private static JsonElement Read(BridgeHost host, string name, string? args = null)
    {
        var exchange = host.Handle(Message("q-" + name, "query", name, args));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), exchange.Response);
        return json.RootElement.GetProperty("data").Clone();
    }

    private static string Message(string id, string kind, string name, string? args = null) =>
        "{\"id\":\"" + id + "\",\"kind\":\"" + kind + "\",\"name\":\"" + name + "\",\"args\":"
        + (args ?? "{\"managerId\":\"" + CareerBridge.HumanManagerId + "\"}") + "}";
}
