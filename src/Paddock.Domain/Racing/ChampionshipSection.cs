using System.Globalization;
using Paddock.Domain.World;

namespace Paddock.Domain.Racing;

/// <summary>
/// One championship entry's results so far: one points total per completed round, and the classified finishing positions
/// (a round the entry missed adds a zero and no position). Ids are driver or organization ids.
/// </summary>
public sealed record ChampionshipLedger
{
    public ChampionshipLedger(string id, IReadOnlyList<decimal> roundPoints, IReadOnlyList<int> positions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(roundPoints);
        ArgumentNullException.ThrowIfNull(positions);
        Id = id;
        RoundPoints = roundPoints.ToArray();
        Positions = positions.ToArray();
    }

    public string Id { get; }

    public IReadOnlyList<decimal> RoundPoints { get; }

    public IReadOnlyList<int> Positions { get; }
}

/// <summary>
/// The <c>championship</c> world section (T47): the drivers' and constructors' tables of the season being raced.
/// It is absent until the first race, and dropped on 1 January so a new season does not inherit last year's places.
/// Points are the counted totals' inputs (every round), so a save resumes the same table (INV-002).
/// <para>
/// Canonical text (schema 1), after the section header:
/// <code>
/// championship &lt;season&gt; &lt;totalRounds&gt; &lt;roundsCompleted&gt; &lt;settled 0|1&gt;
/// drivers &lt;count&gt;
/// driver &lt;len&gt;:&lt;id&gt; &lt;points joined by commas&gt; &lt;positions joined by commas, or -&gt;
/// constructors &lt;count&gt;
/// constructor ...
/// </code>
/// </para>
/// </summary>
public sealed class ChampionshipSection : IWorldSection
{
    public const string SectionName = "championship";

    private readonly ChampionshipLedger[] _drivers;
    private readonly ChampionshipLedger[] _constructors;

    private ChampionshipSection(
        int season,
        int totalRounds,
        int roundsCompleted,
        bool settled,
        ChampionshipLedger[] drivers,
        ChampionshipLedger[] constructors)
    {
        Season = season;
        TotalRounds = totalRounds;
        RoundsCompleted = roundsCompleted;
        Settled = settled;
        _drivers = drivers;
        _constructors = constructors;
    }

    public string Name => SectionName;

    public int SchemaVersion => 1;

    public int Season { get; }

    public int TotalRounds { get; }

    public int RoundsCompleted { get; }

    public bool Settled { get; }

    public IReadOnlyList<ChampionshipLedger> Drivers => _drivers;

    public IReadOnlyList<ChampionshipLedger> Constructors => _constructors;

    public static ChampionshipSection Create(
        int season,
        int totalRounds,
        int roundsCompleted,
        bool settled,
        IReadOnlyList<ChampionshipLedger> drivers,
        IReadOnlyList<ChampionshipLedger> constructors)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(season, 1950);
        ArgumentOutOfRangeException.ThrowIfLessThan(totalRounds, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(roundsCompleted);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(roundsCompleted, totalRounds);
        ArgumentNullException.ThrowIfNull(drivers);
        ArgumentNullException.ThrowIfNull(constructors);
        return new ChampionshipSection(
            season,
            totalRounds,
            roundsCompleted,
            settled,
            Normalize(drivers, roundsCompleted, "driver"),
            Normalize(constructors, roundsCompleted, "constructor"));
    }

    public ChampionshipSection WithSettled() =>
        new(Season, TotalRounds, RoundsCompleted, true, _drivers, _constructors);

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Line(
            "championship "
            + Season.ToString(CultureInfo.InvariantCulture) + " "
            + TotalRounds.ToString(CultureInfo.InvariantCulture) + " "
            + RoundsCompleted.ToString(CultureInfo.InvariantCulture) + " "
            + (Settled ? "1" : "0"));
        writer.Count("drivers", _drivers.Length);
        foreach (var row in _drivers)
        {
            WriteLedger(writer, "driver", row);
        }

        writer.Count("constructors", _constructors.Length);
        foreach (var row in _constructors)
        {
            WriteLedger(writer, "constructor", row);
        }
    }

    private static void WriteLedger(CanonicalWriter writer, string label, ChampionshipLedger row)
    {
        writer.Begin(label);
        writer.Field(row.Id);
        writer.Raw(" " + Join(row.RoundPoints) + " " + (row.Positions.Count == 0 ? "-" : Join(row.Positions)));
        writer.End();
    }

    private static string Join(IReadOnlyList<decimal> values)
    {
        var text = new string[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            text[i] = values[i].ToString(CultureInfo.InvariantCulture);
        }

        return string.Join(',', text);
    }

    private static string Join(IReadOnlyList<int> values)
    {
        var text = new string[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            text[i] = values[i].ToString(CultureInfo.InvariantCulture);
        }

        return string.Join(',', text);
    }

    private static ChampionshipLedger[] Normalize(IReadOnlyList<ChampionshipLedger> rows, int roundsCompleted, string kind)
    {
        var copy = new ChampionshipLedger[rows.Count];
        var seen = new HashSet<string>(rows.Count, StringComparer.Ordinal);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i] ?? throw new ArgumentException("A " + kind + " ledger is missing.", nameof(rows));
            if (row.RoundPoints.Count != roundsCompleted)
            {
                throw new ArgumentException("A " + kind + " ledger must list one points total per completed round.", nameof(rows));
            }

            foreach (var position in row.Positions)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(position, 1);
            }

            if (!seen.Add(row.Id))
            {
                throw new ArgumentException(kind + " '" + row.Id + "' is listed twice.", nameof(rows));
            }

            copy[i] = row;
        }

        Array.Sort(copy, static (left, right) => string.CompareOrdinal(left.Id, right.Id));
        return copy;
    }
}
