using Paddock.Application.Regulation;
using Paddock.Domain.Career;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Simulation.Regulation;

namespace Paddock.Tests.Regulation;

/// <summary>
/// Rules that can never be voted on, per championship (#275, owner decision of round 2): a banned rule is never offered by the FIA or
/// the AI, a human who tries is refused with a reason, a series without a list of its own uses the default, and another series is
/// not affected.
/// </summary>
public class BannedRulesTests
{
    private const string Series = PoliticsHarness.Series;

    private static readonly string[] Core = [DormantRules.CashBonusLastPlace, DormantRules.CashBonusPromotedTeam, DormantRules.ErsChargeByPosition];

    private static BannedRules Banning(params string[] rules) => new([.. Core, .. rules]);

    private static HarnessOptions Options(BannedRules? banned, ulong seed = 7) => new() { Seed = seed, BannedRules = banned, WithCalendar = true };

    [Fact]
    public void TheDomainTypeUsesTheSeriesOwnListOrTheDefaultAndNeverMixesThem()
    {
        var banned = new BannedRules(
            ["a", "b"],
            new Dictionary<string, IReadOnlyList<string>> { ["f1"] = ["c"], ["f2"] = [] });

        Assert.Equal(["c"], banned.For("f1"));
        Assert.Empty(banned.For("f2"));
        Assert.Equal(["a", "b"], banned.For("f3"));
        Assert.True(banned.IsBanned("f3", "a"));
        Assert.False(banned.IsBanned("f1", "a"));
        Assert.True(banned.IsBanned("f1", "c"));
        Assert.False(banned.IsBanned("f3", "c"));
        Assert.True(banned.HasOwnList("f2"));
        Assert.False(banned.HasOwnList("f3"));
        Assert.Empty(BannedRules.None.For("f1"));
    }

    [Fact]
    public void ABannedLiveRuleIsNeverAmongTheChoicesWhichAreWhatTheFiaAndTheAiOfferFrom()
    {
        var banned = Banning("points_scale", "safety_car");
        var harness = PoliticsHarness.Create(Options(banned));
        var free = PoliticsHarness.Create(Options(null));

        var choices = harness.Politics.ChoicesFor(harness.Of(), 1955, new HashSet<string>(StringComparer.Ordinal));
        var unbanned = free.Politics.ChoicesFor(free.Of(), 1955, new HashSet<string>(StringComparer.Ordinal));

        Assert.DoesNotContain(choices, choice => choice.DimensionId is "points_scale" or "safety_car");
        Assert.Contains(unbanned, choice => choice.DimensionId == "points_scale");
        Assert.Contains(unbanned, choice => choice.DimensionId == "safety_car");
        Assert.Equal(unbanned.Count(choice => choice.DimensionId is not ("points_scale" or "safety_car")), choices.Count);
    }

    [Fact]
    public void ABannedCalendarEntryCannotBeVotedEither()
    {
        var free = PoliticsHarness.Create(Options(null));
        var circuit = CalendarPolicy.CircuitOf(free.Politics.ChoicesFor(free.Of(), 1955, new HashSet<string>(StringComparer.Ordinal))
            .First(choice => CalendarPolicy.IsCalendarDimension(choice.DimensionId)).DimensionId);
        var dimension = CalendarPolicy.DimensionOf(circuit);
        var harness = PoliticsHarness.Create(Options(Banning(dimension)));

        Assert.DoesNotContain(harness.Politics.ChoicesFor(harness.Of(), 1955, new HashSet<string>(StringComparer.Ordinal)), choice => choice.DimensionId == dimension);
        Assert.Contains(free.Politics.ChoicesFor(free.Of(), 1955, new HashSet<string>(StringComparer.Ordinal)), choice => choice.DimensionId == dimension);
        harness.LiveFrom(new GameDate(1955, 1, 2), new GameDate(1955, 2, 10));
        Assert.Equal(RegulationKeys.Banned, harness.Politics.CheckPropose(Series, "t01", dimension, CalendarPolicy.Dropped, harness.Today)!.Key);
    }

