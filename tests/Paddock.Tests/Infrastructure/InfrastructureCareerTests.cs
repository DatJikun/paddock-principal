using Paddock.Application.Career;
using Paddock.Domain.Career;
using Paddock.Domain.Finance;
using Paddock.Domain.Infrastructure;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Career;
using Paddock.Simulation.Time;
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
            Assert.Equal(LogisticsMath.CostCents(home, LogisticsMath.Argentina, after.TypicalCents, SeasonCountries(later, 1955)), -line[0].AmountCents);
        }

        var poorest = cash.OrderBy(pair => pair.Value).First().Key;
        Assert.True(paid.ContainsKey(poorest), "the poorest team pays its transport too");
        foreach (var group in paid.GroupBy(pair => countries.TryGetValue(pair.Key, out var c) ? c : "", StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            Assert.Single(group.Select(pair => pair.Value).Distinct());
        }
    }

    /// <summary>The countries of the races of a season, from the career's own calendar and track catalog: what the transport of a round is split over.</summary>
    private static IReadOnlyList<string?> SeasonCountries(CareerSession session, int season)
    {
        var layouts = CareerKit.Options.Inputs.Layouts!;
        return session.World.Section<RaceCalendarSection>(RaceCalendarSection.SectionName)!
            .SeasonSessions(season)
            .Where(item => item.TypeId == ScheduledEventType.Race)
            .Select(item => (string?)layouts.First(layout => layout.Id == item.LayoutId).Country)
            .ToArray();
    }

    /// <summary>
    /// A whole season of the real game: a team that started every round has paid the season share of the era's typical budget for its transport,
    /// in one ledger line a round, and no team has paid more, whatever its cash.
    /// </summary>
    [Fact]
    public void ASeasonOfTransportAddsUpToTheSeasonShareOfTheEraBudget()
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);
        CareerHost.RunUntil(session, new GameDate(1956, 1, 3), null, CareerKit.Options);
        var finance = session.World.Section<FinanceSection>(FinanceSection.SectionName)!;
        var rounds = SeasonCountries(session, 1955).Count;
        Assert.True(rounds >= 5, "the 1955 calendar has races");
        var share = InfrastructureEstimates.LogisticsSeasonShare * finance.TypicalCents;

        var totals = new List<long>();
        foreach (var team in CareerTeams.Active(session.World, session.Date).Where(team => finance.HasBook(team.Id)))
        {
            var lines = finance.EntriesOf(team.Id).Where(entry => entry.Category == LedgerCategories.Logistics && entry.Date.Year == 1955).ToArray();
            Assert.All(lines, line => Assert.True(line.AmountCents < 0));
            Assert.True(lines.Length <= rounds);
            totals.Add(-lines.Sum(line => line.AmountCents));
        }

        Assert.All(totals, total => Assert.True(total <= share + rounds, "no team pays more than the season share"));
        Assert.Contains(totals, total => total >= share - rounds && total <= share + rounds);
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
