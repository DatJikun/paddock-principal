using Paddock.Application.Career;
using Paddock.Domain.Career;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Tests.Career;

namespace Paddock.Tests.Regulation;

/// <summary>
/// Regression guard for regulation voting v2 (#275): a career whose rules are historical never writes the
/// <c>regulations</c> section and keeps its results and state hashes exactly as they were before voting v2 existed.
/// The golden values were captured on main (commit 0d0f46d) without voting v2 (the same values come out of main and of this branch), with the same fixture (Balanced preset, 1955,
/// seed 7). Edit them only after a reviewed change to the day rules; this test prints the actual values and never writes them.
/// Re-pinned in #265 (people, contracts, staff, market): the staff roster changes (two race engineers per team, no strategist, chief mechanic or
/// commercial director on it), and a 1955 career's world contains its people, so its hash moves. The regulations are untouched.
/// Re-pinned again in #268 (sponsors): the sponsors section is schema 2 and sponsor amounts are scaled, so a 1955 career's hash moves.
/// Re-pinned again in #268 (academy, PP-069): the talent pool section moved to schema 2, juniors are recruited into an academy per team and the
/// programmes changed their speed-up; none of it touches the regulations.
/// </summary>
public class HistoricalRulesRegressionTests
{
    private const ulong Seed = 7;

    private const string HashOnTheLastDayOf1955 = "3fd4e4d40013a55250791189dc31a93a205fef6bd76feaff1ab425bbefa044a0";

    private const string HashAfterTheSeasonTurned = "55396a1f604612af467e5a47b58c150974897e4f9ceaf2e74d3ef33663bf0779";

    private const string HashAfterTwoSeasons = "3e6feb85a858c6adad96ad0d96beb03d20347f4ab350dcab8d0dbef8e090b0c2";

    private static (string Hash, bool HasRegulations, int Season, int Rounds, int NextSessions) Probe(GameDate stopOn)
    {
        var opened = CareerKit.Opened(CareerPreset.Balanced, 1955, Seed);
        CareerHost.RunUntil(opened.Session, stopOn, null, CareerKit.OptionsFor(opened));
        var world = opened.Session.World;
        var championship = world.Section<ChampionshipSection>(ChampionshipSection.SectionName);
        var calendar = world.Section<RaceCalendarSection>(RaceCalendarSection.SectionName);
        return (
            world.StateHash(),
            world.Section(RegulationsSection.SectionName) is not null,
            championship?.Season ?? 0,
            championship?.RoundsCompleted ?? 0,
            calendar?.SeasonSessions(stopOn.Year).Count ?? 0);
    }

    [Fact]
    public void AHistoricalSeasonKeepsItsHashAndItsResults()
    {
        var end = Probe(new GameDate(1955, 12, 30));

        Assert.False(end.HasRegulations);
        Assert.Equal(1955, end.Season);
        Assert.Equal(7, end.Rounds);
        Assert.Equal(21, end.NextSessions);
        Assert.Equal(HashOnTheLastDayOf1955, end.Hash);
    }

    [Fact]
    public void AHistoricalCareerLaysOutTheNextCalendarFromTheAuthoredMapAndKeepsItsHash()
    {
        var turned = Probe(new GameDate(1956, 1, 3));

        Assert.False(turned.HasRegulations);
        Assert.Equal(24, turned.NextSessions);
        Assert.Equal(HashAfterTheSeasonTurned, turned.Hash);

        var twice = Probe(new GameDate(1957, 1, 3));

        Assert.False(twice.HasRegulations);
        Assert.Equal(24, twice.NextSessions);
        Assert.Equal(HashAfterTwoSeasons, twice.Hash);
    }
}
