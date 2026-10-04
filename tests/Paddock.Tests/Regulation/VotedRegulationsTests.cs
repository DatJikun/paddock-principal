using System.Globalization;
using Paddock.Data.Authored;
using Paddock.Domain.Random;
using Paddock.Domain.World;
using Paddock.Simulation.Regulation;

namespace Paddock.Tests.Regulation;

public class VotedRegulationsTests
{
    private sealed class FixedInterest(double utility) : IVoterInterest
    {
        public IReadOnlyList<UtilityFactor> Evaluate(RuleSet current, RuleProposal proposal) =>
            [new UtilityFactor("test.fixed", utility)];
    }

    private sealed class HashInterest(string id) : IVoterInterest
    {
        public IReadOnlyList<UtilityFactor> Evaluate(RuleSet current, RuleProposal proposal) =>
            [new UtilityFactor("test.taste", RngStreams.Derive(1, id + proposal.DimensionId + proposal.ProposedValue, 0).NextDouble() * 2 - 1)];
    }

    private static readonly Lazy<AuthoredData> Data = new(() =>
        AuthoredDataLoader.Load(Path.Combine(RepoPaths.Root(), "data")));

    private static IReadOnlyList<RuleDimensionSpec> Specs => RuleCatalog.ToSpecs(Data.Value.Catalog);

    private static Dictionary<string, RuleDimensionSpec> Catalog => Specs.ToDictionary(s => s.Id, StringComparer.Ordinal);

    private static RuleSet Small() => RuleSet.For(
        1990,
        ["a", "n"],
        [new RulePeriod("a", "x", 1950, null), new RulePeriod("n", "500", 1950, null)]);

    private static Dictionary<string, RuleDimensionSpec> SmallCatalog() => new(StringComparer.Ordinal)
    {
        ["a"] = new RuleDimensionSpec("a", ["x", "y", "z"]),
        ["n"] = new RuleDimensionSpec("n", 400, 600, 10),
    };

    private static RuleProposal Proposal(string value = "y") => new("p1", "a", value, "t1", 1991);

    private static List<Voter> Voters(params double[] utilities) =>
        utilities.Select((u, i) => new Voter($"v{i}", new FixedInterest(u))).ToList();

    private static bool Passes(VoteThreshold threshold, params double[] utilities) =>
        SeasonVote.Run(
            Small(),
            [Proposal()],
            Voters(utilities),
            new VotingBodyOptions(threshold, VoteWeighting.Equal, NoiseScale: 0),
            RngStreams.Derive(1, RngStreamName.Regulations, 1991))[0].Passed;

    [Fact]
    public void WithAppliesValidChangeImmutably()
    {
        var current = Small();
        var next = current.With(SmallCatalog(), new RuleChange("a", "z"), new RuleChange("n", "550"));
        Assert.Equal(1991, next.Season);
        Assert.Equal("z", next.Value("a"));
        Assert.Equal("550", next.Value("n"));
        Assert.Equal("x", current.Value("a"));
    }

    [Theory]
    [InlineData("zzz", "x", RuleChangeError.UnknownDimension)]
    [InlineData("a", "nope", RuleChangeError.ValueNotAllowed)]
    [InlineData("n", "heavy", RuleChangeError.NotANumber)]
    [InlineData("n", "601", RuleChangeError.OutOfRange)]
    public void WithRejectsInvalidChangesWithCode(string dimension, string value, RuleChangeError code)
    {
        var ex = Assert.Throws<RuleChangeException>(() => Small().With(SmallCatalog(), new RuleChange(dimension, value)));
        Assert.Equal(code, ex.Code);
    }

    [Fact]
    public void WithRejectsDuplicateDimension()
    {
        var ex = Assert.Throws<RuleChangeException>(() =>
            Small().With(SmallCatalog(), new RuleChange("a", "y"), new RuleChange("a", "z")));
        Assert.Equal(RuleChangeError.DuplicateDimension, ex.Code);
    }

    [Fact]
    public void UnanimityIsBlockedByOneOpponent()
    {
        Assert.True(Passes(VoteThreshold.Unanimity, 1, 1, 1, 1));
        Assert.False(Passes(VoteThreshold.Unanimity, 1, 1, 1, -1));
    }

    [Fact]
    public void TwoThirdsEdgeCases()
    {
        Assert.True(Passes(VoteThreshold.TwoThirds, 1, 1, 1, 1, -1, -1)); // 4 of 6 exactly
        Assert.False(Passes(VoteThreshold.TwoThirds, 1, 1, 1, -1, -1, -1)); // 3 of 6
        Assert.True(Passes(VoteThreshold.TwoThirds, 1, 1, 1, -1)); // 3 of 4
    }

    [Fact]
    public void SimpleMajorityNeedsMoreThanHalf()
    {
        Assert.False(Passes(VoteThreshold.SimpleMajority, 1, 1, -1, -1));
        Assert.True(Passes(VoteThreshold.SimpleMajority, 1, 1, 1, -1));
    }

