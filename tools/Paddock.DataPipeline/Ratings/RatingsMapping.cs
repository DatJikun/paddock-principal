namespace Paddock.DataPipeline;

/// <summary>
/// The single constants file for turning a fitted skill value into an overall rating (1-100) and stars (0-5,
/// half steps, PP-040). Everything here is a GUESS (estimate) until calibrated against the real Jolpica run;
/// change the numbers here and nowhere else.
/// </summary>
public static class RatingsMapping
{
    /// <summary>
    /// Percentile (0 = worst ranked driver, 1 = best) to overall, linearly interpolated between anchors.
    /// ESTIMATES: the issue fixed only 50th percentile = 60 and 99th = 95; the other anchors are guesses.
    /// </summary>
    public static readonly IReadOnlyList<(double Percentile, double Overall)> OverallAnchors =
    [
        (0.00, 30.0),
        (0.25, 50.0),
        (0.50, 60.0),
        (0.75, 72.0),
        (0.90, 82.0),
        (0.99, 95.0),
        (1.00, 100.0),
    ];

    /// <summary>
    /// Stars from percentile: the first cut-off whose percentile is &lt;= the driver's percentile wins
    /// (inclusive at the boundary). Ordered high to low. ESTIMATES, first pass after the real run (2026-10-04):
    /// over ~350 ranked drivers, 5 stars is roughly the all-time top 10 and 4.5 the champions of each era.
    /// </summary>
    public static readonly IReadOnlyList<(double MinPercentile, double Stars)> StarCutoffs =
    [
        (0.97, 5.0),
        (0.92, 4.5),
        (0.85, 4.0),
        (0.75, 3.5),
        (0.62, 3.0),
        (0.48, 2.5),
        (0.34, 2.0),
        (0.20, 1.5),
        (0.10, 1.0),
        (0.04, 0.5),
    ];

    /// <summary>
    /// Mid-rank percentile of <paramref name="value"/> within <paramref name="sortedAscending"/> (which must
    /// contain it): (below + equal / 2) / n. A set of one gives 0.5.
    /// </summary>
    public static double Percentile(double value, IReadOnlyList<double> sortedAscending)
    {
        ArgumentNullException.ThrowIfNull(sortedAscending);
        var n = sortedAscending.Count;
        if (n == 0)
        {
            return 0.5;
        }

        var below = 0;
        var equal = 0;
        for (var i = 0; i < n; i++)
        {
            if (sortedAscending[i] < value)
            {
                below++;
            }
            else if (sortedAscending[i] == value)
            {
                equal++;
            }
        }

        return (below + equal / 2.0) / n;
    }

    /// <summary>Overall rating 1-100; monotone non-decreasing in the percentile.</summary>
    public static int Overall(double percentile)
    {
        var p = Math.Clamp(percentile, 0.0, 1.0);
        var anchors = OverallAnchors;
        var value = anchors[^1].Overall;
        for (var i = 1; i < anchors.Count; i++)
        {
            if (p <= anchors[i].Percentile)
            {
                var (p0, o0) = anchors[i - 1];
                var (p1, o1) = anchors[i];
                value = o0 + (o1 - o0) * (p - p0) / (p1 - p0);
                break;
            }
        }

        return Math.Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero), 1, 100);
    }

    /// <summary>Stars 0-5 in half steps; monotone non-decreasing in the percentile.</summary>
    public static double Stars(double percentile)
    {
        foreach (var (min, stars) in StarCutoffs)
        {
            if (percentile >= min)
            {
                return stars;
            }
        }

        return 0.0;
    }
}
