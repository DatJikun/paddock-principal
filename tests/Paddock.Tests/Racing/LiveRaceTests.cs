using Paddock.Application.Racing;
using Paddock.Domain.People;
using Paddock.Domain.Spy;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Racing;
using Paddock.Simulation.Racing.Playback;

namespace Paddock.Tests.Racing;

public class LiveRaceTests
{
    [Fact]
    public void ABetterStrategistAndChiefMechanicGiveTheirTeamBetterRaceNumbersAndAnEmptyTeamStaysNeutral()
    {
        var on = GameDate.SeasonStart(1955);
        var strong = Team(on, "strong", 18);
        var weak = Team(on, "weak", 4);

        var good = RaceStaff.Of(strong.World, strong.Team, on);
        var poor = RaceStaff.Of(weak.World, weak.Team, on);
        Assert.True(good.StrategistSkill > poor.StrategistSkill);
        Assert.True(good.ForecastQuality > poor.ForecastQuality);
        Assert.True(good.PitCrewQuality > poor.PitCrewQuality);
        Assert.InRange(good.StrategistSkill, 0, 100);
        Assert.InRange(poor.ForecastQuality, RaceStaffEstimates.ForecastFloor, RaceStaffEstimates.ForecastCeiling);
        Assert.Equal("strong_strategist", good.StrategistId);

        var empty = WorldState.At(on);
        (empty, var bare) = empty.AddOrganization(new OrganizationSpec(
            OrganizationKind.Team, true, "bare", on, null, 0, [new OrganizationNameSpan("Bare", on, null)]));
        Assert.Equal(RaceStaff.Neutral, RaceStaff.Of(empty, bare, on));
    }

    [Fact]
    public void TheStrategistsTracesBecomeStopsAndPaceChangesAndNothingElse()
    {
        var calls = new StrategyCalls();
        calls.Record(Trace("strategist:car:a", "lap_check:car:a:lap:1", "stay_out/standard"));
        calls.Record(Trace("strategist:car:a", "lap_check:car:a:lap:5", "stay_out/push"));
        calls.Record(Trace("strategist:car:a", "lap_check:car:a:lap:9", "pit_now/treaded.hard/save"));
        calls.Record(Trace("strategist:car:a", "lap_check:car:a:lap:13", "stay_out/save"));
        calls.Record(Trace("strategist:car:b", "lap_check:car:b:lap:9", "pit_now/keep/standard/swap"));
        calls.Record(Trace("principal:x", "lap_check:car:b:lap:9", "pit_now/keep/standard"));

        Assert.Equal(
            [
                new StrategyCall("car:a", 5, StrategyCalls.Push, null),
                new StrategyCall("car:a", 9, StrategyCalls.Pit, "treaded.hard"),
                new StrategyCall("car:a", 9, StrategyCalls.Save, null),
                new StrategyCall("car:b", 9, StrategyCalls.Pit, null),
            ],
            calls.Calls);
    }

    [Fact]
    public void TheRaceClockRunsAtTheChosenSpeedPausesSkipsAndRefusesASpeedItDoesNotOffer()
    {
        var clock = new ManualClock();
        var playback = new LiveRacePlayback(1955, 1, 600_000, clock);
        Assert.True(playback.View().Paused);
        clock.Now += 5_000;
        Assert.Equal(0, playback.RaceTimeMs);

        Assert.Null(playback.Apply("human:a", LiveRaceAction.Play));
        clock.Now += 1_000;
        Assert.Equal((long)(1_000 * LiveRacePlayback.DefaultSpeed), playback.RaceTimeMs);

        Assert.Null(playback.Apply("human:b", LiveRaceAction.SetSpeed, 20));
        clock.Now += 1_000;
        Assert.Equal((long)(1_000 * LiveRacePlayback.DefaultSpeed) + 20_000, playback.RaceTimeMs);

        Assert.Null(playback.Apply("human:a", LiveRaceAction.Pause));
        var paused = playback.RaceTimeMs;
        clock.Now += 60_000;
        Assert.Equal(paused, playback.RaceTimeMs);

        Assert.Equal(LiveRaceText.BadSpeed, playback.Apply("human:a", LiveRaceAction.SetSpeed, 7));
        Assert.Equal(paused, playback.RaceTimeMs);
        Assert.True(playback.Paused);

        Assert.Null(playback.Apply("human:b", LiveRaceAction.SkipToEnd));
        Assert.True(playback.View().Finished);
        Assert.Equal(600_000, playback.View().RaceTimeMs);
        Assert.Null(playback.Apply("human:a", LiveRaceAction.Play));
        clock.Now += 10_000;
        Assert.Equal(600_000, playback.RaceTimeMs);

        Assert.Equal(
            [LiveRaceAction.Play, LiveRaceAction.SetSpeed, LiveRaceAction.Pause, LiveRaceAction.SkipToEnd, LiveRaceAction.Play],
            playback.Log.Select(order => order.Action));
        Assert.Equal(["human:a", "human:b", "human:a", "human:b", "human:a"], playback.Log.Select(order => order.ManagerId));
    }

    [Fact]
    public void TwoViewersOfOneClockSeeTheSameRaceTime()
    {
        var clock = new ManualClock();
        var playback = new LiveRacePlayback(1955, 1, 600_000, clock);
        playback.Apply("human:a", LiveRaceAction.Play);
        clock.Now += 2_345;
        var host = playback.View();
        var guest = playback.View();
        Assert.Equal(host, guest);
    }

