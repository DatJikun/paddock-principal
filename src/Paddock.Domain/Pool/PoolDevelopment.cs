using Paddock.Domain.People;
using Paddock.Domain.Random;
using Paddock.Domain.World;

namespace Paddock.Domain.Pool;

/// <summary>
/// One season of development for a member of the pool: each attribute moves toward its hidden ceiling (DESIGN 2.1, "at a rate that
/// depends on potential and luck"). The member's luck for the season is drawn once and scales every attribute; a funded junior
/// programme scales it again. Attributes are whole numbers, so the expected step is split into its whole part and a chance for
/// one more point. Pure: the caller hands in a child of the <c>People</c> stream (INV-004).
/// All numbers are ESTIMATES in <see cref="PoolEstimates"/>.
/// </summary>
public static class PoolDevelopment
{
    /// <summary>
    /// The member's truth after one season. Draws one number for the luck and then one for each attribute (in key order),
    /// even when the attribute is already at its ceiling, so the draw sequence does not depend on the state.
    /// </summary>
    /// <param name="speedPercent">100 for an unfunded season, the programme's speed for a funded one.</param>
    public static PersonTruth Develop(PersonTruth truth, int speedPercent, Xoshiro256StarStar rng)
    {
        ArgumentNullException.ThrowIfNull(truth);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(speedPercent);
        var luckSpan = PoolEstimates.LuckMaxPercent - PoolEstimates.LuckMinPercent;
        var luckPercent = PoolEstimates.LuckMinPercent + (rng.NextDouble() * luckSpan);
        var fraction = Math.Min(1.0, PoolEstimates.DevelopmentRatePercent / 100.0 * (luckPercent / 100.0) * (speedPercent / 100.0));
        var next = new NamedAttribute[truth.Attributes.Count];
        for (var i = 0; i < next.Length; i++)
        {
            var current = truth.Attributes[i];
            var ceiling = truth.Potential[i].Value;
            var draw = rng.NextDouble();
            var expected = (ceiling - current.Value) * fraction;
            var whole = (int)Math.Floor(expected);
            var step = whole + (draw < expected - whole ? 1 : 0);
            next[i] = new NamedAttribute(current.Key, Math.Min(ceiling, current.Value + step));
        }

        return new PersonTruth(next, truth.Potential);
    }
}
