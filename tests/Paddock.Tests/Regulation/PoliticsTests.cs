using Paddock.Application.Regulation;
using Paddock.Domain.Career;
using Paddock.Domain.Finance;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Simulation.Regulation;

namespace Paddock.Tests.Regulation;

/// <summary>
/// The political life of a series (#275): proposals and their cooldown, ballots with variants, the FIA's own votes, the count, and
/// the rule that a change voted in season N is never seen in N.
/// </summary>
public class PoliticsTests
{
    private const string Series = PoliticsHarness.Series;

    private static readonly GameDate Feb10 = new(1955, 2, 10);

    private static RuleChoice FirstChoice(PoliticsHarness harness, string dimension, int season = 1955)
    {
        var choices = harness.Politics.ChoicesFor(harness.Of(), season, new HashSet<string>(StringComparer.Ordinal));
        return choices.First(choice => choice.DimensionId == dimension);
    }

    private static void Propose(PoliticsHarness harness, string team, string dimension, string value, GameDate on) =>
        harness.Politics.Propose(Series, team, dimension, value, on);

    private static void VoteAll(PoliticsHarness harness, BallotItem item, string option)
    {
        foreach (var team in harness.Teams)
        {
            harness.Politics.Cast(Series, team, item.Id, option, 0, harness.Today);
        }
    }

    /// <summary>A proposal by the first team, voted for by every team: the rule changes from the next season.</summary>
    private static (PoliticsHarness Harness, string Dimension, string Value) AdoptedPointsChange(VoteMode mode = VoteMode.OneVoteEach)
    {
        var harness = PoliticsHarness.Create(new HarnessOptions { AllHuman = true, Mode = mode });
        harness.LiveFrom(new GameDate(1955, 1, 2), Feb10);
        var choice = FirstChoice(harness, "points_scale");
        Propose(harness, "t01", choice.DimensionId, choice.Value, Feb10);
        harness.LiveTo(new GameDate(1955, 5, 1));
        var item = harness.Items().Single(i => i.DimensionId == "points_scale");
        VoteAll(harness, item, "v1");
        harness.LiveTo(item.Deadline);
        return (harness, choice.DimensionId, choice.Value);
    }

    [Fact]
    public void AChangeVotedInSeasonNIsNeverSeenInNAndIsInForceFromTheFirstDayOfNPlusOne()
    {
        var (harness, dimension, value) = AdoptedPointsChange();
        var authored1955 = PoliticsHarness.Data.RuleSetFor(1955).Value(dimension);
        var item = harness.Items().Single(i => i.DimensionId == dimension);

        Assert.Equal(BallotOutcome.Adopted, item.Result!.Outcome);
        Assert.NotEqual(authored1955, value);

        // Adopted on 31 May, yet 1955 keeps its own rule to the last day.
        for (var day = harness.Today; day <= new GameDate(1955, 12, 31); day = day.AddDays(40))
        {
            harness.LiveTo(day);
            Assert.Equal(authored1955, harness.Section.RuleSetFor(Series, 1955)!.Value(dimension));
            Assert.Equal(value, harness.Section.RuleSetFor(Series, 1956)!.Value(dimension));
        }

        harness.LiveTo(new GameDate(1955, 12, 31));
        Assert.Equal(authored1955, harness.Section.RuleSetFor(Series, 1955)!.Value(dimension));
        Assert.Equal(1955, harness.Of().Season);

        harness.LiveTo(new GameDate(1956, 1, 1));
        Assert.Equal(1956, harness.Of().Season);
        Assert.Equal(value, harness.Section.RuleSetFor(Series, 1956)!.Value(dimension));
        Assert.Null(harness.Section.RuleSetFor(Series, 1955));
    }

