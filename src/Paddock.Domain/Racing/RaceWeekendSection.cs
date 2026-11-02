using System.Globalization;
using Paddock.Domain.World;

namespace Paddock.Domain.Racing;

/// <summary>One planned stop: in at the end of <see cref="Lap"/> for <see cref="CompoundId"/>.</summary>
public sealed record WeekendPlannedStop(int Lap, string CompoundId);

/// <summary>
/// A team's pre-race plan for one car (#316), as the world keeps it. Pace and engine are the names of the race engine's modes;
/// <see cref="Stops"/> null leaves the stops to the strategist.
/// </summary>
public sealed record WeekendPlan(
    string CarId,
    string TeamId,
    string? StartCompoundId,
    string? Pace,
    string Engine,
    IReadOnlyList<WeekendPlannedStop>? Stops);

/// <summary>One pit wall order of a watched race (#316): who gave it, at which race time, and the order the engine reads.</summary>
public sealed record WeekendOrder(
    string ManagerId,
    long AtMs,
    string CarId,
    int Lap,
    string Kind,
    string Pace,
    string? CompoundId,
    string Engine,
    bool On);

/// <summary>
/// The <c>race-weekend</c> world section (#316): the plans and pit wall orders of the round being raced today, so a save keeps
/// them, the race day books the race with them, and the command log alone rebuilds them (INV-002). <see cref="WatchedTapeHash"/>
/// is the tape the players last saw; when the booked race differs, the day says so in the Inbox. The section is dropped once the
/// round is booked.
/// <para>
/// Canonical text (schema 1), after the section header:
/// <code>
/// weekend &lt;season&gt; &lt;round&gt; &lt;len&gt;:&lt;watched hash or empty&gt;
/// plans &lt;count&gt;
/// plan &lt;car&gt; &lt;team&gt; &lt;start or empty&gt; &lt;pace or empty&gt; &lt;engine&gt; &lt;stops lap:compound joined by commas, or - for none, or * for the strategist&gt;
/// orders &lt;count&gt;
/// order &lt;manager&gt; &lt;atMs&gt; &lt;car&gt; &lt;lap&gt; &lt;kind&gt; &lt;pace&gt; &lt;compound or empty&gt; &lt;engine&gt; &lt;on 0|1&gt;
/// </code>
/// Every text field is written length-prefixed.
/// </para>
/// </summary>
public sealed class RaceWeekendSection : IWorldSection
{
    public const string SectionName = "race-weekend";

    private RaceWeekendSection(int season, int round, string? watchedTapeHash, WeekendPlan[] plans, WeekendOrder[] orders)
    {
        Season = season;
        Round = round;
        WatchedTapeHash = watchedTapeHash;
        Plans = plans;
        Orders = orders;
    }

    public string Name => SectionName;

    public int SchemaVersion => 1;

    public int Season { get; }

    public int Round { get; }

    public string? WatchedTapeHash { get; }

    /// <summary>One plan per car, by car id.</summary>
    public IReadOnlyList<WeekendPlan> Plans { get; }

    /// <summary>The orders in the order they were given.</summary>
    public IReadOnlyList<WeekendOrder> Orders { get; }

    public static RaceWeekendSection Empty(int season, int round) => Create(season, round, null, [], []);

