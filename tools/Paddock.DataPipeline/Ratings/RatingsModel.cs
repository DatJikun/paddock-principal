using System.Globalization;
using Paddock.Data.Historical;

namespace Paddock.DataPipeline;

public enum DuelKind
{
    Race,
    Qualifying,
}

public readonly record struct Duel(
    int Season,
    int Round,
    string ConstructorId,
    string WinnerDriverId,
    string LoserDriverId,
    double Weight,
    DuelKind Kind,
    string? LoserConstructorId = null);

public sealed record DriverSeasonParam(
    string DriverId,
    int Season,
    int Index);

public readonly record struct TimeLink(
    int CurrentIndex,
    int PreviousIndex,
    int GapYears);

public sealed record OptimizationResult(
    double[] S,
    double[] Se,
    int Iterations,
    bool Converged,
    double MaxGradient,
    double[]? C = null,
    double[]? CSe = null);

/// <summary>A constructor-season car-effect parameter; <c>Key</c> is the lineage key.</summary>
public sealed record CarSeasonParam(string Key, int Season, int Index);

/// <summary>Car-effect side of the v1 model: parameters, time links and penalties.</summary>
public sealed record CarModel(
    IReadOnlyList<CarSeasonParam> Params,
    IReadOnlyList<TimeLink> Links,
    IReadOnlyDictionary<(string ConstructorId, int Season), int> IndexByConstructorSeason,
    double LambdaC0,
    double LambdaCTime);

public sealed record FittedDriverMetrics(
    string DriverId,
    double CareerPeak,
    double PeakSe,
    string PeakYears,
    int TotalDuels,
    int RaceDuels,
    int QualifyingDuels,
    bool ShortCareer,
    int SeasonsCount,
    IReadOnlyList<(int Season, double Skill, double Se)> Seasons);

public static class RatingsModel
{
    public const double DefaultWRace = 1.0;
    public const double DefaultWQuali = 1.0;
    public const double DefaultLambdaTime = 2.0;
    public const double DefaultLambda0 = 0.01;

    // v1 constants. All are estimates until calibrated on the real data.
    public const double DefaultLambdaC0 = 0.05;
    public const double DefaultLambdaCTime = 1.0;
    public const double DefaultLambdaCurve = 3.0;
    public const int MaxIterations = 500;
    public const double ConvergenceTolerance = 1e-6;
    public const double LossStallTolerance = 1e-12;