    [Fact]
    public void ANewTeamBallotAndEveryResultKeepTheirReason()
    {
        var (harness, _, _) = AdoptedPointsChange();
        harness.LiveTo(new GameDate(1955, 12, 31));

        Assert.NotEmpty(harness.Items());
        Assert.All(harness.Items(), item =>
        {
            Assert.NotNull(item.Result);
            Assert.False(string.IsNullOrWhiteSpace(item.Result!.ReasonKey));
            Assert.False(string.IsNullOrWhiteSpace(item.ReasonKey));
            Assert.Equal(harness.Teams.Count, item.Result.Stances.Count);
            Assert.All(item.Result.Stances.Where(stance => !harness.Environment.IsHuman(PoliticsHarness.Org(stance.TeamId))), stance =>
                Assert.False(string.IsNullOrWhiteSpace(stance.ReasonKey)));
        });
    }

    [Fact]
    public void ATeamThatProposedInNIsRefusedInNPlusOneAndNPlusTwoAndAllowedInNPlusThree()
    {
        var harness = PoliticsHarness.Create();
        harness.LiveFrom(new GameDate(1955, 1, 2), Feb10);
        var choice = FirstChoice(harness, "points_scale");
        Propose(harness, "t01", choice.DimensionId, choice.Value, Feb10);

        foreach (var year in new[] { 1956, 1957 })
        {
            harness.LiveTo(new GameDate(year, 2, 10));
            var refusal = harness.Politics.CheckPropose(Series, "t01", "safety_car", "physical", new GameDate(year, 2, 10));
            Assert.NotNull(refusal);
            Assert.Equal(RegulationKeys.Cooldown, refusal.Key);
            Assert.Equal("1958", refusal.Parameters["season"]);
        }

        harness.LiveTo(new GameDate(1958, 2, 10));
        var rules = harness.Section.RuleSetFor(Series, 1958)!;
        var allowed = harness.Politics.ChoicesFor(harness.Of(), 1958, new HashSet<string>(StringComparer.Ordinal)).First();
        Assert.Null(harness.Politics.CheckPropose(Series, "t01", allowed.DimensionId, allowed.Value, new GameDate(1958, 2, 10)));
        Assert.NotNull(rules);
    }

    [Fact]
    public void AnAiTeamThatProposedInNHasTheSameWaitAndAnAiTeamNeverProposesTwiceInThreeSeasons()
    {
        foreach (var seed in new ulong[] { 1, 2, 3, 4, 5, 6, 7, 8 })
        {
            var harness = PoliticsHarness.Create(new HarnessOptions { Seed = seed, Capital = _ => 400_000 });
            var proposedIn = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (var year = 1955; year <= 1962; year++)
            {
                harness.LiveTo(new GameDate(year, 12, 31));
                foreach (var item in harness.Items().Where(i => i.Season == year && i.Origin == BallotOrigin.Teams))
                {
                    foreach (var proposer in item.Variants.SelectMany(variant => variant.ProposerTeamIds))
                    {
                        if (!proposedIn.TryGetValue(proposer, out var list))
                        {
                            list = [];
                            proposedIn.Add(proposer, list);
                        }

                        list.Add(year);
                    }
                }
            }

            foreach (var (team, years) in proposedIn)
            {
                for (var i = 1; i < years.Count; i++)
                {
                    Assert.True(years[i] - years[i - 1] >= 3, $"{team} proposed in {years[i - 1]} and again in {years[i]} (seed {seed})");
                }
            }
        }
    }

    [Fact]
    public void ARefusedProposalOfAnAiTeamInCooldownSaysWhy()
    {
        var harness = PoliticsHarness.Create();
        var aiTeam = harness.Teams.Skip(1).First(team => harness.Of().TeamOf(team)!.ProposeFromSeason > 1955);
        var first = harness.Of().TeamOf(aiTeam)!.ProposeFromSeason;
        harness.LiveFrom(new GameDate(1955, 1, 2), Feb10);

        var refusal = harness.Politics.CheckPropose(Series, aiTeam, "safety_car", "physical", Feb10);

        Assert.NotNull(refusal);
        Assert.Equal(RegulationKeys.Cooldown, refusal.Key);
        Assert.Equal(first.ToString(System.Globalization.CultureInfo.InvariantCulture), refusal.Parameters["season"]);
    }

