using System.Collections.Immutable;

namespace Paddock.Simulation.Racing.Points;

/// <summary>One line of a championship table.</summary>
/// <param name="Position">1-based rank.</param>
/// <param name="Id">Driver id or constructor id.</param>
/// <param name="CountedPoints">Points after the season's results-counted rule (what ranks the table).</param>
/// <param name="TotalPoints">Every point scored, before any result is dropped.</param>
/// <param name="FinishCounts">
/// Index i is how many times the entry finished in classified position i+1 (all results, dropped or not).
/// Every row of a table has the same length, so the arrays compare directly. The wins are index 0.
/// </param>
/// <param name="TiedOnEverything">
/// True when another row has the same counted points and the same finishing positions, so the order
/// between them is only the id (a deterministic stand-in, not a rule of any era).
/// </param>
public sealed record StandingsRow(
    int Position,
    string Id,
    decimal CountedPoints,
    decimal TotalPoints,
    ImmutableArray<int> FinishCounts,
    bool TiedOnEverything)
{
    public int Wins => FinishCounts.Length > 0 ? FinishCounts[0] : 0;
}

/// <summary>Whether a title is already settled. See <see cref="Standings.DriversChampionDecision"/>.</summary>
/// <param name="IsDecided">True when nobody can catch the leader, or no rounds are left.</param>
/// <param name="LeaderId">The leader, or null when the table is empty.</param>
/// <param name="LeaderPoints">The leader's counted points now. They cannot fall.</param>
/// <param name="ChallengerId">The entry that could end highest if it won everything left, or null when none could.</param>
/// <param name="MaxChallengerPoints">That entry's counted points in the best case.</param>
/// <param name="RoundsRemaining">Rounds still to run.</param>
public sealed record ChampionDecision(
    bool IsDecided,
    string? LeaderId,
    decimal LeaderPoints,
    string? ChallengerId,
    decimal MaxChallengerPoints,
    int RoundsRemaining);

/// <summary>
/// The drivers' and constructors' tables of one season. Immutable: <see cref="Apply"/> returns a new instance.
/// </summary>
/// <remarks>
/// Table order: counted points, then the finishing positions (most wins, then most second places, and so on),
/// then the id so the order is always total and reproducible. The positions count every result, dropped or not.
/// ESTIMATE: the catalog has no tie-break dimension; this is the usual countback of the championship era.
/// Drivers and constructors known to the table are the ones that have appeared in an applied round.
/// </remarks>
public sealed class Standings
{
    private readonly ImmutableSortedDictionary<string, Ledger> _drivers;
    private readonly ImmutableSortedDictionary<string, Ledger> _constructors;

    private Standings(
        PointsRules rules,
        int totalRounds,
        int roundsCompleted,
        ImmutableSortedDictionary<string, Ledger> drivers,
        ImmutableSortedDictionary<string, Ledger> constructors)
    {
        Rules = rules;
        TotalRounds = totalRounds;
        RoundsCompleted = roundsCompleted;
        _drivers = drivers;
        _constructors = constructors;
    }

    public PointsRules Rules { get; }

    /// <summary>Rounds in the season, applied or not.</summary>
    public int TotalRounds { get; }

    public int RoundsCompleted { get; }

    public int RoundsRemaining => TotalRounds - RoundsCompleted;

