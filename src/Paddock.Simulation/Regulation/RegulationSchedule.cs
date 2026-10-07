using Paddock.Domain.Random;
using Paddock.Domain.Time;

namespace Paddock.Simulation.Regulation;

/// <summary>
/// When things happen in the political year of a series (#275). Every date is an ESTIMATE to confirm: the proposals of the teams
/// are taken from <see cref="ProposalsOpen"/> to <see cref="ProposalsClose"/>, the teams' ballot is announced the day after and
/// closes <see cref="RegulationEstimates.VotingDays"/> later, and the FIA's votes are spread over the summer, each open for the
/// same number of days. Every deadline falls before the season ends, so the result is stored for the next season while the
/// current one is untouched.
/// </summary>
public static class RegulationSchedule
{
    /// <summary>ESTIMATE: the first day a team may file a proposal.</summary>
    public static GameDate ProposalsOpen(int season) => new(season, 2, 1);

    /// <summary>ESTIMATE: the last day a team may file a proposal. The proposals filed by then become one ballot.</summary>
    public static GameDate ProposalsClose(int season) => new(season, 4, 30);

    /// <summary>The day the teams' ballot is announced.</summary>
    public static GameDate TeamBallotAnnounced(int season) => ProposalsClose(season).AddDays(1);

    /// <summary>The deadline of an item announced on <paramref name="announced"/>.</summary>
    public static GameDate DeadlineOf(GameDate announced) => announced.AddDays(RegulationEstimates.VotingDays);

    /// <summary>ESTIMATE: the first FIA vote of the year is announced on this day.</summary>
    public static GameDate FiaFirstAnnounced(int season) => new(season, 6, 1);

    /// <summary>ESTIMATE: the last FIA vote of the year is announced on this day at the latest, so its deadline is before 1 November.</summary>
    public static GameDate FiaLastAnnounced(int season) => new(season, 9, 30);

    /// <summary>How many FIA votes the series brings in a season: 4 to 6, drawn from the Regulations stream, a child per series and season.</summary>
    public static int FiaVotesIn(ulong masterSeed, string seriesId, int season)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seriesId);
        var span = RegulationEstimates.FiaVotesMax - RegulationEstimates.FiaVotesMin + 1;
        return RegulationEstimates.FiaVotesMin + Child(masterSeed, season, "fia-count:" + seriesId).NextInt(0, span);
    }

    /// <summary>The announcement days of the FIA's <paramref name="count"/> votes, evenly spread from the first to the last day.</summary>
    public static IReadOnlyList<GameDate> FiaAnnouncementDays(int season, int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        var first = FiaFirstAnnounced(season);
        var span = first.DaysUntil(FiaLastAnnounced(season));
        var days = new GameDate[count];
        for (var i = 0; i < count; i++)
        {
            days[i] = count == 1 ? first : first.AddDays(span * i / (count - 1));
        }

        return days;
    }

    /// <summary>
    /// The day an AI team looks at the chance to file a proposal, once a season, somewhere in the window. A child of the Regulations
    /// stream per team, so a team's day does not depend on how many other teams there are.
    /// </summary>
    public static GameDate AiConsiderationDay(ulong masterSeed, string seriesId, int season, string teamId)
    {
        var open = ProposalsOpen(season);
        var span = open.DaysUntil(ProposalsClose(season)) + 1;
        return open.AddDays(Child(masterSeed, season, "consider:" + seriesId + ":" + teamId).NextInt(0, span));
    }

    /// <summary>A child generator of the Regulations stream of <paramref name="season"/>, keyed by <paramref name="tag"/>. It never advances a shared state (INV-004).</summary>
    public static Xoshiro256StarStar Child(ulong masterSeed, int season, string tag) =>
        RngStream.Derive(masterSeed, RngStreamName.Regulations, season).DeriveChild(tag);
}
