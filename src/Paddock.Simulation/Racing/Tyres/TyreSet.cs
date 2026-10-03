namespace Paddock.Simulation.Racing.Tyres;

/// <summary>
/// The state of one set of tyres: how many laps it has run and how worn it is. Immutable; <see cref="AfterLap"/>
/// returns the next state. Deterministic and RNG-free. Temperature and graining are out of scope.
/// </summary>
/// <param name="Compound">The compound of the set.</param>
/// <param name="AgeLaps">Laps already run on the set.</param>
/// <param name="Wear">Accumulated effective age in laps of typical running; it grows by the wear multiplier of every lap, so changing conditions are summed correctly.</param>
public readonly record struct TyreSet(TyreCompound Compound, int AgeLaps, double Wear)
{
    /// <summary>A fresh set.</summary>
    public static TyreSet New(TyreCompound compound)
    {
        ArgumentNullException.ThrowIfNull(compound);
        return new TyreSet(compound, 0, 0);
    }

    /// <summary>True once the effective age is past the compound's cliff lap.</summary>
    public bool PastCliff => Wear > Compound.CliffLap;

    /// <summary>The wear loss of the lap about to be run (grip and wear, no warm-up), see <see cref="TyreWear.LossAtWear"/>.</summary>
    public double CurrentLossSeconds => TyreWear.LossAtWear(Compound, Wear);

    /// <summary>The whole tyre loss of the lap about to be run: <see cref="CurrentLossSeconds"/> plus the warm-up penalty.</summary>
    public double CurrentLapLossSeconds => CurrentLossSeconds + TyreWear.WarmUpLoss(Compound, AgeLaps);

    /// <summary>The set after one more lap run under <paramref name="conditions"/>.</summary>
    public TyreSet AfterLap(TyreConditions conditions) =>
        new(Compound, AgeLaps + 1, Wear + TyreWear.WearMultiplier(conditions));
}
