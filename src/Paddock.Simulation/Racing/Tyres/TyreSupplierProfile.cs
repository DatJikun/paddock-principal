namespace Paddock.Simulation.Racing.Tyres;

/// <summary>
/// A tyre supplier's character (PP-030): a trade-off between grip and durability, and a bonus for the team that
/// the supplier develops tyres with. No tyre-war logic; the profile only turns a compound into the compound
/// this supplier makes of it. Numbers ESTIMATE.
/// </summary>
public sealed record TyreSupplierProfile
{
    /// <param name="id">Stable supplier id.</param>
    /// <param name="gripBalance">-1 (durable, slow) to +1 (grippy, wears fast); 0 is neutral. The trade-off is the same shape either way: what is gained in grip is paid in wear.</param>
    /// <param name="partnerTunedBonus">0 to 1: how much better the tyre is for the partner team it is tuned for (grip and wear, both).</param>
    public TyreSupplierProfile(string id, double gripBalance, double partnerTunedBonus)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!(double.IsFinite(gripBalance) && gripBalance >= -1 && gripBalance <= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(gripBalance), "Grip balance must be in [-1, 1].");
        }

        if (!(double.IsFinite(partnerTunedBonus) && partnerTunedBonus >= 0 && partnerTunedBonus <= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(partnerTunedBonus), "The partner bonus must be in [0, 1].");
        }

        Id = id;
        GripBalance = gripBalance;
        PartnerTunedBonus = partnerTunedBonus;
    }

    public string Id { get; }

    public double GripBalance { get; }

    public double PartnerTunedBonus { get; }

    /// <summary>A neutral supplier with no partner bonus.</summary>
    public static TyreSupplierProfile Neutral { get; } = new("neutral", 0, 0);

    /// <summary>Seconds per lap added to GripBase (negative = faster).</summary>
    public double GripOffsetSeconds(bool partnerTuned) =>
        -GripBalance * TyreFuelConstants.SupplierGripSwingSeconds
        - (partnerTuned ? PartnerTunedBonus * TyreFuelConstants.PartnerGripBonusSeconds : 0);

    /// <summary>Factor on WearRate (above 1 = wears faster).</summary>
    public double WearFactor(bool partnerTuned) =>
        (1 + GripBalance * TyreFuelConstants.SupplierDurabilitySwing)
        * (partnerTuned ? 1 - PartnerTunedBonus * TyreFuelConstants.PartnerWearSaving : 1);

    /// <summary>The compound as this supplier makes it. The cliff and warm-up are left as they are.</summary>
    public TyreCompound Apply(TyreCompound compound, bool partnerTuned)
    {
        ArgumentNullException.ThrowIfNull(compound);
        return new TyreCompound(
            compound.Id,
            compound.Kind,
            compound.Hardness,
            compound.GripBase + GripOffsetSeconds(partnerTuned),
            compound.WearRate * WearFactor(partnerTuned),
            compound.CliffLap,
            compound.WarmUpLaps);
    }
}
