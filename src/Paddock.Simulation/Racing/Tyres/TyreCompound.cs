using System.Globalization;

namespace Paddock.Simulation.Racing.Tyres;

/// <summary>What a compound is for.</summary>
public enum TyreCompoundKind
{
    /// <summary>A normal dry race compound.</summary>
    Dry,

    /// <summary>A soft, short-lived compound made for one fast qualifying lap (some eras only).</summary>
    QualifyingSpecial,

    /// <summary>A wet-weather tyre. See <see cref="TyreCompoundCatalog.WetCompound"/>.</summary>
    Wet,
}

/// <summary>
/// One tyre compound. Pure data; all numbers ESTIMATE (see <see cref="TyreFuelConstants"/>).
/// </summary>
public sealed record TyreCompound
{
    public TyreCompound(string id, TyreCompoundKind kind, int hardness, double gripBase, double wearRate, double cliffLap, double warmUpLaps)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!double.IsFinite(gripBase))
        {
            throw new ArgumentOutOfRangeException(nameof(gripBase), "GripBase must be finite.");
        }

        if (!(double.IsFinite(wearRate) && wearRate > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(wearRate), "WearRate must be positive.");
        }

        if (!(double.IsFinite(cliffLap) && cliffLap > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(cliffLap), "CliffLap must be positive.");
        }

        if (!(double.IsFinite(warmUpLaps) && warmUpLaps >= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(warmUpLaps), "WarmUpLaps must not be negative.");
        }

        Id = id;
        Kind = kind;
        Hardness = hardness;
        GripBase = gripBase;
        WearRate = wearRate;
        CliffLap = cliffLap;
        WarmUpLaps = warmUpLaps;
    }

    /// <summary>Stable identifier; unique across every era of a catalog.</summary>
    public string Id { get; }

    public TyreCompoundKind Kind { get; }

    /// <summary>0 is the softest of its era's set, larger is harder. For ordering only.</summary>
    public int Hardness { get; }

    /// <summary>
    /// Seconds per lap lost on a fresh, warm set, relative to the grippiest race compound of the same era
    /// (so 0 for the grippiest; a qualifying special is negative). How fast an era is in absolute terms is the
    /// era layer of the lap-time model, not this number.
    /// </summary>
    public double GripBase { get; }

    /// <summary>Seconds per lap of loss added per lap of (effective) age at the start; the loss grows faster than linearly, see <see cref="TyreWear"/>.</summary>
    public double WearRate { get; }

    /// <summary>Effective age (laps) after which the tyre falls off a cliff.</summary>
    public double CliffLap { get; }

    /// <summary>Laps a fresh set needs to reach working temperature; its warm-up penalty is gone after this many laps.</summary>
    public double WarmUpLaps { get; }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Id} ({Kind})");
}
