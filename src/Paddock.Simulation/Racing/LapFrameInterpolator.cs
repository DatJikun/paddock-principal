using Paddock.Domain.Racing;
using Paddock.Simulation.Racing.Pace;

namespace Paddock.Simulation.Racing;

/// <summary>
/// Display frames for the lap engine (PP-052). Positions are <see cref="FrameAccuracy.Approximate"/>:
/// each car moves along the lap at the speed implied by its lap time, diverts into the pit lane between
/// pit-in and pit-out, and under a safety car keeps the gap it had when the car was deployed.
/// No random draws and no trigonometry, so the same tape yields the same frames on every runtime.
/// Frames are not results. The numbers (sample interval, grid gap, pit-lane length, minimum separation) are ESTIMATES.
/// </summary>
public static class LapFrameInterpolator
{
    public const double DefaultSampleSeconds = 1.0;

    public const double MinSampleSeconds = 0.5;

    public const double MaxSampleSeconds = 1.0;

    /// <summary>ESTIMATE: metres between staged cars, so the dummy grid does not stack them.</summary>
    public const double GridGapM = 8;

    /// <summary>ESTIMATE: pit-lane length when the geometry has no pit polyline.</summary>
    public const double PitLaneLengthM = 400;

    /// <summary>ESTIMATE: closest two racing-line samples may be.</summary>
    public const double MinSeparationM = 1;

    /// <summary>
    /// Frames for <paramref name="tape"/>. With a <paramref name="shape"/> (#286) each car brakes for the corners and runs fast on
    /// the straights inside every lap; the lap still starts and ends when the tape says. Without one it moves at an even speed.
    /// </summary>
    public static RaceTape Attach(RaceTape tape, double lapLengthM, double sampleSeconds = DefaultSampleSeconds, LapSpeedShape? shape = null)
    {
        ArgumentNullException.ThrowIfNull(tape);
        if (lapLengthM <= 0 || double.IsNaN(lapLengthM) || double.IsInfinity(lapLengthM))
        {
            throw new ArgumentOutOfRangeException(nameof(lapLengthM), "Lap length must be a positive finite number of metres.");
        }

        if (sampleSeconds < MinSampleSeconds || sampleSeconds > MaxSampleSeconds || double.IsNaN(sampleSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(sampleSeconds), "The frame sample must be between 0.5 and 1 second.");
        }

        if (tape.Events.IsDefaultOrEmpty || tape.Events[0] is not RaceStarted started)
        {
            throw new ArgumentException("A tape needs a RaceStarted event before frames can be derived.", nameof(tape));
        }

        var cars = BuildPaths(tape, started, lapLengthM);
        ApplySafetyCars(tape, cars);
        var frames = Sample(cars, tape.Events[^1].RaceTime, sampleSeconds, lapLengthM, shape);
        return tape.WithFrames(frames, FrameAccuracy.Approximate);
    }

    private static List<CarPath> BuildPaths(RaceTape tape, RaceStarted started, double lapLengthM)
    {
        var cars = new List<CarPath>(started.DriverIds.Length);
        for (var index = 0; index < started.DriverIds.Length; index++)
        {
            var id = started.DriverIds[index];
            var path = new CarPath(id, -index * GridGapM);
            var laps = tape.Events.OfType<LapCompleted>().Where(e => e.DriverId == id).OrderBy(e => e.Lap).ToList();
            var stops = PitStops(tape, id);
            var tPrev = 0L;
            var dPrev = path.OriginM;
            foreach (var lap in laps)
            {
                var tEnd = lap.RaceTime;
                var dEnd = lap.Lap * lapLengthM;
                var stop = stops.FirstOrDefault(s => s.In > tPrev && s.In <= tEnd);
                if (stop.Out > stop.In)
                {
                    path.Pieces.Add(new Piece(tPrev, stop.In, dPrev, dEnd, false, 0, 0));
                    path.Pieces.Add(new Piece(stop.In, stop.Out, dEnd, dEnd, true, 0, PitLaneLengthM));
                    if (stop.Out < tEnd)
                    {
                        path.Pieces.Add(new Piece(stop.Out, tEnd, dEnd, dEnd, false, 0, 0));
                    }

                    tPrev = Math.Max(tEnd, stop.Out);
                }
                else
                {
                    path.Pieces.Add(new Piece(tPrev, tEnd, dPrev, dEnd, false, 0, 0));
                    tPrev = tEnd;
                }

                dPrev = dEnd;
            }

            var stopAt = tape.Events.OfType<Retirement>().Where(e => e.DriverId == id).Select(e => (long?)e.RaceTime).FirstOrDefault()
                ?? tape.Events.OfType<Finished>().Where(e => e.DriverId == id).Select(e => (long?)e.RaceTime).FirstOrDefault();
            if (stopAt is long cut)
            {
                path.StopMs = cut;
                Trim(path, cut);
            }

            cars.Add(path);
        }

        return cars;
    }

