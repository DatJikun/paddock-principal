using Paddock.Domain.Random;

namespace Paddock.Simulation.Racing.Qualifying;

/// <summary>
/// Simulates a qualifying weekend for an era's <see cref="QualifyingRules"/>. Pure function of its inputs: the only
/// randomness comes from the <c>LapNoise</c> stream derived from (seed, season, round), and the draws are laid out
/// by session, lap and entrant index, so they do not depend on who is still running, who was eliminated or on the
/// running order. Flagging one car as not running therefore does not re-roll any other car's luck (only the track
/// evolution it sees can shift slightly, because the lap sequence of the session gets shorter).
/// </summary>
/// <remarks>
/// A lap is: (base + pace) * wetness - evolution + loss, rounded to milliseconds. Evolution grows linearly with the
/// progress through the weekend (<see cref="QualifyingConstants.TrackEvolutionSeconds"/>, ESTIMATE) and the loss is
/// the driver's consistency-scaled half-normal noise. Ties on time go to the car that set the time earlier
/// (an increasing counter ticks once per simulated lap, lap by lap and in running order), then to the lower entry index.
/// </remarks>
public static class QualifyingSimulator
{
    private readonly record struct Timed(double Seconds, long Seq);

    public static QualifyingResult Run(
        QualifyingContext context,
        IReadOnlyList<QualifyingEntrant> entrants,
        QualifyingRules rules)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(entrants);
        ArgumentNullException.ThrowIfNull(rules);
        rules.Validate();
        ValidateInputs(context, entrants);