    [Fact]
    public void TheFiaNeverBringsABannedRuleAndTheAiNeverProposesOneOverManySeasons()
    {
        var banned = Banning("points_scale", "safety_car", "qualifying_format", "refuelling");
        foreach (var seed in new ulong[] { 1, 2, 3, 4, 5 })
        {
            var harness = PoliticsHarness.Create(Options(banned, seed) with { Capital = _ => 400_000, StoreCalendar = true });
            var fia = 0;
            for (var year = 1955; year <= 1962; year++)
            {
                harness.LiveTo(new GameDate(year, 12, 31));
                foreach (var item in harness.Items().Where(item => item.Season == year))
                {
                    Assert.False(banned.IsBanned(Series, item.DimensionId), item.DimensionId + " was on the ballot (seed " + seed + ")");
                    fia += item.Origin == BallotOrigin.Fia ? 1 : 0;
                }
            }

            Assert.True(fia >= 8 * RegulationEstimates.FiaVotesMin - 8, "the FIA still brings its votes from the rules that remain");
            Assert.All(harness.Of().Values.Where(pair => pair.Key is "points_scale" or "safety_car" or "qualifying_format" or "refuelling"), pair =>
                Assert.Equal(PoliticsHarness.Data.RuleSetFor(1955).Value(pair.Key), pair.Value));
        }
    }

    [Fact]
    public void TheBanChangesWhatTheFiaAndTheAiDoSoTheTestAboveIsNotVacuous()
    {
        var names = new[] { "points_scale", "safety_car", "qualifying_format", "refuelling" };
        var offered = 0;
        foreach (var seed in new ulong[] { 1, 2, 3, 4, 5 })
        {
            var harness = PoliticsHarness.Create(Options(null, seed) with { Capital = _ => 400_000, StoreCalendar = true });
            for (var year = 1955; year <= 1962; year++)
            {
                harness.LiveTo(new GameDate(year, 12, 31));
                offered += harness.Items().Count(item => item.Season == year && names.Contains(item.DimensionId));
            }
        }

        Assert.True(offered > 0, "without the ban those rules do get voted on");
    }

    [Fact]
    public void AHumanWhoProposesABannedRuleIsRefusedWithTheReasonAndPaysNothing()
    {
        var harness = PoliticsHarness.Create(Options(Banning("safety_car")) with { AllHuman = true });
        harness.LiveFrom(new GameDate(1955, 1, 2), new GameDate(1955, 2, 10));
        var before = harness.Finance.BalanceOf(PoliticsHarness.Org("t01"));

        var refusal = harness.Politics.CheckPropose(Series, "t01", "safety_car", "physical", harness.Today);

        Assert.NotNull(refusal);
        Assert.Equal(RegulationKeys.Banned, refusal.Key);
        Assert.Equal("safety_car", refusal.Parameters["rule"]);
        Assert.Throws<InvalidOperationException>(() => harness.Politics.Propose(Series, "t01", "safety_car", "physical", harness.Today));
        Assert.Equal(before, harness.Finance.BalanceOf(PoliticsHarness.Org("t01")));
        Assert.Empty(harness.Of().Pending);
        Assert.Equal(1955, harness.Of().TeamOf("t01")!.ProposeFromSeason);

        // The refusal is about the rule, so another rule in the same window is still fine.
        Assert.Null(harness.Politics.CheckPropose(Series, "t01", "refuelling", "banned", harness.Today));
    }

    [Fact]
    public void TheCatchUpMechanicAndTheCashBonusesAreRefusedAsBannedNotAsMissingAndWithoutTheBanTheyAreSimplyNotLive()
    {
        var banned = PoliticsHarness.Create(Options(Banning()) with { AllHuman = true });
        var free = PoliticsHarness.Create(Options(null) with { AllHuman = true });
        foreach (var harness in new[] { banned, free })
        {
            harness.LiveFrom(new GameDate(1955, 1, 2), new GameDate(1955, 2, 10));
        }

        foreach (var mechanic in DormantRules.All)
        {
            Assert.False(LiveRules.IsLive(mechanic), mechanic + " is not live");
            Assert.Equal(RegulationKeys.Banned, banned.Politics.CheckPropose(Series, "t01", mechanic, "on", banned.Today)!.Key);
            Assert.Equal(RegulationKeys.NotLive, free.Politics.CheckPropose(Series, "t01", mechanic, "on", free.Today)!.Key);
        }
    }