    /// <summary>An empty table for a season of <paramref name="totalRounds"/> rounds.</summary>
    public static Standings Start(PointsRules rules, int totalRounds)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentOutOfRangeException.ThrowIfLessThan(totalRounds, 1);
        return new Standings(
            rules,
            totalRounds,
            0,
            ImmutableSortedDictionary.Create<string, Ledger>(StringComparer.Ordinal),
            ImmutableSortedDictionary.Create<string, Ledger>(StringComparer.Ordinal));
    }

    /// <summary>The <see cref="RaceContext"/> for the next round: flags the finale so double points can apply.</summary>
    public RaceContext ContextForNextRound(int scheduledLaps) => new(scheduledLaps, RoundsRemaining == 1);

    /// <summary>Adds one round and returns the new table; this instance is not changed.</summary>
    public Standings Apply(RaceClassification round)
    {
        ArgumentNullException.ThrowIfNull(round);
        if (RoundsRemaining <= 0)
        {
            throw new InvalidOperationException("The season has no rounds left.");
        }

        var drivers = Advance(
            _drivers,
            round.DriverScores.Select(score => (
                score.DriverId,
                score.Points,
                score.Position is int position ? ImmutableArray.Create(position) : ImmutableArray<int>.Empty)));
        var constructors = Rules.ConstructorCounting == ConstructorCounting.NoChampionship
            ? _constructors
            : Advance(
                _constructors,
                round.ConstructorScores.Select(score => (score.ConstructorId, score.Points, score.Positions)));
        return new Standings(Rules, TotalRounds, RoundsCompleted + 1, drivers, constructors);
    }

    public ImmutableArray<StandingsRow> Drivers() => Table(_drivers, Rules.ResultsCounting);

    /// <summary>Empty when the era has no constructors' title.</summary>
    public ImmutableArray<StandingsRow> Constructors() => Table(_constructors, ConstructorsCounting());

    /// <summary>
    /// Can the drivers' title still change hands? Every driver in the table is assumed to win every remaining round
    /// (with the fastest-lap point and the double finale where the rules give them) while the leader scores nothing more.
    /// Decided only when the leader is strictly ahead of every such best case: a tie is not "decided", because the
    /// tie-break could go either way. Drivers who have not appeared yet are not considered.
    /// </summary>
    public ChampionDecision DriversChampionDecision() =>
        Decide(_drivers, Rules.ResultsCounting, Rules.MaxDriverPointsPerRound);

    /// <summary>The same question for the constructors' title. Never decided when the era has none.</summary>
    public ChampionDecision ConstructorsChampionDecision() =>
        Rules.ConstructorCounting == ConstructorCounting.NoChampionship
            ? new ChampionDecision(false, null, 0m, null, 0m, RoundsRemaining)
            : Decide(_constructors, ConstructorsCounting(), Rules.MaxConstructorPointsPerRound);

    private ResultsCountingRule ConstructorsCounting() =>
        Rules.ConstructorsApplyResultsCounting ? Rules.ResultsCounting : ResultsCountingRule.All;

    private ChampionDecision Decide(
        ImmutableSortedDictionary<string, Ledger> ledgers,
        ResultsCountingRule counting,
        Func<bool, decimal> maxPerRound)
    {
        var table = Table(ledgers, counting);
        if (table.Length == 0)
        {
            return new ChampionDecision(false, null, 0m, null, 0m, RoundsRemaining);
        }

        var leader = table[0];
        if (RoundsRemaining == 0)
        {
            return new ChampionDecision(
                true,
                leader.Id,
                leader.CountedPoints,
                table.Length > 1 ? table[1].Id : null,
                table.Length > 1 ? table[1].CountedPoints : 0m,
                0);
        }

        string? challenger = null;
        var challengerMax = decimal.MinValue;
        foreach (var row in table.Skip(1))
        {
            var rounds = ledgers[row.Id].RoundPoints.ToList();
            for (var round = RoundsCompleted; round < TotalRounds; round++)
            {
                rounds.Add(maxPerRound(round == TotalRounds - 1));
            }

            var best = counting.Counted(rounds, TotalRounds);
            if (best > challengerMax)
            {
                challengerMax = best;
                challenger = row.Id;
            }
        }

        if (challenger is null)
        {
            // A lone entry cannot be caught by anyone in the table.
            return new ChampionDecision(true, leader.Id, leader.CountedPoints, null, 0m, RoundsRemaining);
        }

        return new ChampionDecision(
            leader.CountedPoints > challengerMax,
            leader.Id,
            leader.CountedPoints,
            challenger,
            challengerMax,
            RoundsRemaining);
    }

    private ImmutableSortedDictionary<string, Ledger> Advance(
        ImmutableSortedDictionary<string, Ledger> ledgers,
        IEnumerable<(string Id, decimal Points, ImmutableArray<int> Positions)> scores)
    {
        var byId = new Dictionary<string, (decimal Points, ImmutableArray<int> Positions)>(StringComparer.Ordinal);
        foreach (var (id, points, positions) in scores)
        {
            byId.Add(id, (points, positions));
        }

        var next = ledgers.ToBuilder();
        foreach (var (id, ledger) in ledgers)
        {
            next[id] = byId.TryGetValue(id, out var score)
                ? new Ledger(ledger.RoundPoints.Add(score.Points), ledger.Positions.AddRange(score.Positions))
                : new Ledger(ledger.RoundPoints.Add(0m), ledger.Positions);
        }

        foreach (var (id, score) in byId)
        {
            if (!ledgers.ContainsKey(id))
            {
                var zeros = ImmutableArray.CreateRange(Enumerable.Repeat(0m, RoundsCompleted));
                next[id] = new Ledger(zeros.Add(score.Points), score.Positions);
            }
        }

        return next.ToImmutable();
    }

    private ImmutableArray<StandingsRow> Table(
        ImmutableSortedDictionary<string, Ledger> ledgers,
        ResultsCountingRule counting)
    {
        if (ledgers.Count == 0)
        {
            return [];
        }

        var width = ledgers.Values.SelectMany(ledger => ledger.Positions).DefaultIfEmpty(0).Max();
        var rows = ledgers
            .Select(pair => (
                pair.Key,
                Counted: counting.Counted(pair.Value.RoundPoints, TotalRounds),
                Total: pair.Value.RoundPoints.Sum(),
                Counts: FinishCounts(pair.Value.Positions, width)))
            .ToList();
        rows.Sort((a, b) =>
        {
            var byPoints = b.Counted.CompareTo(a.Counted);
            if (byPoints != 0)
            {
                return byPoints;
            }

            var byCounts = CompareCounts(b.Counts, a.Counts);
            return byCounts != 0 ? byCounts : string.CompareOrdinal(a.Key, b.Key);
        });

        var table = ImmutableArray.CreateBuilder<StandingsRow>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var tied = (i > 0 && SameRank(row, rows[i - 1])) || (i + 1 < rows.Count && SameRank(row, rows[i + 1]));
            table.Add(new StandingsRow(i + 1, row.Key, row.Counted, row.Total, row.Counts, tied));
        }

        return table.MoveToImmutable();
    }

    private static bool SameRank(
        (string Key, decimal Counted, decimal Total, ImmutableArray<int> Counts) a,
        (string Key, decimal Counted, decimal Total, ImmutableArray<int> Counts) b) =>
        a.Counted == b.Counted && CompareCounts(a.Counts, b.Counts) == 0;

    private static ImmutableArray<int> FinishCounts(ImmutableArray<int> positions, int width)
    {
        var counts = new int[width];
        foreach (var position in positions)
        {
            counts[position - 1]++;
        }

        return ImmutableArray.Create(counts);
    }

    private static int CompareCounts(ImmutableArray<int> a, ImmutableArray<int> b)
    {
        for (var i = 0; i < a.Length; i++)
        {
            var byCount = a[i].CompareTo(b[i]);
            if (byCount != 0)
            {
                return byCount;
            }
        }

        return 0;
    }

    /// <summary>The round-by-round points and finishing positions of one table entry. Enough to rebuild the table.</summary>
    public sealed record LedgerSnapshot(string Id, ImmutableArray<decimal> RoundPoints, ImmutableArray<int> Positions);

    /// <summary>Drivers then constructors, each in id order. The rules are not part of the snapshot: the caller supplies the same rules on restore.</summary>
    public (ImmutableArray<LedgerSnapshot> Drivers, ImmutableArray<LedgerSnapshot> Constructors) Snapshot() =>
        (SnapshotOf(_drivers), SnapshotOf(_constructors));

    /// <summary>
    /// Rebuilds a table from <see cref="Snapshot"/>. Every ledger has exactly <paramref name="roundsCompleted"/> round points.
    /// The same rules and the same snapshot give the same table (INV-002).
    /// </summary>
    public static Standings Restore(
        PointsRules rules,
        int totalRounds,
        int roundsCompleted,
        IReadOnlyList<LedgerSnapshot> drivers,
        IReadOnlyList<LedgerSnapshot> constructors)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(drivers);
        ArgumentNullException.ThrowIfNull(constructors);
        ArgumentOutOfRangeException.ThrowIfLessThan(totalRounds, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(roundsCompleted);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(roundsCompleted, totalRounds);
        return new Standings(
            rules,
            totalRounds,
            roundsCompleted,
            RestoreLedgers(drivers, roundsCompleted),
            RestoreLedgers(constructors, roundsCompleted));
    }

    private static ImmutableArray<LedgerSnapshot> SnapshotOf(ImmutableSortedDictionary<string, Ledger> ledgers)
    {
        var rows = ImmutableArray.CreateBuilder<LedgerSnapshot>(ledgers.Count);
        foreach (var (id, ledger) in ledgers)
        {
            rows.Add(new LedgerSnapshot(id, ledger.RoundPoints, ledger.Positions));
        }

        return rows.MoveToImmutable();
    }

    private static ImmutableSortedDictionary<string, Ledger> RestoreLedgers(IReadOnlyList<LedgerSnapshot> rows, int roundsCompleted)
    {
        var ledgers = ImmutableSortedDictionary.CreateBuilder<string, Ledger>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(row.Id);
            if (row.RoundPoints.IsDefault || row.RoundPoints.Length != roundsCompleted)
            {
                throw new ArgumentException("A ledger must list one points total per completed round.", nameof(rows));
            }

            if (row.Positions.IsDefault)
            {
                throw new ArgumentException("A ledger is missing its finishing positions.", nameof(rows));
            }

            if (!ledgers.TryAdd(row.Id, new Ledger(row.RoundPoints, row.Positions)))
            {
                throw new ArgumentException("Ledger '" + row.Id + "' is listed twice.", nameof(rows));
            }
        }

        return ledgers.ToImmutable();
    }

    private sealed record Ledger(ImmutableArray<decimal> RoundPoints, ImmutableArray<int> Positions);
}
