using System.Collections.Immutable;
using Paddock.Domain.Racing;
using Paddock.Simulation.Racing;
using Paddock.Simulation.Racing.Playback;

namespace Paddock.Tests.Racing;

public class PlaybackSchedulerTests
{
    // Events at race time 0, 10 s, 20 s (lap 1), 30 s (lap 2), 40 s (lap 3).
    private static RaceTape SmallTape() => RaceTape.From(
    [
        new RaceStarted(0, 0, 0, 3, ["a", "b"]),
        new LapCompleted(1, 1, 10_000, "a", 10_000, 1, 0),
        new LapCompleted(2, 1, 20_000, "b", 20_000, 2, 10_000),
        new LapCompleted(3, 2, 30_000, "a", 20_000, 1, 0),
        new RaceEnded(4, 3, 40_000),
    ]);

    [Fact]
    public void RealTimeSpeedDeliversEventsWhenTheirRaceTimeIsReached()
    {
        var clock = new FakeClock();
        var cursor = new PlaybackScheduler(SmallTape(), clock).CreateCursor();

        Assert.Equal([0], Seqs(cursor.Poll()));
        clock.Advance(9_999);
        Assert.Empty(cursor.Poll());
        clock.Advance(1);
        Assert.Equal([1], Seqs(cursor.Poll()));
        clock.Advance(20_000);
        Assert.Equal([2, 3], Seqs(cursor.Poll()));
        clock.Advance(10_000);
        Assert.Equal([4], Seqs(cursor.Poll()));
        Assert.True(cursor.IsFinished);
        clock.Advance(100_000);
        Assert.Empty(cursor.Poll());
    }

    [Fact]
    public void TwentyTimesSpeedCompressesTheRace()
    {
        var clock = new FakeClock();
        var cursor = new PlaybackScheduler(SmallTape(), clock, speed: 20).CreateCursor();

        Assert.Equal([0], Seqs(cursor.Poll()));
        clock.Advance(500); // 10 s of race time
        Assert.Equal([1], Seqs(cursor.Poll()));
        clock.Advance(1_500); // 30 s more
        Assert.Equal([2, 3, 4], Seqs(cursor.Poll()));
        Assert.True(cursor.IsFinished);
    }

    [Fact]
    public void PauseStopsRaceTimeAndResumeContinuesFromThere()
    {
        var clock = new FakeClock();
        var cursor = new PlaybackScheduler(SmallTape(), clock).CreateCursor();
        cursor.Poll();

        clock.Advance(5_000);
        cursor.Pause();
        clock.Advance(60_000);
        Assert.Empty(cursor.Poll());
        Assert.Equal(5_000, cursor.RaceTimeMs);

        cursor.Resume();
        clock.Advance(5_000);
        Assert.Equal([1], Seqs(cursor.Poll()));
        Assert.Equal(10_000, cursor.RaceTimeMs);
    }

    [Fact]
    public void SpeedChangeAppliesFromTheMomentOfTheChange()
    {
        var clock = new FakeClock();
        var cursor = new PlaybackScheduler(SmallTape(), clock).CreateCursor();
        cursor.Poll();

        clock.Advance(5_000); // 5 s at 1x
        cursor.SetSpeed(10);
        clock.Advance(1_000); // 10 s at 10x

        Assert.Equal([1], Seqs(cursor.Poll()));
        Assert.Equal(15_000, cursor.RaceTimeMs);
        Assert.Equal(10, cursor.Speed);
    }

    [Fact]
    public void SeekToLapJumpsForwardAndSkipsEarlierEvents()
    {
        var clock = new FakeClock();
        var cursor = new PlaybackScheduler(SmallTape(), clock).CreateCursor();
        cursor.Poll();

        cursor.SeekToLap(2);

        Assert.Equal(30_000, cursor.RaceTimeMs);
        Assert.Equal([3], Seqs(cursor.Poll()));
        clock.Advance(10_000);
        Assert.Equal([4], Seqs(cursor.Poll()));
    }

