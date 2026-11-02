using System.Text.Json;
using Paddock.Desktop.Bridge;

namespace Paddock.Tests.Desktop;

/// <summary>
/// Playtest 2 (#318), over the bridge on the first morning of a 2017 Ferrari career (authored data, generated people, never
/// <c>data/cache</c>): every person under contract has a salary and an end date from day one, the team knows its own drivers,
/// the market lists every person once, and a race engineer is signed through the same contract flow as a driver.
/// </summary>
public sealed class PeopleContractsBridgeTests
{
    private const string Manager = "{\"managerId\":\"human:player\"}";

    [Fact]
    public void EveryDriverAndStaffMemberOfTheTeamHasASalaryAndAnEndDateOnTheFirstMorning()
    {
        var host = Started();

        foreach (var driver in Data(host, "query", "drivers").GetProperty("own").EnumerateArray())
        {
            var id = driver.GetProperty("personId").GetString()!;
            var profile = Data(host, "query", "driver", "{\"managerId\":\"human:player\",\"personId\":\"" + id + "\"}");
            var contract = profile.GetProperty("contract");
            Assert.True(contract.GetProperty("salary").GetInt64() > 0, id + " has no salary on the first morning");
            Assert.False(string.IsNullOrEmpty(contract.GetProperty("end").GetString()));
            Assert.Equal(JsonValueKind.Number, contract.GetProperty("winBonus").ValueKind);
            Assert.Equal(JsonValueKind.Number, contract.GetProperty("titleBonus").ValueKind);
        }

        var own = Data(host, "query", "staff").GetProperty("people").EnumerateArray().Where(person => person.GetProperty("ownTeam").GetBoolean()).ToArray();
        Assert.NotEmpty(own);
        Assert.All(own, person =>
        {
            Assert.True(person.GetProperty("salary").GetInt64() > 0, person.GetProperty("personId").GetString() + " has no salary on the first morning");
            Assert.False(string.IsNullOrEmpty(person.GetProperty("contractEnd").GetString()));
        });
    }

    [Fact]
    public void EveryContractOnTheGridIsPaidOnTheFirstMorning()
    {
        var host = Started();

        var market = Data(host, "query", "market");
        var contracted = market.GetProperty("contracted").EnumerateArray().ToArray();
        Assert.NotEmpty(contracted);
        Assert.All(contracted, person => Assert.True(person.GetProperty("salary").GetInt64() > 0, person.GetProperty("personId").GetString() + " is paid nothing"));
    }

    [Fact]
    public void TheTeamKnowsTheAttributesOfItsOwnDriversFromTheFirstMorning()
    {
        var host = Started();

        foreach (var driver in Data(host, "query", "drivers").GetProperty("own").EnumerateArray())
        {
            var profile = Data(host, "query", "driver", "{\"managerId\":\"human:player\",\"personId\":\"" + driver.GetProperty("personId").GetString() + "\"}");
            Assert.NotEmpty(profile.GetProperty("attributes").EnumerateArray());
            Assert.Equal(JsonValueKind.Number, profile.GetProperty("overall").ValueKind);
        }
    }