    [Fact]
    public void TwoOfThreeIsExactlyTwoThirdsAndPasses()
    {
        Assert.True(Passes(VoteThreshold.TwoThirds, 1, 1, -1));
    }

    [Fact]
    public void WeightedBodiesCountWeights()
    {
        var voters = new List<Voter>
        {
            new("big", new FixedInterest(1), ConstructorPoints: 90, ChampionshipPosition: 1, FixedSeats: 3),
            new("s1", new FixedInterest(-1), ConstructorPoints: 5, ChampionshipPosition: 2, FixedSeats: 1),
            new("s2", new FixedInterest(-1), ConstructorPoints: 5, ChampionshipPosition: 3, FixedSeats: 1),
        };
        foreach (var weighting in new[] { VoteWeighting.ByPoints, VoteWeighting.ByChampionshipPosition, VoteWeighting.FixedSeats })
        {
            var outcome = SeasonVote.Run(
                Small(),
                [Proposal()],
                voters,
                new VotingBodyOptions(VoteThreshold.SimpleMajority, weighting, 0),
                RngStreams.Derive(1, RngStreamName.Regulations, 1991))[0];
            Assert.Equal(weighting != VoteWeighting.ByChampionshipPosition, outcome.Passed);
        }
    }

    [Fact]
    public void LobbyingShiftsUtilityAndTraceExplainsIt()
    {
        var voters = Voters(-0.5, -0.5, -0.5);
        var body = new VotingBodyOptions(VoteThreshold.SimpleMajority, VoteWeighting.Equal, 0);
        var lobby = new LobbyingInfluence(new Dictionary<string, double> { ["v0"] = 1, ["v1"] = 1 });
        var outcome = SeasonVote.Run(Small(), [Proposal()], voters, body, RngStreams.Derive(1, RngStreamName.Regulations, 1991), lobby)[0];
        Assert.True(outcome.Passed);
        var trace = outcome.Votes[0];
        Assert.Equal(1, trace.Lobbying);
        Assert.Equal(0.5, trace.TotalUtility, 9);
        Assert.Equal(VoteCast.For, trace.Cast);
        Assert.Equal("test.fixed", Assert.Single(trace.Factors).Reason);
        Assert.Equal(VoteCast.Against, outcome.Votes[2].Cast);
    }

    private static string RunSeasons(ulong seed, int seasons, bool disturbOtherStream)
    {
        var specs = Specs;
        var catalog = Catalog;
        var voters = Enumerable.Range(1, 8)
            .Select(i => new Voter($"t{i}", new HashInterest($"t{i}"), 100 - i, i))
            .ToList();
        var ids = voters.Select(v => v.Id).ToList();
        var ruleSet = Data.Value.RuleSetFor(1990);
        var rejected = new List<RejectedProposal>();
        var log = new List<string>();
        for (var n = 0; n < seasons; n++)
        {
            var next = ruleSet.Season + 1;
            if (disturbOtherStream)
            {
                RngStreams.Derive(seed, RngStreamName.Market, next).NextULong();
                RngStreams.Derive(seed, RngStreamName.Weather, next).NextULong();
            }

            var rng = ProposalGenerator.StreamFor(seed, next);
            var proposals = ProposalGenerator.Generate(ruleSet, specs, ids, rejected, new ProposalGeneratorOptions(), rng);
            var outcomes = SeasonVote.Run(ruleSet, proposals, voters, VotingBodyDefaults.ForSeason(next), rng);
            foreach (var o in outcomes)
            {
                log.Add($"{next}:{o.Proposal.DimensionId}={o.Proposal.ProposedValue}:{o.Passed}:{o.WeightFor.ToString(CultureInfo.InvariantCulture)}");
                if (!o.Passed)
                {
                    rejected.Add(new RejectedProposal(o.Proposal.DimensionId, o.Proposal.ProposedValue, next));
                }
            }

            ruleSet = SeasonVote.Apply(ruleSet, outcomes, catalog, next);
        }

        return string.Join("|", log);
    }

    [Fact]
    public void SameSeedGivesIdenticalOutcomesRegardlessOfOtherStreams()
    {
        var plain = RunSeasons(77, 20, disturbOtherStream: false);
        Assert.Equal(plain, RunSeasons(77, 20, disturbOtherStream: false));
        Assert.Equal(plain, RunSeasons(77, 20, disturbOtherStream: true));
        Assert.NotEqual(plain, RunSeasons(78, 20, disturbOtherStream: false));
    }

    [Fact]
    public void RegulationsStreamIsIsolatedFromExistingStreams()
    {
        var regulations = RngStreams.Derive(5, RngStreamName.Regulations, 1991).NextULong();
        foreach (var name in RngStreamName.All.Where(n => n != RngStreamName.Regulations))
        {
            Assert.NotEqual(regulations, RngStreams.Derive(5, name, 1991).NextULong());
        }

        Assert.Equal(13, RngStreamName.All.Count);
    }

