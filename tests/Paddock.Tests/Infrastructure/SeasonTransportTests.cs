using Paddock.Domain.Finance;
using Paddock.Domain.Infrastructure;

namespace Paddock.Tests.Infrastructure;

/// <summary>
/// The transport of a season (#268, owner: the player must never worry about it, and a long calendar must not make the year dearer). The season
/// costs one share of the era's typical yearly budget whatever the number of rounds, split over the rounds by how far each one is. Every
/// figure is an ESTIMATE in <see cref="InfrastructureEstimates"/>; these tests pin the rules, not the numbers.
/// </summary>
public sealed class SeasonTransportTests
{
    // The seven rounds of 1955 as the game plans them for a European team: Argentina overseas, the Indianapolis 500 overseas, the rest lorry trips.
    private static readonly string[] Season1955 = ["ARG", "MCO", "USA", "BEL", "NLD", "GBR", "ITA"];

    // A modern calendar of nineteen rounds, eleven of them away from Europe.
    private static readonly string[] Modern =
    [
        "AUS", "MYS", "CHN", "BHR", "ESP", "MCO", "CAN", "AUT", "GBR", "HUN", "BEL", "ITA", "SGP", "JPN", "RUS", "USA", "MEX", "BRA", "ARE",
    ];

    private static readonly long Typical1955 = Money.FromDollars(60_000).Cents;

    private static readonly long TypicalModern = Money.FromDollars(120_000_000).Cents;

    private static long SeasonTotal(string home, string[] season, long typical) =>
        season.Sum(circuit => LogisticsMath.CostCents(home, circuit, typical, season));

    [Theory]
    [InlineData("ITA")]
    [InlineData("GBR")]
    [InlineData("DEU")]
    [InlineData("FRA")]
    public void TheSeasonTotalIsTheSameShareOfTheYearForAShortAndALongCalendar(string home)
    {
        var short1955 = SeasonTotal(home, Season1955, Typical1955) / (double)Typical1955;
        var modern = SeasonTotal(home, Modern, TypicalModern) / (double)TypicalModern;

        // Both are the season share, to within the rounding of a cent per round.
        Assert.InRange(short1955, InfrastructureEstimates.LogisticsSeasonShare - 0.0005, InfrastructureEstimates.LogisticsSeasonShare + 0.0005);
        Assert.InRange(modern, InfrastructureEstimates.LogisticsSeasonShare - 0.0005, InfrastructureEstimates.LogisticsSeasonShare + 0.0005);

        // And the share is in the band the owner called right for 1955: six to eight percent of the year.
        Assert.InRange(short1955, 0.06, 0.08);
        Assert.InRange(modern, 0.06, 0.08);
    }

    [Fact]
    public void ALongerCalendarMakesEachRoundCheaperAndNotTheYearDearer()
    {
        var doubled = Season1955.Concat(Season1955).ToArray();

        var one = LogisticsMath.CostCents("ITA", "BEL", Typical1955, Season1955);
        var two = LogisticsMath.CostCents("ITA", "BEL", Typical1955, doubled);

        Assert.True(two < one);
        Assert.InRange(SeasonTotal("ITA", doubled, Typical1955), SeasonTotal("ITA", Season1955, Typical1955) - 20, SeasonTotal("ITA", Season1955, Typical1955) + 20);
    }

    [Fact]
    public void OnTheSameSeasonAShipCostsMoreThanALorryAndAHomeRoundCostsTheLeast()
    {
        foreach (var (season, typical) in new[] { (Season1955, Typical1955), (Modern, TypicalModern) })
        {
            var home = LogisticsMath.CostCents("ITA", "ITA", typical, season.Append("ITA").ToArray());
            var lorry = LogisticsMath.CostCents("ITA", "BEL", typical, season.Append("BEL").ToArray());
            var ship = LogisticsMath.CostCents("ITA", "ARG", typical, season.Append("ARG").ToArray());

            Assert.True(ship > lorry, "a ship to another continent costs more than a lorry trip");
            Assert.True(lorry > home, "a lorry trip costs more than a round in the home country");
        }

        var quote = LogisticsMath.Quote("ITA", "ARG", Typical1955, Season1955);
        var europe = LogisticsMath.Quote("ITA", "BEL", Typical1955, Season1955);
        Assert.Equal(LogisticsMode.Ship, quote.Mode);
        Assert.True(quote.Days > europe.Days);
    }

    [Fact]
    public void TheFirstRoundOf1955IsStillFarAboveTheTwoHundredDollarsTheOwnerFoundTooCheap()
    {
        var ship = LogisticsMath.Quote("ITA", "ARG", Typical1955, Season1955);

        Assert.True(new Money(ship.CostCents).WholeDollars >= 1_000);
    }

    [Fact]
    public void TheCostComesFromTheEraBudgetAndHasNoCashInIt()
    {
        var one = LogisticsMath.CostCents("GBR", "ARG", Typical1955, Season1955);
        var two = LogisticsMath.CostCents("GBR", "ARG", Typical1955, Season1955);

        Assert.Equal(one, two);
        Assert.True(LogisticsMath.CostCents("GBR", "ARG", 0, Season1955) == 0L, "an era without a typical budget has no transport price");
        Assert.Equal(10L, LogisticsMath.CostCents("GBR", "ARG", Typical1955 * 10, Season1955) / one);
    }

    [Fact]
    public void WithoutACalendarARoundIsPricedAsOneOfAUsualSeasonAndStillKeepsTheOrder()
    {
        var home = LogisticsMath.CostCents("ITA", "ITA", Typical1955);
        var lorry = LogisticsMath.CostCents("ITA", "BEL", Typical1955);
        var ship = LogisticsMath.CostCents("ITA", "ARG", Typical1955);

        Assert.True(ship > lorry && lorry > home && home > 0);
        Assert.Equal(LogisticsMath.CostCents("ITA", "BEL", Typical1955), LogisticsMath.CostCents("ITA", "BEL", Typical1955, []));
    }
}
