using Paddock.Domain.Racing;
using Paddock.Domain.Random;
using Paddock.Domain.Time;
using Paddock.Simulation.Time;

namespace Paddock.Simulation.Regulation;

/// <summary>The days of one race weekend from its first session to its race day, both included. No ballot is open or counted on them.</summary>
public readonly record struct WeekendSpan(GameDate First, GameDate Last);

/// <summary>
/// When things happen in the political year of a series (#275, owner decision of round 2). There are no fixed calendar months: the
/// ballots are spread evenly over the gaps between the season's race weekends, each open between two weekends and counted before the
/// next one, so the player always has time to answer and a decision never lands inside a weekend. The dates move with the calendar.
/// <list type="bullet">
/// <item>A gap is the stretch from the day after one race to the day before the first session of the next weekend. A gap shorter than
/// <see cref="RegulationEstimates.MinimumWindowDays"/> holds no ballot.</item>
/// <item>The year has one slot per FIA vote (4 to 6) and one for the teams' ballot. Slot <c>j</c> of <c>n</c> goes to the usable gap at
/// the same relative place, so the votes are spread over the whole season; when there are fewer gaps than ballots, several share a gap.</item>
/// <item>The teams' ballot is the one in the middle of the year (<see cref="RegulationEstimates.TeamBallotShare"/>). Teams file their one
/// proposal from the first day of the season until the day before it opens, so they always have the whole winter and the first
/// weekends, and the proposals of the season become one ballot at the start of its slot.</item>
/// <item>Fewer than two weekends, no usable gap, or a calendar that is not laid out: the placeholder season window of the calendar
/// (<see cref="SeasonCalendar.WindowStartMonth"/> to <see cref="SeasonCalendar.WindowEndMonth"/>) is cut into stretches free of weekends and
/// those are used instead.</item>
/// <item>A career that starts inside the season only gets the gaps still ahead (the first slot opens no earlier than the day the
/// schedule is made).</item>
/// </list>
/// The schedule is a pure function of the weekends, the day it is made and the number of FIA votes. It is stored on the first day of the
/// season (see <see cref="SeriesRegulations.Schedule"/>), so the data it was made from may change afterwards without moving it.
/// </summary>
public static class RegulationSchedule
{
    /// <summary>The first day of the season: the day teams may start filing proposals.</summary>
    public static GameDate ProposalsOpen(int season) => GameDate.SeasonStart(season);

    /// <summary>The last day a ballot may be counted: the day before the season ends, so the next season is laid out after every vote.</summary>
    public static GameDate LastCountingDay(int season) => new(season, 12, 30);

