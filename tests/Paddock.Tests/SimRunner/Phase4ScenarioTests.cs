using Paddock.Application.Board;
using Paddock.Data.Authored;
using Paddock.Data.World;
using Paddock.Domain.Time;
using Paddock.SimRunner;
using Paddock.SimRunner.Scenario;
using static Paddock.Tests.Board.BoardKit;

namespace Paddock.Tests.SimRunner;

/// <summary>
/// Phase-4 gate (#113). The scripted-story hash is rewritten only with
/// <c>PADDOCK_UPDATE_FIXTURES=1 dotnet test --filter Phase4ScenarioTests.TheScriptedStoryHashIsStable</c>.
/// </summary>
public class Phase4ScenarioTests
{
    private static string DataRoot => Path.Combine(RepoPaths.Root(), "data");

    private static string HashPath => Path.Combine(RepoPaths.Root(), "tests", "Paddock.Tests", "SimRunner", "phase4-1955.story.hash");

    [Fact]
    public void HelpListsTheScenarioCommand()
    {
        var stdout = new StringWriter();
        SimRunnerCli.WriteHelp(stdout);
        Assert.Contains("scenario", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("phase4-1955", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AnyPublic1955TeamIsAcceptedAndFoundingIsNot()
    {
        var teams = WorldInitializer.PublicTeams(AuthoredDataLoader.Load(DataRoot), 1955);
        Assert.Contains(teams, team => team.Id == "ferrari");
        Assert.Contains(teams, team => team.Id == "gordini");
        Assert.DoesNotContain(teams, team => team.Id == "NewTeam");
        var stderr = new StringWriter();
        var code = ScenarioCommand.Execute(["scenario", "phase4-1955", "--team", "NewTeam", "--seed", "1"], new StringWriter(), stderr);
        Assert.Equal(1, code);
        Assert.Contains("not in the 1955 field", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void TheScriptedStoryHashIsStable()
    {
        var first = Phase4Bot.Play(DataRoot, Phase4Estimates.DefaultTeam, Phase4Estimates.StorySeed, Phase4Estimates.RobustUntil, Phase4Policy.Default);
        var second = Phase4Bot.Play(DataRoot, Phase4Estimates.DefaultTeam, Phase4Estimates.StorySeed, Phase4Estimates.RobustUntil, Phase4Policy.Default);
        Assert.Equal(first.WorldHash, second.WorldHash);
        Assert.Equal(64, first.WorldHash.Length);
        Assert.True(first.SoftLock is null || first.KnownIssue is not null, first.SoftLock + " / " + first.KnownIssue);
        if (Environment.GetEnvironmentVariable("PADDOCK_UPDATE_FIXTURES") == "1")
        {
            File.WriteAllText(HashPath, first.WorldHash + Environment.NewLine);
        }

        Assert.True(File.Exists(HashPath), "Missing story hash. Generate it with " + Phase4Estimates.UpdateHashCommand);
        var stored = File.ReadAllText(HashPath).Trim();
        Assert.Equal(stored, first.WorldHash);
    }

    [Fact]
    public void CounterfactualPairsNameEveryDivergedObservable()
    {
        var pairs = Phase4Runner.CounterfactualPairs(DataRoot, Phase4Estimates.DefaultTeam, Phase4Estimates.StorySeed);
        Assert.Equal(3, pairs.Count);
        Assert.All(pairs, pair =>
        {
            Assert.False(string.IsNullOrWhiteSpace(pair.Name));
            foreach (var name in pair.Diverged)
            {
                Assert.Contains(name, new[] { "worldHash", "date", "cash", "standings", "signings", "objectives", "inbox", "events" });
            }
        });
    }

    [Fact]
    public void ExplainabilityChecksHoldOnTheScriptedStory()
    {
        var play = Phase4Bot.Play(DataRoot, Phase4Estimates.DefaultTeam, Phase4Estimates.StorySeed, new GameDate(1955, 2, 1), Phase4Policy.StarDriver);
        var explain = Phase4Runner.Explain(play);
        Assert.Equal(0, explain.RefusedWithoutReason);
        Assert.Equal(0, explain.ObjectivesMissingSlice);
        Assert.Equal(0, explain.WhyHiddenLeaks);
    }

    [Fact]
    public void MonkeySeedsUseAnyTeamAndDoNotUnclassifiedLock()
    {
        Assert.Equal(50, Phase4Estimates.MonkeySeeds);
        var rows = Phase4Runner.Monkey(DataRoot, 11, Phase4Estimates.TestMonkeySeeds);
        Assert.Equal(Phase4Estimates.TestMonkeySeeds, rows.Count);
        Assert.NotEqual(rows[0].TeamId, rows[1].TeamId);
        Assert.All(rows, row =>
        {
            Assert.True(row.ReachedUntil || row.KnownIssue is not null, row.TeamId + " " + row.Reached + " " + row.SoftLock);
            Assert.True(row.SoftLock is null || row.KnownIssue is not null);
        });
    }

    [Fact]
    public void DismissalAndCollapseScriptedPathsAreReached()
    {
        var lab = DismissedUntilJune();
        Assert.Contains(lab.Events, item => item.TypeId == BoardEventTypes.ManagerDismissed);
        var collapse = Phase4Bot.Collapse(DataRoot, 3);
        Assert.True(collapse.Insolvent || collapse.KnownIssue is not null, "collapse: " + collapse.Reached + " " + collapse.SoftLock);
    }

    [Fact]
    public void TheCliWritesAReportWithoutComparingHistory()
    {
        var directory = Directory.CreateTempSubdirectory("paddock-gate-").FullName;
        var play = Phase4Bot.Play(DataRoot, "ferrari", 7, new GameDate(1955, 1, 2), Phase4Policy.Default);
        var pack = new Phase4Pack(
            play,
            play,
            [],
            new Phase4Explain(0, 0, 0, 0),
            [],
            play,
            play,
            ["Season wall-time warn is an ESTIMATE."]);
        var path = Phase4Report.Write(directory, pack);
        var report = File.ReadAllText(path);
        Assert.Contains("Checklist", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Fangio", report, StringComparison.Ordinal);
        Assert.DoesNotContain("real 1955", report, StringComparison.Ordinal);
        Assert.Contains("#251", report, StringComparison.Ordinal);
        Assert.Contains("#253", report, StringComparison.Ordinal);
        Assert.Contains("#254", report, StringComparison.Ordinal);
        Assert.Contains("ESTIMATE", report, StringComparison.Ordinal);
    }

    private static Lab DismissedUntilJune()
    {
        var lab = new Lab(42UL, free: 5);
        lab.Appoint(Pam, T3);
        lab.Facts.Position(T3, 5);
        lab.AdvanceTo(new GameDate(1956, 6, 8));
        return lab;
    }
}
