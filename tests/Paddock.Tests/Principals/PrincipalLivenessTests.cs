using Paddock.Domain.Finance;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Xunit.Abstractions;

namespace Paddock.Tests.Principals;

/// <summary>
/// Liveness of the AI principals over ten AI-only seasons on five seeds (issue #109). Every window and share here is an ESTIMATE of what
/// "the market works" means in the SYNTHETIC world of <see cref="PrincipalWorld"/>; none is calibrated.
/// </summary>
public class PrincipalLivenessTests(ITestOutputHelper output)
{
    /// <summary>ESTIMATE: a team may run with fewer than two drivers for at most this many days in a row (a talk has up to 30 days, a counter a few more, and rivals can win a person).</summary>
    private const int MaxDaysBelowTwoDrivers = 120;

    private const int Seasons = 10;

    public static IEnumerable<object[]> Seeds() => new[] { 7UL, 11UL, 23UL, 101UL, 2026UL }.Select(seed => new object[] { seed });

    [Theory]
    [MemberData(nameof(Seeds))]
    public void TenSeasonsKeepSeatsFilledContractsClearAndCashInsideTheOverdraft(ulong seed)
    {
        var kit = new PrincipalKit(new PrincipalKitOptions { Seed = seed });
        var teams = kit.Session.World.Organizations.Where(item => item.Kind == OrganizationKind.Team).Select(item => item.Id).ToArray();
        var below = teams.ToDictionary(team => team.Value, _ => 0, StringComparer.Ordinal);
        var worst = 0;
        var allowanceCents = 0L;
        var lowestCash = long.MaxValue;
        var end = GameDate.SeasonStart(1955 + Seasons);

        kit.RunUntil(end, day =>
        {
            var world = kit.Session.World;
            var today = kit.Session.Date;
            foreach (var team in teams)
            {
                var drivers = world.Contracts
                    .Where(contract => contract.OrganizationId == team && contract.Role.IsDriver && contract.IsActiveOn(today))
                    .Select(contract => contract.PersonId)
                    .Distinct()
                    .Count();
                below[team.Value] = drivers >= 2 ? 0 : below[team.Value] + 1;
                worst = Math.Max(worst, below[team.Value]);
            }

            if (day.Day == 1)
            {
                var finance = world.Section<FinanceSection>(FinanceSection.SectionName)!;
                allowanceCents = (long)(finance.TypicalCents * Paddock.Simulation.Ai.AiEstimates.OverdraftAllowanceShare);
                foreach (var team in teams)
                {
                    lowestCash = Math.Min(lowestCash, finance.BalanceOf(team));
                }

                AssertNoOverlap(world);
            }
        });

        AssertNoOverlap(kit.Session.World);
        output.WriteLine($"seed {seed}: worst run below two drivers {worst} days, lowest cash {lowestCash} cents, allowance {allowanceCents}, accepted {kit.Accepted.Values.Sum()}, rejected {kit.Rejected.Values.Sum()}, director time {kit.DirectorTime.TotalMilliseconds:F0} ms");
        Assert.True(worst <= MaxDaysBelowTwoDrivers, $"A team stood below two drivers for {worst} days.");
        Assert.True(lowestCash >= -allowanceCents, $"A team's cash fell to {lowestCash} cents, below the allowed overdraft of {allowanceCents}.");
        Assert.True(kit.Accepted.GetValueOrDefault("OpenNegotiationCommand") > 0);
        Assert.True(kit.Accepted.GetValueOrDefault("RenewContractCommand") > 0);
    }

    [Theory]
    [InlineData(0.9)]
    [InlineData(0.6)]
    public void WhenMoneyIsTightTheAiStillStaysInsideCashPlusTheOverdraft(double revenueShare)
    {
        var kit = new PrincipalKit(new PrincipalKitOptions { Seed = 31UL, RevenueShare = revenueShare });
        var teams = kit.Session.World.Organizations.Where(item => item.Kind == OrganizationKind.Team).Select(item => item.Id).ToArray();
        var lowest = long.MaxValue;
        var allowance = 0L;
        kit.RunUntil(GameDate.SeasonStart(1961), _ =>
        {
            var finance = kit.Session.World.Section<FinanceSection>(FinanceSection.SectionName)!;
            allowance = (long)(finance.TypicalCents * Paddock.Simulation.Ai.AiEstimates.OverdraftAllowanceShare);
            foreach (var team in teams)
            {
                lowest = Math.Min(lowest, finance.BalanceOf(team));
            }
        });

        output.WriteLine($"revenue {revenueShare}: lowest cash {lowest} cents, allowance {allowance}");
        Assert.True(lowest >= -allowance, $"Cash fell to {lowest} cents, below the allowed overdraft of {allowance}.");
    }

    [Fact]
    public void NoPersonSignsTwiceForTheSameDays()
    {
        var kit = new PrincipalKit();
        kit.RunUntil(GameDate.SeasonStart(1960));
        AssertNoOverlap(kit.Session.World);

        // One negotiation ends in at most one contract, and one person is never signed by two teams for the same day.
        var contracts = kit.Session.World.Contracts.Where(contract => contract.Exclusive).ToArray();
        foreach (var person in contracts.GroupBy(contract => contract.PersonId))
        {
            var ordered = person.OrderBy(contract => contract.Start).ToArray();
            for (var i = 1; i < ordered.Length; i++)
            {
                Assert.True(ordered[i].Start > ordered[i - 1].End, $"{person.Key.Value} holds overlapping contracts.");
            }
        }
    }

    private static void AssertNoOverlap(WorldState world)
    {
        foreach (var person in world.Contracts.Where(contract => contract.Exclusive).GroupBy(contract => contract.PersonId))
        {
            var ordered = person.OrderBy(contract => contract.Start).ThenBy(contract => contract.End).ToArray();
            for (var i = 1; i < ordered.Length; i++)
            {
                Assert.True(
                    ordered[i].Start > ordered[i - 1].End,
                    $"{person.Key.Value}: {ordered[i - 1].Id.Value} ({ordered[i - 1].Start}..{ordered[i - 1].End}) overlaps {ordered[i].Id.Value} ({ordered[i].Start}..{ordered[i].End}).");
            }
        }
    }
}