    [Theory]
    [InlineData("play", true)]
    [InlineData("SetSpeed", true)]
    [InlineData("skipToEnd", true)]
    [InlineData("rewind", false)]
    [InlineData("1", false)]
    [InlineData(null, false)]
    public void OnlyTheKnownViewingOrdersParse(string? text, bool known) =>
        Assert.Equal(known, LiveRacePlayback.TryParse(text, out _));

    [Fact]
    public void FramesComeInAWindowPerCarInTimeOrderAndAWindowIsNeverLongerThanTheCap()
    {
        var tape = LapFrameInterpolator.Attach(FakeRaceTapeBuilder.Build(7).Tape, 4_000);
        var watch = new RaceWatch();
        watch.Publish(1955, 1, "monza_1950", tape, [], []);

        var window = LiveRaceRead.Frames(watch, 60_000, 70_000);
        Assert.True(window.Found);
        Assert.Equal(FakeRaceTapeBuilder.DriverCount, window.Cars.Count);
        foreach (var car in window.Cars)
        {
            Assert.Equal(car.TimeMs.Count, car.DistanceM.Count);
            Assert.Equal(car.TimeMs.Count, car.SpeedMps.Count);
            Assert.Equal(car.TimeMs.Count, car.InPit.Count);
            Assert.Equal(car.TimeMs.Count, car.PitM.Count);
            Assert.All(car.TimeMs, time => Assert.InRange(time, 60_000, 70_000));
            Assert.Equal(car.TimeMs.OrderBy(time => time), car.TimeMs);
        }

        var huge = LiveRaceRead.Frames(watch, 0, long.MaxValue);
        Assert.Equal(LiveRaceRead.MaxWindowMs, huge.ToMs - huge.FromMs);

        Assert.False(LiveRaceRead.Frames(new RaceWatch(), 0, 1_000).Found);
    }

    [Fact]
    public void EveryTranscriptAndRadioKeyIsInBothLanguages()
    {
        var keys = new List<string>
        {
            LiveRaceKeys.Start, LiveRaceKeys.PitIn, LiveRaceKeys.PitOut, LiveRaceKeys.Gain, LiveRaceKeys.Loss, LiveRaceKeys.SafetyCar,
            LiveRaceKeys.SafetyCarEnds, LiveRaceKeys.VirtualSafetyCar, LiveRaceKeys.VirtualSafetyCarEnds, LiveRaceKeys.RedFlag,
            LiveRaceKeys.Fastest, LiveRaceKeys.Winner, LiveRaceKeys.OwnFinish, LiveRaceText.NoRace, LiveRaceText.BadSpeed,
            LiveRaceText.BadAction,
        };
        keys.AddRange(Enum.GetValues<Paddock.Domain.Racing.IncidentSeverity>().Select(LiveRaceKeys.Incident));
        keys.AddRange(LiveRaceKeys.Conditions.Select(LiveRaceKeys.Weather));
        keys.AddRange(LiveRaceKeys.RetireCauses.Select(cause => LiveRaceKeys.Retire("report.retire." + cause, Paddock.Domain.Racing.RetirementReason.Other)));
        keys.AddRange(LiveRaceKeys.CallKinds.Select(call => LiveRaceKeys.Call(call, withTyres: false)));
        keys.Add(LiveRaceKeys.Call(StrategyCalls.Pit, withTyres: true));

        foreach (var language in new[] { "pl", "en" })
        {
            var path = Path.Combine(Paddock.Desktop.Bridge.BridgeHost.RepositoryRoot(), "strings", language + ".json");
            using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            foreach (var key in keys)
            {
                Assert.True(json.RootElement.TryGetProperty(key, out _), language + " lacks " + key);
            }
        }
    }

    private static DecisionTrace Trace(string who, string trigger, string chosen) => new(
        new WeekendKey(1955, 1),
        who,
        50,
        trigger,
        [],
        chosen,
        "",
        null,
        false,
        new Dictionary<string, string>());

    private static (WorldState World, OrganizationId Team) Team(GameDate on, string id, int skill)
    {
        var world = WorldState.At(on);
        (world, var team) = world.AddOrganization(new OrganizationSpec(
            OrganizationKind.Team, true, id, on, null, 0, [new OrganizationNameSpan(id, on, null)]));
        world = Hire(world, team, on, id + "_strategist", StaffRole.Strategist, skill);
        world = Hire(world, team, on, id + "_mechanic", StaffRole.ChiefMechanic, skill);
        return (world, team);
    }

    private static WorldState Hire(WorldState world, OrganizationId team, GameDate on, string id, StaffRole role, int skill)
    {
        var attributes = StaffCatalogue.AttributeKeys(role).Select(key => new NamedAttribute(key, skill)).ToArray();
        (world, var person) = world.AddPerson(new PersonSpec(
            "Pat",
            id,
            new GameDate(1920, 1, 1),
            "GBR",
            true,
            id,
            [PersonRole.Staff(role)],
            new PersonTruth(attributes, attributes)));
        (world, _) = world.AddContract(new ContractSpec(
            person, team, ContractRole.Staff(role), on, GameDate.SeasonEnd(1955), 0, false, null, null));
        return world;
    }

    private sealed class ManualClock : IClock
    {
        public long Now { get; set; }

        public long NowMs => Now;
    }
}
