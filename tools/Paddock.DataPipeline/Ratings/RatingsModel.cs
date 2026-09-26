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
    DuelKind Kind);

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
    double MaxGradient);

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
    public const int MaxIterations = 500;
    public const double ConvergenceTolerance = 1e-8;

    public static List<Duel> ExtractDuels(
        IEnumerable<HistoricalRace> races,
        IEnumerable<HistoricalResult> results,
        int fromYear,
        int toYear,
        double wRace = DefaultWRace,
        double wQuali = DefaultWQuali)
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

            if (driversInCar.Count < 2)
            {
                continue;
            }

            var season = group.Key.Season;
            var round = group.Key.Round;
            var constructorId = group.Key.ConstructorId;

            for (var i = 0; i < driversInCar.Count; i++)
            {
                for (var j = i + 1; j < driversInCar.Count; j++)
                {
                    var a = driversInCar[i];
                    var b = driversInCar[j];

                    // Race duel logic
                    var isClassifiedA = a.IsClassified && FinishStatus.Classify(a.Status) == FinishStatus.Kind.ClassifiedFinish;
                    var isClassifiedB = b.IsClassified && FinishStatus.Classify(b.Status) == FinishStatus.Kind.ClassifiedFinish;

                    if (isClassifiedA && isClassifiedB)
                    {
                        if (a.Position < b.Position)
                        {
                            duels.Add(new Duel(season, round, constructorId, a.DriverId, b.DriverId, wRace, DuelKind.Race));
                        }
                        else if (b.Position < a.Position)
                        {
                            duels.Add(new Duel(season, round, constructorId, b.DriverId, a.DriverId, wRace, DuelKind.Race));
                        }
                    }
                    else if (isClassifiedA && !isClassifiedB)
                    {
                        if (FinishStatus.Classify(b.Status) == FinishStatus.Kind.Accident)
                        {
                            duels.Add(new Duel(season, round, constructorId, a.DriverId, b.DriverId, wRace, DuelKind.Race));
                        }
                    }
                    else if (!isClassifiedA && isClassifiedB)
                    {
                        if (FinishStatus.Classify(a.Status) == FinishStatus.Kind.Accident)
                        {
                            duels.Add(new Duel(season, round, constructorId, b.DriverId, a.DriverId, wRace, DuelKind.Race));
                        }
                    }

                    // Qualifying duel logic
                    if (a.Grid > 0 && b.Grid > 0)
                    {
                        if (a.Grid < b.Grid)
                        {
                            duels.Add(new Duel(season, round, constructorId, a.DriverId, b.DriverId, wQuali, DuelKind.Qualifying));
                        }
                        else if (b.Grid < a.Grid)
                        {
                            duels.Add(new Duel(season, round, constructorId, b.DriverId, a.DriverId, wQuali, DuelKind.Qualifying));
                        }
                    }
                }
            }
        }

        // Sort duels deterministically before fitting:
        // (season, round, constructorId, driverId)
        duels.Sort((d1, d2) =>
        {
            var cmp = d1.Season.CompareTo(d2.Season);
            if (cmp != 0) return cmp;
            cmp = d1.Round.CompareTo(d2.Round);
            if (cmp != 0) return cmp;
            cmp = string.Compare(d1.ConstructorId, d2.ConstructorId, StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            cmp = ((int)d1.Kind).CompareTo((int)d2.Kind);
            if (cmp != 0) return cmp;
            cmp = string.Compare(d1.WinnerDriverId, d2.WinnerDriverId, StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            return string.Compare(d1.LoserDriverId, d2.LoserDriverId, StringComparison.Ordinal);
        });

        return duels;
    }

    public static OptimizationResult Fit(
        IReadOnlyList<Duel> duels,
        IReadOnlyList<DriverSeasonParam> parameters,
        IReadOnlyList<TimeLink> timeLinks,
        double lambdaTime = DefaultLambdaTime,
        double lambda0 = DefaultLambda0)
    {
        ArgumentNullException.ThrowIfNull(duels);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(timeLinks);

        var pCount = parameters.Count;
        if (pCount == 0)
        {
            return new OptimizationResult([], [], 0, true, 0.0);
        }

        var paramIndex = new Dictionary<(string DriverId, int Season), int>(pCount);
        for (var i = 0; i < pCount; i++)
        {
            paramIndex[(parameters[i].DriverId, parameters[i].Season)] = i;
        }

        var duelEntries = new (int WinnerIndex, int LoserIndex, double Weight)[duels.Count];
        for (var i = 0; i < duels.Count; i++)
        {
            var d = duels[i];
            duelEntries[i] = (
                paramIndex[(d.WinnerDriverId, d.Season)],
                paramIndex[(d.LoserDriverId, d.Season)],
                d.Weight);
        }

        var s = new double[pCount];
        var g = new double[pCount];

        ComputeLossAndGradient(s, duelEntries, timeLinks, lambdaTime, lambda0, out _, g);

        var initialMaxGrad = InfinityNorm(g);
        if (initialMaxGrad < ConvergenceTolerance)
        {
            var initialSe = ComputeStandardErrors(s, duelEntries, timeLinks, lambdaTime, lambda0);
            return new OptimizationResult(s, initialSe, 0, true, initialMaxGrad);
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
                for (var i = 0; i < pCount; i++)
                {
                    dir[i] = -g[i];
                }
                dirDotGrad = Dot(dir, g);
            }

            ComputeLossAndGradient(s, duelEntries, timeLinks, lambdaTime, lambda0, out var currentLoss, null);

            var alpha = 1.0;
            const double c1 = 1e-4;
            var sNew = new double[pCount];
            var gNew = new double[pCount];
            var lineSearchSucceeded = false;

            for (var ls = 0; ls < 40; ls++)
            {
                for (var i = 0; i < pCount; i++)
                {
                    sNew[i] = s[i] + alpha * dir[i];
                }

                ComputeLossAndGradient(sNew, duelEntries, timeLinks, lambdaTime, lambda0, out var newLoss, gNew);

                if (newLoss <= currentLoss + c1 * alpha * dirDotGrad)
                {
                    lineSearchSucceeded = true;
                    break;
                }

                alpha *= 0.5;
            }

            if (!lineSearchSucceeded)
            {
                break;
            }

            currentMaxGrad = InfinityNorm(gNew);
            if (currentMaxGrad < ConvergenceTolerance)
            {
                converged = true;
                Array.Copy(sNew, s, pCount);
                break;
            }

            var sDiff = new double[pCount];
            var yDiff = new double[pCount];
            for (var i = 0; i < pCount; i++)
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

            Array.Copy(sNew, s, pCount);
            Array.Copy(gNew, g, pCount);
        }

        var se = ComputeStandardErrors(s, duelEntries, timeLinks, lambdaTime, lambda0);
        return new OptimizationResult(s, se, iterations, converged, currentMaxGrad);
    }

    private static void ComputeLossAndGradient(
        double[] s,
        (int WinnerIndex, int LoserIndex, double Weight)[] duels,
        IReadOnlyList<TimeLink> timeLinks,
        double lambdaTime,
        double lambda0,
        out double loss,
        double[]? grad)
    {
        var pCount = s.Length;
        loss = 0.0;

        if (grad is not null)
        {
            Array.Clear(grad, 0, pCount);
            for (var i = 0; i < pCount; i++)
            {
                grad[i] = 2.0 * lambda0 * s[i];
            }
        }

        var regLoss = 0.0;
        for (var i = 0; i < pCount; i++)
        {
            regLoss += s[i] * s[i];
        }
        loss += lambda0 * regLoss;

        for (var i = 0; i < duels.Length; i++)
        {
            var (w, l, weight) = duels[i];
            var diff = s[w] - s[l];

            loss -= weight * LogSigmoid(diff);

            if (grad is not null)
            {
                var sig = Sigmoid(diff);
                var gFactor = weight * (sig - 1.0);
                grad[w] += gFactor;
                grad[l] -= gFactor;
            }
        }

        for (var i = 0; i < timeLinks.Count; i++)
        {
            var link = timeLinks[i];
            var penaltyCoeff = lambdaTime / link.GapYears;
            var delta = s[link.CurrentIndex] - s[link.PreviousIndex];

            loss += penaltyCoeff * delta * delta;

            if (grad is not null)
            {
                var term = 2.0 * penaltyCoeff * delta;
                grad[link.CurrentIndex] += term;
                grad[link.PreviousIndex] -= term;
            }
        }
    }

    private static double[] ComputeStandardErrors(
        double[] s,
        (int WinnerIndex, int LoserIndex, double Weight)[] duels,
        IReadOnlyList<TimeLink> timeLinks,
        double lambdaTime,
        double lambda0)
    {
        var pCount = s.Length;
        var hDiag = new double[pCount];

        for (var i = 0; i < pCount; i++)
        {
            hDiag[i] = 2.0 * lambda0;
        }

        for (var i = 0; i < duels.Length; i++)
        {
            var (w, l, weight) = duels[i];
            var diff = s[w] - s[l];
            var sig = Sigmoid(diff);
            var hTerm = weight * sig * (1.0 - sig);
            hDiag[w] += hTerm;
            hDiag[l] += hTerm;
        }

        for (var i = 0; i < timeLinks.Count; i++)
        {
            var link = timeLinks[i];
            var hTerm = 2.0 * (lambdaTime / link.GapYears);
            hDiag[link.CurrentIndex] += hTerm;
            hDiag[link.PreviousIndex] += hTerm;
        }

        var se = new double[pCount];
        for (var i = 0; i < pCount; i++)
        {
            // Uncertainty: se[d,t] = 1 / sqrt(H[d,t][d,t]), the inverse square root
            // of the Hessian diagonal at the optimum (an approximation).
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

    private static double LogSigmoid(double x)
    {
        if (x >= 0.0)
        {
            return -Math.Log(1.0 + Math.Exp(-x));
        }
        else
        {
            return x - Math.Log(1.0 + Math.Exp(x));
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
