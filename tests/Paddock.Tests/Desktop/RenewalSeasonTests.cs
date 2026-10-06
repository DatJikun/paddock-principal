using System.Text.Json;
using Paddock.Desktop.Bridge;

namespace Paddock.Tests.Desktop;

/// <summary>
/// #253: a player who answers nothing about renewals must not lock the career or lose the team's race. Every contract that was
/// not renewed ends, and the first race of the next season still starts with two drivers for the player's team.
/// </summary>
public class RenewalSeasonTests
{
    private const string Team = "maserati";

    [Fact]
    public void ALetAllContractsEndSeasonStillStartsTheNextSeasonsFirstRaceWithTwoDrivers()
    {
        var host = BridgeHost.Lobby(BridgeTestData.DataRoot);
        var started = host.Handle(Message(
            "start",
            "command",
            "newCareer",
            """{"managerId":"human:player","teamId":"maserati","givenName":"Jan","familyName":"Test","nationality":"POL","tilt":"none","preset":"Balanced","year":1955,"seed":1}"""));
        using (var json = JsonDocument.Parse(started.Response))
        {
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), started.Response);
        }

        string[]? rows = null;
        string? date = null;
        for (var day = 0; day < 520 && rows is null; day++)
        {
            var step = host.Handle(Message("adv" + day, "command", "advanceDay"));
            using var stepJson = JsonDocument.Parse(step.Response);
            if (!stepJson.RootElement.GetProperty("ok").GetBoolean())
            {
                // Held: only a decision the player can see and that is not a renewal may hold the clock.
                Assert.True(AnswerBlockingDecision(host, day), "The clock was held on " + date + " with nothing to answer: " + step.Response);
                continue;
            }

            date = stepJson.RootElement.GetProperty("data").GetProperty("date").GetString();
            if (string.CompareOrdinal(date, "1956-01-02") < 0)
            {
                continue;
            }

            var result = host.Handle(Message("res" + day, "query", "raceResult", "{\"managerId\":\"human:player\",\"season\":1956,\"round\":1}"));
            using var resultJson = JsonDocument.Parse(result.Response);
            var data = resultJson.RootElement.GetProperty("data");
            if (resultJson.RootElement.GetProperty("ok").GetBoolean() && data.GetProperty("found").GetBoolean())
            {
                rows = data.GetProperty("rows").EnumerateArray()
                    .Select(row => row.GetProperty("teamId").GetString() + "|" + row.GetProperty("driverId").GetString())
                    .ToArray();
            }
        }

        Assert.NotNull(rows);
        var own = rows!.Where(row => row.StartsWith(Team + "|", StringComparison.Ordinal)).Select(row => row.Split('|')[1]).Distinct().ToArray();
        using (var inbox = JsonDocument.Parse(host.Handle(Message("final-inbox", "query", "inbox")).Response))
        {
            var items = inbox.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToArray();
            Assert.Contains(items, item => item.GetProperty("kind").GetString() == "contract.notice");
            Assert.DoesNotContain(items, item => item.GetProperty("kind").GetString() is "contract.renewal" or "contract.renewalGroup"
                && item.GetProperty("status").GetString() == "Open");
        }

        Assert.True(own.Length == 2, "The player's team started " + own.Length + " drivers in the first race of 1956 (" + string.Join(", ", rows!.Take(40)) + ").");
    }

    /// <summary>Answers the first open decision that holds the clock with its first option (a stand-in pick, a concept, a goal). False when there is none.</summary>
    private static bool AnswerBlockingDecision(BridgeHost host, int day)
    {
        var inbox = host.Handle(Message("inbox" + day, "query", "inbox"));
        using var json = JsonDocument.Parse(inbox.Response);
        foreach (var item in json.RootElement.GetProperty("data").GetProperty("items").EnumerateArray())
        {
            if (!item.GetProperty("needsDecision").GetBoolean() || item.GetProperty("status").GetString() != "Open")
            {
                continue;
            }

            var kind = item.GetProperty("kind").GetString();
            if (kind is "contract.renewal" or "contract.renewalGroup")
            {
                continue;
            }

            var options = item.GetProperty("options");
            if (options.GetArrayLength() == 0)
            {
                continue;
            }

            var answered = host.Handle(Message(
                "ans" + day,
                "command",
                "resolveInbox",
                "{\"managerId\":\"human:player\",\"itemId\":\"" + item.GetProperty("id").GetString() + "\",\"optionId\":\"" + options[0].GetProperty("id").GetString() + "\"}"));
            using var answer = JsonDocument.Parse(answered.Response);
            if (answer.RootElement.GetProperty("ok").GetBoolean())
            {
                return true;
            }
        }

        return false;
    }

    private static string Message(string id, string kind, string name, string? args = null) =>
        "{\"id\":\"" + id + "\",\"kind\":\"" + kind + "\",\"name\":\"" + name + "\",\"args\":"
        + (args ?? "{\"managerId\":\"" + CareerBridge.HumanManagerId + "\"}") + "}";
}