    [Fact]
    public void ASeriesWithoutAListOfItsOwnUsesTheDefaultAndAnotherSeriesWithADifferentListIsNotAffected()
    {
        // f1 has its own list (the core and points_scale); f2 has none and uses the default (the core only).
        var banned = new BannedRules(
            Core,
            new Dictionary<string, IReadOnlyList<string>> { [TwoSeries.First] = [.. Core, "points_scale"] });
        var harness = PoliticsHarness.Create(Options(banned) with { Directory = new TwoSeries(), AllHuman = true });
        harness.LiveFrom(new GameDate(1955, 1, 2), new GameDate(1955, 2, 10));

        var first = harness.Politics.ChoicesFor(harness.Of(TwoSeries.First), 1955, new HashSet<string>(StringComparer.Ordinal));
        var second = harness.Politics.ChoicesFor(harness.Of(TwoSeries.Second), 1955, new HashSet<string>(StringComparer.Ordinal));

        Assert.DoesNotContain(first, choice => choice.DimensionId == "points_scale");
        Assert.Contains(second, choice => choice.DimensionId == "points_scale");
        Assert.Equal(RegulationKeys.Banned, harness.Politics.CheckPropose(TwoSeries.First, "t01", "points_scale", "top10_25_18_15_12_10_8_6_4_2_1", harness.Today)!.Key);
        Assert.Null(harness.Politics.CheckPropose(TwoSeries.Second, "t06", "points_scale", "top10_25_18_15_12_10_8_6_4_2_1", harness.Today));

        // The default list still bans the core in the series without a list of its own.
        foreach (var mechanic in Core)
        {
            Assert.Equal(RegulationKeys.Banned, harness.Politics.CheckPropose(TwoSeries.Second, "t06", mechanic, "on", harness.Today)!.Key);
        }
    }

    [Fact]
    public void ABallotItemThatIsBannedWhenItIsCountedIsNotAppliedAndSaysWhy()
    {
        // An item already on the ballot (made while nothing was banned), then the data bans its rule: the count refuses it with a reason.
        var open = PoliticsHarness.Create(Options(null) with { AllHuman = true });
        open.LiveTo(new GameDate(1955, 1, 2));
        open.LiveToFia();
        var item = open.Items().First(i => i.Origin == BallotOrigin.Fia);
        foreach (var team in open.Teams)
        {
            open.Politics.Cast(Series, team, item.Id, "v1", 0, open.Today);
        }

        var environment = new RegulationEnvironment(
            RulesSource.VotedEachSeason,
            VoteMode.OneVoteEach,
            open.Environment.MasterSeed,
            [.. open.Environment.Specs.Values],
            open.Environment.DimensionIds,
            open.Environment.Periods,
            open.Environment.Control,
            open.Environment.Managers,
            open.Environment.Series,
            open.Environment.Layouts,
            open.Environment.Assignments,
            open.Environment.TeamCountries,
            open.Environment.HumanTeams,
            null,
            Banning(item.DimensionId));
        var politics = new RegulationPolitics(new RegulationBook(() => open.World, open.SetWorld), environment);
        for (var day = open.Today; day < item.Deadline;)
        {
            day = day.AddDays(1);
            politics.OnDay(day);
        }

        var result = open.Of().ItemOf(item.Id)!.Result!;
        Assert.Equal(BallotOutcome.Rejected, result.Outcome);
        Assert.Equal(RegulationKeys.ResultBanned, result.ReasonKey);
        Assert.Null(open.Of().NextValues);
    }

    [Fact]
    public void EveryDormantMechanicHasANameAndAnEffectInBothLanguages()
    {
        var catalog = Paddock.Application.Localization.TranslationLoader.LoadDirectory(Path.Combine(RepoPaths.Root(), "strings"));
        foreach (var mechanic in DormantRules.All)
        {
            foreach (var key in new[] { RegulationKeys.DimensionKey(mechanic), RegulationKeys.EffectKey(mechanic) })
            {
                Assert.True(catalog.Entries(Paddock.Application.Localization.Language.Pl).ContainsKey(key), key + " is missing in pl.json");
                Assert.True(catalog.Entries(Paddock.Application.Localization.Language.En).ContainsKey(key), key + " is missing in en.json");
            }
        }
    }
}
