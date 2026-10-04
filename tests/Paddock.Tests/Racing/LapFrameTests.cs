using System.Text;
using Paddock.Domain.Racing;
using Paddock.Persistence;
using Paddock.Simulation.Racing;

namespace Paddock.Tests.Racing;

/// <summary>Position frames are display data derived from the tape. Distances and the sample interval are ESTIMATES.</summary>
public class LapFrameTests
{
    private const double LapM = 1_000;

    [Fact]
    public void OldEventJson_RoundTripsWithoutFrames()
    {
        const string json =
            "[{\"seq\":0,\"lap\":0,\"raceTime\":0,\"kind\":\"RaceStarted\",\"totalLaps\":3,\"driverIds\":[\"a\",\"b\\\"q\"]},"
            + "{\"seq\":1,\"lap\":1,\"raceTime\":10,\"kind\":\"Incident\",\"involvedIds\":[\"a\",\"b\\\"q\"],\"severity\":\"Severe\"},"
            + "{\"seq\":2,\"lap\":1,\"raceTime\":20,\"kind\":\"SafetyCar\",\"phase\":\"Deployed\",\"isVirtual\":true},"
            + "{\"seq\":3,\"lap\":3,\"raceTime\":30,\"kind\":\"RaceEnded\"}]";

        var tape = RaceTape.Parse(json);

        Assert.True(tape.Frames.IsEmpty);
        Assert.Null(tape.FrameAccuracy);
        Assert.Equal(json, tape.ToCanonicalJson());
        Assert.Equal(json, tape.ToDocumentJson());
    }

    [Fact]
    public void FramedDocument_RoundTripsAndLeavesTheEventHashAlone()
    {
        var bare = TwoCarRace();
        var framed = LapFrameInterpolator.Attach(bare, LapM, sampleSeconds: 1);

        var again = RaceTape.Parse(framed.ToDocumentJson());

        Assert.Equal(FrameAccuracy.Approximate, again.FrameAccuracy);
        Assert.Equal(bare.ToCanonicalJson(), framed.ToCanonicalJson());
        Assert.Equal(bare.Hash, framed.Hash);
        Assert.Equal(framed.ToDocumentJson(), again.ToDocumentJson());
        Assert.Equal(framed.Frames.Length, again.Frames.Length);
    }

    [Fact]
    public void LeaderDistance_IsMonotonic_AndAPitStop_IsOnThePitLane()
    {
        var tape = LapFrameInterpolator.Attach(PittingRace(), LapM, sampleSeconds: 1);
        var leader = tape.Frames.Where(f => f.CarId == "a").ToList();

        Assert.Equal(FrameAccuracy.Approximate, tape.FrameAccuracy);
        for (var i = 1; i < leader.Count; i++)
        {
            Assert.True(leader[i].DistanceM >= leader[i - 1].DistanceM, "the leader moved backwards");
        }

        var pitting = tape.Frames.Where(f => f.CarId == "b" && f.RaceTimeMs > 9_000 && f.RaceTimeMs < 11_000).ToList();
        Assert.NotEmpty(pitting);
        Assert.All(pitting, frame =>
        {
            Assert.True(frame.InPitLane);
            Assert.InRange(frame.PitDistanceM, 0, LapFrameInterpolator.PitLaneLengthM);
        });
    }

    [Fact]
    public void SafetyCar_KeepsTheGap_AndFinishOrderMatchesTheFrames()
    {
        var tape = LapFrameInterpolator.Attach(SafetyCarRace(), LapM, sampleSeconds: 1);
        var gapAtDeploy = Gap(tape, 10_000);
        var gapAtEnd = Gap(tape, 20_000);

        Assert.InRange(Math.Abs(gapAtEnd - gapAtDeploy), 0, 1);

        var atFlag = tape.Frames.Where(f => f.RaceTimeMs == 20_000).OrderByDescending(f => f.DistanceM).Select(f => f.CarId).ToList();
        var classified = RaceSummary.From(tape).Classification.Where(c => c.Finished).Select(c => c.DriverId).ToList();
        Assert.Equal(classified, atFlag);
    }

    [Fact]
    public void TheSameTape_YieldsTheSameFramesTwice()
    {
        var once = LapFrameInterpolator.Attach(TwoCarRace(), LapM, 0.5);
        var twice = LapFrameInterpolator.Attach(TwoCarRace(), LapM, 0.5);

        Assert.Equal(once.ToDocumentJson(), twice.ToDocumentJson());
    }

