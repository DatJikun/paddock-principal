using Paddock.Application.Career;
using Paddock.Domain.Cars;
using Paddock.Domain.Finance;
using Paddock.Domain.Supply;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Tests.Career;

/// <summary>
/// #327: the car level on a team card keeps the order of the real car strengths and does not saturate for the top teams. Fixtures
/// only (the Jolpica cache is not in CI): Williams 74.7, Benetton 61.5 and Ferrari 61.0 are the 1996 starting strengths from the
/// issue; every other number is an ESTIMATE written for this test.
/// </summary>
public sealed class TeamCardLevelsTests
{
    private static readonly (string Team, double Strength)[] Field1996 =
    [
        ("williams", 74.7), ("benetton", 61.5), ("ferrari", 61.0), ("mclaren", 55.0), ("jordan", 47.0), ("ligier", 44.0),
        ("sauber", 40.0), ("tyrrell", 36.0), ("footwork", 33.0), ("minardi", 24.0), ("forti", 14.0),
    ];

    private static readonly (string Team, double Strength)[] Field2010 =
    [
        ("red-bull", 82.0), ("mclaren", 78.0), ("ferrari", 77.5), ("mercedes", 70.0), ("renault", 62.0), ("williams", 58.0),
        ("force-india", 56.0), ("sauber", 50.0), ("toro-rosso", 49.0), ("lotus", 35.0), ("hrt", 22.0), ("virgin", 21.0),
    ];

    private sealed class NoTiers : ITeamTierSource
    {
        public TeamTier TierOf(OrganizationId organization, int startSeason) => TeamTier.Typical;
    }

    private static Dictionary<string, TeamCardView> Cards(int year, (string Team, double Strength)[] field)
    {
        var today = new GameDate(year, 1, 1);
        var world = WorldState.At(today);
        foreach (var (team, _) in field)
        {
            (world, _) = world.AddOrganization(new OrganizationSpec(
                OrganizationKind.Team,
                true,
                team,
                new GameDate(1950, 1, 1),
                null,
                1_000_000,
                [new OrganizationNameSpan(team, new GameDate(1950, 1, 1), null)]));
        }

        var strength = new MapCarStrengthSource(field.Select(row => (row.Team, year, row.Strength)).ToArray());
        var cards = TeamCardsRead.Of(world, today, [], new NoTiers(), new Dictionary<string, int>(), strength);
        return cards.ToDictionary(card => card.Id, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData(1996)]
    [InlineData(2010)]
    public void TheCarLevelKeepsTheOrderOfTheStrengthsAndOnlyTheBestCarHoldsTheTop(int year)
    {
        var field = year == 1996 ? Field1996 : Field2010;
        var cards = Cards(year, field);
        var levels = field.Select(row => (row.Team, Level: cards[row.Team].Levels.Car!.Value)).ToArray();

        // Stronger never shows lower, and the strongest car shows the top level alone: a close rival is not at the maximum.
        for (var i = 1; i < levels.Length; i++)
        {
            Assert.True(levels[i - 1].Level >= levels[i].Level, $"{levels[i - 1].Team} vs {levels[i].Team}");
        }

        Assert.Equal(levels.Max(row => row.Level), levels[0].Level);
        Assert.Single(levels, row => row.Level == levels[0].Level);
        Assert.True(levels[0].Level > levels[2].Level);
        Assert.True(levels[^1].Level < levels[0].Level);
    }

    [Fact]
    public void WilliamsIsAheadOfFerrariIn1996AndRedBullAheadOfEveryoneIn2010()
    {
        var cards1996 = Cards(1996, Field1996);
        Assert.True(cards1996["williams"].Levels.Car > cards1996["ferrari"].Levels.Car);
        Assert.True(cards1996["williams"].Levels.Car > cards1996["benetton"].Levels.Car);

        var cards2010 = Cards(2010, Field2010);
        foreach (var other in Field2010.Skip(1))
        {
            Assert.True(cards2010["red-bull"].Levels.Car > cards2010[other.Team].Levels.Car, other.Team);
        }
    }
}
