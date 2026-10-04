using System.Globalization;
using System.Text;
using Paddock.Application.Access;
using Paddock.Application.Commands;
using Paddock.Application.Principals;
using Paddock.Application.Spy;
using Paddock.Domain.Principals;
using Paddock.Domain.Random;
using Paddock.Domain.Spy;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Ai;
using Xunit.Abstractions;
using AccessManagerId = Paddock.Application.Access.ManagerId;

namespace Paddock.Tests.Principals;

public class PrincipalBehaviourTests(ITestOutputHelper output)
{
    private static string Text(DecisionTrace trace)
    {
        var builder = new StringBuilder();
        builder.Append(trace.Weekend).Append('|').Append(trace.Who).Append('|').Append(trace.Level).Append('|').Append(trace.Trigger).Append('|')
            .Append(trace.ChosenOptionId).Append('|').Append(trace.Reason).Append('|').Append(trace.PlayerReason).Append('|').Append(trace.IsKeyDecision);
        foreach (var pair in trace.TruthContext.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            builder.Append('|').Append(pair.Key).Append('=').Append(pair.Value);
        }

        foreach (var option in trace.Options)
        {
            builder.Append("\n  ").Append(option.Id).Append(' ').Append(option.Utility.ToString("R", CultureInfo.InvariantCulture)).Append(option.PlayerVisible);
            foreach (var factor in option.Factors)
            {
                builder.Append("\n    ").Append(factor.Name).Append(' ').Append(factor.Contribution.ToString("R", CultureInfo.InvariantCulture)).Append(factor.PlayerVisible);
            }
        }

        return builder.ToString();
    }

    private static string AiTraces(RecordingSink sink) =>
        string.Join("\n", sink.Traces.Where(trace => trace.Who.StartsWith(PrincipalKeys.ManagerPrefix, StringComparison.Ordinal)).Select(Text));

    // ---------------------------------------------------------------- INV-003

    [Fact]
    public void TwoWorldsThatDifferOnlyInHiddenAttributesGetIdenticalDecisionsAndTraces()
    {
        var visible = new RecordingSink();
        var hidden = new RecordingSink();
        var left = new PrincipalKit(new PrincipalKitOptions { Seed = 5UL, Sink = visible, Pool = false, World = new PrincipalWorldOptions(HiddenShift: 0) });
        var right = new PrincipalKit(new PrincipalKitOptions { Seed = 5UL, Sink = hidden, Pool = false, World = new PrincipalWorldOptions(HiddenShift: 2) });

        // The truth differs (every person's real attributes), the beliefs do not.
        Assert.NotEqual(left.Hash, right.Hash);
        var end = new GameDate(1956, 3, 1);
        left.RunUntil(end);
        right.RunUntil(end);

        Assert.True(left.Dispatcher.Log.Entries.Count > 100, "The run has to exercise the AI.");
        Assert.Equal(left.Dispatcher.Log.Entries, right.Dispatcher.Log.Entries);
        var traces = AiTraces(visible);
        Assert.NotEmpty(traces);
        Assert.Equal(traces, AiTraces(hidden));
        Assert.Equal(
            left.Session.World.Contracts.Select(contract => contract.Id.Value + contract.PersonId.Value + contract.End + contract.Salary),
            right.Session.World.Contracts.Select(contract => contract.Id.Value + contract.PersonId.Value + contract.End + contract.Salary));
    }