    [Fact]
    public void TheCooldownIsOnTheTeamOnlyARuleChangedInNCanBeProposedAgainByAnotherTeamAndTheFia()
    {
        var (harness, dimension, _) = AdoptedPointsChange();
        harness.LiveTo(new GameDate(1956, 2, 10));
        var day = new GameDate(1956, 2, 10);

        // t01 is in cooldown, t02 is not, and the rule just changed in force for 1956.
        Assert.Equal(RegulationKeys.Cooldown, harness.Politics.CheckPropose(Series, "t01", dimension, "top10_25_18_15_12_10_8_6_4_2_1", day)!.Key);
        var again = harness.Politics.ChoicesFor(harness.Of(), 1956, new HashSet<string>(StringComparer.Ordinal))
            .First(choice => choice.DimensionId == dimension);
        Assert.Null(harness.Politics.CheckPropose(Series, "t02", dimension, again.Value, day));

        // The FIA may bring it too: the dimension is among its candidates for the next season, with no lock on it.
        Assert.Contains(
            harness.Politics.ChoicesFor(harness.Of(), 1956, new HashSet<string>(StringComparer.Ordinal)),
            choice => choice.DimensionId == dimension);

        // And it can go through: another team's proposal, voted by all, changes it again for 1957.
        Propose(harness, "t02", dimension, again.Value, day);
        harness.LiveTo(new GameDate(1956, 5, 1));
        var item = harness.Items().Single(i => i.Season == 1956 && i.DimensionId == dimension);
        VoteAll(harness, item, "v1");
        harness.LiveTo(item.Deadline);

        Assert.Equal(BallotOutcome.Adopted, harness.Of().ItemOf(item.Id)!.Result!.Outcome);
        Assert.Equal(again.Value, harness.Section.RuleSetFor(Series, 1957)!.Value(dimension));
    }

    [Fact]
    public void AiStartingCooldownsDifferAreTheSameForTheSameSeedAndThePlayerStartsFree()
    {
        var options = new HarnessOptions { Seed = 11 };
        var first = PoliticsHarness.Create(options);
        var again = PoliticsHarness.Create(options);

        var player = first.Of().TeamOf("t01")!;
        var ai = first.Teams.Skip(1).Select(team => first.Of().TeamOf(team)!.ProposeFromSeason).ToArray();

        Assert.Equal(1955, player.ProposeFromSeason);
        Assert.True(ai.Distinct().Count() > 1, "the AI teams must not all start with the same cooldown");
        Assert.All(ai, from => Assert.InRange(from, 1955, 1955 + RegulationEstimates.StartingCooldownMaxSeasons));
        Assert.Contains(ai, from => from > 1955);
        Assert.Equal(
            first.Teams.Select(team => first.Of().TeamOf(team)!.ProposeFromSeason),
            again.Teams.Select(team => again.Of().TeamOf(team)!.ProposeFromSeason));
        Assert.Equal(first.World.StateHash(), again.World.StateHash());

        var others = Enumerable.Range(12, 6).Select(seed => PoliticsHarness.Create(options with { Seed = (ulong)seed }))
            .Select(h => string.Join(",", h.Teams.Select(team => h.Of().TeamOf(team)!.ProposeFromSeason)))
            .Append(string.Join(",", first.Teams.Select(team => first.Of().TeamOf(team)!.ProposeFromSeason)));
        Assert.True(others.Distinct().Count() > 1, "another seed must stagger the AI teams differently");
    }

