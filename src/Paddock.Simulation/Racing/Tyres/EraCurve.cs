namespace Paddock.Simulation.Racing.Tyres;

/// <summary>Smoothstep interpolation between season anchors, flat outside the first and last anchor.</summary>
internal static class EraCurve
{
    public static double Interpolate(IReadOnlyList<(int Season, double Value)> anchors, int season)
    {
        if (season <= anchors[0].Season)
        {
            return anchors[0].Value;
        }

        for (var i = 1; i < anchors.Count; i++)
        {
            if (season <= anchors[i].Season)
            {
                var (s0, v0) = anchors[i - 1];
                var (s1, v1) = anchors[i];
                var t = (double)(season - s0) / (s1 - s0);
                var eased = t * t * (3 - 2 * t);
                return v0 + (v1 - v0) * eased;
            }
        }

        return anchors[^1].Value;
    }
}
