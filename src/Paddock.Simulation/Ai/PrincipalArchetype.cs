namespace Paddock.Simulation.Ai;

/// <summary>The four styles of DESIGN section 8: everything now, a long plan, selective opportunism, and getting through on the budget.</summary>
public enum PrincipalArchetype
{
    /// <summary>Pretendent: everything on the present season.</summary>
    Contender = 0,

    /// <summary>Budowniczy: a long plan, infrastructure, young talent.</summary>
    Builder = 1,

    /// <summary>Oportunista: aims at bargains and chances, short commitments.</summary>
    Opportunist = 2,

    /// <summary>Przetrwanie: the budget first, and the safe choice.</summary>
    Survivor = 3,
}

/// <summary>Text forms of the archetypes, as the world section stores them.</summary>
public static class PrincipalArchetypes
{
    public static IReadOnlyList<PrincipalArchetype> All { get; } =
        [PrincipalArchetype.Contender, PrincipalArchetype.Builder, PrincipalArchetype.Opportunist, PrincipalArchetype.Survivor];

    public static string Text(PrincipalArchetype archetype) => archetype.ToString();

    public static bool TryParse(string? text, out PrincipalArchetype archetype)
    {
        foreach (var candidate in All)
        {
            if (string.Equals(candidate.ToString(), text, StringComparison.Ordinal))
            {
                archetype = candidate;
                return true;
            }
        }

        archetype = default;
        return false;
    }
}

/// <summary>
/// What an archetype cares about: the weights of the utility terms and its attitude to risk and money. Every value is an
/// ESTIMATE (open question 1 of issue #109): DESIGN section 8 names the archetypes but gives no weights.
/// </summary>
/// <param name="Now">Weight of results this season.</param>
/// <param name="Future">Weight of the next seasons (the next car, young talent).</param>
/// <param name="Thrift">Weight of money kept.</param>
/// <param name="Stability">Weight of continuity (keeping people, keeping a plan).</param>
/// <param name="Upside">Weight of a person's hidden-looking potential, as far as the principal's own belief shows it.</param>
/// <param name="RiskAversion">0 (takes any chance) to 1 (fears uncertainty): discounts a wide band of belief.</param>
/// <param name="PayStretch">Share above a person's market ask the principal is willing to pay (negative: below it).</param>
/// <param name="SacrificeBias">0 to 1: how readily the season is written off for the next car.</param>
/// <param name="PayrollShare">The most of the annual budget the payroll may take.</param>
public sealed record ArchetypeProfile(
    double Now,
    double Future,
    double Thrift,
    double Stability,
    double Upside,
    double RiskAversion,
    double PayStretch,
    double SacrificeBias,
    double PayrollShare)
{
    /// <summary>ESTIMATE profiles.</summary>
    public static ArchetypeProfile Of(PrincipalArchetype archetype) => archetype switch
    {
        PrincipalArchetype.Contender => new ArchetypeProfile(1.0, 0.3, 0.2, 0.4, 0.3, 0.2, 0.25, 0.0, 0.55),
        PrincipalArchetype.Builder => new ArchetypeProfile(0.4, 1.0, 0.5, 0.9, 0.9, 0.5, 0.05, 0.8, 0.40),
        PrincipalArchetype.Opportunist => new ArchetypeProfile(0.6, 0.5, 0.6, 0.2, 0.6, 0.1, 0.10, 0.4, 0.45),
        PrincipalArchetype.Survivor => new ArchetypeProfile(0.3, 0.3, 1.0, 0.6, 0.2, 0.9, -0.10, 0.1, 0.30),
        _ => throw new ArgumentOutOfRangeException(nameof(archetype), archetype, "Unknown archetype."),
    };

    /// <summary>ESTIMATE: seasons asked for in a supply deal: a planner commits long, a survivor stays flexible.</summary>
    public int SupplySeasons => Stability >= 0.8 ? AiEstimates.MaxSupplySeasons : Stability >= 0.5 ? 2 : 1;
}