    public static RaceWeekendSection Create(
        int season,
        int round,
        string? watchedTapeHash,
        IReadOnlyList<WeekendPlan> plans,
        IReadOnlyList<WeekendOrder> orders)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(season, 1950);
        ArgumentOutOfRangeException.ThrowIfLessThan(round, 1);
        ArgumentNullException.ThrowIfNull(plans);
        ArgumentNullException.ThrowIfNull(orders);
        var byCar = new SortedDictionary<string, WeekendPlan>(StringComparer.Ordinal);
        foreach (var plan in plans)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentException.ThrowIfNullOrWhiteSpace(plan.CarId);
            ArgumentException.ThrowIfNullOrWhiteSpace(plan.TeamId);
            ArgumentException.ThrowIfNullOrWhiteSpace(plan.Engine);
            if (!byCar.TryAdd(plan.CarId, plan))
            {
                throw new ArgumentException("Car '" + plan.CarId + "' has two plans.", nameof(plans));
            }
        }

        foreach (var order in orders)
        {
            ArgumentNullException.ThrowIfNull(order);
            ArgumentException.ThrowIfNullOrWhiteSpace(order.ManagerId);
            ArgumentException.ThrowIfNullOrWhiteSpace(order.CarId);
            ArgumentException.ThrowIfNullOrWhiteSpace(order.Kind);
            ArgumentOutOfRangeException.ThrowIfLessThan(order.Lap, 1);
        }

        return new RaceWeekendSection(
            season,
            round,
            string.IsNullOrEmpty(watchedTapeHash) ? null : watchedTapeHash,
            [.. byCar.Values],
            [.. orders]);
    }

    /// <summary>The section with <paramref name="plan"/> in place of the car's earlier plan.</summary>
    public RaceWeekendSection WithPlan(WeekendPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return Create(Season, Round, WatchedTapeHash, [.. Plans.Where(p => p.CarId != plan.CarId), plan], Orders);
    }

    /// <summary>The section with the orders of <paramref name="carIds"/> replaced by <paramref name="orders"/> and the tape now watched.</summary>
    public RaceWeekendSection WithOrders(IReadOnlySet<string> carIds, IReadOnlyList<WeekendOrder> orders, string watchedTapeHash)
    {
        ArgumentNullException.ThrowIfNull(carIds);
        ArgumentNullException.ThrowIfNull(orders);
        return Create(Season, Round, watchedTapeHash, Plans, [.. Orders.Where(o => !carIds.Contains(o.CarId)), .. orders]);
    }

    public RaceWeekendSection WithWatched(string watchedTapeHash) => Create(Season, Round, watchedTapeHash, Plans, Orders);

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Begin("weekend");
        writer.Raw(Season.ToString(CultureInfo.InvariantCulture) + " " + Round.ToString(CultureInfo.InvariantCulture) + " ");
        writer.Field(WatchedTapeHash ?? "");
        writer.End();
        writer.Count("plans", Plans.Count);
        foreach (var plan in Plans)
        {
            writer.Begin("plan");
            writer.Field(plan.CarId);
            writer.Space();
            writer.Field(plan.TeamId);
            writer.Space();
            writer.Field(plan.StartCompoundId ?? "");
            writer.Space();
            writer.Field(plan.Pace ?? "");
            writer.Space();
            writer.Field(plan.Engine);
            writer.Space();
            writer.Field(StopsText(plan.Stops));
            writer.End();
        }

        writer.Count("orders", Orders.Count);
        foreach (var order in Orders)
        {
            writer.Begin("order");
            writer.Field(order.ManagerId);
            writer.Raw(" " + order.AtMs.ToString(CultureInfo.InvariantCulture) + " ");
            writer.Field(order.CarId);
            writer.Raw(" " + order.Lap.ToString(CultureInfo.InvariantCulture) + " ");
            writer.Field(order.Kind);
            writer.Space();
            writer.Field(order.Pace);
            writer.Space();
            writer.Field(order.CompoundId ?? "");
            writer.Space();
            writer.Field(order.Engine);
            writer.Raw(order.On ? " 1" : " 0");
            writer.End();
        }
    }

    /// <summary>The stops as one text: <c>*</c> for the strategist, <c>-</c> for none, else <c>lap:compound</c> joined by commas.</summary>
    public static string StopsText(IReadOnlyList<WeekendPlannedStop>? stops) =>
        stops is null ? "*"
        : stops.Count == 0 ? "-"
        : string.Join(',', stops.Select(stop => stop.Lap.ToString(CultureInfo.InvariantCulture) + ":" + stop.CompoundId));

    /// <summary>The inverse of <see cref="StopsText"/>.</summary>
    public static IReadOnlyList<WeekendPlannedStop>? ParseStops(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text == "*")
        {
            return null;
        }

        if (text == "-")
        {
            return [];
        }

        return text.Split(',').Select(part =>
        {
            var colon = part.IndexOf(':', StringComparison.Ordinal);
            if (colon < 1 || colon == part.Length - 1)
            {
                throw new FormatException("A planned stop must be lap:compound, not '" + part + "'.");
            }

            return new WeekendPlannedStop(int.Parse(part[..colon], CultureInfo.InvariantCulture), part[(colon + 1)..]);
        }).ToArray();
    }
}
