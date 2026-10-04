using System.Collections.Immutable;

namespace Paddock.Application.Racing;

/// <summary>A driver, by stable id. The renderer swaps it for a name the caller is allowed to show.</summary>
public readonly record struct DriverRef(string Id);

/// <summary>A constructor, by stable id. The renderer swaps it for a name the caller is allowed to show.</summary>
public readonly record struct TeamRef(string Id);

/// <summary>Several drivers, by stable id, in the order to show them. <c>Crew</c> marks the drivers of one shared car (shown with a slash, not a comma).</summary>
public readonly record struct DriverList(ImmutableArray<string> Ids, bool Crew = false);

/// <summary>A lap time in milliseconds, shown as <c>m:ss.mmm</c>.</summary>
public readonly record struct LapTime(long Milliseconds)
{
    public static LapTime FromSeconds(double seconds) => new((long)Math.Round(seconds * 1000d, MidpointRounding.AwayFromZero));
}

/// <summary>A race time in milliseconds, shown as <c>h:mm:ss.mmm</c> (or <c>m:ss.mmm</c> under an hour).</summary>
public readonly record struct RaceClock(long Milliseconds)
{
    public static RaceClock FromSeconds(double seconds) => new((long)Math.Round(seconds * 1000d, MidpointRounding.AwayFromZero));
}

/// <summary>A duration or gap in seconds, shown with three decimals in the language's decimal sign.</summary>
public readonly record struct Seconds(double Value);

/// <summary>
/// One line of a report as a translation key plus its arguments, never a finished sentence (TECH 6.3). When
/// <see cref="Count"/> is set the line is a plural entry and the count selects the form; the count is also
/// available to the text as <c>{count}</c>. Argument values are plain numbers, strings or the reference types of
/// this file, which <see cref="RaceReportRenderer"/> turns into text.
/// </summary>
public sealed record RaceReportLine(string Key, ImmutableArray<KeyValuePair<string, object?>> Args, double? Count = null)
{
    public static RaceReportLine Of(string key, params (string Name, object? Value)[] args) =>
        new(key, ToArgs(args));

    public static RaceReportLine Plural(string key, double count, params (string Name, object? Value)[] args) =>
        new(key, ToArgs(args), count);

    public object? Arg(string name)
    {
        foreach (var pair in Args)
        {
            if (pair.Key == name)
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static ImmutableArray<KeyValuePair<string, object?>> ToArgs((string Name, object? Value)[] args) =>
        [.. args.Select(a => new KeyValuePair<string, object?>(a.Name, a.Value))];
}

/// <summary>A titled group of lines: the conditions, a phase of the race, the result.</summary>
public sealed record RaceReportSection(RaceReportLine Title, ImmutableArray<RaceReportLine> Lines);

/// <summary>The readable report of one race: a title and sections of lines. Language-independent.</summary>
public sealed record RaceReport(RaceReportLine Title, ImmutableArray<RaceReportSection> Sections);

/// <summary>What the report builder may be asked for.</summary>
/// <param name="Verbose">Adds the lap-by-lap section (the leader's laps and every event) after the report.</param>
public sealed record RaceReportOptions(bool Verbose = false);
