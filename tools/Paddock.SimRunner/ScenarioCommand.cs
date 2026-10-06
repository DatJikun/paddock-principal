using System.Globalization;
using Paddock.Data.Authored;
using Paddock.Data.World;
using Paddock.SimRunner.Scenario;

namespace Paddock.SimRunner;

/// <summary>
/// <c>scenario phase4-1955 --seed S --team &lt;organizationId&gt; [--out dir] [--monkey] [--seeds N] [--skip-counterfactuals] [--skip-scripts]</c>.
/// A bot manager plays the same Application commands a human uses. Reports go to <c>data/cache/reports/</c> (git-ignored).
/// </summary>
public static class ScenarioCommand
{
    public const string Name = "scenario";

    public static int Execute(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        if (args.Length == 0 || !string.Equals(args[0], Name, StringComparison.Ordinal))
        {
            stderr.WriteLine("Unknown command. Expected: scenario phase4-1955 --seed <N> --team <id>");
            return 1;
        }

        if (args.Length < 2 || !string.Equals(args[1], Phase4Estimates.ScenarioName, StringComparison.Ordinal))
        {
            stderr.WriteLine("Unknown scenario. Expected: " + Phase4Estimates.ScenarioName);
            return 1;
        }

        ulong seed = Phase4Estimates.StorySeed;
        string? team = null;
        string? dataRoot = null;
        string? outDir = null;
        var monkey = false;
        int? monkeySeeds = null;
        var counterfactuals = true;
        var scripts = true;
        var seedSet = false;
        for (var i = 2; i < args.Length; i++)
        {
            var flag = args[i];
            if (flag is "--monkey")
            {
                monkey = true;
                continue;
            }

            if (flag is "--skip-counterfactuals")
            {
                counterfactuals = false;
                continue;
            }

            if (flag is "--skip-scripts")
            {
                scripts = false;
                continue;
            }

            if (flag is not ("--seed" or "--team" or "--out" or "--data-root" or "--seeds"))
            {
                stderr.WriteLine("Unknown argument: " + flag);
                return 1;
            }

            if (i + 1 >= args.Length)
            {
                stderr.WriteLine("Missing value for " + flag + ".");
                return 1;
            }

            var value = args[++i];
            switch (flag)
            {
                case "--seed":
                    if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out seed))
                    {
                        stderr.WriteLine("Seed must be an unsigned integer.");
                        return 1;
                    }

                    seedSet = true;
                    break;
                case "--team":
                    team = value;
                    break;
                case "--out":
                    outDir = value;
                    break;
                case "--data-root":
                    dataRoot = value;
                    break;
                case "--seeds":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 1)
                    {
                        stderr.WriteLine("Seeds must be a positive integer.");
                        return 1;
                    }

                    monkeySeeds = count;
                    monkey = true;
                    break;
            }
        }

        var root = dataRoot ?? RunCommand.FindDataRoot();
        if (root is null)
        {
            stderr.WriteLine("Could not find data/authored. Pass --data-root pointing at the data directory.");
            return 1;
        }
        if (team is null)
        {
            stderr.WriteLine("Missing --team <organizationId> (any id from the 1955 field).");
            return 1;
        }

        var authored = AuthoredDataLoader.Load(root);
        if (!WorldInitializer.PublicTeams(authored, 1955).Any(entry => string.Equals(entry.Id, team, StringComparison.Ordinal)))
        {
            stderr.WriteLine("Team '" + team + "' is not in the 1955 field.");
            return 1;
        }

        if (!seedSet && monkey)
        {
            seed = 1;
        }

        var directory = outDir ?? Path.Combine(root, "cache", "reports");
        var seeds = monkey ? monkeySeeds ?? Phase4Estimates.MonkeySeeds : 0;
        var pack = Phase4Runner.Run(root, team, seed, seeds, counterfactuals, scripts);
        var path = Phase4Report.Write(directory, pack);
        stdout.WriteLine(path);
        stdout.WriteLine(pack.Story.WorldHash);
        stdout.WriteLine(pack.Story.Reached + (pack.Story.ReachedUntil ? " target" : " stopped"));
        if (pack.Story.KnownIssue is { } known)
        {
            stdout.WriteLine("known " + known);
        }

        if (pack.Story.WorldHash != pack.Repeat.WorldHash)
        {
            stderr.WriteLine("Second run diverged (INV-002).");
            return 1;
        }

        return 0;
    }
}