    /// <summary>
    /// Extracts pairwise duels. Teammate duels (same constructor) are always produced. With
    /// <paramref name="includeCross"/>, every pair of drivers from different constructors in the same race also
    /// produces a duel (race and grid) with weight <c>wCross</c>, which is <c>1 / (fieldSize - 1)</c> by default
    /// (<paramref name="wCross"/> overrides it), so one race does not count as ~190 independent facts.
    /// Both kinds use the same filters: no Indianapolis 500, no shared drives, no non-starts.
    /// </summary>
    public static List<Duel> ExtractDuels(
        IEnumerable<HistoricalRace> races,
        IEnumerable<HistoricalResult> results,
        int fromYear,
        int toYear,
        double wRace = DefaultWRace,
        double wQuali = DefaultWQuali,
        bool includeCross = false,
        double? wCross = null)
    {
        ArgumentNullException.ThrowIfNull(races);
        ArgumentNullException.ThrowIfNull(results);

        var raceByKey = races
            .Where(r => r.Season >= fromYear && r.Season <= toYear && !r.IsIndianapolis500)
            .ToDictionary(r => (r.Season, r.Round));

        var validResults = results
            .Where(r => raceByKey.ContainsKey((r.Season, r.Round)))
            .Where(r => !r.IsSharedDrive)
            .Where(r => FinishStatus.Classify(r.Status) != FinishStatus.Kind.NonStart)
            .ToList();

        var grouped = validResults
            .GroupBy(r => (r.Season, r.Round, r.ConstructorId))
            .OrderBy(g => g.Key.Season)
            .ThenBy(g => g.Key.Round)
            .ThenBy(g => g.Key.ConstructorId, StringComparer.Ordinal);

        var duels = new List<Duel>();

        foreach (var group in grouped)
        {
            var driversInCar = group
                .OrderBy(r => r.DriverId, StringComparer.Ordinal)
                .ToList();

            for (var i = 0; i < driversInCar.Count; i++)
            {
                for (var j = i + 1; j < driversInCar.Count; j++)
                {
                    AddPairDuels(duels, driversInCar[i], driversInCar[j], wRace, wQuali, cross: false);
                }
            }
        }

        if (includeCross)
        {
            var races2 = validResults
                .GroupBy(r => (r.Season, r.Round))
                .OrderBy(g => g.Key.Season)
                .ThenBy(g => g.Key.Round);

            foreach (var race in races2)
            {
                var field = race
                    .OrderBy(r => r.ConstructorId, StringComparer.Ordinal)
                    .ThenBy(r => r.DriverId, StringComparer.Ordinal)
                    .ToList();

                var factor = wCross ?? (field.Count > 1 ? 1.0 / (field.Count - 1) : 1.0);

                for (var i = 0; i < field.Count; i++)
                {
                    for (var j = i + 1; j < field.Count; j++)
                    {
                        var a = field[i];
                        var b = field[j];
                        if (string.Equals(a.ConstructorId, b.ConstructorId, StringComparison.Ordinal)
                            || string.Equals(a.DriverId, b.DriverId, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        AddPairDuels(duels, a, b, wRace * factor, wQuali * factor, cross: true);
                    }
                }
            }
        }

        // Sort duels deterministically before fitting.
        duels.Sort((d1, d2) =>
        {
            var cmp = d1.Season.CompareTo(d2.Season);
            if (cmp != 0) return cmp;
            cmp = d1.Round.CompareTo(d2.Round);
            if (cmp != 0) return cmp;
            cmp = string.Compare(d1.ConstructorId, d2.ConstructorId, StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            cmp = string.Compare(d1.LoserConstructorId, d2.LoserConstructorId, StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            cmp = ((int)d1.Kind).CompareTo((int)d2.Kind);
            if (cmp != 0) return cmp;
            cmp = string.Compare(d1.WinnerDriverId, d2.WinnerDriverId, StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            return string.Compare(d1.LoserDriverId, d2.LoserDriverId, StringComparison.Ordinal);
        });

        return duels;
    }

    private static void AddPairDuels(
        List<Duel> duels,
        HistoricalResult a,
        HistoricalResult b,
        double wRace,
        double wQuali,
        bool cross)
    {
        void Emit(HistoricalResult winner, HistoricalResult loser, double weight, DuelKind kind)
        {
            duels.Add(new Duel(
                winner.Season,
                winner.Round,
                winner.ConstructorId,
                winner.DriverId,
                loser.DriverId,
                weight,
                kind,
                cross ? loser.ConstructorId : null));
        }

        // Race duel logic
        var isClassifiedA = a.IsClassified && FinishStatus.Classify(a.Status) == FinishStatus.Kind.ClassifiedFinish;
        var isClassifiedB = b.IsClassified && FinishStatus.Classify(b.Status) == FinishStatus.Kind.ClassifiedFinish;

        if (isClassifiedA && isClassifiedB)
        {
            if (a.Position < b.Position)
            {
                Emit(a, b, wRace, DuelKind.Race);
            }
            else if (b.Position < a.Position)
            {
                Emit(b, a, wRace, DuelKind.Race);
            }
        }
        else if (isClassifiedA && !isClassifiedB)
        {
            if (FinishStatus.Classify(b.Status) == FinishStatus.Kind.Accident)
            {
                Emit(a, b, wRace, DuelKind.Race);
            }
        }
        else if (!isClassifiedA && isClassifiedB)
        {
            if (FinishStatus.Classify(a.Status) == FinishStatus.Kind.Accident)
            {
                Emit(b, a, wRace, DuelKind.Race);
            }
        }

        // Qualifying duel logic (grid)
        if (a.Grid > 0 && b.Grid > 0)
        {
            if (a.Grid < b.Grid)
            {
                Emit(a, b, wQuali, DuelKind.Qualifying);
            }
            else if (b.Grid < a.Grid)
            {
                Emit(b, a, wQuali, DuelKind.Qualifying);
            }
        }
    }

    /// <summary>
    /// v0 entry point: driver-season skills only (no car effect). Kept so v0 behaviour stays reproducible.
    /// </summary>
    public static OptimizationResult Fit(
        IReadOnlyList<Duel> duels,
        IReadOnlyList<DriverSeasonParam> parameters,
        IReadOnlyList<TimeLink> timeLinks,
        double lambdaTime = DefaultLambdaTime,
        double lambda0 = DefaultLambda0)
    {
        return Fit(duels, parameters, timeLinks, lambdaTime, lambda0, null);
    }

    /// <summary>
    /// v1 fit: utility = s[d,t] + c[k,t]. Identifiability of s and c comes from teammate duels (the car
    /// cancels), driver transfers between constructors, and the ridge terms (lambdaC0 on c, lambda0 on s).
    /// </summary>
    public static OptimizationResult Fit(
        IReadOnlyList<Duel> duels,
        IReadOnlyList<DriverSeasonParam> parameters,
        IReadOnlyList<TimeLink> timeLinks,
        double lambdaTime,
        double lambda0,
        CarModel? car)
    {
        ArgumentNullException.ThrowIfNull(duels);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(timeLinks);

        var pCount = parameters.Count;
        var cCount = car?.Params.Count ?? 0;
        var n = pCount + cCount;
        if (pCount == 0)
        {
            return new OptimizationResult([], [], 0, true, 0.0, [], []);
        }

        var paramIndex = new Dictionary<(string DriverId, int Season), int>(pCount);
        for (var i = 0; i < pCount; i++)
        {
            paramIndex[(parameters[i].DriverId, parameters[i].Season)] = i;
        }

        var pairs = new Pair[duels.Count];
        for (var i = 0; i < duels.Count; i++)
        {
            var d = duels[i];
            var wCar = -1;
            var lCar = -1;
            if (car is not null && d.LoserConstructorId is not null)
            {
                wCar = pCount + car.IndexByConstructorSeason[(d.ConstructorId, d.Season)];
                lCar = pCount + car.IndexByConstructorSeason[(d.LoserConstructorId, d.Season)];
            }

            pairs[i] = new Pair(
                paramIndex[(d.WinnerDriverId, d.Season)],
                paramIndex[(d.LoserDriverId, d.Season)],
                wCar,
                lCar,
                d.Weight);
        }

        var links = new List<Link>(timeLinks.Count + (car?.Links.Count ?? 0));
        foreach (var l in timeLinks)
        {
            links.Add(new Link(l.CurrentIndex, l.PreviousIndex, lambdaTime / l.GapYears));
        }

        var ridge = new double[n];
        for (var i = 0; i < pCount; i++)
        {
            ridge[i] = lambda0;
        }

        if (car is not null)
        {
            foreach (var l in car.Links)
            {
                links.Add(new Link(pCount + l.CurrentIndex, pCount + l.PreviousIndex, car.LambdaCTime / l.GapYears));
            }

            for (var i = 0; i < cCount; i++)
            {
                ridge[pCount + i] = car.LambdaC0;
            }
        }

        var problem = new Problem(pairs, links.ToArray(), ridge);

        var s = new double[n];
        var g = new double[n];

        ComputeLossAndGradient(s, problem, out _, g);

        var initialMaxGrad = InfinityNorm(g);
        if (initialMaxGrad < ConvergenceTolerance)
        {
            return Finish(s, ComputeStandardErrors(s, problem), pCount, 0, true, initialMaxGrad);
        }

        const int mHistory = 10;
        var sHistory = new List<double[]>(mHistory);
        var yHistory = new List<double[]>(mHistory);
        var rhoHistory = new List<double>(mHistory);

        var iterations = 0;
        var converged = false;
        var currentMaxGrad = initialMaxGrad;

        for (var iter = 0; iter < MaxIterations; iter++)
        {
            iterations = iter + 1;

            var dir = ComputeSearchDirection(g, sHistory, yHistory, rhoHistory);

            var dirDotGrad = Dot(dir, g);
            if (dirDotGrad >= 0.0)
            {
                sHistory.Clear();
                yHistory.Clear();
                rhoHistory.Clear();
                for (var i = 0; i < n; i++)
                {
                    dir[i] = -g[i];
                }
                dirDotGrad = Dot(dir, g);
            }

            ComputeLossAndGradient(s, problem, out var currentLoss, null);

            var alpha = 1.0;
            const double c1 = 1e-4;
            var sNew = new double[n];
            var gNew = new double[n];
            var lineSearchSucceeded = false;
            var acceptedLoss = currentLoss;

            for (var ls = 0; ls < 40; ls++)
            {
                for (var i = 0; i < n; i++)
                {
                    sNew[i] = s[i] + alpha * dir[i];
                }

                ComputeLossAndGradient(sNew, problem, out var newLoss, gNew);

                if (newLoss <= currentLoss + c1 * alpha * dirDotGrad)
                {
                    lineSearchSucceeded = true;
                    acceptedLoss = newLoss;
                    break;
                }

                alpha *= 0.5;
            }

            if (!lineSearchSucceeded)
            {
                break;
            }

            currentMaxGrad = InfinityNorm(gNew);

            // The loss is flat along weakly identified directions (driver skill vs. car effect), where the
            // gradient norm shrinks slowly: also stop when a full step no longer changes the loss.
            var lossStalled = currentLoss - acceptedLoss <= LossStallTolerance * (1.0 + Math.Abs(currentLoss));
            if (currentMaxGrad < ConvergenceTolerance || lossStalled)
            {
                converged = true;
                Array.Copy(sNew, s, n);
                break;
            }

            var sDiff = new double[n];
            var yDiff = new double[n];
            for (var i = 0; i < n; i++)
            {
                sDiff[i] = sNew[i] - s[i];
                yDiff[i] = gNew[i] - g[i];
            }

            var sy = Dot(sDiff, yDiff);
            if (sy > 1e-12)
            {
                if (sHistory.Count == mHistory)
                {
                    sHistory.RemoveAt(0);
                    yHistory.RemoveAt(0);
                    rhoHistory.RemoveAt(0);
                }

                sHistory.Add(sDiff);
                yHistory.Add(yDiff);
                rhoHistory.Add(1.0 / sy);
            }

            Array.Copy(sNew, s, n);
            Array.Copy(gNew, g, n);
        }

        return Finish(s, ComputeStandardErrors(s, problem), pCount, iterations, converged, currentMaxGrad);
    }

    private static OptimizationResult Finish(double[] x, double[] se, int pCount, int iterations, bool converged, double maxGrad)
    {
        return new OptimizationResult(
            x[..pCount],
            se[..pCount],
            iterations,
            converged,
            maxGrad,
            x[pCount..],
            se[pCount..]);
    }

    private readonly record struct Pair(int W, int L, int WCar, int LCar, double Weight);

    private readonly record struct Link(int Cur, int Prev, double Coef);

    private sealed record Problem(Pair[] Duels, Link[] Links, double[] Ridge);

    private static void ComputeLossAndGradient(
        double[] x,
        Problem problem,
        out double loss,
        double[]? grad)
    {
        var n = x.Length;
        loss = 0.0;

        var ridge = problem.Ridge;
        for (var i = 0; i < n; i++)
        {
            loss += ridge[i] * x[i] * x[i];
            if (grad is not null)
            {
                grad[i] = 2.0 * ridge[i] * x[i];
            }
        }

        var duels = problem.Duels;
        for (var i = 0; i < duels.Length; i++)
        {
            var (w, l, wc, lc, weight) = duels[i];
            var uw = x[w] + (wc >= 0 ? x[wc] : 0.0);
            var ul = x[l] + (lc >= 0 ? x[lc] : 0.0);
            var diff = uw - ul;

            // One exp per duel: sigmoid and log-sigmoid share e = exp(-|diff|).
            var e = Math.Exp(-Math.Abs(diff));
            var log1pE = Math.Log(1.0 + e);
            loss += weight * (diff >= 0.0 ? log1pE : log1pE - diff);

            if (grad is not null)
            {
                var sig = diff >= 0.0 ? 1.0 / (1.0 + e) : e / (1.0 + e);
                var gFactor = weight * (sig - 1.0);
                grad[w] += gFactor;
                grad[l] -= gFactor;
                if (wc >= 0)
                {
                    grad[wc] += gFactor;
                }

                if (lc >= 0)
                {
                    grad[lc] -= gFactor;
                }
            }
        }

        var links = problem.Links;
        for (var i = 0; i < links.Length; i++)
        {
            var (cur, prev, coef) = links[i];
            var delta = x[cur] - x[prev];

            loss += coef * delta * delta;

            if (grad is not null)
            {
                var term = 2.0 * coef * delta;
                grad[cur] += term;
                grad[prev] -= term;
            }
        }
    }

    private static double[] ComputeStandardErrors(double[] x, Problem problem)
    {
        var n = x.Length;
        var hDiag = new double[n];

        for (var i = 0; i < n; i++)
        {
            hDiag[i] = 2.0 * problem.Ridge[i];
        }

        foreach (var (w, l, wc, lc, weight) in problem.Duels)
        {
            var diff = x[w] + (wc >= 0 ? x[wc] : 0.0) - x[l] - (lc >= 0 ? x[lc] : 0.0);
            var sig = Sigmoid(diff);
            var hTerm = weight * sig * (1.0 - sig);
            hDiag[w] += hTerm;
            hDiag[l] += hTerm;
            if (wc >= 0)
            {
                hDiag[wc] += hTerm;
            }

            if (lc >= 0)
            {
                hDiag[lc] += hTerm;
            }
        }

        foreach (var (cur, prev, coef) in problem.Links)
        {
            hDiag[cur] += 2.0 * coef;
            hDiag[prev] += 2.0 * coef;
        }

        var se = new double[n];
        for (var i = 0; i < n; i++)
        {
            // Uncertainty: se = 1 / sqrt(H[i][i]), the inverse square root of the Hessian diagonal
            // at the optimum (an approximation that ignores off-diagonal terms).
            se[i] = 1.0 / Math.Sqrt(Math.Max(hDiag[i], 1e-12));
        }

        return se;
    }

    private static double[] ComputeSearchDirection(
        double[] g,
        List<double[]> sHistory,
        List<double[]> yHistory,
        List<double> rhoHistory)
    {
        var n = g.Length;
        var k = sHistory.Count;
        if (k == 0)
        {
            var d = new double[n];
            for (var i = 0; i < n; i++) d[i] = -g[i];
            return d;
        }

        var q = (double[])g.Clone();
        var alphas = new double[k];

        for (var i = k - 1; i >= 0; i--)
        {
            var s_i = sHistory[i];
            var y_i = yHistory[i];
            var rho_i = rhoHistory[i];

            alphas[i] = rho_i * Dot(s_i, q);
            for (var j = 0; j < n; j++)
            {
                q[j] -= alphas[i] * y_i[j];
            }
        }

        var sLast = sHistory[^1];
        var yLast = yHistory[^1];
        var gamma = Dot(sLast, yLast) / Math.Max(Dot(yLast, yLast), 1e-12);

        var r = new double[n];
        for (var j = 0; j < n; j++)
        {
            r[j] = gamma * q[j];
        }

        for (var i = 0; i < k; i++)
        {
            var s_i = sHistory[i];
            var y_i = yHistory[i];
            var rho_i = rhoHistory[i];

            var beta = rho_i * Dot(y_i, r);
            var factor = alphas[i] - beta;
            for (var j = 0; j < n; j++)
            {
                r[j] += factor * s_i[j];
            }
        }

        var dir = new double[n];
        for (var j = 0; j < n; j++)
        {
            dir[j] = -r[j];
        }

        return dir;
    }

    private static double Dot(double[] a, double[] b)
    {
        var sum = 0.0;
        for (var i = 0; i < a.Length; i++)
        {
            sum += a[i] * b[i];
        }
        return sum;
    }

    private static double InfinityNorm(double[] v)
    {
        var max = 0.0;
        for (var i = 0; i < v.Length; i++)
        {
            var abs = Math.Abs(v[i]);
            if (abs > max)
            {
                max = abs;
            }
        }
        return max;
    }

    public static double Sigmoid(double x)
    {
        if (x >= 0.0)
        {
            var z = Math.Exp(-x);
            return 1.0 / (1.0 + z);
        }
        else
        {
            var z = Math.Exp(x);
            return z / (1.0 + z);
        }
    }

    public static List<List<string>> FindComponents(IEnumerable<Duel> duels, IEnumerable<string> allDrivers)
    {
        var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var driver in allDrivers)
        {
            adjacency[driver] = new HashSet<string>(StringComparer.Ordinal);
        }

        foreach (var duel in duels)
        {
            adjacency.TryAdd(duel.WinnerDriverId, new HashSet<string>(StringComparer.Ordinal));
            adjacency.TryAdd(duel.LoserDriverId, new HashSet<string>(StringComparer.Ordinal));
            adjacency[duel.WinnerDriverId].Add(duel.LoserDriverId);
            adjacency[duel.LoserDriverId].Add(duel.WinnerDriverId);
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var components = new List<List<string>>();

        var sortedNodes = adjacency.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

        foreach (var node in sortedNodes)
        {
            if (visited.Contains(node))
            {
                continue;
            }

            var comp = new List<string>();
            var queue = new Queue<string>();
            queue.Enqueue(node);
            visited.Add(node);

            while (queue.Count > 0)
            {
                var curr = queue.Dequeue();
                comp.Add(curr);

                foreach (var neighbor in adjacency[curr].OrderBy(x => x, StringComparer.Ordinal))
                {
                    if (visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            comp.Sort(StringComparer.Ordinal);
            components.Add(comp);
        }

        components.Sort((c1, c2) =>
        {
            var cmp = c2.Count.CompareTo(c1.Count);
            if (cmp != 0) return cmp;
            return string.Compare(c1[0], c2[0], StringComparison.Ordinal);
        });

        return components;
    }

    public static Dictionary<string, FittedDriverMetrics> CalculateDriverMetrics(
        IReadOnlyList<Duel> duels,
        IReadOnlyList<DriverSeasonParam> parameters,
        OptimizationResult fit)
    {
        var raceCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var qualiCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var d in duels)
        {
            if (d.LoserConstructorId is not null)
            {
                // Cross-constructor duels feed the fit but are not "teammate evidence" for the ranked set.
                continue;
            }

            if (d.Kind == DuelKind.Race)
            {
                raceCounts[d.WinnerDriverId] = raceCounts.GetValueOrDefault(d.WinnerDriverId) + 1;
                raceCounts[d.LoserDriverId] = raceCounts.GetValueOrDefault(d.LoserDriverId) + 1;
            }
            else
            {
                qualiCounts[d.WinnerDriverId] = qualiCounts.GetValueOrDefault(d.WinnerDriverId) + 1;
                qualiCounts[d.LoserDriverId] = qualiCounts.GetValueOrDefault(d.LoserDriverId) + 1;
            }
        }

        var byDriver = parameters
            .GroupBy(p => p.DriverId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(p => p.Season).Select(p => (p.Season, fit.S[p.Index], fit.Se[p.Index])).ToList(),
                StringComparer.Ordinal);

        var result = new Dictionary<string, FittedDriverMetrics>(byDriver.Count, StringComparer.Ordinal);

        foreach (var (driverId, seasons) in byDriver)
        {
            var rDuels = raceCounts.GetValueOrDefault(driverId);
            var qDuels = qualiCounts.GetValueOrDefault(driverId);
            var totalDuels = rDuels + qDuels;

            var (peak, peakSe, peakYears, shortCareer) = CalculatePeak(seasons);

            result[driverId] = new FittedDriverMetrics(
                driverId,
                peak,
                peakSe,
                peakYears,
                totalDuels,
                rDuels,
                qDuels,
                shortCareer,
                seasons.Count,
                seasons);
        }

        return result;
    }

    public static (double Peak, double PeakSe, string PeakYears, bool ShortCareer) CalculatePeak(
        IReadOnlyList<(int Season, double Skill, double Se)> seasons)
    {
        if (seasons.Count == 0)
        {
            return (0.0, 0.0, string.Empty, true);
        }

        if (seasons.Count == 1)
        {
            var s = seasons[0];
            return (s.Skill, s.Se, s.Season.ToString(CultureInfo.InvariantCulture), true);
        }

        if (seasons.Count == 2)
        {
            var s0 = seasons[0];
            var s1 = seasons[1];
            var avg = (s0.Skill + s1.Skill) / 2.0;
            var se = Math.Sqrt(s0.Se * s0.Se + s1.Se * s1.Se) / 2.0;
            var years = FormatYears([s0.Season, s1.Season]);
            return (avg, se, years, true);
        }

        var bestAvg = double.NegativeInfinity;
        var bestSe = 0.0;
        var bestYears = string.Empty;

        for (var i = 0; i <= seasons.Count - 3; i++)
        {
            var s0 = seasons[i];
            var s1 = seasons[i + 1];
            var s2 = seasons[i + 2];

            var avg = (s0.Skill + s1.Skill + s2.Skill) / 3.0;
            if (avg > bestAvg)
            {
                bestAvg = avg;
                bestSe = Math.Sqrt(s0.Se * s0.Se + s1.Se * s1.Se + s2.Se * s2.Se) / 3.0;
                bestYears = FormatYears([s0.Season, s1.Season, s2.Season]);
            }
        }

        return (bestAvg, bestSe, bestYears, false);
    }

    private static string FormatYears(int[] years)
    {
        if (years.Length == 1)
        {
            return years[0].ToString(CultureInfo.InvariantCulture);
        }

        var isConsecutive = true;
        for (var i = 1; i < years.Length; i++)
        {
            if (years[i] != years[i - 1] + 1)
            {
                isConsecutive = false;
                break;
            }
        }

        if (isConsecutive)
        {
            return $"{years[0].ToString(CultureInfo.InvariantCulture)}-{years[^1].ToString(CultureInfo.InvariantCulture)}";
        }

        return string.Join(", ", years.Select(y => y.ToString(CultureInfo.InvariantCulture)));
    }

    public static double SpearmanCorrelation(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        if (x.Count != y.Count || x.Count < 2)
        {
            return 0.0;
        }

        var rankX = ComputeRanks(x);
        var rankY = ComputeRanks(y);
        return PearsonCorrelation(rankX, rankY);
    }

    private static double[] ComputeRanks(IReadOnlyList<double> values)
    {
        var n = values.Count;
        var indices = Enumerable.Range(0, n).ToArray();
        Array.Sort(indices, (i1, i2) => values[i1].CompareTo(values[i2]));

        var ranks = new double[n];
        var i = 0;
        while (i < n)
        {
            var j = i;
            while (j < n - 1 && Math.Abs(values[indices[j]] - values[indices[j + 1]]) < 1e-12)
            {
                j++;
            }

            var groupRank = (i + 1 + j + 1) / 2.0;
            for (var k = i; k <= j; k++)
            {
                ranks[indices[k]] = groupRank;
            }

            i = j + 1;
        }

        return ranks;
    }

    private static double PearsonCorrelation(double[] x, double[] y)
    {
        var n = x.Length;
        var meanX = 0.0;
        var meanY = 0.0;
        for (var i = 0; i < n; i++)
        {
            meanX += x[i];
            meanY += y[i];
        }
        meanX /= n;
        meanY /= n;

        var cov = 0.0;
        var varX = 0.0;
        var varY = 0.0;

        for (var i = 0; i < n; i++)
        {
            var dx = x[i] - meanX;
            var dy = y[i] - meanY;
            cov += dx * dy;
            varX += dx * dx;
            varY += dy * dy;
        }

        if (varX < 1e-12 || varY < 1e-12)
        {
            return 0.0;
        }

        return cov / Math.Sqrt(varX * varY);
    }
}
