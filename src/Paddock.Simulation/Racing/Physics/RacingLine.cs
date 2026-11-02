using System.Globalization;
using Paddock.Domain.World.Tracks;

namespace Paddock.Simulation.Racing.Physics;

/// <summary>
/// The minimum-curvature racing line of one track and its lookup tables (PP-074, TECH §6.5a). Built once per track;
/// the per-step physics only reads the tables.
/// </summary>
/// <remarks>
/// <para>Station <c>i</c> sits on the normal through centreline sample <c>i</c> of <see cref="TrackGeometry"/>; the line is
/// the centreline moved sideways by <see cref="OffsetsM"/> (positive = left of the direction of travel), never more than
/// <see cref="MaxOffsetM"/>.</para>
/// <para>The line is solved on coarse knots (<see cref="LinePhysicsConstants.LineKnotSpacingM"/>): it minimises the sum of
/// squared curvature, linearised in the centreline's frame, inside the width, by an active-set method over a dense
/// Cholesky solve; a Catmull-Rom curve through the knot offsets then fills the stations.</para>
/// <para>Determinism across operating systems: everything here, like <see cref="TrackGeometry"/> itself, uses only
/// <c>+ − × ÷</c> and <see cref="Math.Sqrt"/>, which IEEE 754 fixes to the last bit. Heading is kept as a unit vector,
/// not an angle, so no <c>Atan2</c>, <c>Sin</c> or <c>Cos</c> is needed even at load. A pure function of the geometry and
/// the width: no RNG, no world state.</para>
/// </remarks>
public sealed class RacingLine
{
    private readonly double[] _offsets;
    private readonly (double X, double Y)[] _points;
    private readonly (double X, double Y)[] _headings;
    private readonly double[] _curvature;
    private readonly double[] _segments;

    private RacingLine(
        double widthM,
        double maxOffsetM,
        double centerlineStepM,
        int knotCount,
        double[] offsets,
        (double X, double Y)[] points,
        (double X, double Y)[] headings,
        double[] curvature,
        double[] segments)
    {
        WidthM = widthM;
        MaxOffsetM = maxOffsetM;
        CenterlineStepM = centerlineStepM;
        KnotCount = knotCount;
        _offsets = offsets;
        _points = points;
        _headings = headings;
        _curvature = curvature;
        _segments = segments;

        var length = 0d;
        for (var i = 0; i < segments.Length; i++)
        {
            length += segments[i];
        }

        LengthM = length;
    }

    /// <summary>Stations on the lap (one per centreline sample).</summary>
    public int Count => _offsets.Length;

    /// <summary>Track width the line was solved for, m.</summary>
    public double WidthM { get; }

    /// <summary>Furthest the car centre may sit from the centreline, m.</summary>
    public double MaxOffsetM { get; }

    /// <summary>Distance between stations measured along the centreline, m.</summary>
    public double CenterlineStepM { get; }

    /// <summary>Coarse knots the line was solved on.</summary>
    public int KnotCount { get; }

    /// <summary>Length of the racing line, m (shorter than the centreline where it cuts the corners).</summary>
    public double LengthM { get; }

    /// <summary>Sideways offset of the line from the centreline at each station, m; positive is to the left.</summary>
    public IReadOnlyList<double> OffsetsM => _offsets;

    /// <summary>Line position at each station, m (x east, y north).</summary>
    public IReadOnlyList<(double X, double Y)> Points => _points;

    /// <summary>Direction of travel at each station as a unit vector (no angles, see remarks).</summary>
    public IReadOnlyList<(double X, double Y)> Headings => _headings;

    /// <summary>Signed curvature of the line at each station, 1/m; positive is a left turn.</summary>
    public IReadOnlyList<double> CurvaturePerM => _curvature;

    /// <summary>Distance along the line from station <c>i</c> to station <c>i + 1</c> (the last one closes the loop), m.</summary>
    public IReadOnlyList<double> SegmentLengthsM => _segments;

