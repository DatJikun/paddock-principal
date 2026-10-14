using System.Text.Json;
using Paddock.Data.Historical;
using Paddock.Desktop.Bridge;
using Paddock.Simulation.Racing.Playback;

namespace Paddock.Tests.Desktop;

/// <summary>
/// The first driver of a team comes first wherever the bridge lists a team's drivers (#325): the team card, the drivers screen and the
/// cars of a watched race, and the contract end date follows the option chosen at career start. The people are an invented schedule in
/// a temp folder (never <c>data/cache</c>): two Ferrari drivers whose names sort the other way round from their roles.
/// </summary>
public sealed class FirstDriverBridgeTests : IDisposable
{
    private const string Manager = "{\"managerId\":\"human:player\"}";
    private readonly string _root = Directory.CreateTempSubdirectory("paddock-first-driver-").FullName;

    public FirstDriverBridgeTests()
    {
        Copy(Path.Combine(BridgeTestData.DataRoot, "authored"), Path.Combine(_root, "authored"));
        File.WriteAllText(
            Path.Combine(_root, "authored", "people", "driver_roles.json"),
            """
            { "notes": "fixture", "roles": [
              { "year": 1955, "driver_id": "zora_first", "role": "first" },
              { "year": 1955, "driver_id": "adam_second", "role": "second" } ] }
            """);
        var normalized = Path.Combine(_root, "cache", "jolpica", "normalized");
        var reports = Path.Combine(_root, "cache", "reports");
        Directory.CreateDirectory(normalized);
        Directory.CreateDirectory(reports);
        var drivers = new HistoricalDriversDocument(
            1,
            [
                new HistoricalDriver("adam_second", "Adam", "Alpha", "1925-03-04", "Italian", null, null, null),
                new HistoricalDriver("zora_first", "Zora", "Zeta", "1926-05-06", "Italian", null, null, null),
            ]);
        File.WriteAllText(Path.Combine(normalized, "drivers.json"), JsonSerializer.Serialize(drivers, HistoricalJson.Options));
        var schedule = new PeopleScheduleReport(
            2.5m,
            1980,
            [
                Driver("adam_second", 1925, new DriverStint(1955, "ferrari", 1, 7, 7, ScheduleRoles.Race)),
                Driver(
                    "zora_first",
                    1926,
                    new DriverStint(1955, "ferrari", 1, 7, 7, ScheduleRoles.Race),
                    new DriverStint(1956, "ferrari", 1, 7, 7, ScheduleRoles.Race),
                    new DriverStint(1957, "ferrari", 1, 7, 7, ScheduleRoles.Race),
                    new DriverStint(1958, "ferrari", 1, 7, 7, ScheduleRoles.Race)),
            ],
            [],
            [],
            []);
        File.WriteAllText(Path.Combine(reports, "people_schedule.json"), JsonSerializer.Serialize(schedule, HistoricalJson.Options));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static ScheduledDriver Driver(string id, int born, params DriverStint[] stints) =>
        new(id, born, "Italian", 1955, stints[^1].Season, 1953, stints, []);

    private BridgeHost Start(string contracts, IClock? clock = null)
    {
        var career = CareerBridge.Lobby(_root);
        if (clock is not null)
        {
            career.LiveClock = clock;
        }

        var host = new BridgeHost(career);
        var started = host.Handle(Message(
            "start",
            "command",
            "newCareer",
            "{\"managerId\":\"human:player\",\"teamId\":\"ferrari\",\"givenName\":\"Enzo\",\"familyName\":\"Test\",\"nationality\":\"IT\",\"tilt\":\"none\","
            + "\"preset\":\"Balanced\",\"year\":1955,\"seed\":1,\"startContracts\":\"" + contracts + "\"}"));
        using var json = JsonDocument.Parse(started.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), started.Response);
        return host;
    }

    [Fact]
    public void TheTeamCardListsTheFirstDriverBeforeTheSecond()
    {
        var host = BridgeHost.Lobby(_root);
        var teams = Data(host, "query", "teams", "{\"managerId\":\"human:player\",\"year\":1955,\"preset\":\"Balanced\",\"seed\":1}");
        var ferrari = teams.GetProperty("teams").EnumerateArray().Single(team => team.GetProperty("id").GetString() == "ferrari");

        var line = ferrari.GetProperty("drivers").EnumerateArray()
            .Where(driver => driver.GetProperty("seat").GetString() != "Reserve")
            .Select(driver => (Name: driver.GetProperty("name").GetString(), Seat: driver.GetProperty("seat").GetString()))
            .ToArray();
        Assert.Equal([("Zora Zeta", "NumberOne"), ("Adam Alpha", "NumberTwo")], line);
    }

