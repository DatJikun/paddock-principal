using System.Globalization;
using Paddock.Domain.Racing;
using Paddock.Simulation.Racing;
using Paddock.Simulation.Racing.Points;

namespace Paddock.Application.Racing;

/// <summary>
/// Writes one finished round into <see cref="RaceResultsSection"/>. The report is keys and arguments (TECH 6.3).
/// Person and team arguments keep the id, prefixed so a query can swap in a name without reading hidden truth.
/// </summary>
public static class RaceArchive
{
    public const string PersonPrefix = "person:";

    public const string TeamPrefix = "team:";

    public const string PeoplePrefix = "people:";

    public static RaceResultsSection Record(RaceResultsSection current, RacePublishedFacts facts, int season, int round, string layoutId)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutId);
        var report = RaceReportBuilder.Build(RaceReportInput.From(facts, season, round, layoutId));
        var reasons = Reasons(facts);
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
                reason ?? "");
        }

        var sections = new StoredReportSection[report.Sections.Length];
        for (var i = 0; i < sections.Length; i++)
        {
            var section = report.Sections[i];
            var lines = new StoredReportLine[section.Lines.Length];
            for (var line = 0; line < lines.Length; line++)
            {
                lines[line] = Line(section.Lines[line]);
            }

            sections[i] = new StoredReportSection(Line(section.Title), lines);
        }

        return current.With(new StoredRace(season, round, layoutId, rows, sections));
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
