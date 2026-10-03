using System.Collections.Immutable;
using Paddock.Domain.Racing;
using Paddock.Simulation.Racing;

namespace Paddock.Tests.Racing;

public class RaceTapeTests
{
    private static readonly ImmutableArray<string> Drivers = ["a", "b"];

    private static RaceStarted Start() => new(0, 0, 0, 3, Drivers);

    [Fact]
    public void ValidTapeIsAccepted()
    {
        var tape = RaceTape.From(
        [
            Start(),
            new LapCompleted(1, 1, 90_000, "a", 90_000, 1, 0),
            new Retirement(2, 2, 100_000, "b", RetirementReason.Mechanical),
            new RaceEnded(3, 3, 300_000),
        ]);

        Assert.Equal(4, tape.Count);
    }

    [Fact]
    public void FirstEventMustBeRaceStartedWithSeqZero()
    {
        Assert.Throws<RaceTapeException>(() => RaceTape.From([new RaceEnded(0, 0, 0)]));
        Assert.Throws<RaceTapeException>(() => RaceTape.From([new RaceStarted(1, 0, 0, 3, Drivers)]));
    }

    [Fact]
    public void RaceStartedCannotAppearTwice()
    {
        Assert.Throws<RaceTapeException>(() => RaceTape.From([Start(), new RaceStarted(1, 0, 0, 3, Drivers)]));
    }

    [Fact]
    public void SeqMustIncrease()
    {
        Assert.Throws<RaceTapeException>(() => RaceTape.From(
            [Start(), new RedFlag(1, 1, 10), new RedFlag(1, 1, 20)]));
        Assert.Throws<RaceTapeException>(() => RaceTape.From(
            [Start(), new RedFlag(2, 1, 10), new RedFlag(1, 1, 20)]));
    }

    [Fact]
    public void RaceTimeMustNotDecreaseOrBeNegative()
    {
        Assert.Throws<RaceTapeException>(() => RaceTape.From(
            [Start(), new RedFlag(1, 1, 20), new RedFlag(2, 1, 10)]));
        Assert.Throws<RaceTapeException>(() => RaceTape.From([Start(), new RedFlag(1, 1, -1)]));
        RaceTape.From([Start(), new RedFlag(1, 1, 20), new RedFlag(2, 1, 20)]);
    }

    [Fact]
    public void LapMustBeWithinBounds()
    {
        Assert.Throws<RaceTapeException>(() => RaceTape.From([Start(), new RedFlag(1, 4, 10)]));
        Assert.Throws<RaceTapeException>(() => RaceTape.From([Start(), new RedFlag(1, -1, 10)]));
        RaceTape.From([Start(), new RedFlag(1, 3, 10)]);
    }

    [Fact]
    public void DriverCannotHaveEventsAfterRetirement()
    {
        Assert.Throws<RaceTapeException>(() => RaceTape.From(
        [
            Start(),
            new Retirement(1, 1, 10, "a", RetirementReason.Accident),
            new LapCompleted(2, 1, 20, "a", 90_000, 1, 0),
        ]));
    }

    [Fact]
    public void DriverCannotHaveEventsAfterFinishing()
    {
        Assert.Throws<RaceTapeException>(() => RaceTape.From(
        [
            Start(),
            new Finished(1, 3, 10, "a", 1, 3, 10),
            new Retirement(2, 3, 20, "a", RetirementReason.Other),
        ]));
    }

    [Fact]
    public void UnknownDriversAreRejected()
    {
        Assert.Throws<RaceTapeException>(() => RaceTape.From(
            [Start(), new LapCompleted(1, 1, 10, "zzz", 1, 1, 0)]));
        Assert.Throws<RaceTapeException>(() => RaceTape.From(
            [Start(), new Incident(1, 1, 10, ["a", "zzz"], IncidentSeverity.Minor)]));
    }

    [Fact]
    public void NothingMayFollowRaceEnded()
    {
        Assert.Throws<RaceTapeException>(() => RaceTape.From(
            [Start(), new RaceEnded(1, 3, 10), new RedFlag(2, 3, 20)]));
    }

    [Fact]
    public void IncidentMayNameAnAlreadyRetiredCar()
    {
        RaceTape.From(
        [
            Start(),
            new Retirement(1, 1, 10, "a", RetirementReason.Mechanical),
            new Incident(2, 1, 20, ["a", "b"], IncidentSeverity.Minor),
        ]);
    }

    [Fact]
    public void BuilderRejectsAnInvalidAppendWithoutChangingTheTape()
    {
        var builder = new RaceTape.Builder().Append(Start());
        Assert.Throws<RaceTapeException>(() => builder.Append(new RedFlag(0, 1, 10)));
        Assert.Equal(1, builder.Count);
        builder.Append(new RedFlag(1, 1, 10));
        Assert.Equal(2, builder.Build().Count);
    }

    [Fact]
    public void EqualTapesSerializeToIdenticalJsonAndHash()
    {
        var a = FakeRaceTapeBuilder.Build(7).Tape;
        var b = FakeRaceTapeBuilder.Build(7).Tape;
        var other = FakeRaceTapeBuilder.Build(8).Tape;

        Assert.Equal(a.ToCanonicalJson(), b.ToCanonicalJson());
        Assert.Equal(a.Hash, b.Hash);
        Assert.NotEqual(a.Hash, other.Hash);
        Assert.Equal(64, a.Hash.Length);
    }

    [Fact]
    public void CanonicalJsonHasAFixedShape()
    {
        var tape = RaceTape.From(
        [
            new RaceStarted(0, 0, 0, 3, ["a", "b\"q"]),
            new Incident(1, 1, 10, ["a", "b\"q"], IncidentSeverity.Severe),
            new SafetyCar(2, 1, 20, SafetyCarPhase.Deployed, true),
            new RaceEnded(3, 3, 30),
        ]);

        Assert.Equal(
            "[{\"seq\":0,\"lap\":0,\"raceTime\":0,\"kind\":\"RaceStarted\",\"totalLaps\":3,\"driverIds\":[\"a\",\"b\\\"q\"]},"
            + "{\"seq\":1,\"lap\":1,\"raceTime\":10,\"kind\":\"Incident\",\"involvedIds\":[\"a\",\"b\\\"q\"],\"severity\":\"Severe\"},"
            + "{\"seq\":2,\"lap\":1,\"raceTime\":20,\"kind\":\"SafetyCar\",\"phase\":\"Deployed\",\"isVirtual\":true},"
            + "{\"seq\":3,\"lap\":3,\"raceTime\":30,\"kind\":\"RaceEnded\"}]",
            tape.ToCanonicalJson());
    }

    [Fact]
    public void FakeBuilderIsDeterministicAndProducesTwentyCarsAndTenLaps()
    {
        var race = FakeRaceTapeBuilder.Build(42);
        var start = Assert.IsType<RaceStarted>(race.Tape.Events[0]);

        Assert.Equal(20, start.DriverIds.Length);
        Assert.Equal(10, start.TotalLaps);
        Assert.IsType<RaceEnded>(race.Tape.Events[^1]);
        Assert.Equal(FakeRaceTapeBuilder.Build(42).Tape.Hash, race.Tape.Hash);
    }
}