    [Fact]
    public void TheDriversScreenListsTheFirstDriverFirstAndTheContractsFollowTheirRealStints()
    {
        var host = Start("Real");

        var own = Data(host, "query", "drivers", Manager).GetProperty("own").EnumerateArray()
            .Where(driver => driver.GetProperty("seat").GetString() != "Reserve")
            .Select(driver => (Name: driver.GetProperty("name").GetString(), Seat: driver.GetProperty("seat").GetString(), End: driver.GetProperty("end").GetString()))
            .ToArray();

        Assert.Equal(
            [("Zora Zeta", "NumberOne", "1958-12-31"), ("Adam Alpha", "NumberTwo", "1955-12-31")],
            own);
    }

    [Fact]
    public void WithTheOldOptionEveryContractEndsInTheStartYearButTheOrderIsTheSame()
    {
        var host = Start("AllEndThisYear");

        var own = Data(host, "query", "drivers", Manager).GetProperty("own").EnumerateArray()
            .Where(driver => driver.GetProperty("seat").GetString() != "Reserve")
            .Select(driver => (Name: driver.GetProperty("name").GetString(), End: driver.GetProperty("end").GetString()))
            .ToArray();

        Assert.Equal([("Zora Zeta", "1955-12-31"), ("Adam Alpha", "1955-12-31")], own);
    }

    [Fact]
    public void AnUnknownStartContractsOptionIsRefusedAndNothingStarts()
    {
        var career = CareerBridge.Lobby(_root);
        var host = new BridgeHost(career);
        var refused = host.Handle(Message(
            "start",
            "command",
            "newCareer",
            "{\"managerId\":\"human:player\",\"teamId\":\"ferrari\",\"givenName\":\"Enzo\",\"familyName\":\"Test\",\"nationality\":\"IT\",\"tilt\":\"none\","
            + "\"preset\":\"Balanced\",\"year\":1955,\"seed\":1,\"startContracts\":\"Forever\"}"));

        using var json = JsonDocument.Parse(refused.Response);
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean(), refused.Response);
    }

    [Fact]
    public void TheCarsOfAWatchedRaceCarryTheirSeatOrderWithinTheTeam()
    {
        var clock = new ManualClock();
        var host = Start("Real", clock);
        AdvanceUntilFirstRace(host);

        var cars = Data(host, "query", "liveRace", Manager).GetProperty("cars").EnumerateArray()
            .Where(car => car.GetProperty("own").GetBoolean())
            .OrderBy(car => car.GetProperty("seatOrder").GetInt32())
            .Select(car => (Name: car.GetProperty("driverName").GetString(), Order: car.GetProperty("seatOrder").GetInt32()))
            .ToArray();

        Assert.Equal([("Zora Zeta", 0), ("Adam Alpha", 1)], cars);
    }

    private static JsonElement Data(BridgeHost host, string kind, string name, string? args = null)
    {
        var exchange = host.Handle(Message(name, kind, name, args));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), exchange.Response);
        return json.RootElement.GetProperty("data").Clone();
    }

    private static string Message(string id, string kind, string name, string? args = null) =>
        "{\"id\":\"" + id + "\",\"kind\":\"" + kind + "\",\"name\":\"" + name + "\",\"args\":" + (args ?? Manager) + "}";

    private static void AdvanceUntilFirstRace(BridgeHost host)
    {
        for (var day = 0; day < 90; day++)
        {
            AnswerDecisions(host, day);
            var moved = host.Handle(Message("day" + day, "command", "advanceDay"));
            using var movedJson = JsonDocument.Parse(moved.Response);
            Assert.True(movedJson.RootElement.GetProperty("ok").GetBoolean(), moved.Response);
            if (moved.Events.Any(item => item.Contains("raceFinished", StringComparison.Ordinal)))
            {
                return;
            }
        }

        Assert.Fail("the first 1955 race did not run");
    }

    private static void AnswerDecisions(BridgeHost host, int day)
    {
        for (var pass = 0; pass < 8; pass++)
        {
            var inbox = Data(host, "query", "inbox");
            var open = inbox.GetProperty("items").EnumerateArray().FirstOrDefault(item =>
                item.GetProperty("needsDecision").GetBoolean()
                && item.GetProperty("status").GetString() == "Open"
                && item.GetProperty("options").GetArrayLength() > 0);
            if (open.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            var resolved = host.Handle(Message(
                "yes" + day + "-" + pass,
                "command",
                "resolveInbox",
                "{\"managerId\":\"human:player\",\"itemId\":\"" + open.GetProperty("id").GetString()
                + "\",\"optionId\":\"" + open.GetProperty("options")[0].GetProperty("id").GetString() + "\"}"));
            using var json = JsonDocument.Parse(resolved.Response);
            Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), resolved.Response);
        }
    }

    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.GetDirectories(from))
        {
            Copy(directory, Path.Combine(to, Path.GetFileName(directory)));
        }
    }

    private sealed class ManualClock : IClock
    {
        public long NowMs => 0;
    }
}
