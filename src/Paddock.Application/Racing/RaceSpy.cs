using System.Globalization;
using Paddock.Domain.Racing;
using Paddock.Simulation.Racing.Weather;

namespace Paddock.Application.Racing;

/// <summary>
/// The developer Spy block for a race: the true weather. SimRunner and other tools may print it; a manager query must not
/// (INV-003).
/// </summary>
public static class RaceSpy
{
    public static bool IsSpy(string? key) =>
        string.Equals(key, RaceReportKeys.SectionSpy, StringComparison.Ordinal)
        || string.Equals(key, RaceReportKeys.SpyWeather, StringComparison.Ordinal);

    public static bool IsSpy(StoredReportSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (IsSpy(section.Title.Key))
        {
            return true;
        }

        foreach (var line in section.Lines)
        {
            if (IsSpy(line.Key))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsSpy(ReportSectionView section)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (IsSpy(section.Title.Key))
        {
            return true;
        }

        foreach (var line in section.Lines)
        {
            if (IsSpy(line.Key))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>One Spy section from the true weather. Decision traces stay with the sink the tool collected.</summary>
    public static RaceReportSection Weather(TruthWeather truth)
    {
        ArgumentNullException.ThrowIfNull(truth);
        var peak = 0d;
        foreach (var sample in truth.Samples)
        {
            if (sample.TrackWetness > peak)
            {
                peak = sample.TrackWetness;
            }
        }

        return new RaceReportSection(
            RaceReportLine.Of(RaceReportKeys.SectionSpy),
            [
                RaceReportLine.Of(
                    RaceReportKeys.SpyWeather,
                    ("showery", truth.IsShowery ? "yes" : "no"),
                    ("onset", truth.OnsetMinute?.ToString(CultureInfo.InvariantCulture) ?? "-"),
                    ("peak", peak.ToString("0.00", CultureInfo.InvariantCulture))),
            ]);
    }
}
