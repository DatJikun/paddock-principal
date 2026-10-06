using System.Globalization;
using Paddock.Application.Access;
using Paddock.Application.Career;
using Paddock.Application.Racing;
using Paddock.Domain.Career;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Persistence;
using Paddock.Simulation.Career;
using Paddock.Simulation.Racing;
using Paddock.Tests.Career;
using Paddock.Tests.Persistence;
using Paddock.Tests.Racing.Weekend;

namespace Paddock.Tests.Racing;

/// <summary>#230: what a finished round keeps beyond the classification, how the read shows it, and that it survives a save.</summary>
public sealed class RaceResultDetailsTests : IDisposable
{
    private const ulong Seed = 7;
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-v25-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static CareerSession RunToFirstRace()
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);
        CareerHost.RunUntil(session, new GameDate(1955, 3, 4), null, CareerKit.Options);
        return session;
    }

    [Fact]
    public void AFinishedRoundKeepsGridLapsTimesPoleAndFastestLap()
    {
        var session = RunToFirstRace();
        var race = session.World.Section<RaceResultsSection>(RaceResultsSection.SectionName)!.Latest()!;

        Assert.NotNull(race.Facts);
        var facts = race.Facts!;
        Assert.True(facts.Laps > 0);
        Assert.True(facts.LapLengthMeters > 0);
        Assert.NotNull(facts.PoleDriverId);
        Assert.True(facts.PoleTimeMs > 0);
        Assert.NotNull(facts.FastestLapDriverId);
        Assert.True(facts.FastestLapMs > 0);

        Assert.All(race.Rows, row => Assert.NotNull(row.Detail));
        var pole = race.Rows.Single(row => row.DriverId == facts.PoleDriverId);
        Assert.Equal(1, pole.Detail!.GridPosition);

        var best = race.Rows.Where(row => row.Detail!.FastestLapMs is not null).Min(row => row.Detail!.FastestLapMs!.Value);
        Assert.Equal(best, facts.FastestLapMs);
        var holder = race.Rows.Single(row => row.DriverId == facts.FastestLapDriverId);
        Assert.Equal(facts.FastestLapMs, holder.Detail!.FastestLapMs);

        var winner = race.Rows.Single(row => row.Position == 1).Detail!;
        Assert.Equal(facts.Laps, winner.LapsCompleted);
        Assert.NotNull(winner.TimeMs);
        Assert.All(race.Rows, row => Assert.True(row.Detail!.FastestLapMs is null or > 0));
    }

    [Fact]
    public void ANegativeLapOnTheTapeDoesNotThrowWhenTheRoundIsArchived()
    {
        var weekend = WeekendTestKit.Run(WeekendTestKit.Input(1955));
        var driver = weekend.CarResults[0].DriverId;
        var events = weekend.Tape.Events
            .Select(raceEvent => raceEvent switch
            {
                LapCompleted lap when lap.DriverId == driver => lap with { LapTimeMs = -3 },
                FastestLap fastest when fastest.DriverId == driver => fastest with { LapTimeMs = -3 },
                _ => raceEvent,
            })
            .ToArray();
        var facts = RacePublishedFacts.From(weekend) with { Tape = RaceTape.From(events) };
        var stored = RaceArchive.Record(RaceResultsSection.Empty, facts, 1955, 1, "testring");
        var row = stored.Latest()!.Rows.Single(item => item.DriverId == driver);
        Assert.True(row.Detail!.FastestLapMs is null or > 0);
        Assert.True(stored.Latest()!.Facts!.FastestLapMs is null or > 0);
    }

    [Fact]
    public void TheViewShowsGapsAndLapsDownFromTheWinnerAndNoSpy()
    {
        var session = RunToFirstRace();
        var view = ChampionshipRead.Result(session, null, null);

        Assert.True(view.Found);
        Assert.NotNull(view.Facts);
        Assert.Equal(view.Facts!.Laps * session.World.Section<RaceResultsSection>(RaceResultsSection.SectionName)!.Latest()!.Facts!.LapLengthMeters, view.Facts.DistanceMeters);
        Assert.False(string.IsNullOrEmpty(view.Facts.Pole!.DriverName));
        Assert.False(string.IsNullOrEmpty(view.Facts.FastestLap!.DriverName));
        Assert.DoesNotContain(view.Sections, RaceSpy.IsSpy);

        var winner = view.Rows.Single(row => row.Position == 1);
        Assert.Null(winner.GapMs);
        Assert.Equal(0, winner.LapsDown);
        foreach (var row in view.Rows.Where(row => row.Position > 1 && row.Classified))
        {
            if (row.LapsDown > 0)
            {
                Assert.Null(row.GapMs);
                Assert.Equal(winner.LapsCompleted!.Value - row.LapsCompleted!.Value, row.LapsDown);
            }
            else
            {
                Assert.True(row.GapMs >= 0, $"{row.DriverName} gap");
                Assert.Equal(row.TimeMs - winner.TimeMs, row.GapMs);
            }
        }

        Assert.All(view.Rows.Where(row => !row.Classified), row =>
        {
            Assert.Null(row.GapMs);
            Assert.Equal(0, row.LapsDown);
        });
    }

    [Fact]
    public void TheSameSeedStoresTheSameDetails()
    {
        var first = RunToFirstRace().World.StateHash();
        var second = RunToFirstRace().World.StateHash();
        Assert.Equal(first, second);
    }

    [Fact]
    public void DetailsSurviveASaveAndLoad()
    {
        var session = RunToFirstRace();
        var before = session.World.Section<RaceResultsSection>(RaceResultsSection.SectionName)!.Latest()!;
        var path = Path.Combine(_directory, "round-trip.paddock");
        using var save = SaveFile.Create(path, WorldFixtures.Meta());
        var repo = new WorldRepository(save);
        repo.SaveWorld(session.World, session.Date);

        var after = repo.LoadWorld().Section<RaceResultsSection>(RaceResultsSection.SectionName)!.Latest()!;
        Assert.Equal(before.Facts, after.Facts);
        Assert.Equal(
            before.Rows.Select(Describe).ToArray(),
            after.Rows.Select(Describe).ToArray());
        Assert.Equal(session.World.StateHash(), repo.LoadWorld().StateHash());
    }

    [Fact]
    public void AV24SaveKeepsItsRoundsWithoutDetails()
    {
        var path = Path.Combine(_directory, "v24.paddock");
        using (var created = SaveFile.Create(path, WorldFixtures.Meta(), [.. SaveMigrations.Production.Take(24)]))
        {
            Assert.Equal(24, created.ReadMeta().SchemaVersion);
            Exec(created, "INSERT INTO race_results (season, round, layout_id) VALUES (1955, 1, 'buenos-aires')");
            Exec(created, """
                INSERT INTO race_result_rows (season, round, position, classified, driver_id, team_id, points, retirement_key)
                VALUES (1955, 1, 1, 1, 'fangio', 'mercedes', '8', '')
                """);
        }

        using var opened = SaveFile.Open(path);
        Assert.Equal(SaveMigrations.CurrentVersion, opened.ReadMeta().SchemaVersion);
        Assert.True(SaveMigrations.CurrentVersion >= 25);
        Assert.Equal(1L, Scalar(opened, "SELECT COUNT(*) FROM race_result_rows WHERE driver_id = 'fangio' AND grid_position IS NULL AND race_time_ms IS NULL"));
        Assert.Equal(1L, Scalar(opened, "SELECT COUNT(*) FROM race_results WHERE laps IS NULL AND pole_driver_id IS NULL"));
    }

    private static string Describe(RaceResultRow row) =>
        string.Join(
            "|",
            row.Position,
            row.DriverId,
            row.Detail?.GridPosition,
            row.Detail?.LapsCompleted,
            row.Detail?.TimeMs?.ToString(CultureInfo.InvariantCulture),
            row.Detail?.FastestLapMs?.ToString(CultureInfo.InvariantCulture));

    private static object Scalar(SaveFile save, string sql)
    {
        using var command = save.Connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar()!;
    }

    private static void Exec(SaveFile save, string sql)
    {
        using var command = save.Connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
