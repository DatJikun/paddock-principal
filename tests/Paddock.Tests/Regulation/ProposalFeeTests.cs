using Paddock.Application.Regulation;
using Paddock.Domain.Finance;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Simulation.Regulation;

namespace Paddock.Tests.Regulation;

/// <summary>The fee of a proposal (#275, owner decisions 7 and 8): a share of last season's revenue, never of cash.</summary>
public class ProposalFeeTests
{
    private static LedgerEntry Line(long sequence, GameDate date, string category, long cents) =>
        new(sequence, date, category, null, cents, "test.reason");

    [Fact]
    public void TheBasisIsTheRevenueOfTheLastCompletedSeasonAndNothingElse()
    {
        var entries = new[]
        {
            Line(1, new GameDate(1954, 1, 1), LedgerCategories.OwnerFunds, 9_000_000),
            Line(2, new GameDate(1954, 5, 1), LedgerCategories.StartMoney, 100_000),
            Line(3, new GameDate(1954, 6, 1), LedgerCategories.PrizeMoney, 300_000),
            Line(4, new GameDate(1954, 7, 1), LedgerCategories.Sponsor, 600_000),
            Line(5, new GameDate(1954, 8, 1), LedgerCategories.Salary, -5_000_000),
            Line(6, new GameDate(1953, 8, 1), LedgerCategories.PrizeMoney, 7_000_000),
            Line(7, new GameDate(1955, 3, 1), LedgerCategories.PrizeMoney, 8_000_000),
        };

        Assert.Equal(1_000_000, ProposalFee.RevenueBasis(entries, 1955));
    }

    [Fact]
    public void WithNoCompletedSeasonTheOpeningLedgerIsTheBasis()
    {
        var entries = new[]
        {
            Line(1, new GameDate(1955, 1, 1), LedgerCategories.OwnerFunds, 6_000_000),
            Line(2, new GameDate(1955, 3, 1), LedgerCategories.PrizeMoney, 900_000),
        };

        Assert.Equal(6_000_000, ProposalFee.RevenueBasis(entries, 1955));
        Assert.Equal(0, ProposalFee.RevenueBasis([], 1955));
    }

    [Theory]
    [InlineData(1_000_000_000L, 60_000_000L)]
    [InlineData(20_000_000L, 1_200_000L)]
    public void TheFeeIsTheConfiguredShareOfTheBasis(long basis, long expected)
    {
        var typical = Money.FromDollars(60_000).Cents;

        Assert.Equal(expected, ProposalFee.Quote(basis, typical));
        Assert.Equal(expected, (long)Math.Round(basis * RegulationEstimates.FeeRevenueShare));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(-5_000L)]
    public void TheFeeIsNeverBelowTheFloorNeverZeroAndNeverNegative(long basis)
    {
        var typical = Money.FromDollars(60_000).Cents;

        var fee = ProposalFee.Quote(basis, typical);

        Assert.True(fee > 0);
        Assert.Equal(ProposalFee.Floor(typical), fee);
        Assert.True(ProposalFee.Floor(typical) >= RegulationEstimates.MinimumFeeCents);
        Assert.True(ProposalFee.Quote(0, 0) >= RegulationEstimates.MinimumFeeCents);
    }

    [Fact]
    public void ARichTeamAndAPoorTeamPayTheSameShareOfWhatTheyEarn()
    {
        var typical = Money.FromDollars(60_000).Cents;
        var rich = ProposalFee.Quote(Money.FromDollars(250_000).Cents, typical);
        var poor = ProposalFee.Quote(Money.FromDollars(20_000).Cents, typical);

        Assert.InRange(rich / (double)Money.FromDollars(250_000).Cents, 0.059, 0.061);
        Assert.InRange(poor / (double)Money.FromDollars(20_000).Cents, 0.059, 0.061);
    }

    [Fact]
    public void TheFeeDoesNotChangeWithTheCashOfTheTeamDuringTheSeason()
    {
        // Two teams with the same revenue in 1954: one rich, one deep in debt. Same fee, and it stays when the cash moves.
        var harness = PoliticsHarness.Create(new HarnessOptions { Capital = index => index == 0 ? 5_000_000 : 100 });
        var rich = PoliticsHarness.TeamId(0);
        var broke = PoliticsHarness.TeamId(1);
        harness.PostRevenue(rich, 1954, Money.FromDollars(80_000).Cents);
        harness.PostRevenue(broke, 1954, Money.FromDollars(80_000).Cents);
        harness.SetFinance(harness.Finance.Post(
            PoliticsHarness.Org(broke), new GameDate(1955, 1, 5), LedgerCategories.Salary, null, -Money.FromDollars(90_000).Cents, FinanceReason.Salary));
        Assert.True(harness.Finance.BalanceOf(PoliticsHarness.Org(broke)) < 0);

        var feeRich = harness.Politics.QuoteFor(PoliticsHarness.Series, rich, 1955)!.FeeCents;
        var feeBroke = harness.Politics.QuoteFor(PoliticsHarness.Series, broke, 1955)!.FeeCents;

        Assert.Equal(feeRich, feeBroke);
        Assert.Equal(Money.FromDollars(80_000).Cents * 6 / 100, feeRich);

        // The cash swings during the season; the fee does not.
        harness.SetFinance(harness.Finance
            .Post(PoliticsHarness.Org(rich), new GameDate(1955, 6, 1), LedgerCategories.Salary, null, -Money.FromDollars(4_999_000).Cents, FinanceReason.Salary)
            .Post(PoliticsHarness.Org(broke), new GameDate(1955, 6, 1), LedgerCategories.PrizeMoney, null, Money.FromDollars(500_000).Cents, FinanceReason.PrizeMoney));
        Assert.Equal(feeRich, harness.Politics.QuoteFor(PoliticsHarness.Series, rich, 1955)!.FeeCents);
        Assert.Equal(feeRich, harness.Politics.QuoteFor(PoliticsHarness.Series, broke, 1955)!.FeeCents);
    }
}
