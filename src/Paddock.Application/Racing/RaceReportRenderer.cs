using System.Globalization;
using Paddock.Application.Localization;

namespace Paddock.Application.Racing;

/// <summary>Turns ids into the names the caller may show. The default shows the ids, as the synthetic fixture field has no names.</summary>
public sealed record RaceReportNames(Func<string, string> Driver, Func<string, string> Team)
{
    public static RaceReportNames Ids { get; } = new(id => id, id => id);
}

/// <summary>
/// Makes text from a <see cref="RaceReport"/> with an <see cref="ILocalizer"/>: plural lines through
/// <see cref="ILocalizer.GetPlural"/>, the reference types of the model as names, lap times and gaps in the language's
/// number format. Section titles are wrapped in <c>== ... ==</c>.
/// </summary>
public static class RaceReportRenderer
{
    /// <summary>The report as lines of text: the title, then per section a blank line, the title and its lines.</summary>
    public static IReadOnlyList<string> Render(RaceReport report, ILocalizer localizer, RaceReportNames? names = null)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(localizer);
        names ??= RaceReportNames.Ids;
        var lines = new List<string> { Line(report.Title, localizer, names) };
        foreach (var section in report.Sections)
        {
            lines.Add(string.Empty);
            lines.Add("== " + Line(section.Title, localizer, names) + " ==");
            foreach (var line in section.Lines)
            {
                lines.Add(Line(line, localizer, names));
            }
        }

        return lines;
    }

    public static string Line(RaceReportLine line, ILocalizer localizer, RaceReportNames names)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(names);
        var args = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in line.Args)
        {
            args[pair.Key] = Resolve(pair.Value, localizer.Language, names);
        }

        return line.Count is { } count ? localizer.GetPlural(line.Key, count, args) : localizer.Get(line.Key, args);
    }

    private static object? Resolve(object? value, Language language, RaceReportNames names) => value switch
    {
        DriverRef driver => names.Driver(driver.Id),
        TeamRef team => names.Team(team.Id),
        DriverList list => string.Join(list.Crew ? " / " : ", ", list.Ids.Select(names.Driver)),
        LapTime time => Clock(time.Milliseconds, withHours: false),
        RaceClock clock => Clock(clock.Milliseconds, withHours: clock.Milliseconds >= 3_600_000),
        Seconds seconds => FixedSeconds(seconds.Value, language),
        _ => value,
    };

    private static string Clock(long milliseconds, bool withHours)
    {
        var hours = milliseconds / 3_600_000;
        var minutes = withHours ? milliseconds / 60_000 % 60 : milliseconds / 60_000;
        var rest = milliseconds % 60_000;
        var tail = string.Create(CultureInfo.InvariantCulture, $"{rest / 1000:00}.{rest % 1000:000}");
        var text = withHours
            ? string.Create(CultureInfo.InvariantCulture, $"{hours}:{minutes:00}:{tail}")
            : string.Create(CultureInfo.InvariantCulture, $"{minutes}:{tail}");
        return text;
    }

    private static string FixedSeconds(double seconds, Language language)
    {
        var text = seconds.ToString("0.000", CultureInfo.InvariantCulture);
        return language == Language.Pl ? text.Replace('.', ',') : text;
    }
}
