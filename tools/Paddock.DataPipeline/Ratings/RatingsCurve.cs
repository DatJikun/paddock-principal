namespace Paddock.DataPipeline;

public sealed record SeasonValue(int Season, double Value);

/// <summary>Career curve of one driver. <c>Ceiling</c> is an estimate (peak + 1 se of the raw peak-season skill).</summary>
public sealed record CareerCurve(
    int PeakSeason,
    double PeakValue,
    int? AgeAtPeak,
    double DeclineRate,
    double Ceiling,
    bool CeilingIsEstimate,
    IReadOnlyList<SeasonValue> Smoothed);

/// <summary>
/// Career curve smoothing: a penalised second-difference smoother (Whittaker-Eilers, i.e. a discrete
/// cubic-spline-like fit) over the driver's season grid. Seasons without a parameter get weight 0 and are
/// interpolated. Minimises sum (z - s)^2 + lambda * sum (second difference of z)^2.
/// </summary>
public static class RatingsCurveModel
{
    public const int MinSeasons = 4;

    public static double[] Smooth(IReadOnlyList<(int Season, double Skill, double Se)> seasons, double lambda)
    {
        ArgumentNullException.ThrowIfNull(seasons);
        if (seasons.Count == 0)
        {
            return [];
        }

        var first = seasons[0].Season;
        var m = seasons[^1].Season - first + 1;
        var y = new double[m];
        var w = new double[m];
        foreach (var (season, skill, _) in seasons)
        {
            y[season - first] = skill;
            w[season - first] = 1.0;
        }

        var a = new double[m, m];
        for (var i = 0; i < m; i++)
        {
            a[i, i] += w[i];
        }

        for (var k = 0; k + 2 < m; k++)
        {
            ReadOnlySpan<double> coef = [1.0, -2.0, 1.0];
            for (var p = 0; p < 3; p++)
            {
                for (var q = 0; q < 3; q++)
                {
                    a[k + p, k + q] += lambda * coef[p] * coef[q];
                }
            }
        }

        var rhs = new double[m];
        for (var i = 0; i < m; i++)
        {
            rhs[i] = w[i] * y[i];
        }

        return SolveSpd(a, rhs, m);
    }

    /// <summary>Builds the curve for a driver with at least <see cref="MinSeasons"/> seasons, otherwise null.</summary>
    public static CareerCurve? Build(
        IReadOnlyList<(int Season, double Skill, double Se)> seasons,
        int? birthYear,
        double lambda)
    {
        ArgumentNullException.ThrowIfNull(seasons);
        if (seasons.Count < MinSeasons)
        {
            return null;
        }

        var z = Smooth(seasons, lambda);
        var first = seasons[0].Season;

        var peakIdx = 0;
        for (var i = 1; i < z.Length; i++)
        {
            if (z[i] > z[peakIdx])
            {
                peakIdx = i;
            }
        }

        var peakSeason = first + peakIdx;

        // Mean yearly change after the peak (the telescoping mean of the consecutive differences).
        var lastIdx = z.Length - 1;
        var decline = lastIdx > peakIdx ? (z[lastIdx] - z[peakIdx]) / (lastIdx - peakIdx) : 0.0;

        // Ceiling = peak + 1 se, using the se of the observed season nearest to the peak (an estimate).
        var nearest = seasons
            .OrderBy(s => Math.Abs(s.Season - peakSeason))
            .ThenBy(s => s.Season)
            .First();

        var smoothed = new List<SeasonValue>(z.Length);
        for (var i = 0; i < z.Length; i++)
        {
            smoothed.Add(new SeasonValue(first + i, z[i]));
        }

        return new CareerCurve(
            peakSeason,
            z[peakIdx],
            birthYear is { } by ? peakSeason - by : null,
            decline,
            z[peakIdx] + nearest.Se,
            CeilingIsEstimate: true,
            smoothed);
    }

    private static double[] SolveSpd(double[,] a, double[] b, int n)
    {
        // Cholesky factorisation A = L L^T (A is symmetric positive definite for >= 2 observed seasons).
        var l = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j <= i; j++)
            {
                var sum = a[i, j];
                for (var k = 0; k < j; k++)
                {
                    sum -= l[i, k] * l[j, k];
                }

                l[i, j] = i == j ? Math.Sqrt(Math.Max(sum, 1e-12)) : sum / l[j, j];
            }
        }

        var t = new double[n];
        for (var i = 0; i < n; i++)
        {
            var sum = b[i];
            for (var k = 0; k < i; k++)
            {
                sum -= l[i, k] * t[k];
            }

            t[i] = sum / l[i, i];
        }

        var x = new double[n];
        for (var i = n - 1; i >= 0; i--)
        {
            var sum = t[i];
            for (var k = i + 1; k < n; k++)
            {
                sum -= l[k, i] * x[k];
            }

            x[i] = sum / l[i, i];
        }

        return x;
    }
}