    [Fact]
    public void AProposalThatIsMergedIntoAnotherOneStillStartsTheCooldownOfTheTeamThatPaidForIt()
    {
        var harness = PoliticsHarness.Create(new HarnessOptions { AllHuman = true });
        harness.LiveFrom(new GameDate(1955, 1, 2), Feb10);
        var choices = harness.Politics.ChoicesFor(harness.Of(), 1955, new HashSet<string>(StringComparer.Ordinal))
            .Where(choice => choice.DimensionId == "points_scale").ToArray();
        Propose(harness, "t01", "points_scale", choices[0].Value, Feb10);
        Propose(harness, "t02", "points_scale", choices[1].Value, Feb10.AddDays(1));
        Propose(harness, "t03", "points_scale", choices[0].Value, Feb10.AddDays(2));

        harness.LiveTo(new GameDate(1955, 5, 1));

        var item = Assert.Single(harness.Items(), i => i.Origin == BallotOrigin.Teams);
        Assert.Equal(2, item.Variants.Count);
        Assert.Equal(["t01", "t03"], item.Variants.Single(variant => variant.Value == choices[0].Value).ProposerTeamIds);
        Assert.Equal(["t02"], item.Variants.Single(variant => variant.Value == choices[1].Value).ProposerTeamIds);
        foreach (var team in new[] { "t01", "t02", "t03" })
        {
            Assert.Equal(1958, harness.Of().TeamOf(team)!.ProposeFromSeason);
        }
    }

    [Fact]
    public void TwoProposalsOnTheSameDimensionBecomeOneVoteWithVariantsAndOthersStaySeparate()
    {
        var harness = PoliticsHarness.Create(new HarnessOptions { AllHuman = true });
        harness.LiveFrom(new GameDate(1955, 1, 2), Feb10);
        var points = harness.Politics.ChoicesFor(harness.Of(), 1955, new HashSet<string>(StringComparer.Ordinal))
            .Where(choice => choice.DimensionId == "points_scale").Take(2).ToArray();
        var safety = FirstChoice(harness, "safety_car");
        Propose(harness, "t01", points[0].DimensionId, points[0].Value, Feb10);
        Propose(harness, "t02", points[1].DimensionId, points[1].Value, Feb10);
        Propose(harness, "t03", safety.DimensionId, safety.Value, Feb10);

        harness.LiveTo(new GameDate(1955, 5, 1));

        var items = harness.Items().Where(i => i.Origin == BallotOrigin.Teams).ToArray();
        Assert.Equal(2, items.Length);
        var merged = items.Single(i => i.DimensionId == "points_scale");
        Assert.Equal([points[0].Value, points[1].Value], merged.Variants.Select(variant => variant.Value).Order());
        Assert.Equal(["v1", "v2"], merged.Variants.Select(variant => variant.Id));
        Assert.Single(items.Single(i => i.DimensionId == "safety_car").Variants);
        Assert.Empty(harness.Of().Pending);
    }

    [Fact]
    public void TheFeeIsChargedWithItsReasonIsNotRefundedWhenTheProposalFailsAndMayTakeCashBelowZero()
    {
        var harness = PoliticsHarness.Create(new HarnessOptions { AllHuman = true, Capital = _ => 100 });
        harness.LiveFrom(new GameDate(1955, 1, 2), Feb10);
        var team = PoliticsHarness.Org("t01");
        Assert.True(harness.Finance.BalanceOf(team) > 0);
        var quote = harness.Politics.QuoteFor(Series, "t01", 1955)!;
        var before = harness.Finance.BalanceOf(team);
        var choice = FirstChoice(harness, "safety_car");

        Propose(harness, "t01", choice.DimensionId, choice.Value, Feb10);

        var fee = harness.Finance.EntriesOf(team).Last();
        Assert.Equal(RegulationKeys.LedgerProposalFee, fee.ReasonKey);
        Assert.Equal(-quote.FeeCents, fee.AmountCents);
        Assert.Equal(LedgerCategories.Other, fee.Category);
        Assert.Equal(Series, fee.Counterparty);
        Assert.Equal(before - quote.FeeCents, harness.Finance.BalanceOf(team));
        Assert.True(harness.Finance.BalanceOf(team) < 0, "the fee took the cash below zero and was still charged");

        // Everybody votes to keep the rule: the proposal fails, and nothing comes back.
        harness.LiveTo(new GameDate(1955, 5, 1));
        var item = harness.Items().Single(i => i.DimensionId == choice.DimensionId);
        VoteAll(harness, item, BallotOptions.StatusQuo);
        harness.LiveTo(item.Deadline.AddDays(1));

        Assert.Equal(BallotOutcome.Rejected, harness.Of().ItemOf(item.Id)!.Result!.Outcome);
        Assert.DoesNotContain(harness.Finance.EntriesOf(team), entry => entry.AmountCents > 0 && entry.Date >= Feb10);
        Assert.Single(harness.Finance.EntriesOf(team), entry => entry.ReasonKey == RegulationKeys.LedgerProposalFee);
        Assert.Equal(before - quote.FeeCents, harness.Finance.BalanceOf(team));
    }

