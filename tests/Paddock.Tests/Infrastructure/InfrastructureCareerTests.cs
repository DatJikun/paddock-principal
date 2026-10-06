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
