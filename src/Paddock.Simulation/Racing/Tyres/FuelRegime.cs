using System.Globalization;
using Paddock.Domain.World;

namespace Paddock.Simulation.Racing.Tyres;

/// <summary>The kind of engine, which decides how much fuel a car needs and which cap applies.</summary>
public enum FuelEngineType
{
    NaturallyAspirated,
    Turbo,

    /// <summary>A turbo-hybrid; counts as forced induction for fuel caps.</summary>
    Hybrid,
}

/// <summary>
/// The fuel rules of one season: whether refuelling during the race is allowed and what the race fuel caps are.
/// </summary>
/// <param name="RefuellingAllowed">Catalog dimension <c>refuelling</c>.</param>
/// <param name="ForcedInductionCapKg">Race fuel cap for turbo and hybrid cars, kg; null if none.</param>
/// <param name="AspiratedCapKg">Race fuel cap for naturally aspirated cars, kg; null if none.</param>
/// <param name="AllowanceUnknown">True if the data says "unknown" (or has no value) for the race fuel allowance.</param>
public sealed record FuelRegime(bool RefuellingAllowed, double? ForcedInductionCapKg, double? AspiratedCapKg, bool AllowanceUnknown)
{
    /// <summary>Dimension id of the refuelling rule in the regulation catalog.</summary>
    public const string RefuellingDimension = "refuelling";

    /// <summary>Dimension id of the race fuel allowance in the regulation catalog.</summary>
    public const string AllowanceDimension = "race_fuel_allowance";

    /// <summary>
    /// Reads the fuel rules of a season from its rule set. A dimension the rule set does not have is not an error
    /// for the allowance (it counts as unknown) and needs <paramref name="refuellingFallback"/> for refuelling.
    /// </summary>
    /// <exception cref="InvalidOperationException">The refuelling dimension is missing and there is no fallback, or a value is not understood.</exception>
    public static FuelRegime FromRules(RuleSet rules, bool? refuellingFallback = null)
    {
        ArgumentNullException.ThrowIfNull(rules);

        bool refuelling;
        if (rules.Values.TryGetValue(RefuellingDimension, out var refuellingValue))
        {
            refuelling = refuellingValue switch
            {
                "allowed" => true,
                "banned" => false,
                _ => throw new InvalidOperationException(
                    $"Dimension '{RefuellingDimension}' has the value '{refuellingValue}', which is not understood."),
            };
        }
        else if (refuellingFallback is { } fallback)
        {
            refuelling = fallback;
        }
        else
        {
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Season {rules.Season} has no '{RefuellingDimension}' dimension and no fallback was given."));
        }

        if (!rules.Values.TryGetValue(AllowanceDimension, out var allowance))
        {
            return new FuelRegime(refuelling, null, null, true);
        }

        const double d = TyreFuelConstants.FuelDensityKgPerLitre;
        return allowance switch
        {
            "none" => new FuelRegime(refuelling, null, null, false),
            "unknown" => new FuelRegime(refuelling, null, null, true),
            "litres_220" => new FuelRegime(refuelling, 220 * d, 220 * d, false),
            "litres_195" => new FuelRegime(refuelling, 195 * d, 195 * d, false),
            "na_unlimited_turbo_restricted" => new FuelRegime(refuelling, TyreFuelConstants.TurboRestrictedLitres1987 * d, null, false),
            "turbo_150_na_unlimited" => new FuelRegime(refuelling, TyreFuelConstants.TurboLitres150 * d, null, false),
            "kg_105" => new FuelRegime(refuelling, 105, 105, false),
            "kg_110" => new FuelRegime(refuelling, 110, 110, false),
            _ => throw new InvalidOperationException(
                $"Dimension '{AllowanceDimension}' has the value '{allowance}', which is not understood."),
        };
    }
}
