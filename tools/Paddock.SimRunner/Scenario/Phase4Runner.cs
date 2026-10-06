using System.Diagnostics;
using Paddock.Application.Access;
using Paddock.Application.Career;
using Paddock.Application.Contracts;
using Paddock.Application.Inbox;
using Paddock.Application.Objectives;
using Paddock.Application.Spy;
using Paddock.Data.Authored;
using Paddock.Data.World;
using Paddock.Domain.Contracts;
using Paddock.Domain.Objectives;
using Paddock.Domain.Spy;
using Paddock.Domain.Time;
using AccessManagerId = Paddock.Application.Access.ManagerId;

namespace Paddock.SimRunner.Scenario;

public sealed record Phase4Pair(string Name, IReadOnlyList<string> Diverged, Phase4Snapshot Left, Phase4Snapshot Right);

public sealed record Phase4Explain(
    int RefusedWithoutReason,
    int ObjectivesMissingSlice,
    int WhyHiddenLeaks,
    int WhyLines);

public sealed record Phase4MonkeySeed(ulong Seed, string TeamId, GameDate Reached, bool ReachedUntil, string WorldHash, string? SoftLock, string? KnownIssue);

public sealed record Phase4Pack(
    Phase4Play Story,
    Phase4Play Repeat,
    IReadOnlyList<Phase4Pair> Counterfactuals,
    Phase4Explain Explain,
    IReadOnlyList<Phase4MonkeySeed> Monkey,
    Phase4Play Dismissal,
    Phase4Play Collapse,
    IReadOnlyList<string> Estimates);

/// <summary>Builds the phase-4 evidence pack: one story, a second run, counterfactuals, monkey, scripts.</summary>
public static class Phase4Runner
{
    public static Phase4Pack Run(
        string dataRoot,
        string teamId,
        ulong seed,
        int monkeySeeds,
        bool counterfactuals,
        bool scripts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        var story = Phase4Bot.Play(dataRoot, teamId, seed, Phase4Estimates.RobustUntil, Phase4Policy.Default);
        var repeat = Phase4Bot.Play(dataRoot, teamId, seed, Phase4Estimates.RobustUntil, Phase4Policy.Default);
        var pairs = counterfactuals ? CounterfactualPairs(dataRoot, teamId, seed) : [];
        var monkey = Monkey(dataRoot, seed, monkeySeeds);
        var dismissal = scripts
            ? Phase4Bot.Play(dataRoot, "gordini", seed, Phase4Estimates.RobustUntil, Phase4Policy.Ambitious)
            : story;
        var collapse = scripts ? Phase4Bot.Collapse(dataRoot, seed) : story;
        return new Phase4Pack(
            story,
            repeat,
            pairs,
            Explain(story),
            monkey,
            dismissal,
            collapse,
            [
                "InfrastructureEstimates, facilities.json milli-units, and every Phase4Estimates constant are ESTIMATES.",
                "AI factory upgrades stay below 0.40 relative quality (#231).",
                "Season wall-time warn is " + Phase4Estimates.SeasonWallWarnSeconds + "s (ESTIMATE).",
                "Save-size warn is 50 MB (TECH §8 design target).",
                "Monkey sample is " + Phase4Estimates.MonkeySeeds + " seeds (ESTIMATE of robustness coverage).",
            ]);
    }

    public static IReadOnlyList<Phase4MonkeySeed> Monkey(string dataRoot, ulong baseSeed, int count)
    {
        var data = AuthoredDataLoader.Load(dataRoot);
        var teams = WorldInitializer.PublicTeams(data, 1955);
        if (teams.Count == 0 || count <= 0)
        {
            return [];
        }

        var rows = new List<Phase4MonkeySeed>(count);
        for (var i = 0; i < count; i++)
        {
            var seed = baseSeed + (ulong)i;
            var team = teams[i % teams.Count].Id;
            var play = Phase4Bot.Play(dataRoot, team, seed, Phase4Estimates.RobustUntil, Phase4Policy.Default);
            rows.Add(new Phase4MonkeySeed(seed, team, play.Reached, play.ReachedUntil, play.WorldHash, play.SoftLock, play.KnownIssue));
        }

        return rows;
    }

    public static IReadOnlyList<Phase4Pair> CounterfactualPairs(string dataRoot, string teamId, ulong seed)
    {
        var until = Phase4Estimates.CounterfactualUntil;
        Phase4Pair Pair(string name, Phase4Policy left, Phase4Policy right)
        {
            var a = Phase4Bot.Play(dataRoot, teamId, seed, until, left);
            var b = Phase4Bot.Play(dataRoot, teamId, seed, until, right);
            return new Phase4Pair(name, Phase4Observables.Diverged(Phase4Observables.Capture(a.Shell), Phase4Observables.Capture(b.Shell)), Phase4Observables.Capture(a.Shell), Phase4Observables.Capture(b.Shell));
        }

        return
        [
            Pair("star driver vs another", Phase4Policy.StarDriver, Phase4Policy.OtherDriver),
            Pair("next year's car vs current", Phase4Policy.NextYearCar, Phase4Policy.CurrentCar),
            Pair("accept sponsor vs decline", Phase4Policy.AcceptSponsor, Phase4Policy.DeclineSponsor),
        ];
    }

    public static Phase4Explain Explain(Phase4Play play)
    {
        var shell = play.Shell;
        var access = AccessContext.ForManager(new AccessManagerId(shell.Player.Value));
        var book = shell.Modules.Require<ContractBook>();
        var refused = 0;
        foreach (var talk in new NegotiationQuery(book).View(access).Items)
        {
            if (talk.Status == NegotiationStatus.Refused && talk.Reasons.Count == 0)
            {
                refused++;
            }
        }

        var missing = 0;
        if (shell.TeamOf(shell.Player) is { } team)
        {
            var objectives = new ObjectiveQuery(shell.Modules.Require<IObjectiveFacts>(), shell.Modules.Require<IManagerOrganizations>())
                .View(access, shell.Modules.Require<Paddock.Application.Board.BoardBook>().Objectives, shell.Date);
            foreach (var item in objectives.Items)
            {
                if (item.Why.Reason.Key.Length == 0 || (item.State.Status == ObjectiveStatus.Open && item.Forecast is null))
                {
                    missing++;
                }
            }
        }

        var sink = book.Environment.Trace;
        var ownTalks = new NegotiationQuery(book).View(access).Items.Select(item => item.Id).ToArray();
        var lines = WhyQuery.Visible(sink, access, shell.TeamOf(shell.Player), ownTalks, null);
        var leaks = 0;
        if (sink is MemorySink memory)
        {
            foreach (var trace in memory.KeyDecisions.Concat(memory.Traces))
            {
                var view = WhyView.For(access, trace);
                if (view is null)
                {
                    continue;
                }

                if (view.TruthContext.Count > 0)
                {
                    leaks++;
                }
            }
        }

        return new Phase4Explain(refused, missing, leaks, lines.Count);
    }
}
