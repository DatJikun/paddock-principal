namespace Paddock.Simulation.Racing.Pace;

/// <summary>Smooth interpolation through (season, value) anchors. Pure; used for era-parameterised numbers.</summary>
internal static class EraCurve
{
    /// <summary>
    /// Value at <paramref name="season"/>: smoothstep between the two surrounding anchors, flat outside the ends.
    /// Anchors must be sorted by season, strictly increasing.
    /// </summary>
    public static double Evaluate(IReadOnlyList<(int Season, double Value)> anchors, int season)
    {
        if (season <= anchors[0].Season)
        {
            return anchors[0].Value;
        }

        for (var i = 1; i < anchors.Count; i++)
        {
            var (toSeason, toValue) = anchors[i];
            if (season > toSeason)
            {
                continue;
            }

            var (fromSeason, fromValue) = anchors[i - 1];
            var t = (double)(season - fromSeason) / (toSeason - fromSeason);
            var s = t * t * (3d - (2d * t));
            return fromValue + ((toValue - fromValue) * s);
        }

        return anchors[^1].Value;
    }
}
