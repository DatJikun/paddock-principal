using Paddock.Application.Career;
using Paddock.Domain.Career;
using Paddock.Domain.Finance;
using Paddock.Domain.Infrastructure;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Career;
using Paddock.Tests.Career;

namespace Paddock.Tests.Infrastructure;

/// <summary>#231 on a real 1955 opening world: a factory for every team, no dyno, Argentina is a ship.</summary>
public sealed class InfrastructureCareerTests
{
    private const ulong Seed = 7;

    [Fact]
    public void ANewCareerGivesEveryTeamAFactoryAndLeavesSupplyAlone()
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);
        CareerHost.RunUntil(session, session.Date, null, CareerKit.Options);

        var section = session.World.Section<InfrastructureSection>(InfrastructureSection.SectionName);
        Assert.NotNull(section);
        foreach (var team in CareerTeams.Active(session.World, session.Date))
        {
            var factory = section.Find(team.Id, FacilityKind.Factory);
            Assert.NotNull(factory);
            Assert.True(factory.QualityMilli > 0);
            Assert.DoesNotContain(section.Of(team.Id), facility => facility.Kind.ToString().Contains("Dyno", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void TheFirst1955RacePostsShipLogisticsOnTopOfRaceRunning()
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);
        CareerHost.RunUntil(session, new GameDate(1955, 3, 4), null, CareerKit.Options);
        var finance = session.World.Section<FinanceSection>(FinanceSection.SectionName)!;
        var posted = 0;
        var days = 0;
        foreach (var team in CareerTeams.Active(session.World, session.Date))
        {
            foreach (var entry in finance.EntriesOf(team.Id))
            {
                if (entry.Category == LedgerCategories.Logistics)
                {
                    posted++;
                    Assert.True(entry.AmountCents < 0);
                    var home = CareerKit.Options.Inputs.TeamCountries is { } countries
                        && countries.TryGetValue(team.Id.Value, out var country)
                        ? country
                        : "ITA";
                    days = LogisticsMath.Quote(home, LogisticsMath.Argentina, finance.TypicalCents).Days;
                }
            }
        }

        Assert.True(posted > 0, "the opening round posts logistics");
        Assert.Equal(InfrastructureEstimates.LogisticsShipDays, days);
        Assert.Contains(
            CareerTeams.Active(session.World, session.Date)
                .SelectMany(team => finance.EntriesOf(team.Id)),
            entry => entry.Category == LedgerCategories.RaceRunning);
    }

    /// <summary>
    /// The owner worried that a team with no cash would travel for free. The price is a share of the era's typical budget, never of the team's
    /// money, and it is posted whether or not the team can pay: the poorest team of the grid pays exactly what its own quote says, and two
    /// teams from the same country pay the same however different their cash is.
    /// </summary>
    [Fact]
    public void TransportCostsTheSameForAPoorTeamAndARichTeamOnTheSameRoute()
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);
        var countries = CareerKit.Options.Inputs.TeamCountries!;
        CareerHost.RunUntil(session, new GameDate(1955, 1, 8), null, CareerKit.Options);
        var before = session.World.Section<FinanceSection>(FinanceSection.SectionName)!;
        var teams = CareerTeams.Active(session.World, session.Date).Where(team => before.HasBook(team.Id)).ToArray();
        Assert.DoesNotContain(teams.SelectMany(team => before.EntriesOf(team.Id)), entry => entry.Category == LedgerCategories.Logistics);
        var cash = teams.ToDictionary(team => team.Id.Value, team => before.BalanceOf(team.Id), StringComparer.Ordinal);
        Assert.True(cash.Values.Max() > cash.Values.Min() * 2, "the grid has teams with very different cash");

        var later = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);
        CareerHost.RunUntil(later, new GameDate(1955, 3, 4), null, CareerKit.Options);
        var after = later.World.Section<FinanceSection>(FinanceSection.SectionName)!;
        var paid = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var team in teams)
        {
            var line = after.EntriesOf(team.Id).Where(entry => entry.Category == LedgerCategories.Logistics).ToArray();
            if (line.Length == 0)
            {
                continue;
            }

            Assert.Single(line);
            paid[team.Id.Value] = -line[0].AmountCents;
            var home = countries.TryGetValue(team.Id.Value, out var country) ? country : null;
            Assert.Equal(LogisticsMath.CostCents(home, LogisticsMath.Argentina, after.TypicalCents), -line[0].AmountCents);
        }

        var poorest = cash.OrderBy(pair => pair.Value).First().Key;
        Assert.True(paid.ContainsKey(poorest), "the poorest team pays its transport too");
        foreach (var group in paid.GroupBy(pair => countries.TryGetValue(pair.Key, out var c) ? c : "", StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            Assert.Single(group.Select(pair => pair.Value).Distinct());
        }
    }

    [Fact]
    public void TheQuoteHasNoCashInIt()
    {
        var typical = Money.FromDollars(60_000).Cents;
        var one = LogisticsMath.Quote("GBR", LogisticsMath.Argentina, typical);
        var two = LogisticsMath.Quote("GBR", LogisticsMath.Argentina, typical);
        Assert.Equal(one, two);
        Assert.True(LogisticsMath.CostCents("GBR", LogisticsMath.Argentina, 0) == 0L, "an era without a typical budget has no transport price");
        Assert.True(LogisticsMath.CostCents("GBR", LogisticsMath.Argentina, typical * 10) > 9 * one.CostCents, "it scales with the era's budget");
    }

    [Fact]
    public void TwoRunsOfTheSameSeedMatchAfterTheFactoryIsInstalled()
    {
        string Hash()
        {
            var session = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);
            CareerHost.RunUntil(session, session.Date, null, CareerKit.Options);
            return session.World.StateHash();
        }

        Assert.Equal(Hash(), Hash());
    }
}
