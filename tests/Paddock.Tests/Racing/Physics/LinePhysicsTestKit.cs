using System.Security.Cryptography;
using Paddock.Data.Authored;
using Paddock.Domain.World.Tracks;
using Paddock.Simulation.Racing.Physics;
using Paddock.Tests.Racing.Points;

namespace Paddock.Tests.Racing.Physics;

/// <summary>Shared tracks for the v2 line tests: each line is solved once per test run (it takes a fraction of a second).</summary>
internal static class LinePhysicsTestKit
{
    private static readonly Lazy<TrackGeometryCatalog> Catalog = new(() => TrackGeometryCatalog.Create(PointsTestKit.Real));
    private static readonly Lazy<RacingLine> MonzaLine = new(() => RacingLine.Build(Geometry("monza_1972")));
    private static readonly Lazy<RacingLine> MonacoLine = new(() => RacingLine.Build(Geometry("monaco_1950")));

    /// <summary>The closest layout in the repo to PP-049's Monza prototype (5.8 km road course with chicanes, no Roggia).</summary>
    public static RacingLine Monza => MonzaLine.Value;

    /// <summary>The twisty layout of #353.</summary>
    public static RacingLine Monaco => MonacoLine.Value;

    public static ResolvedTrackGeometry Resolve(string layoutId) => Catalog.Value.Resolve(layoutId);

    public static TrackGeometry Geometry(string layoutId) => Resolve(layoutId).Geometry;

    /// <summary>A counter-clockwise circle of <paramref name="radius"/> metres (test input only; built with Sin/Cos here).</summary>
    public static TrackGeometry Circle(double radius, int points = 72)
    {
        var control = new (double X, double Y)[points];
        for (var i = 0; i < points; i++)
        {
            var angle = 2d * Math.PI * i / points;
            control[i] = (radius * Math.Cos(angle), radius * Math.Sin(angle));
        }

        return TrackGeometry.Build(control);
    }

    /// <summary>SHA-256 of the exact bits of every value, in order. Pinned in tests to catch any cross-OS drift.</summary>
    public static string Hash(IEnumerable<double> values)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> buffer = stackalloc byte[8];
        foreach (var value in values)
        {
            BitConverter.TryWriteBytes(buffer, BitConverter.DoubleToInt64Bits(value));
            if (!BitConverter.IsLittleEndian)
            {
                buffer.Reverse();
            }

            sha.AppendData(buffer);
        }

        return Convert.ToHexString(sha.GetHashAndReset());
    }

    public static IEnumerable<double> Tables(RacingLine line)
    {
        for (var i = 0; i < line.Count; i++)
        {
            yield return line.OffsetsM[i];
            yield return line.Points[i].X;
            yield return line.Points[i].Y;
            yield return line.Headings[i].X;
            yield return line.Headings[i].Y;
            yield return line.CurvaturePerM[i];
            yield return line.SegmentLengthsM[i];
        }
    }

    public static IEnumerable<double> Trace(LineLapTrace trace)
    {
        foreach (var speed in trace.SpeedsMs)
        {
            yield return speed;
        }

        yield return trace.LapSeconds;
        yield return trace.FullThrottleShare;
    }
}
