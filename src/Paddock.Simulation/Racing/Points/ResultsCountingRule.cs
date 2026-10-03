using System.Globalization;
using System.Text.RegularExpressions;

namespace Paddock.Simulation.Racing.Points;

public enum ResultsCountingKind
{
    /// <summary>Every round counts (<c>all</c>).</summary>
    All,

    /// <summary>The best N rounds count (<c>best_N</c>).</summary>
    BestOverall,

    /// <summary>A quota from the first part of the season and a quota from the second (<c>best_N_split_A_and_B</c>).</summary>
    Split,
}

/// <summary>
/// Which of a season's results count toward a title, parsed from the catalog dimension <c>results_counted</c>.
/// </summary>
/// <remarks>
/// For a split rule the data gives the two quotas and, for two values only (<c>_of_6</c>, <c>_of_7</c>), the size of
/// the first block. For the others it does not give the block size. ESTIMATE: the first block is
/// <c>ceil(rounds / 2)</c> when the quotas differ (the larger quota goes with the larger block) and
/// <c>floor(rounds / 2)</c> when they are equal. This matches every split described in the timeline notes
/// (11 races = 6 + 5, 13 = 7 + 6, 15 = 8 + 7, 17 = 9 + 8, even seasons = half and half) and the 15-race 1979 season, which
/// was 7 + 8 according to the implementer's recollection (NOT in the data; verify).
/// </remarks>
public sealed partial record ResultsCountingRule
{
    private ResultsCountingRule(
        ResultsCountingKind kind,
        int totalCounted,
        int firstQuota,
        int secondQuota,
        int? firstBlockRounds)
    {
        Kind = kind;
        TotalCounted = totalCounted;
        FirstQuota = firstQuota;
        SecondQuota = secondQuota;
        FirstBlockRounds = firstBlockRounds;
    }

    public ResultsCountingKind Kind { get; }

    /// <summary>The N of <c>best_N</c> and of <c>best_N_split_A_and_B</c>. Zero for <see cref="ResultsCountingKind.All"/>.</summary>
    public int TotalCounted { get; }

    /// <summary>Quota of the first block of a split rule. Zero otherwise.</summary>
    public int FirstQuota { get; }

    /// <summary>Quota of the second block of a split rule. Zero otherwise.</summary>
    public int SecondQuota { get; }

    /// <summary>Explicit size of the first block when the data names it (<c>_of_6</c>), otherwise null.</summary>
    public int? FirstBlockRounds { get; }

    public static ResultsCountingRule All { get; } = new(ResultsCountingKind.All, 0, 0, 0, null);

    public static ResultsCountingRule Best(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        return new ResultsCountingRule(ResultsCountingKind.BestOverall, count, 0, 0, null);
    }

    public static ResultsCountingRule Split(int firstQuota, int secondQuota, int? firstBlockRounds = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(firstQuota, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(secondQuota, 1);
        if (firstBlockRounds is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(firstBlockRounds));
        }

        return new ResultsCountingRule(
            ResultsCountingKind.Split,
            firstQuota + secondQuota,
            firstQuota,
            secondQuota,
            firstBlockRounds);
    }

    /// <summary>Parses a catalog value such as <c>all</c>, <c>best_6</c>, <c>best_9_split_5_and_4</c> or <c>best_10_split_5_and_5_of_6</c>.</summary>
    public static ResultsCountingRule Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (string.Equals(value, "all", StringComparison.Ordinal))
        {
            return All;
        }

        var match = Pattern().Match(value);
        if (!match.Success)
        {
            throw new FormatException($"Unknown results_counted value '{value}'.");
        }

        var total = Number(match.Groups["n"]);
        if (!match.Groups["a"].Success)
        {
            return Best(total);
        }

        var first = Number(match.Groups["a"]);
        var second = Number(match.Groups["b"]);
        if (first + second != total)
        {
            throw new FormatException($"results_counted '{value}': {first} + {second} is not {total}.");
        }

        int? block = match.Groups["k"].Success ? Number(match.Groups["k"]) : null;
        return Split(first, second, block);
    }

    /// <summary>How many of the first rounds form the first block of a split rule in a season of <paramref name="totalRounds"/> rounds.</summary>
    public int FirstBlockSize(int totalRounds)
    {
        if (Kind != ResultsCountingKind.Split)
        {
            throw new InvalidOperationException("Only a split rule has blocks.");
        }

        if (FirstBlockRounds is int explicitSize)
        {
            return explicitSize;
        }

        return FirstQuota > SecondQuota ? (totalRounds + 1) / 2 : totalRounds / 2;
    }

    /// <summary>
    /// The points that count: all of them, the best N, or the best quota of each block.
    /// <paramref name="roundPoints"/> holds one entry per round played so far in round order, a round
    /// without a score being zero. <paramref name="totalRounds"/> is the length of the whole season.
    /// </summary>
    public decimal Counted(IReadOnlyList<decimal> roundPoints, int totalRounds)
    {
        ArgumentNullException.ThrowIfNull(roundPoints);
        switch (Kind)
        {
            case ResultsCountingKind.All:
                return roundPoints.Sum();
            case ResultsCountingKind.BestOverall:
                return BestSum(roundPoints, 0, roundPoints.Count, TotalCounted);
            default:
                var firstEnd = Math.Min(FirstBlockSize(totalRounds), roundPoints.Count);
                return BestSum(roundPoints, 0, firstEnd, FirstQuota)
                    + BestSum(roundPoints, firstEnd, roundPoints.Count, SecondQuota);
        }
    }

    private static decimal BestSum(IReadOnlyList<decimal> values, int from, int to, int quota)
    {
        var slice = new List<decimal>(Math.Max(0, to - from));
        for (var i = from; i < to; i++)
        {
            slice.Add(values[i]);
        }

        slice.Sort();
        var sum = 0m;
        for (var i = slice.Count - 1; i >= 0 && quota > 0; i--, quota--)
        {
            sum += slice[i];
        }

        return sum;
    }

    private static int Number(Group group) => int.Parse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^best_(?<n>\d+)(?:_split_(?<a>\d+)_and_(?<b>\d+)(?:_of_(?<k>\d+))?)?$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
