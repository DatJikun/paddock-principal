namespace Paddock.Domain.Board;

/// <summary>How hard a season the board is asking for (PP-058). Expected is the finish the public facts already imply.</summary>
public enum SeasonAmbition
{
    Safe = 0,
    Expected = 1,
    Ambitious = 2,
}

/// <summary>
/// The three season targets and what they pay. Pure: public facts in, a position and a reward out. No random number.
/// An ambitious failure is the only one that can dismiss a principal by itself; protection is applied by the caller.
/// </summary>
public static class SeasonTarget
{
    public const string Safe = "safe";
    public const string Expected = "expected";
    public const string Ambitious = "ambitious";

    public static bool TryParse(string? text, out SeasonAmbition ambition)
    {
        switch (text)
        {
            case Safe:
                ambition = SeasonAmbition.Safe;
                return true;
            case Expected:
                ambition = SeasonAmbition.Expected;
                return true;
            case Ambitious:
                ambition = SeasonAmbition.Ambitious;
                return true;
            default:
                ambition = SeasonAmbition.Expected;
                return false;
        }
    }

    public static string KeyOf(SeasonAmbition ambition) => ambition switch
    {
        SeasonAmbition.Safe => Safe,
        SeasonAmbition.Ambitious => Ambitious,
        _ => Expected,
    };

    /// <summary>
    /// What an AI principal picks. Contender (and the older name Pretender) takes the ambitious target, Survivor the safe one.
    /// Every other style, including a missing one, stays on the expected finish.
    /// </summary>
    public static SeasonAmbition ForArchetype(string? archetype) => archetype switch
    {
        "Contender" or "Pretender" => SeasonAmbition.Ambitious,
        "Survivor" or "Survival" => SeasonAmbition.Safe,
        _ => SeasonAmbition.Expected,
    };

    /// <summary>The championship position asked for. A larger number is easier. Clamped to the field.</summary>
    public static int Position(int expected, int fieldSize, SeasonAmbition ambition)
    {
        var size = Math.Max(1, fieldSize);
        var finish = Math.Clamp(expected, 1, size);
        var shift = ambition switch
        {
            SeasonAmbition.Safe => BoardEstimates.SafePlacesEasier,
            SeasonAmbition.Ambitious => -BoardEstimates.AmbitiousPlacesHarder,
            _ => 0,
        };
        return Math.Clamp(finish + shift, 1, size);
    }

    /// <summary>Confidence tenths gained when the target is met. Strictly safe &lt; expected &lt; ambitious.</summary>
    public static int RewardTenths(SeasonAmbition ambition) => ambition switch
    {
        SeasonAmbition.Safe => BoardEstimates.SafeObjectiveTenths,
        SeasonAmbition.Ambitious => BoardEstimates.AmbitiousObjectiveTenths,
        _ => BoardEstimates.SeasonObjectiveTenths,
    };

    /// <summary>Confidence tenths lost when the target is failed. The same order as the reward.</summary>
    public static int PenaltyTenths(SeasonAmbition ambition) => ambition switch
    {
        SeasonAmbition.Safe => BoardEstimates.SafeFailTenths,
        SeasonAmbition.Ambitious => BoardEstimates.AmbitiousFailTenths,
        _ => BoardEstimates.SeasonObjectiveTenths,
    };
}