    private static List<(long In, long Out)> PitStops(RaceTape tape, string id)
    {
        var stops = new List<(long In, long Out)>();
        long? open = null;
        foreach (var pit in tape.Events.OfType<PitStop>())
        {
            if (pit.DriverId != id)
            {
                continue;
            }

            if (pit.Phase == PitLanePhase.In)
            {
                open = pit.RaceTime;
            }
            else if (open is long entered)
            {
                stops.Add((entered, pit.RaceTime));
                open = null;
            }
        }

        return stops;
    }

    private static void ApplySafetyCars(RaceTape tape, List<CarPath> cars)
    {
        long? open = null;
        foreach (var flag in tape.Events.OfType<SafetyCar>())
        {
            if (flag.Phase == SafetyCarPhase.Deployed)
            {
                open = flag.RaceTime;
            }
            else if (open is long start)
            {
                HoldGaps(cars, start, flag.RaceTime);
                open = null;
            }
        }
    }

    private static void HoldGaps(List<CarPath> cars, long start, long end)
    {
        if (end <= start)
        {
            return;
        }

        CarPath? leader = null;
        var leaderD = double.NegativeInfinity;
        foreach (var car in cars)
        {
            if (start >= car.StopMs || InPit(car, start))
            {
                continue;
            }

            var distance = At(car, start).Distance;
            if (distance > leaderD)
            {
                leaderD = distance;
                leader = car;
            }
        }

        if (leader is null)
        {
            return;
        }

        var leaderEnd = At(leader, end).Distance;
        foreach (var car in cars)
        {
            if (ReferenceEquals(car, leader) || start >= car.StopMs || InPit(car, start) || OverlapsPit(car, start, end))
            {
                continue;
            }

            var gap = leaderD - At(car, start).Distance;
            Splice(car, start, leaderD - gap, end, leaderEnd - gap);
        }
    }

    private static bool InPit(CarPath car, long time)
    {
        foreach (var piece in car.Pieces)
        {
            if (piece.Pit && time >= piece.T0 && time < piece.T1)
            {
                return true;
            }
        }

        return false;
    }

    private static bool OverlapsPit(CarPath car, long start, long end)
    {
        foreach (var piece in car.Pieces)
        {
            if (piece.Pit && piece.T1 > start && piece.T0 < end)
            {
                return true;
            }
        }

        return false;
    }

    private static void Splice(CarPath car, long start, double startDistance, long end, double endDistance)
    {
        if (endDistance < startDistance)
        {
            endDistance = startDistance;
        }

        var next = new List<Piece>(car.Pieces.Count + 1);
        Piece? tail = null;
        foreach (var piece in car.Pieces)
        {
            if (piece.T1 <= start || piece.T0 >= end)
            {
                next.Add(piece);
                continue;
            }

            if (piece.T0 < start)
            {
                next.Add(piece with { T1 = start, D1 = Lerp(piece, start) });
            }

            if (piece.T1 > end)
            {
                tail = piece with { T0 = end, D0 = Lerp(piece, end) };
            }
        }

        next.Add(new Piece(start, end, startDistance, endDistance, false, 0, 0));
        if (tail is { } rest)
        {
            next.Add(rest);
        }

        next.Sort(static (a, b) => a.T0.CompareTo(b.T0));
        for (var i = 0; i < next.Count; i++)
        {
            if (next[i].T0 == end && !next[i].Pit)
            {
                next[i] = Join(next[i], endDistance);
            }
        }
        car.Pieces.Clear();
        car.Pieces.AddRange(next);
    }

    private static Piece Join(Piece piece, double distance)
    {
        var joined = piece.D0 < distance ? piece with { D0 = distance } : piece;
        return joined.D1 < joined.D0 ? joined with { D1 = joined.D0 } : joined;
    }

    private static void Trim(CarPath car, long cut)
    {
        var next = new List<Piece>(car.Pieces.Count);
        foreach (var piece in car.Pieces)
        {
            if (piece.T0 >= cut)
            {
                break;
            }

            if (piece.T1 <= cut)
            {
                next.Add(piece);
                continue;
            }

            var distance = Lerp(piece, cut);
            var pit = piece.Pit ? LerpPit(piece, cut) : 0;
            next.Add(piece with { T1 = cut, D1 = distance, Pit1 = pit });
            break;
        }

        car.Pieces.Clear();
        car.Pieces.AddRange(next);
    }

