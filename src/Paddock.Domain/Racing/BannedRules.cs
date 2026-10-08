namespace Paddock.Domain.Racing;

/// <summary>
/// Mechanics that are known and described but are not part of the simulation: they stay in the catalogue as "possible to introduce
/// someday" and are never live (#275, owner decision of round 2). Each one is a rule id like any other, so a series' banned list can
/// name it and a refusal can show its name.
/// </summary>
public static class DormantRules
{
    /// <summary>A catch-up mechanic: the energy-recovery charge depends on the position, the leader charges least and the last car most.</summary>
    public const string ErsChargeByPosition = "ers_charge_by_position";

    /// <summary>A cash bonus for the team that finished last.</summary>
    public const string CashBonusLastPlace = "cash_bonus_last_place";

    /// <summary>A cash bonus for a team that has just been promoted into the series.</summary>
    public const string CashBonusPromotedTeam = "cash_bonus_promoted_team";

    /// <summary>The ids of every dormant mechanic, in ordinal order.</summary>
    public static IReadOnlyList<string> All { get; } = [CashBonusLastPlace, CashBonusPromotedTeam, ErsChargeByPosition];

    public static bool IsDormant(string ruleId) => All.Contains(ruleId, StringComparer.Ordinal);
}

/// <summary>
/// The rules and mechanics that can never be proposed or voted on in a championship, so that it keeps a stable core (#275, owner
/// decision of round 2). The list is authored data, per series: a series that has a list of its own uses exactly that list, and a
/// series without one uses the default. An id is a rule dimension (<c>safety_car</c>), a calendar dimension
/// (<c>calendar.circuit.&lt;circuit&gt;</c>) or a dormant mechanic (<see cref="DormantRules"/>).
/// </summary>
public sealed class BannedRules
{
    private readonly HashSet<string> _default;
    private readonly Dictionary<string, HashSet<string>> _bySeries;

    public BannedRules(IEnumerable<string> defaultList, IReadOnlyDictionary<string, IReadOnlyList<string>>? bySeries = null)
    {
        ArgumentNullException.ThrowIfNull(defaultList);
        _default = new HashSet<string>(defaultList, StringComparer.Ordinal);
        _bySeries = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        if (bySeries is not null)
        {
            foreach (var (series, list) in bySeries)
            {
                _bySeries[series] = new HashSet<string>(list, StringComparer.Ordinal);
            }
        }
    }

    /// <summary>No rule is banned. A data directory without the file reads as this.</summary>
    public static BannedRules None { get; } = new([]);

    /// <summary>The ids banned in <paramref name="seriesId"/>, in ordinal order: the series' own list, or the default when it has none.</summary>
    public IReadOnlyList<string> For(string seriesId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seriesId);
        var list = _bySeries.TryGetValue(seriesId, out var own) ? own : _default;
        var sorted = list.ToList();
        sorted.Sort(StringComparer.Ordinal);
        return sorted;
    }

    public bool IsBanned(string seriesId, string ruleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seriesId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        return (_bySeries.TryGetValue(seriesId, out var own) ? own : _default).Contains(ruleId);
    }

    /// <summary>True when the series has a list of its own (and so does not use the default).</summary>
    public bool HasOwnList(string seriesId) => _bySeries.ContainsKey(seriesId);

    public IReadOnlyList<string> Default
    {
        get
        {
            var sorted = _default.ToList();
            sorted.Sort(StringComparer.Ordinal);
            return sorted;
        }
    }
}
