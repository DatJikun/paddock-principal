namespace Paddock.Domain.Infrastructure;

/// <summary>
/// Team facilities (DESIGN §4.3). The 1950s hold a factory. Later kinds exist in data and unlock with their era.
/// Engine dyno and foundry are a later update (PP-064). A test track is rented per test, not owned.
/// </summary>
public enum FacilityKind
{
    Factory = 0,
    WindTunnel = 1,
    Cfd = 2,
    Simulator = 3,
}

/// <summary>Stable authored ids of <see cref="FacilityKind"/> (camelCase, the JSON form).</summary>
public static class FacilityKindIds
{
    public const string Factory = "factory";

    public const string WindTunnel = "windTunnel";

    public const string Cfd = "cfd";

    public const string Simulator = "simulator";

    public static string Of(FacilityKind kind) => kind switch
    {
        FacilityKind.Factory => Factory,
        FacilityKind.WindTunnel => WindTunnel,
        FacilityKind.Cfd => Cfd,
        FacilityKind.Simulator => Simulator,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown facility kind."),
    };

    public static bool TryParse(string? text, out FacilityKind kind)
    {
        kind = default;
        if (text is null)
        {
            return false;
        }

        if (Enum.TryParse(text, ignoreCase: false, out FacilityKind parsed) && Enum.IsDefined(parsed) && parsed.ToString() == text)
        {
            kind = parsed;
            return true;
        }

        kind = text switch
        {
            Factory => FacilityKind.Factory,
            WindTunnel => FacilityKind.WindTunnel,
            Cfd => FacilityKind.Cfd,
            Simulator => FacilityKind.Simulator,
            _ => (FacilityKind)(-1),
        };
        return Enum.IsDefined(kind);
    }
}
