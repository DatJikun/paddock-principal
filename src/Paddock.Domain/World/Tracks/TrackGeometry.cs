using System.Globalization;
using System.Text;

namespace Paddock.Domain.World.Tracks;

/// <summary>
/// Closed track layout geometry reconstructed from control points using a centripetal Catmull-Rom spline.
/// Provides arc-length resampled points, signed curvature, and SVG path rendering.
/// </summary>
public sealed class TrackGeometry
{
    private TrackGeometry(
        double lengthM,
        double scaleFactor,
        IReadOnlyList<(double X, double Y)> samples,
        IReadOnlyList<double> curvature)
    {
        LengthM = lengthM;
        ScaleFactor = scaleFactor;
        Samples = samples;
        Curvature = curvature;
    }

    public double LengthM { get; }

    public double ScaleFactor { get; }

    public IReadOnlyList<(double X, double Y)> Samples { get; }

    public IReadOnlyList<double> Curvature { get; }

    /// <summary>
    /// Builds a closed track geometry from control points.
    /// </summary>
    /// <param name="controlPoints">Control points in metres (x east, y north).</param>
    /// <param name="referenceLengthM">Optional target track length in metres to scale the geometry to.</param>
    /// <param name="stepM">Nominal sampling distance in metres along the arc length (default 2.0 m).</param>
    public static TrackGeometry Build(
        IReadOnlyList<(double X, double Y)> controlPoints,
        double? referenceLengthM = null,
        double stepM = 2.0)
    {
        ArgumentNullException.ThrowIfNull(controlPoints);
        if (controlPoints.Count < 3)
        {
            throw new ArgumentException("At least 3 control points are required to build a closed spline.", nameof(controlPoints));
        }

        if (stepM <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(stepM), "Sampling step must be positive.");
        }

