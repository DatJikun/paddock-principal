namespace Paddock.Domain.People;

/// <summary>
/// Key staff roles from DESIGN §6.2, plus the team principal from PP-044.
/// Scout has the two attributes listed in that table. Innovation is stored on every
/// person as well, including roles whose own list already contains it.
/// </summary>
public enum StaffRole
{
    TechnicalDirector = 0,
    ChiefDesigner = 1,
    EngineDesigner = 2,
    HeadOfAerodynamics = 3,
    HeadOfVehicleDynamics = 4,
    RaceEngineer = 5,
    Strategist = 6,
    ChiefMechanic = 7,
    Scout = 8,
    CommercialDirector = 9,
    TeamPrincipal = 10,
}

public static class StaffCatalogue
{
    public const string InnovationKey = "innovation";

    /// <summary>
    /// Every team role exists from 1950 (PP-059). The era downforce cap, not this date, is what makes an aero head
    /// matter little before wings. The old era constants on <see cref="GenerationEstimates"/> stay as history.
    /// </summary>
    public static int AvailableFrom(StaffRole role)
    {
        EnsureRole(role);
        return GenerationEstimates.MinSeason;
    }

    /// <summary>
    /// Roles hidden from the team and the market for now (#265): nothing the player can understand reads them, or what reads them
    /// uses a neutral value instead. The single switch: remove a role from this list and it is back on every roster, hired again
    /// and shown again. The enum values and every old contract stay, so no save breaks.
    /// </summary>
    public static IReadOnlyList<StaffRole> HiddenRoles { get; } = [StaffRole.Strategist, StaffRole.ChiefMechanic, StaffRole.CommercialDirector];

    /// <summary>A chair on the team roster. The engine designer sits at the engine maker and the principal is not staff.</summary>
    public static bool IsTeamRoster(StaffRole role)
    {
        EnsureRole(role);
        // Strategist and ChiefMechanic are hidden (#265): nothing in the simulation reads them yet. The enum values stay so old
        // saves load; their holders just are not shown, hired or replaced. They come back when a system reads them.
        return role is not (StaffRole.EngineDesigner or StaffRole.TeamPrincipal) && !HiddenRoles.Contains(role);
    }

    /// <summary>Team chairs, in enum order, without the engine designer and the team principal.</summary>
    public static IReadOnlyList<StaffRole> TeamRoster { get; } =
        Enum.GetValues<StaffRole>().Where(IsTeamRoster).ToArray();

    public static IReadOnlyList<string> AttributeKeys(StaffRole role)
    {
        EnsureRole(role);
        return role switch
        {
            StaffRole.TechnicalDirector => ["vision", "project_management", InnovationKey],
            StaffRole.ChiefDesigner => ["chassis", "integration", "precision"],
            StaffRole.EngineDesigner => ["power", "reliability", "efficiency"],
            StaffRole.HeadOfAerodynamics => ["aerodynamics", "tunnel_correlation", InnovationKey],
            StaffRole.HeadOfVehicleDynamics => ["suspension", "tyres", "tyre_temperature"],
            StaffRole.RaceEngineer => ["setup", "driver_relationship", "data_analysis"],
            StaffRole.Strategist => ["strategy", "reaction", "weather"],
            StaffRole.ChiefMechanic => ["pit_stops", "build_quality", "organisation"],
            StaffRole.Scout => ["talent_judgement", "contact_network"],
            StaffRole.CommercialDirector => ["negotiation", "marketing", "network"],
            StaffRole.TeamPrincipal => ["negotiation", "people_management", "politics", "business"],
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown staff role."),
        };
    }

    private static void EnsureRole(StaffRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown staff role.");
        }
    }
}