    [Fact]
    public void SeekBackwardsReplaysEvents()
    {
        var clock = new FakeClock();
        var cursor = new PlaybackScheduler(SmallTape(), clock, speed: 100).CreateCursor();
        clock.Advance(1_000);
        Assert.Equal(5, cursor.Poll().Count);

        cursor.SeekToLap(1);

        Assert.Equal(10_000, cursor.RaceTimeMs);
        Assert.Equal([1], Seqs(cursor.Poll()));
        clock.Advance(300); // 30 s of race time at 100x
        Assert.Equal([2, 3, 4], Seqs(cursor.Poll()));
    }

    [Fact]
    public void SeekToLapZeroRestartsAndSeekPastTheEndFinishes()
    {
        var clock = new FakeClock();
        var cursor = new PlaybackScheduler(SmallTape(), clock).CreateCursor();
        clock.Advance(25_000);
        cursor.Poll();

        cursor.SeekToLap(0);
        Assert.Equal(0, cursor.RaceTimeMs);
        Assert.Equal([0], Seqs(cursor.Poll()));

        cursor.SeekToLap(99);
        Assert.True(cursor.IsFinished);
        Assert.Empty(cursor.Poll());
    }

    [Fact]
    public void CursorsAreIndependentAndInSyncWhenDrivenByTheSameClock()
    {
        var clock = new FakeClock();
        var scheduler = new PlaybackScheduler(SmallTape(), clock, speed: 2);
        var host = scheduler.CreateCursor();
        var guest = scheduler.CreateCursor();

        clock.Advance(7_000);
        var hostFirst = Seqs(host.Poll());
        Assert.Equal([0, 1], hostFirst);
        Assert.Equal(hostFirst, Seqs(guest.Poll()));

        guest.Pause();
        host.SeekToLap(3);
        clock.Advance(100_000);

        Assert.Empty(guest.Poll());
        Assert.Equal(14_000, guest.RaceTimeMs);
        Assert.Equal([4], Seqs(host.Poll()));
        Assert.True(host.IsFinished);
        Assert.False(guest.IsFinished);
    }

    [Fact]
    public void LateCursorStartsAtRaceTimeZero()
    {
        var clock = new FakeClock();
        var scheduler = new PlaybackScheduler(SmallTape(), clock);
        clock.Advance(1_000_000);

        var cursor = scheduler.CreateCursor();

        Assert.Equal([0], Seqs(cursor.Poll()));
        Assert.False(cursor.IsFinished);
    }

    [Fact]
    public void InvalidSpeedIsRejected()
    {
        var clock = new FakeClock();
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlaybackScheduler(SmallTape(), clock, 0));
        var cursor = new PlaybackScheduler(SmallTape(), clock).CreateCursor();
        Assert.Throws<ArgumentOutOfRangeException>(() => cursor.SetSpeed(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => cursor.SetSpeed(double.NaN));
    }

    [Fact]
    public void PlaybackNeverMutatesTheTape()
    {
        var race = FakeRaceTapeBuilder.Build(5);
        var tape = race.Tape;
        var hashBefore = tape.Hash;
        var eventsBefore = tape.Events;
        var clock = new FakeClock();
        var scheduler = new PlaybackScheduler(tape, clock, speed: 50);
        var a = scheduler.CreateCursor();
        var b = scheduler.CreateCursor();

        for (var i = 0; i < 100; i++)
        {
            clock.Advance(1_000);
            a.Poll();
            b.SeekToLap(i % 11);
            b.Poll();
        }

        Assert.Equal(hashBefore, tape.Hash);
        Assert.Equal(eventsBefore, tape.Events);
        Assert.Same(tape, scheduler.Tape);
    }

    [Fact]
    public void FullPlaybackOfTheStubRaceDeliversEveryEventExactlyOnceInOrder()
    {
        var tape = FakeRaceTapeBuilder.Build(11).Tape;
        var clock = new FakeClock();
        var cursor = new PlaybackScheduler(tape, clock, speed: 20).CreateCursor();
        var delivered = new List<RaceEvent>();

        while (!cursor.IsFinished)
        {
            delivered.AddRange(cursor.Poll());
            clock.Advance(250);
        }

        Assert.Equal(tape.Events, delivered);
    }

    private static List<int> Seqs(IEnumerable<RaceEvent> events) => events.Select(e => e.Seq).ToList();

    private sealed class FakeClock : IClock
    {
        public long NowMs { get; private set; }

        public void Advance(long ms) => NowMs += ms;
    }
}
