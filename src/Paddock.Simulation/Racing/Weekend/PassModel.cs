namespace Paddock.Simulation.Racing.Weekend;

/// <summary>What the pass rule looks at when a faster car is stuck behind a slower one.</summary>
/// <param name="GapSeconds">Gap to the car in front at the start of the lap, seconds (not negative).</param>
/// <param name="ChaserLapSeconds">The chaser's lap time in clean air this lap, seconds.</param>
/// <param name="AheadLapSeconds">The car in front's lap time this lap, seconds.</param>
/// <param name="Overtaking">The chaser's overtaking rating, 0..100.</param>
/// <param name="Defending">The defender's defending rating, 0..100.</param>
public readonly record struct PassSituation(
    double GapSeconds,
    double ChaserLapSeconds,
    double AheadLapSeconds,
    double Overtaking,
    double Defending);

/// <summary>
/// The pass rule. DESIGN §7 does not say when a faster car may pass, so the orchestrator asks this small, replaceable
/// interface. An implementation is a pure function of its argument: no state, no RNG (INV-004, INV-005).
/// </summary>
public interface IPassModel
{
    /// <summary>A car this many seconds behind another runs in its dirty air; also the base pace advantage it needs to pass.</summary>
    double OvertakeMarginSeconds { get; }

    /// <summary>True when the chaser gets past the car in front on this lap.</summary>
    bool Passes(in PassSituation situation);
}

/// <summary>
/// The default pass rule (ESTIMATE, see <see cref="WeekendConstants"/>): the chaser passes when its pace advantage over the
/// car in front is more than the margin, where the margin is scaled by <c>1 + OvertakeSkillWeight * (defending - overtaking) / 100</c>.
/// </summary>
public sealed class MarginPassModel : IPassModel
{
    public static MarginPassModel Default { get; } = new();

    public double OvertakeMarginSeconds => WeekendConstants.OvertakeMarginSeconds;

    /// <summary>The pace advantage (seconds per lap) a chaser needs against a given defender.</summary>
    public double RequiredAdvantageSeconds(double overtaking, double defending) =>
        OvertakeMarginSeconds * (1d + (WeekendConstants.OvertakeSkillWeight * (defending - overtaking) / 100d));

    public bool Passes(in PassSituation situation)
    {
        var advantage = situation.AheadLapSeconds - situation.ChaserLapSeconds;
        return advantage > RequiredAdvantageSeconds(situation.Overtaking, situation.Defending);
    }
}