    /// <summary>How many FIA votes the series brings in a season: 4 to 6, drawn from the Regulations stream, a child per series and season.</summary>
    public static int FiaVotesIn(ulong masterSeed, string seriesId, int season)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seriesId);
        var span = RegulationEstimates.FiaVotesMax - RegulationEstimates.FiaVotesMin + 1;
        return RegulationEstimates.FiaVotesMin + Child(masterSeed, season, "fia-count:" + seriesId).NextInt(0, span);
    }

    /// <summary>
    /// The political year of <paramref name="season"/>, made on <paramref name="today"/> from the weekends of the season's calendar (any
    /// order; empty when the calendar is not laid out) for <paramref name="fiaVotes"/> FIA votes and the teams' ballot.
    /// </summary>
    public static PoliticalSchedule Plan(int season, GameDate today, IReadOnlyList<WeekendSpan> weekends, int fiaVotes)
    {
        ArgumentNullException.ThrowIfNull(weekends);
        ArgumentOutOfRangeException.ThrowIfLessThan(fiaVotes, 0);
        var open = ProposalsOpen(season);
        var from = today.Year == season ? today : today.Year < season ? open : LastCountingDay(season);
        var last = LastCountingDay(season);
        var inSeason = weekends
            .Where(weekend => weekend.First.Year == season && weekend.Last.Year == season && weekend.Last >= weekend.First)
            .OrderBy(weekend => weekend.First)
            .ThenBy(weekend => weekend.Last)
            .ToList();

        var count = fiaVotes + 1;
        var stretches = Gaps(inSeason, from, last);
        if (stretches.Count == 0)
        {
            // No gap between two weekends (fewer than two weekends, weekends back to back, or no calendar): use the placeholder window
            // of the season, free of weekends, and cut it for even spacing. A set of real gaps is never cut: its ballots share a gap.
            var frameStart = new GameDate(season, SeasonCalendar.WindowStartMonth, SeasonCalendar.WindowStartDay);
            var frameEnd = new GameDate(season, SeasonCalendar.WindowEndMonth, SeasonCalendar.WindowEndDay);
            stretches = FreeStretches(inSeason, frameStart < from ? from : frameStart, frameEnd < last ? frameEnd : last);
            SplitUntil(stretches, count);
        }

        if (stretches.Count == 0)
        {
            return new PoliticalSchedule(season, open, open.AddDays(-1), []);
        }

        var teamIndex = Math.Min(count - 1, (int)(count * RegulationEstimates.TeamBallotShare));
        var slots = new List<BallotSlot>(count);
        for (var j = 0; j < count; j++)
        {
            var stretch = stretches[(int)((2L * j + 1) * stretches.Count / (2L * count))];
            slots.Add(new BallotSlot(j == teamIndex ? BallotSlotKind.Teams : BallotSlotKind.Fia, stretch.Opens, stretch.Closes));
        }

        return new PoliticalSchedule(season, open, slots[teamIndex].Opens.AddDays(-1), slots);
    }

    /// <summary>
    /// The day an AI team looks at the chance to file a proposal, once a season, somewhere in the window; null when the window is empty.
    /// A child of the Regulations stream per team, so a team's day does not depend on how many other teams there are.
    /// </summary>
    public static GameDate? AiConsiderationDay(ulong masterSeed, string seriesId, int season, string teamId, PoliticalSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        var open = schedule.ProposalsOpen;
        var span = open.DaysUntil(schedule.ProposalsClose) + 1;
        if (span < 1)
        {
            return null;
        }

        return open.AddDays(Child(masterSeed, season, "consider:" + seriesId + ":" + teamId).NextInt(0, span));
    }

    /// <summary>A child generator of the Regulations stream of <paramref name="season"/>, keyed by <paramref name="tag"/>. It never advances a shared state (INV-004).</summary>
    public static Xoshiro256StarStar Child(ulong masterSeed, int season, string tag) =>
        RngStream.Derive(masterSeed, RngStreamName.Regulations, season).DeriveChild(tag);

    private readonly record struct Stretch(GameDate Opens, GameDate Closes)
    {
        public int Days => Opens.DaysUntil(Closes) + 1;
    }

    /// <summary>The gaps between consecutive weekends that are still ahead of <paramref name="from"/> and long enough to hold a ballot.</summary>
    private static List<Stretch> Gaps(List<WeekendSpan> weekends, GameDate from, GameDate last)
    {
        var gaps = new List<Stretch>();
        for (var i = 0; i + 1 < weekends.Count; i++)
        {
            var opens = weekends[i].Last.AddDays(1);
            var closes = weekends[i + 1].First.AddDays(-1);
            if (opens < from)
            {
                opens = from;
            }

            if (closes > last)
            {
                closes = last;
            }

            if (closes >= opens && opens.DaysUntil(closes) + 1 >= RegulationEstimates.MinimumWindowDays)
            {
                gaps.Add(new Stretch(opens, closes));
            }
        }

        return gaps;
    }

    /// <summary>The stretches of <c>[start, end]</c> that no weekend touches and that can hold a ballot.</summary>
    private static List<Stretch> FreeStretches(List<WeekendSpan> weekends, GameDate start, GameDate end)
    {
        var free = new List<Stretch>();
        var cursor = start;
        foreach (var weekend in weekends)
        {
            if (weekend.Last < cursor)
            {
                continue;
            }

            if (weekend.First > end)
            {
                break;
            }

            var closes = weekend.First.AddDays(-1);
            if (closes >= cursor && cursor.DaysUntil(closes) + 1 >= RegulationEstimates.MinimumWindowDays)
            {
                free.Add(new Stretch(cursor, closes));
            }

            cursor = weekend.Last.AddDays(1);
        }

        if (end >= cursor && cursor.DaysUntil(end) + 1 >= RegulationEstimates.MinimumWindowDays)
        {
            free.Add(new Stretch(cursor, end));
        }

        return free;
    }

    /// <summary>Cuts the longest stretch (the earliest of equals) in two until there are <paramref name="count"/> of them or none is long enough to cut.</summary>
    private static void SplitUntil(List<Stretch> stretches, int count)
    {
        while (stretches.Count > 0 && stretches.Count < count)
        {
            var longest = 0;
            for (var i = 1; i < stretches.Count; i++)
            {
                if (stretches[i].Days > stretches[longest].Days)
                {
                    longest = i;
                }
            }

            var chosen = stretches[longest];
            if (chosen.Days < 2 * RegulationEstimates.MinimumWindowDays)
            {
                return;
            }

            var firstHalf = chosen.Days / 2;
            stretches[longest] = new Stretch(chosen.Opens, chosen.Opens.AddDays(firstHalf - 1));
            stretches.Insert(longest + 1, new Stretch(chosen.Opens.AddDays(firstHalf), chosen.Closes));
        }
    }
}
