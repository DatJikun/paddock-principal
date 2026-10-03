using System.Collections.Immutable;

namespace Paddock.Simulation.Racing.Points;

/// <summary>
/// Turns the finishing order of one race into a classification and the points it pays, under the rules of one season.
/// Pure and deterministic: the result depends only on the arguments.
/// </summary>
public static class RaceClassifier
{
    private const int FastestLapTopPositions = 10;

    /// <summary>
    /// Classifies a race.
    /// </summary>
    /// <param name="rules">The season's points rules.</param>
    /// <param name="finishOrder">One entry per car, in the order the cars finished (the winner first). Order is the caller's: laps, then time.</param>
    /// <param name="context">Scheduled distance and whether this is the season finale.</param>
    /// <remarks>
    /// Rules applied, in this order:
    /// <list type="number">
    /// <item>Classification: with the 90 percent rule, a car is classified when it completed at least
    /// <c>floor(0.9 * winner laps)</c> laps, whatever its status; otherwise (the data does not know the rule) when it was running at the flag.
    /// Classified cars take positions 1..n in the given order; the rest follow.</item>
    /// <item>Points by classified position; the fastest-lap point (divided between tied cars) when the rule and its conditions allow;
    /// everything doubled in a double-points finale.</item>
    /// <item>Shared drive: split equally between the drivers of the car, or (from 1958) no driver of it scores.</item>
    /// <item>Constructors: every classified car, or only the best-placed one, or nothing.</item>
    /// </list>
    /// A driver who appears in more than one car (primary in one, partner in another) keeps only his best share and best position.
    /// ESTIMATE: the data says only that such a driver "was sometimes limited to his best finish".
    /// </remarks>
    public static RaceClassification Classify(
        PointsRules rules,
        IReadOnlyList<RaceResultInput> finishOrder,
        RaceContext context)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(finishOrder);
        if (context.ScheduledLaps < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(context), "ScheduledLaps must be at least 1.");
        }

        Validate(finishOrder);
        if (finishOrder.Count == 0)
        {
            return new RaceClassification([], [], []);
        }

        var winnerLaps = finishOrder[0].LapsCompleted;
        var minimumLaps = winnerLaps * 9 / 10; // floor(0.9 * winnerLaps), exact in integers.
        var classified = finishOrder
            .Select(car => rules.Classification == ClassificationRule.NinetyPercentOfWinnerLaps
                ? car.LapsCompleted >= minimumLaps
                : car.Status == FinishStatus.Classified)
            .ToArray();

        // Classified cars first (positions 1..n), then the rest; both keep the given order.
        var order = Enumerable.Range(0, finishOrder.Count)
            .OrderBy(i => classified[i] ? 0 : 1)
            .ThenBy(i => i)
            .ToArray();
        var position = new int[finishOrder.Count];
        for (var rank = 0; rank < order.Length; rank++)
        {
            position[order[rank]] = rank + 1;
        }

        var fastestLapShare = FastestLapShare(finishOrder);
        var halfDistance = winnerLaps * 2 >= context.ScheduledLaps;
        var multiplier = rules.RoundMultiplier(context.IsFinalRound);

        // Best-placed classified car of each constructor, for the "best car only" counting rule.
        var bestCarOfConstructor = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var i in order)
        {
            if (classified[i])
            {
                bestCarOfConstructor.TryAdd(finishOrder[i].ConstructorId, i);
            }
        }

        var cars = ImmutableArray.CreateBuilder<ClassifiedCar>(finishOrder.Count);
        var driverPoints = new Dictionary<string, (decimal Points, int? Position)>(StringComparer.Ordinal);
        var driverOrder = new List<string>();
        foreach (var i in order)
        {
            var input = finishOrder[i];
            var basePoints = classified[i] ? rules.PointsForPosition(position[i]) : 0;
            var lapPoint = 0m;
            if (input.SetFastestLap && FastestLapEligible(rules, classified[i], position[i], halfDistance))
            {
                lapPoint = fastestLapShare;
            }

            var carPoints = (basePoints + lapPoint) * multiplier;
            var constructorBase = (basePoints + (rules.FastestLapCountsForConstructors ? lapPoint : 0m)) * multiplier;
            var constructorPoints = rules.ConstructorCounting switch
            {
                ConstructorCounting.NoChampionship => 0m,
                ConstructorCounting.AllCars => constructorBase,
                _ => bestCarOfConstructor.TryGetValue(input.ConstructorId, out var best) && best == i ? constructorBase : 0m,
            };

            var drivers = ImmutableArray.CreateBuilder<string>();
            drivers.Add(input.DriverId);
            foreach (var partner in input.SharedDrivePartnerIds)
            {
                drivers.Add(partner);
            }

            cars.Add(new ClassifiedCar(
                position[i],
                classified[i],
                input.ConstructorId,
                drivers.ToImmutable(),
                carPoints,
                constructorPoints));

            for (var d = 0; d < drivers.Count; d++)
            {
                // Shared car: split equally, or (from 1958) no driver of it scores at all.
                var shared = drivers.Count > 1;
                var scores = !shared || rules.SharedDrive == SharedDriveRule.SharedEqually;
                var share = scores ? carPoints / drivers.Count : 0m;
                int? driverPosition = classified[i] && scores ? position[i] : null;
                AddDriver(driverPoints, driverOrder, drivers[d], share, driverPosition);
            }
        }

        var driverScores = driverOrder
            .Select(id => new DriverScore(id, driverPoints[id].Points, driverPoints[id].Position))
            .ToImmutableArray();

        var constructorScores = cars
            .GroupBy(car => car.ConstructorId, StringComparer.Ordinal)
            .Select(group => new ConstructorScore(
                group.Key,
                group.Sum(car => car.ConstructorPoints),
                group.Where(car => car.IsClassified).Select(car => car.Position).ToImmutableArray()))
            .ToImmutableArray();

        return new RaceClassification(cars.ToImmutable(), driverScores, constructorScores);
    }

    private static void AddDriver(
        Dictionary<string, (decimal Points, int? Position)> scores,
        List<string> order,
        string driverId,
        decimal points,
        int? position)
    {
        if (!scores.TryGetValue(driverId, out var existing))
        {
            scores[driverId] = (points, position);
            order.Add(driverId);
            return;
        }

        var bestPosition = (existing.Position, position) switch
        {
            (int a, int b) => Math.Min(a, b),
            (int a, null) => a,
            (null, int b) => b,
            _ => (int?)null,
        };
        scores[driverId] = (Math.Max(existing.Points, points), bestPosition);
    }

    private static decimal FastestLapShare(IReadOnlyList<RaceResultInput> finishOrder)
    {
        var flagged = finishOrder.Count(car => car.SetFastestLap);
        return flagged == 0 ? 0m : 1m / flagged;
    }

    private static bool FastestLapEligible(PointsRules rules, bool isClassified, int position, bool halfDistance) =>
        rules.FastestLap switch
        {
            FastestLapRule.OnePointSharedIfTied => true,
            FastestLapRule.OnePointIfTopTen => isClassified && position <= FastestLapTopPositions,
            FastestLapRule.OnePointIfTopTenAndHalfDistance =>
                isClassified && position <= FastestLapTopPositions && halfDistance,
            _ => false,
        };

    private static void Validate(IReadOnlyList<RaceResultInput> finishOrder)
    {
        var primaries = new HashSet<string>(StringComparer.Ordinal);
        foreach (var car in finishOrder)
        {
            ArgumentNullException.ThrowIfNull(car);
            ArgumentException.ThrowIfNullOrWhiteSpace(car.DriverId);
            ArgumentException.ThrowIfNullOrWhiteSpace(car.ConstructorId);
            ArgumentNullException.ThrowIfNull(car.SharedDrivePartnerIds);
            ArgumentOutOfRangeException.ThrowIfNegative(car.LapsCompleted);
            if (!primaries.Add(car.DriverId))
            {
                throw new ArgumentException($"Driver '{car.DriverId}' is the primary driver of two cars.", nameof(finishOrder));
            }

            var seen = new HashSet<string>(StringComparer.Ordinal) { car.DriverId };
            foreach (var partner in car.SharedDrivePartnerIds)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(partner);
                if (!seen.Add(partner))
                {
                    throw new ArgumentException(
                        $"Driver '{partner}' is listed twice for the car of '{car.DriverId}'.",
                        nameof(finishOrder));
                }
            }
        }
    }
}