    private static List<CarFrame> Sample(List<CarPath> cars, long endMs, double sampleSeconds, double lapLengthM, LapSpeedShape? shape)
    {
        var step = Math.Max(1L, (long)Math.Round(sampleSeconds * 1000d, MidpointRounding.AwayFromZero));
        var times = new List<long>();
        for (var t = 0L; t < endMs; t += step)
        {
            times.Add(t);
        }

        if (times.Count == 0 || times[^1] != endMs)
        {
            times.Add(endMs);
        }

        var previous = new double[cars.Count];
        for (var i = 0; i < cars.Count; i++)
        {
            previous[i] = cars[i].OriginM;
        }

        var frames = new List<CarFrame>(times.Count * cars.Count);
        foreach (var time in times)
        {
            var row = new List<Placed>(cars.Count);
            for (var i = 0; i < cars.Count; i++)
            {
                var car = cars[i];
                if (time > car.StopMs)
                {
                    continue;
                }

                var at = Shaped(At(car, time), lapLengthM, shape);
                if (at.Distance < previous[i])
                {
                    at = at with { Distance = previous[i], Speed = 0 };
                }

                row.Add(new Placed(i, car.Id, at.Distance, at.Speed, at.Pit, at.PitM));
            }

            Separate(row, previous);
            row.Sort(static (a, b) => string.CompareOrdinal(a.Id, b.Id));
            foreach (var placed in row)
            {
                previous[placed.Index] = placed.Distance;
                frames.Add(new CarFrame(time, placed.Id, placed.Distance, placed.Speed, placed.Pit, placed.PitM));
            }
        }

        return frames;
    }

    private static void Separate(List<Placed> row, double[] previous)
    {
        var line = row.Where(p => !p.Pit).OrderByDescending(p => p.Distance).ToList();
        for (var i = 1; i < line.Count; i++)
        {
            var ahead = line[i - 1];
            var behind = line[i];
            var limit = ahead.Distance - MinSeparationM;
            if (behind.Distance <= limit)
            {
                continue;
            }

            var nudged = limit < previous[behind.Index] ? previous[behind.Index] : limit;
            line[i] = behind with { Distance = nudged, Speed = 0 };
            var index = row.FindIndex(p => p.Index == behind.Index);
            row[index] = line[i];
        }
    }

    // The even run through a lap, bent by the shape: the share of the lap's time gone becomes a share of its distance.
    private static AtSample Shaped(AtSample at, double lapLengthM, LapSpeedShape? shape)
    {
        if (shape is null || at.Pit || at.Distance <= 0)
        {
            return at;
        }

        var lap = Math.Floor(at.Distance / lapLengthM);
        var share = (at.Distance - (lap * lapLengthM)) / lapLengthM;
        var distanceShare = shape.DistanceShareAt(share);
        return at with
        {
            Distance = (lap + distanceShare) * lapLengthM,
            Speed = at.Speed * shape.SpeedRatioAt(distanceShare),
        };
    }

    private static AtSample At(CarPath car, long time)
    {
        if (car.Pieces.Count == 0)
        {
            return new AtSample(car.OriginM, 0, false, 0);
        }

        var piece = car.Pieces[0];
        foreach (var candidate in car.Pieces)
        {
            if (candidate.T0 > time)
            {
                break;
            }

            piece = candidate;
        }

        if (time <= piece.T0)
        {
            return new AtSample(piece.D0, Speed(piece), piece.Pit, piece.Pit0);
        }

        if (time >= piece.T1)
        {
            return new AtSample(piece.D1, 0, piece.Pit && time == piece.T1, piece.Pit ? piece.Pit1 : 0);
        }

        return new AtSample(Lerp(piece, time), Speed(piece), piece.Pit, piece.Pit ? LerpPit(piece, time) : 0);
    }

    private static double Speed(Piece piece)
    {
        var span = piece.T1 - piece.T0;
        if (span <= 0)
        {
            return 0;
        }

        var metres = piece.Pit ? piece.Pit1 - piece.Pit0 : piece.D1 - piece.D0;
        return metres / (span / 1000d);
    }

    private static double Lerp(Piece piece, long time)
    {
        var span = piece.T1 - piece.T0;
        if (span <= 0)
        {
            return piece.D1;
        }

        var u = (time - piece.T0) / (double)span;
        if (u < 0)
        {
            u = 0;
        }
        else if (u > 1)
        {
            u = 1;
        }

        return piece.D0 + ((piece.D1 - piece.D0) * u);
    }

    private static double LerpPit(Piece piece, long time)
    {
        var span = piece.T1 - piece.T0;
        if (span <= 0)
        {
            return piece.Pit1;
        }

        var u = (time - piece.T0) / (double)span;
        return piece.Pit0 + ((piece.Pit1 - piece.Pit0) * u);
    }

    private sealed class CarPath(string id, double originM)
    {
        public string Id { get; } = id;

        public double OriginM { get; } = originM;

        public List<Piece> Pieces { get; } = [];

        public long StopMs { get; set; } = long.MaxValue;
    }

    private readonly record struct Piece(long T0, long T1, double D0, double D1, bool Pit, double Pit0, double Pit1);

    private readonly record struct AtSample(double Distance, double Speed, bool Pit, double PitM);

    private readonly record struct Placed(int Index, string Id, double Distance, double Speed, bool Pit, double PitM);
}
