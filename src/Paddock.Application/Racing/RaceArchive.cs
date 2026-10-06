using System.Globalization;
using Paddock.Domain.Racing;
using Paddock.Simulation.Racing;
using Paddock.Simulation.Racing.Points;
using Paddock.Simulation.Racing.Weekend;

namespace Paddock.Application.Racing;

/// <summary>
/// Writes one finished round into <see cref="RaceResultsSection"/>. The report is keys and arguments (TECH 6.3).
/// Person and team arguments keep the id, prefixed so a query can swap in a name without reading hidden truth.
/// The Spy section (true weather) is stored for developer tools; <see cref="ChampionshipRead.Result"/> strips it for a manager
/// (INV-003).
/// </summary>
public static class RaceArchive
{
    public const string PersonPrefix = "person:";

    public const string TeamPrefix = "team:";

    public const string PeoplePrefix = "people:";

    public static RaceResultsSection Record(
        RaceResultsSection current,
        RacePublishedFacts facts,
        int season,
        int round,
        string layoutId,
        System.Collections.Immutable.ImmutableArray<StandInFact> standIns = default,
        int lapLengthMeters = 0)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutId);
        var report = RaceReportBuilder.Build(RaceReportInput.From(facts, season, round, layoutId, standIns));
        var reasons = Reasons(facts);
        var bestLaps = BestLaps(facts.Tape);
        var rows = new RaceResultRow[facts.Classification.Cars.Length];
        for (var i = 0; i < rows.Length; i++)
        {
            var car = facts.Classification.Cars[i];
            var driver = car.DriverIds.IsDefaultOrEmpty ? "" : car.DriverIds[0];
            if (driver.Length == 0)
            {
                throw new InvalidOperationException("A classified car has no driver.");
            }

            reasons.TryGetValue(driver, out var reason);
            rows[i] = new RaceResultRow(
                car.Position,
                car.IsClassified,
                driver,
                car.ConstructorId,
                car.CarPoints.ToString(CultureInfo.InvariantCulture),
                reason ?? "",
                Detail(facts, driver, bestLaps));
        }

        var reportSections = report.Sections.Add(RaceSpy.Weather(facts.TruthWeather));
        var sections = new StoredReportSection[reportSections.Length];
        for (var i = 0; i < sections.Length; i++)
        {
            var section = reportSections[i];
            var lines = new StoredReportLine[section.Lines.Length];
            for (var line = 0; line < lines.Length; line++)
            {
                lines[line] = Line(section.Lines[line]);
            }

            sections[i] = new StoredReportSection(Line(section.Title), lines);
        }

        return current.With(new StoredRace(season, round, layoutId, rows, sections, WholeRace(facts, lapLengthMeters)));
    }

    private static Dictionary<string, long> BestLaps(RaceTape tape)
    {
        var best = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var lap in tape.Events.OfType<LapCompleted>())
        {
            if (lap.LapTimeMs <= 0)
            {
                continue;
            }

            if (!best.TryGetValue(lap.DriverId, out var current) || lap.LapTimeMs < current)
            {
                best[lap.DriverId] = lap.LapTimeMs;
            }
        }

        return best;
    }

    /// <summary>Grid, laps, time at the flag and best lap of one car, read from the weekend result and the tape (no new simulation).</summary>
    private static RaceRowDetail? Detail(RacePublishedFacts facts, string driver, Dictionary<string, long> bestLaps)
    {
        CarRaceResult? result = null;
        foreach (var candidate in facts.CarResults)
        {
            if (string.Equals(candidate.DriverId, driver, StringComparison.Ordinal))
            {
                result = candidate;
                break;
            }
        }

        if (result is null)
        {
            return null;
        }

        long? time = null;
        foreach (var finished in facts.Tape.Events.OfType<Finished>())
        {
            if (string.Equals(finished.DriverId, driver, StringComparison.Ordinal))
            {
                time = finished.TotalTimeMs > 0 ? finished.TotalTimeMs : null;
                break;
            }
        }

        long? fastest = bestLaps.TryGetValue(driver, out var lap) ? lap : null;
        return new RaceRowDetail(result.GridPosition, result.LapsCompleted, time, fastest);
    }

    private static RaceFacts WholeRace(RacePublishedFacts facts, int lapLengthMeters)
    {
        var pole = facts.Qualifying.Pole;
        FastestLap? fastest = null;
        foreach (var candidate in facts.Tape.Events.OfType<FastestLap>())
        {
            if (candidate.LapTimeMs > 0)
            {
                fastest = candidate;
            }
        }

        return new RaceFacts(
            facts.LapsRun,
            lapLengthMeters,
            pole?.DriverId,
            pole is null ? null : LapTime.FromSeconds(pole.ScoreSeconds).Milliseconds,
            fastest?.DriverId,
            fastest?.LapTimeMs);
    }

    private static Dictionary<string, string> Reasons(RacePublishedFacts facts)
    {
        var reasons = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var car in facts.CarResults)
        {
            if (car.Status == FinishStatus.Classified)
            {
                continue;
            }

            var reason = car.Status switch
            {
                FinishStatus.Mechanical => RetirementReason.Mechanical,
                FinishStatus.Accident => RetirementReason.Accident,
                _ => RetirementReason.Other,
            };
            reasons[car.DriverId] = RaceReportKeys.RetirementKey(reason, car.FailedComponent, car.SuddenFailure);
        }

        return reasons;
    }

    private static StoredReportLine Line(RaceReportLine line)
    {
        var args = new RaceReportArg[line.Args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            var pair = line.Args[i];
            args[i] = new RaceReportArg(pair.Key, Flatten(pair.Value));
        }

        string? count = null;
        if (line.Count is double number)
        {
            count = number.ToString("R", CultureInfo.InvariantCulture);
        }

        return new StoredReportLine(line.Key, count, args);
    }

    private static string Flatten(object? value) => value switch
    {
        null => "",
        string text => text,
        DriverRef driver => PersonPrefix + driver.Id,
        TeamRef team => TeamPrefix + team.Id,
        DriverList list => PeoplePrefix + string.Join(',', list.Ids.ToArray()),
        LapTime time => "ms:" + time.Milliseconds.ToString(CultureInfo.InvariantCulture),
        RaceClock clock => "ms:" + clock.Milliseconds.ToString(CultureInfo.InvariantCulture),
        Seconds seconds => "sec:" + seconds.Value.ToString("R", CultureInfo.InvariantCulture),
        bool flag => flag ? "1" : "0",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? "",
        _ => value.ToString() ?? "",
    };
}
