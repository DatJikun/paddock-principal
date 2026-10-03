using System.Globalization;
using Paddock.Data.Authored;
using Paddock.Domain.Random;
using Paddock.Domain.World;
using Paddock.Simulation.Regulation;

namespace Paddock.SimRunner;

/// <summary>
/// <c>vote-sim --from-year Y --seasons N --seed S [--data DIR]</c>: runs the voted-regulations engine
/// with stand-in voters and prints which rules changed each season.
/// </summary>
public static class VoteSimCommand
{
    private const int VoterCount = 10;

    public static int Execute(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        int? fromYear = null;
        int? seasons = null;
        ulong? seed = null;
        string? dataRoot = null;
        for (var i = 1; i < args.Length; i++)
        {
            var flag = args[i];
            if (flag is not ("--from-year" or "--seasons" or "--seed" or "--data") || i + 1 >= args.Length)
            {
                stderr.WriteLine($"Bad or incomplete argument: {flag}");
                return 1;
            }

            var value = args[++i];
            var ok = true;
            switch (flag)
            {
                case "--from-year":
                    ok = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var y);
                    fromYear = y;
                    break;
                case "--seasons":
                    ok = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0;
                    seasons = n;
                    break;
                case "--seed":
                    ok = ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var s);
                    seed = s;
                    break;
                default:
                    dataRoot = value;
                    break;
            }

            if (!ok)
            {
                stderr.WriteLine($"Invalid {flag} value: {value}");
                return 1;
            }
        }

        if (fromYear is null || seasons is null || seed is null)
        {
            stderr.WriteLine("Expected: vote-sim --from-year <int> --seasons <int> --seed <ulong> [--data <dir>]");
            return 1;
        }

        dataRoot ??= FindDataRoot();
        if (dataRoot is null)
        {
            stderr.WriteLine("Could not find the data directory; pass --data <dir>.");
            return 1;
        }

        var data = AuthoredDataLoader.Load(dataRoot);
        var specs = RuleCatalog.ToSpecs(data.Catalog);
        var catalog = specs.ToDictionary(s => s.Id, StringComparer.Ordinal);
        var voters = Enumerable.Range(1, VoterCount)
            .Select(p => new Voter($"team{p:00}", new StandInInterest($"team{p:00}"), ConstructorPoints: 100 - p * 7, ChampionshipPosition: p))
            .ToList();
        var proposerIds = voters.Select(v => v.Id).ToList();
        var options = new ProposalGeneratorOptions();
        var rejected = new List<RejectedProposal>();
        var ruleSet = data.RuleSetFor(fromYear.Value);

        for (var n = 0; n < seasons.Value; n++)
        {
            var next = ruleSet.Season + 1;
            var rng = ProposalGenerator.StreamFor(seed.Value, next);
            var proposals = ProposalGenerator.Generate(ruleSet, specs, proposerIds, rejected, options, rng);
            var outcomes = SeasonVote.Run(ruleSet, proposals, voters, VotingBodyDefaults.ForSeason(next), rng);
            var changed = new List<string>();
            foreach (var outcome in outcomes)
            {
                var p = outcome.Proposal;
                if (outcome.Passed)
                {
                    changed.Add($"{p.DimensionId}: {ruleSet.Value(p.DimensionId)} -> {p.ProposedValue}");
                }
                else
                {
                    rejected.Add(new RejectedProposal(p.DimensionId, p.ProposedValue, next));
                }
            }

            ruleSet = SeasonVote.Apply(ruleSet, outcomes, catalog, next);
            stdout.WriteLine(
                $"{next.ToString(CultureInfo.InvariantCulture)}: {(changed.Count == 0 ? "no changes" : string.Join("; ", changed))}");
        }

        return 0;
    }

    private static string? FindDataRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "data");
            if (Directory.Exists(Path.Combine(candidate, "authored")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>Stand-in voter: a stable pseudo-random taste per (team, dimension, value) plus a status-quo bias. Not an AI personality.</summary>
    private sealed class StandInInterest(string teamId) : IVoterInterest
    {
        public IReadOnlyList<UtilityFactor> Evaluate(RuleSet current, RuleProposal proposal)
        {
            var taste = RngStreams
                .Derive(0, teamId + "|" + proposal.DimensionId + "|" + proposal.ProposedValue, 0)
                .NextDouble() * 2 - 1;
            return
            [
                new UtilityFactor("vote.reason.taste", taste),
                new UtilityFactor("vote.reason.statusQuo", -0.2),
            ];
        }
    }
}
