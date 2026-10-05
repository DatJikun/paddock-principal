using Paddock.Domain.Career;

namespace Paddock.Application.Career;

/// <summary>
/// How a career starts. The MVP has one path: take over an existing team (PP-050). Founding a team is the extension
/// point and is refused; nothing is built for it.
/// </summary>
public static class CareerStartPath
{
    public const string TakeOver = "take-over";

    /// <summary>Post-MVP (PP-050). The wizard refuses this word. No founding flow is implemented.</summary>
    public const string Found = "found";

    /// <summary>True when the player asked to found or buy a team instead of taking an existing one over.</summary>
    public static bool IsFounding(string teamId) =>
        string.Equals(teamId, CareerConfig.NewTeam, StringComparison.Ordinal)
        || string.Equals(teamId, Found, StringComparison.Ordinal)
        || string.Equals(teamId, "own", StringComparison.Ordinal);
}
