using Paddock.Application.Board;
using Paddock.Application.Career;
using Paddock.Domain.Board;
using Paddock.Domain.Cars;
using Paddock.Domain.Career;
using Paddock.Domain.Finance;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Career;
using Paddock.Tests.Career;

namespace Paddock.Tests.Board;

/// <summary>
/// #234: the board's expectation follows the budget level of a team (the tier of the authored 1954 order), not the order of the
/// team ids. A real 1955 opening world with generated people (the Jolpica cache is not in the repository, PP-041). The tiers and
/// the order are authored ESTIMATES.
/// </summary>
public sealed class BoardBudgetRankTests
{
    private const ulong Seed = 7;

    private static readonly string[] Order1955 =
    [
        "mercedes", "ferrari", "maserati", "gordini", "cooper", "connaught", "vanwall", "hwm", "lancia", "arzani-volpini",
    ];

    private static (BoardEngine Engine, CareerSession Session) Opened()
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);
        var result = CareerHost.RunUntil(session, session.Date, null, CareerKit.Options);
        return (result.Modules.Require<BoardEngine>(), session);
    }

    [Fact]
    public void TheTopBudgetTeamIsExpectedAheadOfEveryLowBudgetTeamAndNoTwoTeamsAreOrderedById()
    {
        var (engine, session) = Opened();
        var teams = engine.ActiveTeams(session.Date);
        var ranks = teams.ToDictionary(team => team.Id.Value, team => engine.BudgetRank(team.Id, session.Date), StringComparer.Ordinal);

        // Mercedes is the top level, so it is ahead of every other team, and the low-level teams are behind the typical ones.
        Assert.Equal(1, ranks["mercedes"]);
        foreach (var low in new[] { "gordini", "cooper", "connaught", "vanwall", "hwm", "lancia", "arzani-volpini" })
        {
            Assert.True(ranks["mercedes"] < ranks[low], low);
            Assert.True(ranks["ferrari"] < ranks[low], low);
            Assert.True(ranks["maserati"] < ranks[low], low);
        }

        // The ranks are a permutation: nobody shares a place.
        Assert.Equal(Enumerable.Range(1, teams.Count), ranks.Values.OrderBy(rank => rank));

        // The real teams follow the authored 1954 order, which is not the alphabetical one.
        var byRank = ranks.Where(pair => Order1955.Contains(pair.Key)).OrderBy(pair => pair.Value).Select(pair => pair.Key).ToArray();
        Assert.Equal(Order1955, byRank);
        Assert.NotEqual(Order1955.OrderBy(id => id, StringComparer.Ordinal), byRank);
    }

    private sealed class FakeTiers : ITeamTierSource
    {
        private readonly Dictionary<string, int> _places;

        public FakeTiers(Dictionary<string, int> places) => _places = places;

        public TeamTier TierOf(OrganizationId organization, int startSeason) => TeamTier.Typical;

        public int? PreviousPlaceOf(OrganizationId organization, int startSeason) =>
            _places.TryGetValue(organization.Value, out var place) ? place : null;
    }

    [Fact]
    public void TeamsOfOneBudgetAreOrderedByThePreviousPlaceThenTheCarStrengthAndOnlyThenById()
    {
        var today = new GameDate(1955, 1, 1);
        var world = WorldState.At(today);
        foreach (var id in new[] { "zeta", "alpha", "beta", "gamma", "delta", "aaa", "rich" })
        {
            (world, _) = world.AddOrganization(new OrganizationSpec(
                OrganizationKind.Team,
                true,
                id,
                new GameDate(1950, 1, 1),
                null,
                id == "rich" ? 2_000_000 : 1_000_000,
                [new OrganizationNameSpan(id, new GameDate(1950, 1, 1), null)]));
        }

        var keys = new PublicRankKeys(
            new FakeTiers(new Dictionary<string, int>(StringComparer.Ordinal) { ["zeta"] = 1, ["alpha"] = 2 }),
            new MapCarStrengthSource(("beta", 1955, 70d), ("gamma", 1955, 60d), ("zeta", 1955, 10d)));

        var order = new[] { "zeta", "alpha", "beta", "gamma", "delta", "aaa", "rich" }
            .OrderBy(id => PublicStrength.BudgetRank(world, OrganizationId.Real(id), today, keys))
            .ToArray();

        // The richer team first; then the places of last season (zeta before alpha, though its car is weaker and its id later);
        // then the stronger car (beta before gamma); then the ids of the teams nothing tells apart (aaa before delta).
        Assert.Equal(["rich", "zeta", "alpha", "beta", "gamma", "aaa", "delta"], order);
    }

    [Fact]
    public void TheWorldOpensEveryTeamWithTheBudgetFinanceOpensItsBooksWith()
    {
        var session = CareerKit.Open(CareerPreset.Chaos, 1955, Seed);
        var facts = new EraPeriodFinance(CareerKit.Data.EraPeriods).Facts(1955);

        long Budget(string team) => session.World.GetOrganization(OrganizationId.Real(team)).Budget;

        Assert.Equal(facts.TopDollars, Budget("mercedes"));
        Assert.Equal(facts.TypicalDollars, Budget("ferrari"));
        Assert.Equal(facts.LowDollars, Budget("gordini"));
        Assert.Equal(facts.LowDollars, Budget("arzani-volpini"));
        Assert.Equal(facts.LowDollars, Budget("lancia"));
        Assert.Equal(facts.LowDollars, Budget("vanwall"));
    }
}