        return new Weekend(context, entrants, rules).Run();
    }

    private static void ValidateInputs(QualifyingContext context, IReadOnlyList<QualifyingEntrant> entrants)
    {
        if (!(context.BaseLapSeconds > 0d) || double.IsInfinity(context.BaseLapSeconds))
        {
            throw new ArgumentException("BaseLapSeconds must be a positive finite number.", nameof(context));
        }

        if (!(context.WetnessLapMultiplier >= 1d) || double.IsInfinity(context.WetnessLapMultiplier))
        {
            throw new ArgumentException("WetnessLapMultiplier must be a finite number of at least 1.", nameof(context));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in entrants)
        {
            if (!seen.Add(e.DriverId))
            {
                throw new ArgumentException($"Duplicate driver id '{e.DriverId}'.", nameof(entrants));
            }

            if (double.IsNaN(e.PaceSeconds) || double.IsInfinity(e.PaceSeconds)
                || (context.BaseLapSeconds + e.PaceSeconds) * context.WetnessLapMultiplier <= QualifyingConstants.TrackEvolutionSeconds)
            {
                throw new ArgumentException($"Pace of '{e.DriverId}' gives an impossible lap time.", nameof(entrants));
            }

            if (!(e.Consistency >= 0d && e.Consistency <= 1d))
            {
                throw new ArgumentException($"Consistency of '{e.DriverId}' must be within 0..1.", nameof(entrants));
            }
        }
    }

    private sealed class Weekend
    {
        private readonly QualifyingContext _ctx;
        private readonly IReadOnlyList<QualifyingEntrant> _entrants;
        private readonly QualifyingRules _rules;
        private readonly Xoshiro256StarStar _rng;
        private readonly List<SegmentTime>[] _segments;
        private readonly Timed?[] _overallBest;
        private long _seq;
        private int _simulatedLaps;

        public Weekend(QualifyingContext ctx, IReadOnlyList<QualifyingEntrant> entrants, QualifyingRules rules)
        {
            _ctx = ctx;
            _entrants = entrants;
            _rules = rules;
            _rng = RngStreams.Derive(ctx.Seed, RngStreamName.LapNoise, ctx.Season, ctx.Round);
            _segments = Enumerable.Range(0, entrants.Count).Select(_ => new List<SegmentTime>()).ToArray();
            _overallBest = new Timed?[entrants.Count];
        }

        public QualifyingResult Run()
        {
            int n = _entrants.Count;
            var notRunning = Enumerable.Range(0, n).Where(i => _entrants[i].CarNotRunning).ToList();
            var running = Enumerable.Range(0, n).Where(i => !_entrants[i].CarNotRunning).ToList();

            var failedPre = new List<QualifyingNonQualifier>();
            var preEntries = new List<PreQualifyingEntry>();
            if (_rules.PreQualifying is { } pre)
            {
                running = RunPreQualifying(pre, running, failedPre, preEntries);
            }

            List<int> ordered;
            var score = new Timed[n];
            var cutoffTime = new Timed[n];
            if (_rules.Format == QualifyingFormat.Knockout)
            {
                ordered = RunKnockout(running, score, cutoffTime);
            }
            else
            {
                ordered = RunBestOfOrAggregate(running, score);
                foreach (int i in running)
                {
                    cutoffTime[i] = score[i];
                }
            }

            var outsideCutoff = new List<QualifyingNonQualifier>();
            if (ApplyCutoff(ordered, cutoffTime) is { } outside)
            {
                outsideCutoff.AddRange(ordered.Where(outside.Contains).Select(i => NonQ(i, NonQualifierReason.OutsideCutoff)));
                ordered = ordered.Where(i => !outside.Contains(i)).ToList();
            }

            var gridLimit = new List<QualifyingNonQualifier>();
            if (_rules.MaxGridSize is { } cap && ordered.Count > cap)
            {
                gridLimit.AddRange(ordered.Skip(cap).Select(i => NonQ(i, NonQualifierReason.GridLimit)));
                ordered = ordered.Take(cap).ToList();
            }

            var grid = BuildGrid(ordered, score);
            var nonQualifiers = failedPre
                .Concat(outsideCutoff)
                .Concat(gridLimit)
                .Concat(notRunning.Select(i => NonQ(i, NonQualifierReason.CarNotRunning)))
                .ToArray();

            return new QualifyingResult(
                grid,
                nonQualifiers,
                preEntries,
                _simulatedLaps,
                QualifyingResult.BuildSummary(grid, nonQualifiers));
        }

        // ---- Pre-qualifying ---------------------------------------------------------------------------------

        private List<int> RunPreQualifying(
            PreQualifyingOptions pre,
            List<int> running,
            List<QualifyingNonQualifier> failed,
            List<PreQualifyingEntry> entries)
        {
            var flagged = running.Where(i => _entrants[i].InPreQualifying).ToList();
            if (flagged.Count == 0)
            {
                return running;
            }

            var spec = new QualifyingSessionSpec("PQ", pre.LapsPerDriver);
            var times = RunSession(spec, flagged, sessionIndex: 0, sessionCount: 1);
            var ranked = Rank(flagged, i => times[i]);
            var failedSet = new HashSet<int>();
            for (int r = 0; r < ranked.Count; r++)
            {
                int i = ranked[r];
                bool advanced = r < pre.AdvancingCount;
                entries.Add(new PreQualifyingEntry(_entrants[i].DriverId, times[i].Seconds, advanced));
                if (!advanced)
                {
                    failedSet.Add(i);
                    failed.Add(new QualifyingNonQualifier(
                        _entrants[i].DriverId,
                        _entrants[i].ConstructorId,
                        NonQualifierReason.FailedPreQualifying,
                        times[i].Seconds,
                        [new SegmentTime(spec.Id, times[i].Seconds)]));
                }
            }

            return running.Where(i => !failedSet.Contains(i)).ToList();
        }

        // ---- Single, two-day, shootout and aggregate formats ------------------------------------------------

        private List<int> RunBestOfOrAggregate(List<int> running, Timed[] score)
        {
            var specs = _rules.Sessions;
            Dictionary<int, Timed>? previous = null;
            var perSession = new List<Dictionary<int, Timed>>();
            for (int s = 0; s < specs.Count; s++)
            {
                var order = running;
                if (specs[s].RunningOrder == QualifyingRunningOrder.SlowestFirst && previous is not null)
                {
                    var prev = previous;
                    var ranking = Rank(running, i => prev[i]);
                    ranking.Reverse();
                    order = ranking;
                }

                var times = RunSession(specs[s], order, s, specs.Count);
                foreach (int i in running)
                {
                    Record(i, specs[s].Id, times[i]);
                }

                perSession.Add(times);
                previous = times;
            }

            foreach (int i in running)
            {
                var counted = specs
                    .Select((spec, s) => (spec, s))
                    .Where(x => x.spec.CountsForGrid)
                    .Select(x => perSession[x.s][i])
                    .ToList();
                score[i] = _rules.Format == QualifyingFormat.Aggregate
                    ? new Timed(RoundTime(counted.Sum(t => t.Seconds)), counted.Max(t => t.Seq))
                    : counted.OrderBy(t => t, TimedOrder).First();
            }

            return Rank(running, i => score[i]);
        }

        // ---- Knockout ---------------------------------------------------------------------------------------

        private List<int> RunKnockout(List<int> running, Timed[] score, Timed[] cutoffTime)
        {
            var specs = _rules.Sessions;
            var active = running.ToList();
            var eliminatedGroups = new List<List<int>>();
            for (int s = 0; s < specs.Count; s++)
            {
                var times = RunSession(specs[s], active, s, specs.Count);
                foreach (int i in active)
                {
                    Record(i, specs[s].Id, times[i]);
                    score[i] = times[i];
                    if (s == 0)
                    {
                        cutoffTime[i] = times[i];
                    }
                }

                var ranked = Rank(active, i => times[i]);
                if (s == specs.Count - 1)
                {
                    active = ranked;
                    break;
                }

                // Never eliminate everybody: at least one car goes through to the next segment.
                int cut = Math.Min(_rules.KnockoutEliminations[s], ranked.Count - 1);
                eliminatedGroups.Add(ranked.Skip(ranked.Count - cut).ToList());
                active = ranked.Take(ranked.Count - cut).ToList();
            }

            var ordered = new List<int>(active);
            for (int g = eliminatedGroups.Count - 1; g >= 0; g--)
            {
                ordered.AddRange(eliminatedGroups[g]);
            }

            return ordered;
        }

        // ---- Cutoff -----------------------------------------------------------------------------------------

        /// <summary>The cars outside the cutoff, in rank order, or null when the rule does not apply.</summary>
        private HashSet<int>? ApplyCutoff(List<int> ordered, Timed[] cutoffTime)
        {
            bool applies = _rules.Cutoff switch
            {
                QualifyingCutoff.None => false,
                QualifyingCutoff.WithinOfFastestQ1UnlessWet => !_ctx.DeclaredWet,
                _ => true,
            };
            if (!applies || ordered.Count == 0)
            {
                return null;
            }

            double reference = ordered.Min(i => cutoffTime[i].Seconds);
            double limit = reference * QualifyingConstants.CutoffFactor;
            var outside = ordered.Where(i => cutoffTime[i].Seconds > limit).ToHashSet();
            return outside.Count == 0 ? null : outside;
        }

        // ---- Grid -------------------------------------------------------------------------------------------

        private List<QualifyingGridSlot> BuildGrid(List<int> ordered, Timed[] score)
        {
            var onGrid = ordered.Where(i => !_entrants[i].StartsFromPitLane).ToList();
            var pitLane = ordered.Where(i => _entrants[i].StartsFromPitLane).ToList();
            var grid = new List<QualifyingGridSlot>(ordered.Count);
            for (int p = 0; p < onGrid.Count; p++)
            {
                grid.Add(Slot(onGrid[p], p + 1, score));
            }

            grid.AddRange(pitLane.Select(i => Slot(i, 0, score)));
            return grid;
        }

        private QualifyingGridSlot Slot(int i, int position, Timed[] score) => new(
            _entrants[i].DriverId,
            _entrants[i].ConstructorId,
            position,
            score[i].Seconds,
            _overallBest[i]!.Value.Seconds,
            _segments[i].ToArray());

        private QualifyingNonQualifier NonQ(int i, NonQualifierReason reason) => new(
            _entrants[i].DriverId,
            _entrants[i].ConstructorId,
            reason,
            _overallBest[i]?.Seconds,
            _segments[i].ToArray());

        // ---- Laps -------------------------------------------------------------------------------------------

        private void Record(int i, string segmentId, Timed time)
        {
            _segments[i].Add(new SegmentTime(segmentId, time.Seconds));
            if (_overallBest[i] is not { } best || Compare(time, best) < 0)
            {
                _overallBest[i] = time;
            }
        }

        /// <summary>
        /// Simulates one session for the participants (listed in running order) and returns each one's best lap.
        /// Draws are made for every entrant and lap first so the stream layout is fixed by the rules alone.
        /// </summary>
        private Dictionary<int, Timed> RunSession(QualifyingSessionSpec spec, List<int> participants, int sessionIndex, int sessionCount)
        {
            int n = _entrants.Count;
            int laps = spec.LapsPerDriver;
            var loss = new double[laps, n];
            for (int lap = 0; lap < laps; lap++)
            {
                for (int i = 0; i < n; i++)
                {
                    double u1 = _rng.NextDouble();
                    double u2 = _rng.NextDouble();
                    loss[lap, i] = Math.Abs(Math.Sqrt(-2d * Math.Log(1d - u1)) * Math.Cos(2d * Math.PI * u2));
                }
            }

            var best = new Dictionary<int, Timed>();
            int p = participants.Count;
            for (int lap = 0; lap < laps; lap++)
            {
                for (int r = 0; r < p; r++)
                {
                    int i = participants[r];
                    var e = _entrants[i];
                    _seq++;
                    _simulatedLaps++;

                    double progress = (sessionIndex + (((double)lap * p) + r) / ((double)laps * p)) / sessionCount;
                    double ideal = ((_ctx.BaseLapSeconds + e.PaceSeconds) * _ctx.WetnessLapMultiplier)
                        - (QualifyingConstants.TrackEvolutionSeconds * progress);
                    double scale = QualifyingConstants.NoiseScaleInconsistent
                        + ((QualifyingConstants.NoiseScaleConsistent - QualifyingConstants.NoiseScaleInconsistent) * e.Consistency);
                    var time = new Timed(RoundTime(ideal + (scale * loss[lap, i])), _seq);
                    if (!best.TryGetValue(i, out var current) || Compare(time, current) < 0)
                    {
                        best[i] = time;
                    }
                }
            }

            return best;
        }

        // ---- Helpers ----------------------------------------------------------------------------------------

        private static double RoundTime(double seconds) =>
            Math.Round(seconds, QualifyingConstants.TimeDecimals, MidpointRounding.AwayFromZero);

        /// <summary>Faster first; on equal time the one set earlier.</summary>
        private static int Compare(Timed a, Timed b)
        {
            int c = a.Seconds.CompareTo(b.Seconds);
            return c != 0 ? c : a.Seq.CompareTo(b.Seq);
        }

        private static readonly Comparer<Timed> TimedOrder = Comparer<Timed>.Create(Compare);

        /// <summary>Entry indices ordered by time; a remaining tie falls back to the lower entry index.</summary>
        private static List<int> Rank(IEnumerable<int> indices, Func<int, Timed> key) =>
            indices.OrderBy(key, TimedOrder).ThenBy(i => i).ToList();
    }
}
