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
/// seed 7). Re-pinned twice for the transport of #268 (PP-070): a 1955 team pays its race travel from the ledger, first with raised
/// per-round shares and then with one season share split over the rounds of the season (<c>InfrastructureEstimates.LogisticsSeasonShare</c>). With the
/// old shares these tests give the values captured on main; nothing else moved, and the regulations are untouched.
/// Edit them only after a reviewed change to the day rules; this test prints the actual values and never writes them.
/// </summary>
public class HistoricalRulesRegressionTests
{
    private const ulong Seed = 7;

    private const string HashOnTheLastDayOf1955 = "48f64a58646f4fdd2c9c99105a3ead6341b3a61071d732e17cb9546d8cf86dcf";

    private const string HashAfterTheSeasonTurned = "7c154a27ef93ea8b944c0067f1cebe37e4fc2e14fa2b6e5265af2bcb281ff5ae";

    private const string HashAfterTwoSeasons = "6e567e0e5b6ed8a0e3a0fd1e941d40a8388f02e1692b9fbc3f01c2b100984a2b";

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