    [Fact]
    public void TheMarketListsEveryPersonOnceAndStaffUnderContractIsNotFree()
    {
        var host = Started();

        var market = Data(host, "query", "market");
        var free = market.GetProperty("freeAgents").EnumerateArray().Select(person => person.GetProperty("personId").GetString()!).ToArray();
        var contracted = market.GetProperty("contracted").EnumerateArray().Select(person => person.GetProperty("personId").GetString()!).ToArray();
        var ownStaff = Data(host, "query", "staff").GetProperty("people").EnumerateArray()
            .Where(person => person.GetProperty("ownTeam").GetBoolean())
            .Select(person => person.GetProperty("personId").GetString()!)
            .ToArray();

        Assert.Equal(free.Length + contracted.Length, free.Concat(contracted).Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(free.Intersect(ownStaff, StringComparer.Ordinal));
    }

    [Fact]
    public void ARaceEngineerOfAnotherTeamIsSignedThroughTheSameContractFlowAndStartsWhenHisContractEnds()
    {
        var host = Started();
        var engineer = Data(host, "query", "market").GetProperty("contracted").EnumerateArray()
            .First(person => person.GetProperty("kind").GetString() == "RaceEngineer");
        var id = engineer.GetProperty("personId").GetString()!;
        var currentEnd = engineer.GetProperty("contractEnd").GetString()!;

        Command(host, "openNegotiation", "{\"managerId\":\"human:player\",\"organizationId\":\"ferrari\",\"personId\":\"" + id + "\",\"subject\":\"staff:RaceEngineer\",\"deadline\":null}");
        var talks = Data(host, "query", "negotiations").GetProperty("items").EnumerateArray().Single(item => item.GetProperty("person").GetString() == id);
        var negotiation = talks.GetProperty("id").GetString()!;
        var salary = talks.GetProperty("salaryGuide").GetProperty("suggestedHigh").GetInt64();
        Command(host, "submitOffer", "{\"managerId\":\"human:player\",\"negotiationId\":\"" + negotiation + "\",\"salary\":" + salary
            + ",\"winBonus\":0,\"titleBonus\":0,\"years\":2,\"seat\":null,\"optionHolder\":null,\"optionYears\":null,\"exitWorseThan\":null}");

        for (var day = 0; day < 40 && Status(host, negotiation) is not ("Agreed" or "Refused" or "Lapsed" or "WalkedAway" or "Lost"); day++)
        {
            AnswerDecisions(host, day);
            Command(host, "advanceDay", Manager);
        }

        Assert.Equal("Agreed", Status(host, negotiation));
        var signed = Data(host, "query", "staff").GetProperty("people").EnumerateArray()
            .Where(person => person.GetProperty("personId").GetString() == id)
            .ToArray();
        // He finishes the season with his old team and joins us the day after: one employer at a time.
        Assert.Contains(signed, person => !person.GetProperty("ownTeam").GetBoolean());
        var ours = Assert.Single(signed, person => person.GetProperty("ownTeam").GetBoolean() || person.GetProperty("organizationId").GetString() == "ferrari");
        Assert.True(ours.GetProperty("salary").GetInt64() > 0);
        Assert.True(string.CompareOrdinal(ours.GetProperty("contractStart").GetString(), currentEnd) > 0, ours.ToString());
    }

    private static string Status(BridgeHost host, string negotiation) =>
        Data(host, "query", "negotiations").GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == negotiation).GetProperty("status").GetString()!;

    private static BridgeHost Started()
    {
        var host = BridgeHost.Lobby(BridgeTestData.DataRoot);
        Command(host, "newCareer", """{"managerId":"human:player","teamId":"ferrari","givenName":"Enzo","familyName":"Test","nationality":"ITA","tilt":"none","preset":"Balanced","year":2017,"seed":1}""");
        return host;
    }

    private static void Command(BridgeHost host, string name, string args)
    {
        var exchange = host.Handle(Message(name, "command", name, args));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), exchange.Response);
    }

    private static void AnswerDecisions(BridgeHost host, int day)
    {
        for (var pass = 0; pass < 8; pass++)
        {
            var open = Data(host, "query", "inbox").GetProperty("items").EnumerateArray().FirstOrDefault(item =>
                item.GetProperty("needsDecision").GetBoolean()
                && item.GetProperty("status").GetString() == "Open"
                && item.GetProperty("options").GetArrayLength() > 0);
            if (open.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            var option = open.GetProperty("options").EnumerateArray().Select(item => item.GetProperty("id").GetString()).ToArray();
            var choice = option.Contains("sign") ? "sign" : option[0];
            Command(host, "resolveInbox", "{\"managerId\":\"human:player\",\"itemId\":\"" + open.GetProperty("id").GetString() + "\",\"optionId\":\"" + choice + "\"}");
        }
    }

    private static JsonElement Data(BridgeHost host, string kind, string name, string? args = null)
    {
        var exchange = host.Handle(Message(name + "-" + kind, kind, name, args ?? Manager));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), exchange.Response);
        return json.RootElement.GetProperty("data").Clone();
    }

    private static string Message(string id, string kind, string name, string args) =>
        "{\"id\":\"" + id + "\",\"kind\":\"" + kind + "\",\"name\":\"" + name + "\",\"args\":" + args + "}";
}
