using System.Text.Json;
using Paddock.Application.Commands;
using Paddock.Application.Racing;
using Paddock.Desktop.Bridge;
using static Paddock.Tests.Desktop.LiveRaceBridgeTests;

namespace Paddock.Tests.Desktop;

/// <summary>
/// The race weekend's steps (#316): the practice report and qualifying are read before the race, a plan set before the start is
/// read by the race, the watched race is the race the day books (every module on), and plans and pit wall orders are world
/// commands, so a save keeps them and a replay of the command log books the same race (INV-002).
/// </summary>
public class WeekendStepsTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TheBookedRaceIsTheWatchedRaceWithEveryModuleOn(int seed)
    {
        var host = RaceMorning2010(seed, out var clock, out _);
        Data(host, "command", "watchRace");
        var watched = Events(host);
        Data(host, "command", "liveRaceControl", "{\"managerId\":\"human:player\",\"action\":\"setSpeed\",\"speed\":30}");
        Data(host, "command", "liveRaceControl", "{\"managerId\":\"human:player\",\"action\":\"play\"}");
        clock.Now += 100_000_000;

        Assert.True(Booked(host));

        Assert.Equal(watched, Events(host));
        Assert.DoesNotContain(InboxKinds(host), kind => kind == RaceWeekendKeys.RerunKind);
    }

    [Fact]
    public void TheWeekendShowsPracticeQualifyingAndThePlansOfOwnCarsOnly()
    {
        var host = RaceMorning2010(1, out _, out _);

        var weekend = Data(host, "query", "raceWeekend");

        Assert.True(weekend.GetProperty("found").GetBoolean());
        Assert.True(weekend.GetProperty("canPlan").GetBoolean());
        Assert.False(weekend.GetProperty("refuelling").GetBoolean());
        Assert.Equal(2, weekend.GetProperty("requiredDryCompounds").GetInt32());
        var practice = weekend.GetProperty("practice").EnumerateArray().ToArray();
        Assert.NotEmpty(practice);
        Assert.All(practice, line => Assert.Equal(line.GetProperty("own").GetBoolean(), line.GetProperty("tyres").GetArrayLength() > 0));
        Assert.Contains(practice, line => line.GetProperty("own").GetBoolean());
        Assert.NotEmpty(weekend.GetProperty("grid").EnumerateArray());
        var plans = weekend.GetProperty("plans").EnumerateArray().ToArray();
        Assert.NotEmpty(plans);
        Assert.All(plans, plan => Assert.False(plan.GetProperty("set").GetBoolean()));
        Assert.All(plans, plan => Assert.Equal(JsonValueKind.Null, plan.GetProperty("stops").ValueKind));

        // Reading the weekend changes nothing.
        var hash = host.StateHash;
        Data(host, "query", "raceWeekend");
        Assert.Equal(hash, host.StateHash);
    }

    [Fact]
    public void APlanSetBeforeTheStartIsReadByTheRaceAndLockedOnceItIsWatched()
    {
        var planned = RaceMorning2010(1, out _, out _);
        var weekend = Data(planned, "query", "raceWeekend");
        var car = weekend.GetProperty("plans")[0].GetProperty("carId").GetString()!;
        var dry = weekend.GetProperty("dryTyres").EnumerateArray().Select(item => item.GetString()!).ToArray();
        var season = weekend.GetProperty("season").GetInt32();
        var round = weekend.GetProperty("round").GetInt32();
        string Plan(string stops) =>
            "{\"managerId\":\"human:player\",\"season\":" + season + ",\"round\":" + round + ",\"carId\":\"" + car + "\",\"startTyres\":\"" + dry[0]
            + "\",\"pace\":\"push\",\"engine\":\"full\",\"stops\":" + stops + "}";

        Assert.Equal(RaceWeekendKeys.NeedsMix, Error(planned, "setRacePlan", Plan("[]")));
        Assert.Equal(RaceWeekendKeys.BadStops, Error(planned, "setRacePlan", Plan("[{\"lap\":999,\"tyres\":\"" + dry[^1] + "\"}]")));
        Data(planned, "command", "setRacePlan", Plan("[{\"lap\":12,\"tyres\":\"" + dry[^1] + "\"}]"));
        var stored = Data(planned, "query", "raceWeekend").GetProperty("plans").EnumerateArray().Single(plan => plan.GetProperty("carId").GetString() == car);
        Assert.True(stored.GetProperty("set").GetBoolean());
        Assert.Equal("push", stored.GetProperty("pace").GetString());
        Assert.Equal(12, stored.GetProperty("stops")[0].GetProperty("lap").GetInt32());

        Data(planned, "command", "watchRace");
        Assert.Equal(RaceWeekendKeys.Started, Error(planned, "setRacePlan", Plan("[{\"lap\":20,\"tyres\":\"" + dry[^1] + "\"}]")));
        var plannedRace = Events(planned);

        var plain = RaceMorning2010(1, out _, out _);
        Data(plain, "command", "watchRace");
        Assert.NotEqual(Events(plain), plannedRace);

        Assert.True(Booked(planned));
        Assert.True(Booked(plain));
        Assert.Equal(plannedRace, Events(planned));
        Assert.NotEqual(plain.StateHash, planned.StateHash);
    }

    [Fact]
    public void ASaveMidRaceKeepsThePitWallOrdersAndTheLoadedCareerBooksTheSameRace()
    {
        var live = RaceMorning2010(2, out var clock, out _);
        Data(live, "command", "watchRace");
        var own = OwnRunner(Data(live, "query", "liveRace"));
        Data(live, "command", "liveRaceControl", "{\"managerId\":\"human:player\",\"action\":\"setSpeed\",\"speed\":10}");
        Data(live, "command", "liveRaceControl", "{\"managerId\":\"human:player\",\"action\":\"play\"}");
        clock.Now += 20_000;
        Data(live, "command", "liveRaceOrder", "{\"managerId\":\"human:player\",\"carId\":\"" + own + "\",\"action\":\"pace\",\"pace\":\"push\"}");
        var ordered = Events(live);

        var name = "weekend-" + Guid.NewGuid().ToString("N");
        var path = Path.Combine(BridgeTestData.SavesDirectory, name + ".paddock");
        try
        {
            Data(live, "command", "saveCareer", "{\"managerId\":\"human:player\",\"name\":\"" + name + "\"}");
            var resumed = new BridgeHost(Lobby(new ManualClock()));
            Data(resumed, "command", "loadCareer", "{\"managerId\":\"human:player\",\"path\":\"" + name + "\"}");
            Assert.Equal(live.StateHash, resumed.StateHash);

            // The loaded race opens on the orders already given.
            Data(resumed, "command", "watchRace");
            Assert.Equal(ordered, Events(resumed));

            Assert.True(Booked(live));
            Assert.True(Booked(resumed));
            Assert.Equal(live.StateHash, resumed.StateHash);
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
    public void ReplayingTheCommandLogOfAWatchedAndOrderedRaceBooksTheSameRace()
    {
        var watched = RaceMorning2010(3, out var clock, out var career);
        var from = career.CommandLog.Count;
        Data(watched, "command", "watchRace");
        var own = OwnRunner(Data(watched, "query", "liveRace"));
        Data(watched, "command", "liveRaceControl", "{\"managerId\":\"human:player\",\"action\":\"setSpeed\",\"speed\":10}");
        Data(watched, "command", "liveRaceControl", "{\"managerId\":\"human:player\",\"action\":\"play\"}");
        clock.Now += 30_000;
        Data(watched, "command", "liveRaceOrder", "{\"managerId\":\"human:player\",\"carId\":\"" + own + "\",\"action\":\"engine\",\"engine\":\"full\"}");
        var commands = career.CommandLog.Skip(from).ToArray();
        Assert.Contains(commands, command => command is SetPitWallOrdersCommand);

        // The same career, never watched: the commands come back through the save codec and run as they are.
        var replayed = RaceMorning2010(3, out _, out var replayCareer);
        foreach (var command in commands)
        {
            var saved = CommandCodec.Production.Encode(command);
            var decoded = CommandCodec.Production.Decode(saved.Tag, saved.Text, command.ManagerId, command.SubmissionNumber, command.IssuedOn).WithSubmissionNumber(0);
            Assert.IsNotType<CommandResult.Rejected>(replayCareer.Submit(decoded));
        }

        Assert.Equal(watched.StateHash, replayed.StateHash);
        Assert.True(Booked(watched));
        Assert.True(Booked(replayed));
        Assert.Equal(watched.StateHash, replayed.StateHash);

        var simulated = RaceMorning2010(3, out _, out _);
        Assert.True(Booked(simulated));
        Assert.NotEqual(simulated.StateHash, watched.StateHash);
    }

    [Fact]
    public void ABookedRaceThatDiffersFromTheWatchedOneIsToldInTheInbox()
    {
        var host = RaceMorning2010(1, out _, out var career);
        Data(host, "command", "watchRace");
        var weekend = Data(host, "query", "raceWeekend");
        // As if the race had changed after it was watched: the world records another tape than the one the day books.
        var changed = career.Submit(new WatchRaceCommand
        {
            ManagerId = new Paddock.Application.Managers.ManagerId("human:player"),
            IssuedOn = DateOnly.Parse(Data(host, "query", "shell").GetProperty("date").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
            Season = weekend.GetProperty("season").GetInt32(),
            Round = weekend.GetProperty("round").GetInt32(),
            TapeHash = "another-tape",
        });
        Assert.IsNotType<CommandResult.Rejected>(changed);

        Assert.True(Booked(host));

        Assert.Contains(InboxKinds(host), kind => kind == RaceWeekendKeys.RerunKind);
    }

    [Fact]
    public void TheSummaryOfABookedRoundShowsTheResultTheTablesTheMoneyAndTheBoard()
    {
        var host = RaceMorning2010(1, out _, out _);
        var weekend = Data(host, "query", "raceWeekend");
        Assert.True(Booked(host));

        var summary = Data(host, "query", "raceSummary", "{\"managerId\":\"human:player\",\"season\":" + weekend.GetProperty("season").GetInt32() + ",\"round\":" + weekend.GetProperty("round").GetInt32() + "}");

        Assert.True(summary.GetProperty("found").GetBoolean());
        Assert.NotEmpty(summary.GetProperty("result").GetProperty("rows").EnumerateArray());
        var team = summary.GetProperty("team");
        Assert.Equal(JsonValueKind.Null, team.GetProperty("before").ValueKind);
        Assert.True(team.GetProperty("after").GetInt32() >= 1);
        Assert.NotEmpty(summary.GetProperty("drivers").EnumerateArray());
        Assert.NotEmpty(summary.GetProperty("money").EnumerateArray());
        Assert.Equal(JsonValueKind.Number, summary.GetProperty("balanceCents").ValueKind);
        Assert.Equal(JsonValueKind.Object, summary.GetProperty("board").ValueKind);
        Assert.False(Data(host, "query", "raceWeekend").GetProperty("found").GetBoolean());
    }

    [Fact]
    public void TheRaceOpensAtTheSlowestWatchingSpeed()
    {
        var host = RaceMorning2010(1, out _, out _);
        var clock = Data(host, "command", "watchRace");

        Assert.Equal(LiveRacePlayback.DefaultSpeed, clock.GetProperty("speed").GetDouble());
        Assert.Equal(1, clock.GetProperty("speed").GetDouble());
    }

    private static JsonElement Data(BridgeHost host, string kind, string name, string? args = null) =>
        LiveRaceBridgeTests.Data(host, kind, name, args);

    private static CareerBridge Lobby(ManualClock clock)
    {
        var career = CareerBridge.Lobby(BridgeTestData.DataRoot);
        career.LiveClock = clock;
        return career;
    }

    private static BridgeHost RaceMorning2010(int seed, out ManualClock clock, out CareerBridge career)
    {
        clock = new ManualClock();
        career = Lobby(clock);
        var host = new BridgeHost(career);
        Data(host, "command", "newCareer", """{"managerId":"human:player","teamId":"ferrari","givenName":"Enzo","familyName":"Test","nationality":"IT","tilt":"none","preset":"Balanced","year":2010,"seed":""" + seed + "}");
        AdvanceToRaceMorning(host);
        return host;
    }

    private static bool Booked(BridgeHost host)
    {
        var moved = host.Handle(Message("book", "command", "advanceDay"));
        using var json = JsonDocument.Parse(moved.Response);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean(), moved.Response);
        return moved.Events.Any(item => item.Contains("raceFinished", StringComparison.Ordinal));
    }

    private static IEnumerable<string> InboxKinds(BridgeHost host) =>
        Data(host, "query", "inbox").GetProperty("items").EnumerateArray().Select(item => item.GetProperty("kind").GetString()!).ToArray();

    private static string Events(BridgeHost host) =>
        string.Join('\n', Data(host, "query", "liveRace").GetProperty("events").EnumerateArray()
            .Where(e => e.GetProperty("kind").GetString() is not ("order" or "driver" or "call"))
            .Select(e => e.GetRawText()));

    private static string OwnRunner(JsonElement race)
    {
        var finishers = race.GetProperty("events").EnumerateArray()
            .Where(e => e.GetProperty("kind").GetString() == "finish")
            .Select(e => e.GetProperty("carId").GetString()!)
            .ToHashSet();
        return race.GetProperty("cars").EnumerateArray()
            .Where(c => c.GetProperty("own").GetBoolean())
            .Select(c => c.GetProperty("carId").GetString()!)
            .First(finishers.Contains);
    }

    private static string? Error(BridgeHost host, string name, string args)
    {
        var exchange = host.Handle(Message(name, "command", name, args));
        using var json = JsonDocument.Parse(exchange.Response);
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean(), exchange.Response);
        return json.RootElement.GetProperty("error").GetProperty("key").GetString();
    }
}
