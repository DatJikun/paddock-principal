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
/// Re-pinned again in #268 (infrastructure): a career pays its race travel from the ledger, so the season's transport share changes the state hash.
/// Re-pinned again in #270 (crash damage): a crash can damage a car and a repair costs money, so a 1955 career's ledger and hash move.
/// Re-pinned again in #325 (real contracts): the real staff of the authored data (Enzo Ferrari, Colotti, Maddock and others) now hold a
/// contract to the end of their authored stint instead of the end of 1955, and contract end dates are in the hash. The 1955 fixture has no
/// people cache, so every driver is generated: no driver contract, role or id moved, and the regulations are untouched.
/// Re-pinned again in #315 (board targets): the three season targets sit one place apart near the front (they were two), so the AI
/// boards' season objectives and expected positions move, the target decision carries each bonus, and a met target pays the board's
/// bonus into the ledger at the season end. No RNG stream or id moved; later seasons can differ because the teams hold more cash.
/// Re-pinned again in #318 (playtest 2): every contract of the first morning is paid from the era scale instead of 0 (salaries are in the
/// hash and the monthly payroll moves every ledger), each team knows its own drivers, and a staff member under contract is no longer a
/// free agent (an AI signing of one starts after that contract). Pinned again after merging #315: these values carry both the board target change and the starting pay.
/// </summary>
public class HistoricalRulesRegressionTests
{
    private const ulong Seed = 7;

    private const string HashOnTheLastDayOf1955 = "55b2d9031e02a52f504173ea556bae9d96caf98a5ad3134bc3552251cbb47825";

    private const string HashAfterTheSeasonTurned = "20bad8b25db8dc1e8b27da1b5477901c159011dc30c332b39466f50af7664a58";

    private const string HashAfterTwoSeasons = "1da9f30cf71bc1bb0ea41b513c875522e166ce264f0658b7562810d766a1f1ed";

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
    [Trait(TestCategory.Name, TestCategory.Slow)]
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
    [Trait(TestCategory.Name, TestCategory.Slow)]
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