        if (referenceLengthM is <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(referenceLengthM), "Reference length must be positive.");
        }

        // 1. Build dense polygon for raw control points to measure unscaled arc length
        var (rawPoints, rawLengths) = SampleSplineDense(controlPoints);
        var rawLengthM = rawLengths[^1];

        var scaleFactor = referenceLengthM.HasValue ? referenceLengthM.Value / rawLengthM : 1.0;
        var finalLengthM = referenceLengthM ?? rawLengthM;

        // 2. If scaled, reconstruct dense points from scaled control points
        IReadOnlyList<(double X, double Y)> densePoints;
        IReadOnlyList<double> denseLengths;
        if (Math.Abs(scaleFactor - 1.0) > 1e-12)
        {
            var scaledControlPoints = new (double X, double Y)[controlPoints.Count];
            for (var i = 0; i < controlPoints.Count; i++)
            {
                scaledControlPoints[i] = (controlPoints[i].X * scaleFactor, controlPoints[i].Y * scaleFactor);
            }

            (densePoints, denseLengths) = SampleSplineDense(scaledControlPoints);
        }
        else
        {
            densePoints = rawPoints;
            denseLengths = rawLengths;
        }

        // 3. Arc-length resampling every stepM metres along the closed loop
        var sampleCount = Math.Max(8, (int)Math.Round(finalLengthM / stepM));
        var ds = finalLengthM / sampleCount;

        var samples = new (double X, double Y)[sampleCount];
        var denseIndex = 0;
        var totalDense = densePoints.Count;

        for (var k = 0; k < sampleCount; k++)
        {
            var targetDist = k * ds;
            while (denseIndex < totalDense - 1 && denseLengths[denseIndex + 1] < targetDist)
            {
                denseIndex++;
            }

            if (denseIndex >= totalDense - 1)
            {
                samples[k] = densePoints[^1];
            }
            else
            {
                var segStart = denseLengths[denseIndex];
                var segEnd = denseLengths[denseIndex + 1];
                var span = segEnd - segStart;
                var factor = span > 1e-12 ? (targetDist - segStart) / span : 0.0;
                var pA = densePoints[denseIndex];
                var pB = densePoints[denseIndex + 1];
                samples[k] = (pA.X + factor * (pB.X - pA.X), pA.Y + factor * (pB.Y - pA.Y));
            }
        }

        // 4. Compute curvature for each sample using Menger curvature over +/- 6 samples
        var curvature = ComputeCurvature(samples);

        return new TrackGeometry(finalLengthM, scaleFactor, samples, curvature);
    }

    /// <summary>
    /// Neutral stadium-shaped layout (two straights of 3 r joined by two semicircles of radius r, counter-clockwise)
    /// scaled to <paramref name="lengthM"/>. This is the defined fallback for a layout that has no authored geometry
    /// file: it keeps lap length and start/finish conventions valid, but it says nothing about the real shape.
    /// Built from literal sine/cosine constants only, so it is identical on every platform.
    /// </summary>
    public static TrackGeometry Fallback(double lengthM, double stepM = 2.0)
    {
        if (lengthM <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(lengthM), "Length must be positive.");
        }

        // Unit stadium with r = 1: straights are 3 long, bends are semicircles; Build scales it to lengthM.
        (double Cos, double Sin)[] arc =
        [
            (0.3826834323650898, -0.9238795325112867),
            (0.7071067811865476, -0.7071067811865476),
            (0.9238795325112867, -0.3826834323650898),
            (1.0, 0.0),
            (0.9238795325112867, 0.3826834323650898),
            (0.7071067811865476, 0.7071067811865476),
            (0.3826834323650898, 0.9238795325112867),
        ];

        var points = new List<(double X, double Y)>(20)
        {
            (-1.5, -1.0),
            (0.0, -1.0),
            (1.5, -1.0),
        };

        foreach (var (cos, sin) in arc)
        {
            points.Add((1.5 + cos, sin));
        }

        points.Add((1.5, 1.0));
        points.Add((0.0, 1.0));
        points.Add((-1.5, 1.0));

        for (var i = arc.Length - 1; i >= 0; i--)
        {
            points.Add((-1.5 - arc[i].Cos, arc[i].Sin));
        }

        return Build(points, lengthM, stepM);
    }

    /// <summary>
    /// Renders the path as an SVG string ("M … L … Z") fitted into a square view box with a margin.
    /// </summary>
    public string SvgPath(double viewBoxSize)
    {
        if (viewBoxSize <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewBoxSize), "View box size must be positive.");
        }

        if (Samples.Count == 0)
        {
            return string.Empty;
        }

        var minX = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var minY = double.PositiveInfinity;
        var maxY = double.NegativeInfinity;

        foreach (var (x, y) in Samples)
        {
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        var width = maxX - minX;
        var height = maxY - minY;
        var maxDim = Math.Max(width, height);

        var margin = viewBoxSize * 0.05;
        var usableSize = viewBoxSize - 2.0 * margin;
        var scale = maxDim > 1e-9 ? usableSize / maxDim : 1.0;

        var midX = (minX + maxX) / 2.0;
        var midY = (minY + maxY) / 2.0;
        var centerView = viewBoxSize / 2.0;

        var sb = new StringBuilder();
        for (var i = 0; i < Samples.Count; i++)
        {
            var (x, y) = Samples[i];
            var svgX = centerView + (x - midX) * scale;
            // Invert Y so North is up
            var svgY = centerView - (y - midY) * scale;

            if (i == 0)
            {
                sb.Append(CultureInfo.InvariantCulture, $"M {svgX:F2} {svgY:F2}");
            }
            else
            {
                sb.Append(CultureInfo.InvariantCulture, $" L {svgX:F2} {svgY:F2}");
            }
        }

        sb.Append(" Z");
        return sb.ToString();
    }

    private static (List<(double X, double Y)> Points, List<double> Lengths) SampleSplineDense(
        IReadOnlyList<(double X, double Y)> controlPoints)
    {
        var n = controlPoints.Count;
        var points = new List<(double X, double Y)>();
        var lengths = new List<double>();

        points.Add(controlPoints[0]);
        lengths.Add(0.0);

        var cumDist = 0.0;
        for (var i = 0; i < n; i++)
        {
            var p0 = controlPoints[(i - 1 + n) % n];
            var p1 = controlPoints[i];
            var p2 = controlPoints[(i + 1) % n];
            var p3 = controlPoints[(i + 2) % n];

            var chordDist = Math.Sqrt((p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y));
            // Micro-step size <= 0.05 m for sub-millimetre arc length accuracy
            var steps = Math.Max(20, (int)Math.Ceiling(chordDist / 0.05));

            var prevPt = points[^1];
            for (var m = 1; m <= steps; m++)
            {
                var u = (double)m / steps;
                var pt = EvaluateCentripetalCatmullRom(p0, p1, p2, p3, u);
                var segDist = Math.Sqrt((pt.X - prevPt.X) * (pt.X - prevPt.X) + (pt.Y - prevPt.Y) * (pt.Y - prevPt.Y));
                cumDist += segDist;

                points.Add(pt);
                lengths.Add(cumDist);
                prevPt = pt;
            }
        }

        return (points, lengths);
    }

    private static (double X, double Y) EvaluateCentripetalCatmullRom(
        (double X, double Y) p0,
        (double X, double Y) p1,
        (double X, double Y) p2,
        (double X, double Y) p3,
        double u)
    {
        var d01 = Math.Sqrt((p1.X - p0.X) * (p1.X - p0.X) + (p1.Y - p0.Y) * (p1.Y - p0.Y));
        var d12 = Math.Sqrt((p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y));
        var d23 = Math.Sqrt((p3.X - p2.X) * (p3.X - p2.X) + (p3.Y - p2.Y) * (p3.Y - p2.Y));

        // Centripetal: alpha = 0.5 -> t_step = sqrt(d)
        var t0 = 0.0;
        var t1 = t0 + Math.Sqrt(d01);
        var t2 = t1 + Math.Sqrt(d12);
        var t3 = t2 + Math.Sqrt(d23);

        var t = t1 + u * (t2 - t1);

        var dt01 = t1 - t0;
        var dt12 = t2 - t1;
        var dt23 = t3 - t2;

        (double X, double Y) a1 = dt01 > 1e-12
            ? (((t1 - t) * p0.X + (t - t0) * p1.X) / dt01, ((t1 - t) * p0.Y + (t - t0) * p1.Y) / dt01)
            : p1;

        (double X, double Y) a2 = dt12 > 1e-12
            ? (((t2 - t) * p1.X + (t - t1) * p2.X) / dt12, ((t2 - t) * p1.Y + (t - t1) * p2.Y) / dt12)
            : p1;

        (double X, double Y) a3 = dt23 > 1e-12
            ? (((t3 - t) * p2.X + (t - t2) * p3.X) / dt23, ((t3 - t) * p2.Y + (t - t2) * p3.Y) / dt23)
            : p2;

        var dt02 = t2 - t0;
        var dt13 = t3 - t1;

        (double X, double Y) b1 = dt02 > 1e-12
            ? (((t2 - t) * a1.X + (t - t0) * a2.X) / dt02, ((t2 - t) * a1.Y + (t - t0) * a2.Y) / dt02)
            : a2;

        (double X, double Y) b2 = dt13 > 1e-12
            ? (((t3 - t) * a2.X + (t - t1) * a3.X) / dt13, ((t3 - t) * a2.Y + (t - t1) * a3.Y) / dt13)
            : a2;

        return dt12 > 1e-12
            ? (((t2 - t) * b1.X + (t - t1) * b2.X) / dt12, ((t2 - t) * b1.Y + (t - t1) * b2.Y) / dt12)
            : p1;
    }

    /// <summary>
    /// Menger curvature from samples i−6, i, and i+6:
    /// κ = 2·cross(b−a, c−a) / (|b−a|·|c−b|·|c−a|).
    /// Positive is a left turn. Only +, −, ×, ÷ and <see cref="Math.Sqrt"/> — no Atan2.
    /// </summary>
    private static double[] ComputeCurvature((double X, double Y)[] samples)
    {
        var count = samples.Length;
        var curvature = new double[count];

        for (var i = 0; i < count; i++)
        {
            var a = samples[(i - 6 + count) % count];
            var b = samples[i];
            var c = samples[(i + 6) % count];

            var baX = b.X - a.X;
            var baY = b.Y - a.Y;
            var caX = c.X - a.X;
            var caY = c.Y - a.Y;
            var cbX = c.X - b.X;
            var cbY = c.Y - b.Y;

            var cross = baX * caY - baY * caX;
            var dAB = Math.Sqrt(baX * baX + baY * baY);
            var dCB = Math.Sqrt(cbX * cbX + cbY * cbY);
            var dCA = Math.Sqrt(caX * caX + caY * caY);

            var denom = dAB * dCB * dCA;
            curvature[i] = denom > 1e-12 ? 2.0 * cross / denom : 0.0;
        }

        return curvature;
    }
}