    /// <summary>
    /// The racing line of <paramref name="geometry"/> on a track <paramref name="widthM"/> wide (default: the constant
    /// ESTIMATE <see cref="LinePhysicsConstants.DefaultTrackWidthM"/> until the track file carries a width, #348).
    /// </summary>
    public static RacingLine Build(TrackGeometry geometry, double widthM = LinePhysicsConstants.DefaultTrackWidthM)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (!double.IsFinite(widthM) || widthM <= 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(widthM),
                widthM,
                $"Expected a finite width > 0, got {widthM.ToString("R", CultureInfo.InvariantCulture)}.");
        }

        var centre = geometry.Samples.ToArray();
        var n = centre.Length;
        if (n < 8)
        {
            throw new ArgumentException("The geometry needs at least 8 samples.", nameof(geometry));
        }

        var bound = Math.Max(0d, (widthM / 2d) - (LinePhysicsConstants.CarWidthM2010 / 2d) - LinePhysicsConstants.EdgeMarginM);
        var normals = Normals(centre);

        var knotCount = (int)Math.Round(geometry.LengthM / LinePhysicsConstants.LineKnotSpacingM);
        knotCount = Math.Clamp(knotCount, 8, Math.Min(LinePhysicsConstants.MaxLineKnots, n));

        var centreCurvature = geometry.Curvature;
        var knotCurvature = new double[knotCount];
        for (var j = 0; j < knotCount; j++)
        {
            // Knot j sits at sample position j·n/M, split exactly into an integer and a fraction.
            var scaled = (long)j * n;
            var baseIndex = (int)(scaled / knotCount);
            var fraction = (double)(scaled - ((long)baseIndex * knotCount)) / knotCount;
            var a = centreCurvature[baseIndex];
            var b = centreCurvature[(baseIndex + 1) % n];
            knotCurvature[j] = a + (fraction * (b - a));
        }

        var knotOffsets = MinimumCurvatureOffsets(knotCurvature, geometry.LengthM / knotCount, bound);

        var offsets = new double[n];
        var points = new (double X, double Y)[n];
        for (var i = 0; i < n; i++)
        {
            var scaled = (long)i * knotCount;
            var j = (int)(scaled / n);
            var f = (double)(scaled - ((long)j * n)) / n;
            var o = CatmullRom(
                knotOffsets[(j - 1 + knotCount) % knotCount],
                knotOffsets[j],
                knotOffsets[(j + 1) % knotCount],
                knotOffsets[(j + 2) % knotCount],
                f);
            o = Math.Clamp(o, -bound, bound);
            offsets[i] = o;
            points[i] = (centre[i].X + (o * normals[i].X), centre[i].Y + (o * normals[i].Y));
        }

        var headings = new (double X, double Y)[n];
        var segments = new double[n];
        for (var i = 0; i < n; i++)
        {
            var prev = points[(i - 1 + n) % n];
            var next = points[(i + 1) % n];
            headings[i] = Unit(next.X - prev.X, next.Y - prev.Y);
            var dx = next.X - points[i].X;
            var dy = next.Y - points[i].Y;
            segments[i] = Math.Sqrt((dx * dx) + (dy * dy));
        }

        var curvature = MengerCurvature(points, LinePhysicsConstants.CurvatureHalfWindow);
        return new RacingLine(widthM, bound, geometry.LengthM / n, knotCount, offsets, points, headings, curvature, segments);
    }

    /// <summary>Left normals of the centreline from a central difference.</summary>
    private static (double X, double Y)[] Normals((double X, double Y)[] centre)
    {
        var n = centre.Length;
        var normals = new (double X, double Y)[n];
        for (var i = 0; i < n; i++)
        {
            var prev = centre[(i - 1 + n) % n];
            var next = centre[(i + 1) % n];
            var t = Unit(next.X - prev.X, next.Y - prev.Y);
            normals[i] = (-t.Y, t.X);
        }

        return normals;
    }

    /// <summary>
    /// Knot offsets <c>n_j ∈ [−b, b]</c> minimising <c>Σ κ_line(j)² + λ·Σ n_j²</c> on a closed loop, with the line's curvature
    /// linearised in the centreline's frame: <c>κ_line ≈ κ + κ²·n + n''</c> (κ is the centreline curvature, positive left;
    /// n is positive left; <c>n''</c> is the second difference over the knot spacing). The <c>κ²·n</c> term is what makes
    /// the outside of a bend the gentler path; a plain second difference of positions would also reward the shorter
    /// inside line. Active set: solve the free offsets with the bound ones fixed, pin every free offset that crosses a
    /// bound, and when none does, free the bound offset whose gradient pulls it inward the hardest; stop when nothing
    /// changes.
    /// </summary>
    private static double[] MinimumCurvatureOffsets(double[] curvature, double spacing, double bound)
    {
        // Each residual is scaled by spacing² so the system's entries are of order one.
        var m = curvature.Length;
        var h = new double[m, m];
        var g = new double[m];
        var h2 = spacing * spacing;
        Span<int> knots = stackalloc int[3];
        Span<double> weights = stackalloc double[3];
        for (var j = 0; j < m; j++)
        {
            var kappa = curvature[j];
            knots[0] = (j - 1 + m) % m;
            knots[1] = j;
            knots[2] = (j + 1) % m;
            weights[0] = 1d;
            weights[1] = -2d + (kappa * kappa * h2);
            weights[2] = 1d;
            var constant = kappa * h2;
            for (var p = 0; p < 3; p++)
            {
                g[knots[p]] += weights[p] * constant;
                for (var q = 0; q < 3; q++)
                {
                    h[knots[p], knots[q]] += weights[p] * weights[q];
                }
            }
        }

        for (var j = 0; j < m; j++)
        {
            h[j, j] += LinePhysicsConstants.LineCentringWeight;
        }

        var offsets = new double[m];
        if (bound <= 0d)
        {
            return offsets;
        }

        // 0 = free, +1 = at the left bound, −1 = at the right bound.
        var state = new int[m];
        for (var round = 0; round < LinePhysicsConstants.MaxLineSolveRounds; round++)
        {
            var solution = SolveFree(h, g, state, offsets);
            var pinned = false;
            for (var k = 0; k < m; k++)
            {
                if (state[k] != 0)
                {
                    continue;
                }

                var x = solution[k];
                if (x > bound)
                {
                    state[k] = 1;
                    offsets[k] = bound;
                    pinned = true;
                }
                else if (x < -bound)
                {
                    state[k] = -1;
                    offsets[k] = -bound;
                    pinned = true;
                }
                else
                {
                    offsets[k] = x;
                }
            }

            if (pinned)
            {
                continue;
            }

            // Every free offset is inside: free the bound one that most wants to move inward, if any.
            var release = -1;
            var worst = 0d;
            for (var k = 0; k < m; k++)
            {
                if (state[k] == 0)
                {
                    continue;
                }

                var gradient = g[k];
                for (var l = 0; l < m; l++)
                {
                    gradient += h[k, l] * offsets[l];
                }

                // At the left bound the objective must not fall when n_k decreases (gradient ≤ 0), and vice versa.
                var pull = state[k] > 0 ? gradient : -gradient;
                if (pull > worst)
                {
                    worst = pull;
                    release = k;
                }
            }

            if (release < 0)
            {
                return offsets;
            }

            state[release] = 0;
        }

        // Did not settle: the last iterate is inside the bounds, which is a usable (if slightly worse) line.
        return offsets;
    }

    /// <summary>Solves <c>H_FF · x_F = −g_F − H_FA · n_A</c> by Cholesky; bound entries of the result keep their offsets.</summary>
    private static double[] SolveFree(double[,] h, double[] g, int[] state, double[] offsets)
    {
        var m = g.Length;
        var free = new List<int>(m);
        for (var k = 0; k < m; k++)
        {
            if (state[k] == 0)
            {
                free.Add(k);
            }
        }

        var result = (double[])offsets.Clone();
        var f = free.Count;
        if (f == 0)
        {
            return result;
        }

        var l = new double[f, f];
        var rhs = new double[f];
        for (var r = 0; r < f; r++)
        {
            var kr = free[r];
            var value = -g[kr];
            for (var k = 0; k < m; k++)
            {
                if (state[k] != 0)
                {
                    value -= h[kr, k] * offsets[k];
                }
            }

            rhs[r] = value;
            for (var c = 0; c <= r; c++)
            {
                l[r, c] = h[kr, free[c]];
            }
        }

        // In-place Cholesky of the lower triangle: H_FF = L·Lᵀ.
        for (var c = 0; c < f; c++)
        {
            var diagonal = l[c, c];
            for (var k = 0; k < c; k++)
            {
                diagonal -= l[c, k] * l[c, k];
            }

            if (!(diagonal > 0d))
            {
                throw new InvalidOperationException("The racing-line system is not positive definite.");
            }

            var root = Math.Sqrt(diagonal);
            l[c, c] = root;
            for (var r = c + 1; r < f; r++)
            {
                var value = l[r, c];
                for (var k = 0; k < c; k++)
                {
                    value -= l[r, k] * l[c, k];
                }

                l[r, c] = value / root;
            }
        }

        var y = new double[f];
        for (var r = 0; r < f; r++)
        {
            var value = rhs[r];
            for (var k = 0; k < r; k++)
            {
                value -= l[r, k] * y[k];
            }

            y[r] = value / l[r, r];
        }

        for (var r = f - 1; r >= 0; r--)
        {
            var value = y[r];
            for (var k = r + 1; k < f; k++)
            {
                value -= l[k, r] * result[free[k]];
            }

            result[free[r]] = value / l[r, r];
        }

        return result;
    }

    /// <summary>Uniform Catmull-Rom between <paramref name="p1"/> and <paramref name="p2"/> at <paramref name="t"/> in [0, 1).</summary>
    private static double CatmullRom(double p0, double p1, double p2, double p3, double t)
    {
        var t2 = t * t;
        var t3 = t2 * t;
        return 0.5d * ((2d * p1)
            + ((p2 - p0) * t)
            + (((2d * p0) - (5d * p1) + (4d * p2) - p3) * t2)
            + (((3d * p1) - p0 - (3d * p2) + p3) * t3));
    }

    /// <summary>Signed three-point (Menger) curvature over ±<paramref name="half"/> stations; positive is a left turn.</summary>
    private static double[] MengerCurvature((double X, double Y)[] points, int half)
    {
        var n = points.Length;
        var curvature = new double[n];
        for (var i = 0; i < n; i++)
        {
            var a = points[((i - half) % n + n) % n];
            var b = points[i];
            var c = points[(i + half) % n];
            var baX = b.X - a.X;
            var baY = b.Y - a.Y;
            var caX = c.X - a.X;
            var caY = c.Y - a.Y;
            var cbX = c.X - b.X;
            var cbY = c.Y - b.Y;
            var cross = (baX * caY) - (baY * caX);
            var denominator = Math.Sqrt((baX * baX) + (baY * baY))
                * Math.Sqrt((cbX * cbX) + (cbY * cbY))
                * Math.Sqrt((caX * caX) + (caY * caY));
            curvature[i] = denominator > 1e-12 ? 2d * cross / denominator : 0d;
        }

        return curvature;
    }

    private static (double X, double Y) Unit(double x, double y)
    {
        var length = Math.Sqrt((x * x) + (y * y));
        return length > 1e-12 ? (x / length, y / length) : (1d, 0d);
    }
}