    [Fact]
    public void SixtyLapTape_GrowsByTheFrames_AndSavesDoNotStoreThem()
    {
        var bare = LongRace(laps: 60, lapMs: 90_000);
        var framed = LapFrameInterpolator.Attach(bare, LapM, sampleSeconds: 1);
        var events = Encoding.UTF8.GetByteCount(framed.ToCanonicalJson());
        var document = Encoding.UTF8.GetByteCount(framed.ToDocumentJson());

        Assert.True(framed.Frames.Length > 60 * 2, "a one-second sample of a 60-lap race should be denser than the lap crossings");
        Assert.True(document > events * 5, "frames dominate the document; the event JSON itself is unchanged");
        Assert.DoesNotContain("\"frames\"", framed.ToCanonicalJson(), StringComparison.Ordinal);

        var persistence = typeof(CareerConfigCodec).Assembly.GetTypes().Select(type => type.Name);
        Assert.DoesNotContain(persistence, name => name.Contains("RaceTape", StringComparison.Ordinal) || name.Contains("CarFrame", StringComparison.Ordinal));
    }

    private static double Gap(RaceTape tape, long time)
    {
        var row = tape.Frames.Where(f => f.RaceTimeMs == time).ToDictionary(f => f.CarId, f => f.DistanceM);
        return row["a"] - row["b"];
    }

    private static RaceTape TwoCarRace() => RaceTape.From(
    [
        new RaceStarted(0, 0, 0, 2, ["a", "b"]),
        new LapCompleted(1, 1, 10_000, "a", 10_000, 1, 0),
        new LapCompleted(2, 1, 11_000, "b", 11_000, 2, 1_000),
        new LapCompleted(3, 2, 20_000, "a", 10_000, 1, 0),
        new Finished(4, 2, 20_000, "a", 1, 2, 20_000),
        new LapCompleted(5, 2, 22_000, "b", 11_000, 2, 2_000),
        new Finished(6, 2, 22_000, "b", 2, 2, 22_000),
        new RaceEnded(7, 2, 22_000),
    ]);

    private static RaceTape PittingRace() => RaceTape.From(
    [
        new RaceStarted(0, 0, 0, 1, ["a", "b"]),
        new PitStop(1, 1, 9_000, "b", PitLanePhase.In, 0, "soft"),
        new LapCompleted(2, 1, 10_000, "a", 10_000, 1, 0),
        new Finished(3, 1, 10_000, "a", 1, 1, 10_000),
        new PitStop(4, 1, 11_000, "b", PitLanePhase.Out, 2_000, "medium"),
        new LapCompleted(5, 1, 11_000, "b", 11_000, 2, 1_000),
        new Finished(6, 1, 11_000, "b", 2, 1, 11_000),
        new RaceEnded(7, 1, 11_000),
    ]);

    private static RaceTape SafetyCarRace() => RaceTape.From(
    [
        new RaceStarted(0, 0, 0, 2, ["a", "b"]),
        new LapCompleted(1, 1, 10_000, "a", 10_000, 1, 0),
        new SafetyCar(2, 1, 10_000, SafetyCarPhase.Deployed, false),
        new LapCompleted(3, 1, 12_000, "b", 12_000, 2, 2_000),
        new LapCompleted(4, 2, 20_000, "a", 10_000, 1, 0),
        new LapCompleted(5, 2, 20_000, "b", 8_000, 2, 0),
        new SafetyCar(6, 2, 20_000, SafetyCarPhase.Ending, false),
        new Finished(7, 2, 20_000, "a", 1, 2, 20_000),
        new Finished(8, 2, 20_000, "b", 2, 2, 20_000),
        new RaceEnded(9, 2, 20_000),
    ]);

    private static RaceTape LongRace(int laps, long lapMs)
    {
        var events = new List<RaceEvent> { new RaceStarted(0, 0, 0, laps, ["a", "b"]) };
        var seq = 1;
        for (var lap = 1; lap <= laps; lap++)
        {
            var time = lap * lapMs;
            events.Add(new LapCompleted(seq++, lap, time, "a", lapMs, 1, 0));
            events.Add(new LapCompleted(seq++, lap, time + 1_000, "b", lapMs, 2, 1_000));
        }

        var end = (laps * lapMs) + 1_000;
        events.Add(new Finished(seq++, laps, end, "a", 1, laps, end));
        events.Add(new Finished(seq++, laps, end, "b", 2, laps, end));
        events.Add(new RaceEnded(seq, laps, end));
        return RaceTape.From(events);
    }
}