    [Fact]
    public void AProposalOutsideTheWindowIsRefusedWithTheDateItOpens()
    {
        var harness = PoliticsHarness.Create();
        harness.LiveFrom(new GameDate(1955, 1, 2), new GameDate(1955, 6, 1));

        var refusal = harness.Politics.CheckPropose(Series, "t01", "safety_car", "physical", new GameDate(1955, 6, 1));

        Assert.Equal(RegulationKeys.WindowClosed, refusal!.Key);
        Assert.Equal("1956-02-01", refusal.Parameters["opens"]);
        Assert.Equal(RegulationKeys.WindowClosed, harness.Politics.CheckPropose(Series, "t01", "safety_car", "physical", new GameDate(1955, 1, 5))!.Key);
    }

    [Fact]
    public void ARuleThatIsNotLiveOrNotAValueOrNoChangeIsRefusedWithAReason()
    {
        var harness = PoliticsHarness.Create();
        harness.LiveFrom(new GameDate(1955, 1, 2), Feb10);

        Assert.Equal(RegulationKeys.NotLive, harness.Politics.CheckPropose(Series, "t01", "red_flag_restart", "resume_behind_safety_car", Feb10)!.Key);
        Assert.Equal(RegulationKeys.ValueNotAllowed, harness.Politics.CheckPropose(Series, "t01", "safety_car", "nonsense", Feb10)!.Key);
        Assert.Equal(RegulationKeys.SameValue, harness.Politics.CheckPropose(Series, "t01", "safety_car", harness.Of().Values.First(pair => pair.Key == "safety_car").Value, Feb10)!.Key);
        Assert.Equal(RegulationKeys.UnknownSeries, harness.Politics.CheckPropose("nope", "t01", "safety_car", "physical", Feb10)!.Key);
        Assert.Equal(RegulationKeys.UnknownTeam, harness.Politics.CheckPropose(Series, "ghost", "safety_car", "physical", Feb10)!.Key);
    }

    [Fact]
    public void TheFiaBringsFourToSixVotesPerSeriesPerYearSpreadOverTheSeasonAndEachClosesBeforeItEnds()
    {
        foreach (var seed in new ulong[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 })
        {
            var harness = PoliticsHarness.Create(new HarnessOptions { Seed = seed });
            for (var year = 1955; year <= 1958; year++)
            {
                harness.LiveTo(new GameDate(year, 12, 31));
                var fia = harness.Items().Where(item => item.Season == year && item.Origin == BallotOrigin.Fia).OrderBy(item => item.Announced).ToArray();

                Assert.InRange(fia.Length, RegulationEstimates.FiaVotesMin, RegulationEstimates.FiaVotesMax);
                Assert.Equal(fia.Length, fia.Select(item => item.DimensionId).Distinct().Count());
                Assert.Equal(fia.Length, harness.Of().FiaSlotsDone);
                Assert.True(fia.First().Announced >= RegulationSchedule.FiaFirstAnnounced(year));
                Assert.True(fia.Last().Announced <= RegulationSchedule.FiaLastAnnounced(year));
                Assert.True(fia.Select(item => item.Announced).Distinct().Count() == fia.Length, "the votes are spread, not announced on one day");
                Assert.All(fia, item => Assert.True(item.Deadline < new GameDate(year, 11, 1)));
                Assert.All(harness.Items().Where(item => item.Season == year), item => Assert.True(item.IsResolved));
            }
        }
    }

