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

    public static int AvailableFrom(StaffRole role)
    {
        EnsureRole(role);
        return role switch
        {
            StaffRole.HeadOfAerodynamics => GenerationEstimates.AeroAvailableFrom,
            StaffRole.CommercialDirector => GenerationEstimates.CommercialAvailableFrom,
            StaffRole.RaceEngineer => GenerationEstimates.RaceEngineerAvailableFrom,
            StaffRole.Strategist => GenerationEstimates.StrategistAvailableFrom,
            _ => GenerationEstimates.MinSeason,
        };
    }

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
