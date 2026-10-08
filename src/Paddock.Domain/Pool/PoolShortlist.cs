using System.Text;
using Paddock.Domain.Random;
using Paddock.Domain.World;

namespace Paddock.Domain.Pool;

/// <summary>
/// Which members of the talent pool a team's own scouts put in front of it in a season (#268, owner decision: each team recruits its own juniors,
/// not one pool shared by every team). A pure function of the team, the season and the member's handle: no state is stored and no random
/// number is drawn, so every run and every save show the same list, and the handle says nothing about who is real (INV-003).
/// A member the team recruited is always on its list; one another team recruited is on nobody else's.
/// </summary>
public static class PoolShortlist
{
    /// <summary>True when the member is on the team's list this season: its own junior, or free and picked by its scouts.</summary>
    public static bool Offers(OrganizationId team, int season, PoolMember member)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (member.Academy is { } owner)
        {
            return owner == team;
        }

        var key = "shortlist:v1:" + team.Value + ":" + season.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + member.HandleText;
        return Fnv1a64.Hash(Encoding.UTF8.GetBytes(key)) % 1000UL < (ulong)PoolEstimates.ShortlistShareMilli;
    }
}