    [Fact]
    public void FiaVotesComeFromTheStateOfTheWorldAndAreNeverANoOp()
    {
        var harness = PoliticsHarness.Create(new HarnessOptions { Seed = 3 });
        harness.LiveTo(new GameDate(1955, 12, 31));

        foreach (var item in harness.Items().Where(i => i.Origin == BallotOrigin.Fia))
        {
            var variant = Assert.Single(item.Variants);
            Assert.NotEqual(item.CurrentValue, variant.Value);
            Assert.True(LiveRules.HasEffect(item.DimensionId, item.CurrentValue, variant.Value), item.DimensionId);
            Assert.StartsWith("regulation.fia.reason.", item.ReasonKey, StringComparison.Ordinal);
            Assert.Empty(variant.ProposerTeamIds);
        }

        // Distress in the field pushes the FIA toward what saves money; a calm world has no such push.
        var calm = new FiaPressures(0, 0, 0);
        var distressed = new FiaPressures(0, 0, 1);
        var candidates = new[]
        {
            new RuleCandidate("refuelling", "allowed", "banned", LiveRules.TraitsOf("refuelling", "banned") - LiveRules.TraitsOf("refuelling", "allowed")),
            new RuleCandidate("double_points_finale", "no", "yes", LiveRules.TraitsOf("double_points_finale", "yes") - LiveRules.TraitsOf("double_points_finale", "no")),
        };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var seed = 0; seed < 20; seed++)
        {
            var rng = RegulationSchedule.Child((ulong)seed, 1955, "test");
            seen.Add(FiaAgenda.Propose(distressed, candidates, rng)!.ReasonKey + ":" + FiaAgenda.Propose(distressed, [candidates[0]], RegulationSchedule.Child((ulong)seed, 1955, "x"))!.ReasonKey);
        }