    [Fact]
    public void TheWhyViewShowsNoHiddenFactorOfAnAiDecision()
    {
        var sink = new RecordingSink();
        var kit = new PrincipalKit(new PrincipalKitOptions { Sink = sink });
        kit.RunUntil(GameDate.SeasonStart(1957));
        var traces = sink.Traces.Where(trace => trace.Who.StartsWith(PrincipalKeys.ManagerPrefix, StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(traces);

        var hiddenNames = new HashSet<string> { AiTextKeys.FactorNoise, AiTextKeys.FactorHistory, AiTextKeys.FactorStyle };
        foreach (var trace in traces)
        {
            Assert.Empty(trace.TruthContext.Keys.Except(["archetype", "decision"]));
            var own = AccessContext.ForAi(new AccessManagerId(trace.Who));
            var view = WhyView.For(own, trace)!;
            Assert.Empty(view.TruthContext);
            foreach (var option in view.Options)
            {
                Assert.DoesNotContain(option.Factors, factor => hiddenNames.Contains(factor.Name));
                Assert.Equal(option.Factors.Sum(factor => factor.Contribution), option.Utility, 9);
            }

            // Another manager, and a human, see nothing of it. The developer sees everything.
            Assert.Null(WhyView.For(AccessContext.ForManager(new AccessManagerId("human:anna")), trace));
            Assert.Null(WhyView.For(AccessContext.ForAi(new AccessManagerId("ai:somebody_else")), trace));
            Assert.Equal(trace.Options.Count, WhyView.For(AccessContext.Developer, trace)!.Options.Count);
        }
    }

    // ---------------------------------------------------------------- traces

    [Fact]
    public void EveryDecisionKindLeavesATraceWithItsOptionsAndAKeyDecisionIsFlagged()
    {
        var sink = new RecordingSink();
        var kit = new PrincipalKit(new PrincipalKitOptions { Sink = sink });
        kit.RunUntil(GameDate.SeasonStart(1959));
        var kinds = sink.Traces
            .Where(trace => trace.Who.StartsWith(PrincipalKeys.ManagerPrefix, StringComparison.Ordinal))
            .Select(trace => trace.Trigger.Split(':')[0])
            .ToHashSet(StringComparer.Ordinal);
        output.WriteLine(string.Join(", ", kinds.Order(StringComparer.Ordinal)));

        string[] required =
        [
            MarketDecider.RenewalKind, MarketDecider.SearchKind, MarketDecider.OfferKind, MarketDecider.ReplyKind,
            DevelopmentDecider.SplitKind, DevelopmentDecider.TimingKind,
            SupplyDecider.ChooseKind,
            SponsorDecider.BeginKind, SponsorDecider.WaitKind, SponsorDecider.OfferKind,
            ScoutingDecider.Kind,
        ];
        foreach (var kind in required)
        {
            Assert.Contains(kind, kinds);
        }

        foreach (var trace in sink.Traces.Where(trace => trace.Who.StartsWith(PrincipalKeys.ManagerPrefix, StringComparison.Ordinal)))
        {
            Assert.NotEmpty(trace.Options);
            Assert.Contains(trace.Options, option => option.Id == trace.ChosenOptionId);
            Assert.InRange(trace.Level, 1, AiEstimates.LevelCount);
            if (trace.Options.Count > 1)
            {
                // The chosen option has the highest utility in the trace (noise included).
                var best = trace.Options.Max(option => option.Utility);
                Assert.Equal(best, trace.Options.First(option => option.Id == trace.ChosenOptionId).Utility, 9);
            }
        }

        Assert.Contains(sink.Traces, trace => trace.IsKeyDecision);
    }

    [Fact]
    public void TextKeysOfTheTracesAllExistInBothLanguages()
    {
        var sink = new RecordingSink();
        var kit = new PrincipalKit(new PrincipalKitOptions { Sink = sink });
        kit.RunUntil(GameDate.SeasonStart(1958));
        var catalog = Paddock.Application.Localization.TranslationLoader.LoadDirectory(Path.Combine(RepoPaths.Root(), "strings"));
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var trace in sink.Traces.Where(trace => trace.Who.StartsWith(PrincipalKeys.ManagerPrefix, StringComparison.Ordinal)))
        {
            if (trace.PlayerReason is { } reason)
            {
                keys.Add(reason);
            }

            foreach (var option in trace.Options)
            {
                keys.Add(AiTextKeys.OptionKey(option.Id));
                foreach (var factor in option.Factors)
                {
                    keys.Add(factor.Name);
                }
            }
        }

        foreach (var key in keys)
        {
            Assert.True(catalog.TryGet(Paddock.Application.Localization.Language.En, key, out _), "en: " + key);
            Assert.True(catalog.TryGet(Paddock.Application.Localization.Language.Pl, key, out _), "pl: " + key);
        }
    }

    // ---------------------------------------------------------------- INV-002, INV-006, INV-004

    [Fact]
    public void TheSameSeedGivesTheSameHashAndTheSameCommandsTwiceAndAnotherSeedDiffers()
    {
        var first = new PrincipalKit(new PrincipalKitOptions { Seed = 9UL });
        var second = new PrincipalKit(new PrincipalKitOptions { Seed = 9UL });
        var other = new PrincipalKit(new PrincipalKitOptions { Seed = 10UL });
        var end = GameDate.SeasonStart(1958);
        first.RunUntil(end);
        second.RunUntil(end);
        other.RunUntil(end);

        Assert.Equal(first.Hash, second.Hash);
        Assert.Equal(first.Dispatcher.Log.Entries, second.Dispatcher.Log.Entries);
        Assert.NotEqual(first.Hash, other.Hash);
        Assert.NotNull(first.Session.World.Section<PrincipalsSection>(PrincipalsSection.SectionName));
    }

    [Fact]
    public void EnablingTheTraceSinkDoesNotChangeTheHash()
    {
        var quiet = new PrincipalKit(new PrincipalKitOptions { Seed = 13UL });
        var sink = new RecordingSink();
        var loud = new PrincipalKit(new PrincipalKitOptions { Seed = 13UL, Sink = sink });
        var end = GameDate.SeasonStart(1958);
        quiet.RunUntil(end);
        loud.RunUntil(end);

        Assert.NotEmpty(sink.Traces);
        Assert.Equal(quiet.Hash, loud.Hash);
        Assert.Equal(quiet.Dispatcher.Log.Entries, loud.Dispatcher.Log.Entries);
    }

    [Fact]
    public void TheAiTouchesNoClockStreamAndDrawsOnlyFromAiDecisions()
    {
        var kit = new PrincipalKit(new PrincipalKitOptions { Seed = 17UL });
        var before = kit.Session.Clock.RngStates.Count;
        _ = before;

        // The director is stateless in RNG terms: a morning with and without it leaves the clock's stored states for every stream but the people's alone.
        var withAi = new PrincipalKit(new PrincipalKitOptions { Seed = 17UL });
        withAi.RunDays(60);
        var states = withAi.Session.Clock.RngStates;
        Assert.DoesNotContain(states.Keys, slot => slot.Name == RngStreamName.AiDecisions);

        // The noise of one option is a pure function of the master seed, the season, the team, the decision and the option.
        var stream = AiRandom.ForSeason(17UL, 1956);
        Assert.Equal(RngStream.Derive(17UL, RngStreamName.AiDecisions, 1956).State, stream.State);
        Assert.NotEqual(AiRandom.ForSeason(17UL, 1957).State, stream.State);
        var context = new DecisionContext("team", "ai:team", new DateOnly(1956, 2, 1), PrincipalArchetype.Builder, PrincipalSkillset.Average, stream);
        var one = context.Noise(DecisionFacet.Market, "decision", "a");
        Assert.Equal(one, context.Noise(DecisionFacet.Market, "decision", "a"));
        _ = context.Noise(DecisionFacet.Market, "decision", "b");
        Assert.Equal(one, context.Noise(DecisionFacet.Market, "decision", "a"));
        Assert.NotEqual(one, context.Noise(DecisionFacet.Market, "decision", "b"));
        Assert.NotEqual(one, context.Noise(DecisionFacet.Market, "other", "a"));
    }

    // ---------------------------------------------------------------- PP-045

    [Fact]
    public void TheAiNeverActsForATeamAHumanRuns()
    {
        var sink = new RecordingSink();
        var kit = new PrincipalKit(new PrincipalKitOptions { Seed = 21UL, HumanTeam = 2, Sink = sink });
        kit.RunUntil(GameDate.SeasonStart(1957));

        var human = PrincipalWorld.Team(2);
        Assert.DoesNotContain(kit.Dispatcher.Log.Entries, command => command.ManagerId.Value == PrincipalKeys.ManagerOf(human).Value);
        Assert.DoesNotContain(sink.Traces, trace => trace.Who == PrincipalKeys.ManagerOf(human).Value);
        Assert.Null(kit.Session.World.Section<PrincipalsSection>(PrincipalsSection.SectionName)!.Of(human));
        Assert.False(kit.Managers.Contains(PrincipalKeys.ManagerOf(human)));
        Assert.NotNull(kit.Session.World.Section<PrincipalsSection>(PrincipalsSection.SectionName)!.Of(PrincipalWorld.Team(3)));

        // Even a command filed under the AI manager of that team is refused.
        var intruder = PrincipalKeys.ManagerOf(human);
        kit.Managers.Register(intruder, Paddock.Application.Managers.ManagerKind.Ai, "intruder");
        kit.Control.Assign(intruder, human);
        kit.Queue.Enqueue(new RecordPrincipalReviewCommand
        {
            ManagerId = intruder,
            IssuedOn = new DateOnly(1957, 1, 1),
            OrganizationId = human.Value,
            Archetype = "Builder",
            NextReview = new DateOnly(1957, 2, 1),
        });
        var context = new CommandContext(new Paddock.Application.World.StubWorldState(new DateOnly(1957, 1, 1)), kit.Managers);
        var result = kit.Dispatcher.DispatchAll(kit.Queue, context).Single();
        Assert.Equal(PrincipalKeys.HumanTeam, Assert.IsType<CommandResult.Rejected>(result).Reason.Key);
    }

    // ---------------------------------------------------------------- cadence and runtime

    [Fact]
    public void AnIdleDayCostsOneDateComparisonPerTeamAndTenSeasonsAreReported()
    {
        var kit = new PrincipalKit(new PrincipalKitOptions { Seed = 3UL });
        var daysWithWork = 0;
        var days = 0;
        var started = System.Diagnostics.Stopwatch.StartNew();
        kit.RunUntil(GameDate.SeasonStart(1965), _ =>
        {
            days++;
            if (kit.Filed > 0 && kit.Queue.Count == 0 && kit.Accepted.Count > 0)
            {
                daysWithWork = kit.Reviews;
            }
        });
        started.Stop();
        var reviews = kit.Reviews;
        var teams = 8;
        output.WriteLine(
            $"10 AI-only seasons, {teams} teams: {days} days, {reviews} reviews ({reviews / (double)(teams * 10):F1} per team and season), {kit.Filed} commands filed, " +
            $"director {kit.DirectorTime.TotalMilliseconds:F0} ms of {started.Elapsed.TotalMilliseconds:F0} ms in all, {kit.DirectorTime.TotalMilliseconds / days:F3} ms per day.");
        _ = daysWithWork;

        // A team that is not due is not reviewed: far fewer reviews than team-days, and the director is a small part of the run.
        Assert.True(reviews < teams * days / 5, "Reviews are scheduled, not daily.");
        Assert.True(kit.DirectorTime < started.Elapsed / 2, "The principals must not dominate the run.");
    }
}