    [Fact]
    public void ProposalsAreValidForEveryCatalogDimension()
    {
        var specs = Specs;
        var catalog = Catalog;
        var ruleSet = Data.Value.RuleSetFor(1990);
        var proposed = new HashSet<string>(StringComparer.Ordinal);
        for (ulong seed = 0; seed < 200; seed++)
        {
            var rng = ProposalGenerator.StreamFor(seed, 1991);
            var proposals = ProposalGenerator.Generate(
                ruleSet,
                specs,
                ["t1", "t2"],
                [],
                new ProposalGeneratorOptions(ProposalsPerSeason: specs.Count),
                rng);
            Assert.Equal(proposals.Count, proposals.Select(p => p.DimensionId).Distinct().Count());
            foreach (var p in proposals)
            {
                proposed.Add(p.DimensionId);
                Assert.NotEqual(ruleSet.Value(p.DimensionId), p.ProposedValue);
                // Throws if invalid.
                var next = ruleSet.With(catalog, p.ToChange());
                Assert.Equal(p.ProposedValue, next.Value(p.DimensionId));
            }
        }

        Assert.True(proposed.Count > 30, $"only {proposed.Count} dimensions were ever proposed");
    }

    [Fact]
    public void ProposalsNeverUseTheUnknownSentinelAsATargetValue()
    {
        var specs = Specs;
        var ruleSet = Data.Value.RuleSetFor(1990);
        Assert.Contains(specs, s => s.Kind == RuleDimensionKind.Choice && s.Values.Contains("unknown"));
        for (ulong seed = 0; seed < 500; seed++)
        {
            var rng = ProposalGenerator.StreamFor(seed, 1991);
            var proposals = ProposalGenerator.Generate(
                ruleSet,
                specs,
                ["t1"],
                [],
                new ProposalGeneratorOptions(ProposalsPerSeason: specs.Count),
                rng);
            Assert.DoesNotContain(proposals, p => p.ProposedValue == "unknown");
        }
    }

    [Fact]
    public void RejectedProposalsAreNotRepeatedWithinMemoryWindowButReturnAfterIt()
    {
        var specs = new List<RuleDimensionSpec> { new("a", ["x", "y"]) };
        var options = new ProposalGeneratorOptions(ProposalsPerSeason: 1, MaxStep: 1, RejectionMemorySeasons: 3);
        var rng = RngStreams.Derive(1, RngStreamName.Regulations, 1991);
        var rejected = new[] { new RejectedProposal("a", "y", 1990) };

        var inside = RuleSet.For(1992, ["a"], [new RulePeriod("a", "x", 1950, null)]); // start 1993, age 3
        Assert.Empty(ProposalGenerator.Generate(inside, specs, ["t"], rejected, options, rng));
        var outside = RuleSet.For(1993, ["a"], [new RulePeriod("a", "x", 1950, null)]); // start 1994, age 4
        Assert.Single(ProposalGenerator.Generate(outside, specs, ["t"], rejected, options, rng));
    }

    [Fact]
    public void FiftySeasonsFrom1990StayValid()
    {
        var specs = Specs;
        var catalog = Catalog;
        var voters = Enumerable.Range(1, 10)
            .Select(i => new Voter($"t{i}", new HashInterest($"t{i}"), 100 - i, i))
            .ToList();
        var ruleSet = Data.Value.RuleSetFor(1990);
        var rejected = new List<RejectedProposal>();
        var changes = 0;
        for (var n = 0; n < 50; n++)
        {
            var next = ruleSet.Season + 1;
            var rng = ProposalGenerator.StreamFor(2024, next);
            var proposals = ProposalGenerator.Generate(
                ruleSet, specs, voters.Select(v => v.Id).ToList(), rejected, new ProposalGeneratorOptions(), rng);
            var outcomes = SeasonVote.Run(ruleSet, proposals, voters, VotingBodyDefaults.ForSeason(next), rng);
            rejected.AddRange(outcomes.Where(o => !o.Passed)
                .Select(o => new RejectedProposal(o.Proposal.DimensionId, o.Proposal.ProposedValue, next)));
            changes += outcomes.Count(o => o.Passed);
            ruleSet = SeasonVote.Apply(ruleSet, outcomes, catalog, next);
            Assert.Equal(1990 + n + 1, ruleSet.Season);
        }

        Assert.Equal(2040, ruleSet.Season);
        Assert.True(changes > 0);
        foreach (var spec in specs)
        {
            var value = ruleSet.Value(spec.Id);
            if (spec.Kind == RuleDimensionKind.Choice)
            {
                // Dimensions that start at a non-catalog token ("unknown") are never touched.
                Assert.True(spec.Values.Contains(value) || value == Data.Value.RuleSetFor(1990).Value(spec.Id), spec.Id);
            }
            else if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                Assert.InRange(number, spec.Min, spec.Max);
            }
        }
    }
}
