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
/// </summary>
public class HistoricalRulesRegressionTests
{
    private const ulong Seed = 7;

    private const string HashOnTheLastDayOf1955 = "8d5c7b62d509c19d5e97e002313160e940e15fa982295eeb0b0023d5660add82";

    private const string HashAfterTheSeasonTurned = "6eef70bc323526d2442e4f43ab136e95c3e5c2e8aa0e4a1c013cd00bf7512676";

    private const string HashAfterTwoSeasons = "df70e7bd358a6e4293d358ad1fa81c88d33659a37f40edf7c8aa128aba99a869";

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
