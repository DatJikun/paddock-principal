namespace Paddock.Simulation.Racing.Incidents;

/// <summary>
/// The fatality-risk band of an era: the value of the <c>fatality_risk</c> era dimension
/// (<c>data/authored/eras/f1_timeline.json</c>). It scales how often incidents happen and how severe they are.
/// </summary>
public enum FatalityRiskBand
{
    VeryHigh,
    High,
    Moderate,
    Low,
    VeryLow,
}

/// <summary>What the era's rules offer to slow or stop a race after an incident.</summary>
public enum NeutralisationRules
{
    /// <summary>No neutralisation: the race simply goes on (the 1950s).</summary>
    None,

    /// <summary>Marshals and local yellow flags; no safety car.</summary>
    MarshalsAndYellowFlags,

    /// <summary>A physical safety car (1993 onwards).</summary>
    SafetyCar,

    /// <summary>A physical safety car and a virtual safety car (2015 onwards).</summary>
    SafetyCarAndVsc,
}

/// <summary>Parsing of the authored era values. Simulation does not reference the data project, so the adapter passes the strings.</summary>
public static class FatalityRiskBands
{
    /// <summary>Parses an authored <c>fatality_risk</c> value such as <c>very_high</c>.</summary>
    public static FatalityRiskBand Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value switch
        {
            "very_high" => FatalityRiskBand.VeryHigh,
            "high" => FatalityRiskBand.High,
            "moderate" => FatalityRiskBand.Moderate,
            "low" => FatalityRiskBand.Low,
            "very_low" => FatalityRiskBand.VeryLow,
            _ => throw new ArgumentException($"Unknown fatality_risk value '{value}'.", nameof(value)),
        };
    }
}

/// <summary>
/// The safety facts of one season that the incident model needs.
/// Data fields used: the era dimension <c>fatality_risk</c> (<c>data/authored/eras/f1_timeline.json</c>) and the regulation
/// dimension <c>safety_car</c> (<c>data/authored/regulations/f1_timeline.json</c>: <c>none</c>, <c>physical</c>, <c>physical_and_vsc</c>).
/// The data has no dimension for marshals and yellow flags, or for red flags before 2000, so both start at
/// <see cref="IncidentConstants.MarshalsAndFlagsFromSeason"/> (ESTIMATE). A caller without the data can build the record directly.
/// </summary>
/// <param name="Risk">The fatality-risk band.</param>
/// <param name="Rules">What the era's rules offer for neutralising a race.</param>
/// <param name="RedFlagAvailable">Whether a race can be stopped with a red flag and then resumed.</param>
public sealed record EraSafetyProfile(FatalityRiskBand Risk, NeutralisationRules Rules, bool RedFlagAvailable)
{
    public bool HasSafetyCar => Rules is NeutralisationRules.SafetyCar or NeutralisationRules.SafetyCarAndVsc;

    public bool HasVsc => Rules == NeutralisationRules.SafetyCarAndVsc;

    /// <summary>Builds the profile from the authored values of a season.</summary>
    /// <param name="season">The season.</param>
    /// <param name="fatalityRisk">Authored <c>fatality_risk</c> value of the season.</param>
    /// <param name="safetyCar">Authored <c>safety_car</c> value of the season.</param>
    public static EraSafetyProfile FromAuthored(int season, string fatalityRisk, string safetyCar)
    {
        ArgumentNullException.ThrowIfNull(safetyCar);
        var marshals = season >= IncidentConstants.MarshalsAndFlagsFromSeason;
        var rules = safetyCar switch
        {
            "physical_and_vsc" => NeutralisationRules.SafetyCarAndVsc,
            "physical" => NeutralisationRules.SafetyCar,
            "none" => marshals ? NeutralisationRules.MarshalsAndYellowFlags : NeutralisationRules.None,
            _ => throw new ArgumentException($"Unknown safety_car value '{safetyCar}'.", nameof(safetyCar)),
        };
        return new EraSafetyProfile(FatalityRiskBands.Parse(fatalityRisk), rules, marshals);
    }
}
