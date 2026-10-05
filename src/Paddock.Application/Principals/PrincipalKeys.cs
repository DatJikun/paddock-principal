using Paddock.Application.Localization;
using Paddock.Application.Managers;
using Paddock.Domain.World;

namespace Paddock.Application.Principals;

/// <summary>
/// Player-facing keys of the AI principals' own command (PP-021) and the rule that names their managers. The texts of a decision trace are
/// <see cref="Paddock.Simulation.Ai.AiTextKeys"/>; copy for both lives in <c>strings/pl.json</c> and <c>strings/en.json</c>.
/// </summary>
public static class PrincipalKeys
{
    /// <summary>Prefix of the manager id of the AI that runs a team: <c>ai:{organizationId}</c>.</summary>
    public const string ManagerPrefix = "ai:";

    [TranslationKey]
    public const string NotAnAiManager = "ai.error.notAnAiManager";

    [TranslationKey]
    public const string NotInControl = "ai.error.notInControl";

    [TranslationKey]
    public const string HumanTeam = "ai.error.humanTeam";

    [TranslationKey]
    public const string UnknownOrganization = "ai.error.unknownOrganization";

    [TranslationKey]
    public const string NotATeam = "ai.error.notATeam";

    [TranslationKey]
    public const string BadArchetype = "ai.error.badArchetype";

    [TranslationKey]
    public const string BadPerson = "ai.error.badPerson";

    [TranslationKey]
    public const string BadDate = "ai.error.badDate";

    [TranslationKey]
    public const string BadSeason = "ai.error.badSeason";

    [TranslationKey]
    public const string BadRoles = "ai.error.badRoles";

    /// <summary>The manager that runs <paramref name="organization"/> for the AI. One per team, so a decision is always a team's own.</summary>
    public static ManagerId ManagerOf(OrganizationId organization) => new(ManagerPrefix + organization.Value);

    public static bool IsPrincipalManager(ManagerId manager) =>
        manager.IsAssigned && manager.Value.StartsWith(ManagerPrefix, StringComparison.Ordinal) && manager.Value.Length > ManagerPrefix.Length;
}