        Assert.Contains(seen, text => text.EndsWith(FiaAgenda.ReasonCosts, StringComparison.Ordinal));
        Assert.Equal(FiaAgenda.ReasonCosts, FiaAgenda.Propose(distressed, [candidates[0]], RegulationSchedule.Child(1, 1955, "x"))!.ReasonKey);
        Assert.NotEqual(FiaAgenda.ReasonCosts, FiaAgenda.Propose(calm, [candidates[0]], RegulationSchedule.Child(1, 1955, "x"))!.ReasonKey);
    }

    [Fact]
    public void AllTheDeadlinesAreInOrderAndTheTeamsBallotComesBeforeTheFiasVotes()
    {
        var harness = PoliticsHarness.Create(new HarnessOptions { AllHuman = true });
        harness.LiveFrom(new GameDate(1955, 1, 2), Feb10);
        var choice = FirstChoice(harness, "safety_car");
        Propose(harness, "t01", choice.DimensionId, choice.Value, Feb10);
        harness.LiveTo(new GameDate(1955, 12, 31));

        var team = harness.Items().Single(item => item.Origin == BallotOrigin.Teams);
        Assert.Equal(new GameDate(1955, 5, 1), team.Announced);
        Assert.Equal(new GameDate(1955, 5, 31), team.Deadline);
        Assert.All(harness.Items().Where(item => item.Origin == BallotOrigin.Fia), item => Assert.True(item.Announced > team.Deadline));
    }

    [Fact]
    public void ASameSeedGivesTheSameVotesAndOutcomesAndOtherStreamsDoNotMoveThem()
    {
        var first = PoliticsHarness.Create(new HarnessOptions { Seed = 21 });
        var second = PoliticsHarness.Create(new HarnessOptions { Seed = 21 });
        var other = PoliticsHarness.Create(new HarnessOptions { Seed = 22 });
        foreach (var harness in new[] { first, second, other })
        {
            harness.LiveTo(new GameDate(1957, 12, 31));
        }

        Assert.Equal(first.World.StateHash(), second.World.StateHash());
        Assert.NotEqual(first.World.StateHash(), other.World.StateHash());

        // Drawing from the other streams in between changes nothing: the Regulations stream is isolated (INV-004).
        var disturbed = PoliticsHarness.Create(new HarnessOptions { Seed = 21 });
        for (var day = new GameDate(1955, 1, 2); day <= new GameDate(1957, 12, 31); day = day.AddDays(1))
        {
            Paddock.Domain.Random.RngStreams.Derive(21, Paddock.Domain.Random.RngStreamName.Market, day.Year).NextULong();
            Paddock.Domain.Random.RngStreams.Derive(21, Paddock.Domain.Random.RngStreamName.Weather, day.Year, day.DayOfYear).NextULong();
            disturbed.LiveTo(day);
        }

        Assert.Equal(first.World.StateHash(), disturbed.World.StateHash());
    }

    [Fact]
    public void ATiedVoteOnATeamsProposalIsDecidedByThePresidentAndAClearOneIsNot()
    {
        var harness = PoliticsHarness.Create(new HarnessOptions { AllHuman = true });
        harness.LiveFrom(new GameDate(1955, 1, 2), Feb10);
        var choice = FirstChoice(harness, "points_scale");
        Propose(harness, "t01", choice.DimensionId, choice.Value, Feb10);
        var other = FirstChoice(harness, "safety_car");
        Propose(harness, "t02", other.DimensionId, other.Value, Feb10);
        harness.LiveTo(new GameDate(1955, 5, 1));
        var tie = harness.Items().Single(item => item.DimensionId == "points_scale");
        var clear = harness.Items().Single(item => item.DimensionId == "safety_car");

        foreach (var (team, index) in harness.Teams.Select((team, index) => (team, index)))
        {
            harness.Politics.Cast(Series, team, tie.Id, index < 5 ? "v1" : BallotOptions.StatusQuo, 0, harness.Today);
            harness.Politics.Cast(Series, team, clear.Id, index < 7 ? "v1" : BallotOptions.StatusQuo, 0, harness.Today);
        }

        harness.LiveTo(tie.Deadline);

        var tied = harness.Of().ItemOf(tie.Id)!.Result!;
        var decided = harness.Of().ItemOf(clear.Id)!.Result!;
        var variant = tie.Variants.Single();
        var pick = FiaPresident.Choose(
            BallotOrigin.Teams,
            [BallotOptions.StatusQuo, variant.Id],
            _ => LiveRules.TraitsOf(tie.DimensionId, variant.Value) - LiveRules.TraitsOf(tie.DimensionId, tie.CurrentValue));
        Assert.True(tied.PresidentDecided);
        Assert.Equal(pick, tied.WinningOption);
        Assert.Equal(pick == BallotOptions.StatusQuo ? BallotOutcome.Rejected : BallotOutcome.Adopted, tied.Outcome);
        Assert.Equal(pick == BallotOptions.StatusQuo ? BallotTally.ReasonPresidentKeptStatusQuo : BallotTally.ReasonAdoptedByPresident, tied.ReasonKey);
        Assert.False(decided.PresidentDecided);
        Assert.Equal(BallotOutcome.Adopted, decided.Outcome);
    }

    [Fact]
    public void ATiedVoteOnAnFiaProposalGoesToTheFiasOwnAgenda()
    {
        var harness = PoliticsHarness.Create(new HarnessOptions { AllHuman = true });
        harness.LiveTo(new GameDate(1955, 6, 1));
        var item = harness.Items().First(i => i.Origin == BallotOrigin.Fia);
        foreach (var (team, index) in harness.Teams.Select((team, index) => (team, index)))
        {
            harness.Politics.Cast(Series, team, item.Id, index < 5 ? "v1" : BallotOptions.StatusQuo, 0, harness.Today);
        }

        harness.LiveTo(item.Deadline);

        var result = harness.Of().ItemOf(item.Id)!.Result!;
        Assert.True(result.PresidentDecided);
        Assert.Equal(BallotOutcome.Adopted, result.Outcome);
        Assert.Equal(BallotTally.ReasonAdoptedByPresident, result.ReasonKey);
    }

    [Fact]
    public void AHumanWhoDoesNotVoteAbstainsAndTheResultSaysSo()
    {
        var harness = PoliticsHarness.Create(new HarnessOptions { Humans = ["t01"] });
        harness.LiveTo(new GameDate(1955, 12, 31));

        foreach (var item in harness.Items())
        {
            var stance = item.Result!.Stances.Single(entry => entry.TeamId == "t01");
            Assert.Equal(BallotOptions.Abstain, stance.Option);
            Assert.Equal(0, stance.Weight);
        }
    }

    [Fact]
    public void TheHumanTeamHearsOfEveryVoteWhenItOpensAndOfItsResultWhenItIsCounted()
    {
        var harness = PoliticsHarness.Create(new HarnessOptions { Humans = ["t01"], WithInbox = true });
        harness.LiveTo(new GameDate(1955, 12, 31));

        var items = harness.Inbox!.Section.ItemsOf("human:t01");
        var announced = items.Where(item => item.Kind == RegulationKeys.AnnouncedKind).ToArray();
        var counted = items.Where(item => item.Kind == RegulationKeys.ResultKind).ToArray();
        var ballot = harness.Items();

        Assert.Equal(ballot.Count, announced.Length);
        Assert.Equal(ballot.Count, counted.Length);
        Assert.All(announced, item => Assert.False(item.NeedsDecision));
        Assert.All(announced, item => Assert.Equal(RegulationKeys.AnnouncedSubject, item.SubjectKey));
        Assert.Contains(announced, item => item.Arguments["origin"] == "fia");
        Assert.All(counted, item => Assert.Contains(item.Arguments["outcome"], new[] { "Adopted", "Rejected", "NoVotes" }));
        Assert.All(counted, item => Assert.StartsWith("regulation.result.", item.Arguments["reason"], StringComparison.Ordinal));
        Assert.Empty(harness.Inbox.Section.ItemsOf("human:t02"));
        Assert.Equal(0, harness.Inbox.Section.HoldingClockCount("human:t01"));
    }

    [Fact]
    public void ASeriesWithNoHistoricalVotingDoesNothing()
    {
        var historical = PoliticsHarness.Create();
        Assert.True(historical.Environment.Votes);
        var noVotes = new RegulationEnvironment(
            RulesSource.Historical,
            VoteMode.OneVoteEach,
            1,
            [],
            PoliticsHarness.Data.DimensionIds,
            PoliticsHarness.Data.Periods,
            historical.Environment.Control,
            historical.Environment.Managers,
            new SingleSeriesDirectory());
        Assert.False(noVotes.Votes);
        var world = historical.World.WithoutSection(RegulationsSection.SectionName);
        var politics = new RegulationPolitics(new RegulationBook(() => world, next => world = next), noVotes);

        politics.OnDay(new GameDate(1955, 6, 1));

        Assert.Null(world.Section(RegulationsSection.SectionName));
        Assert.Equal(RegulationKeys.NotVoted, politics.CheckPropose(Series, "t01", "safety_car", "physical", Feb10)!.Key);
    }
}
