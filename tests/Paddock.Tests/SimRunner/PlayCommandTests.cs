using Paddock.SimRunner;
using Paddock.Career;
using Paddock.Application.Board;
using Paddock.Application.Career;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Development;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Data.Authored;
using Paddock.Data.World;
using Paddock.Domain.Board;
using Paddock.Domain.Career;
using Paddock.Domain.Inbox;
using Paddock.Domain.Objectives;
using Paddock.Domain.People;
using Paddock.Domain.Racing;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Career;

namespace Paddock.Tests.SimRunner;

/// <summary>
/// Part A of issue #112: the wizard (existing team only), the shell, and save/load.
/// Path B and race weekends are not covered.
/// </summary>
public class PlayCommandTests
{
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-play-").FullName;

    [Fact]
    public void ScriptedPathASessionSavesAndReloadsTheSameHashTwice()
    {
        var save = Path.Combine(_directory, "career.paddock");
        var again = Path.Combine(_directory, "career-again.paddock");
        var first = Run(Script(save), "en");
        var second = Run(Script(again), "en");
        Assert.Equal(0, first.Code);
        Assert.Equal(0, second.Code);
        Assert.Equal(string.Empty, first.Error);
        Assert.Equal(string.Empty, second.Error);
        var normalized = first.Output.Replace(save, "SAVE", StringComparison.Ordinal).Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Equal(normalized, second.Output.Replace(again, "SAVE", StringComparison.Ordinal).Replace("\r\n", "\n", StringComparison.Ordinal));
        var goldenPath = Path.Combine(RepoPaths.Root(), "tests", "Paddock.Tests", "SimRunner", "play-path-a.en.txt");
        // PADDOCK_UPDATE_FIXTURES=1 rewrites the transcript, as for the track spline fixture (TECH 6.5). Review the diff.
        if (Environment.GetEnvironmentVariable("PADDOCK_UPDATE_FIXTURES") == "1")
        {
            File.WriteAllText(goldenPath, normalized);
        }

        var golden = File.ReadAllText(goldenPath).Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Equal(golden, normalized);

        var loaded = Run("hash\nquit\n", "en", ["play", "--load", save, "--lang", "en", "--seed", "7"]);
        Assert.Equal(0, loaded.Code);
        var hash = HashLine(first.Output);
        Assert.Contains(hash, loaded.Output, StringComparison.Ordinal);
        Assert.Contains("Career started on 1955-01-01", first.Output, StringComparison.Ordinal);
        Assert.Contains("Talks opened:", first.Output, StringComparison.Ordinal);
        Assert.Contains("Development split: current car 75, next concept 25.", first.Output, StringComparison.Ordinal);
        Assert.Contains("Founding or buying your own team comes after the MVP.", first.Output, StringComparison.Ordinal);
        Assert.Contains("Principal attributes are an uncalibrated estimate", first.Output, StringComparison.Ordinal);

        var polish = Run(Script(Path.Combine(_directory, "career-pl.paddock")), "pl");
        Assert.Equal(0, polish.Code);
        Assert.Contains("Kariera zaczęła się", polish.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("play.wizard.started", polish.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAiPrincipalDirectorLeavesThePlayersTeamAlone()
    {
        // Regression: the wizard registers the player after the host is attached, so the T44 director ran the player's
        // team as an AI team (it opened talks and set the scouting focus for Ferrari).
        var shell = OpenFerrari();
        TakeOver(shell, "Ada", "Lovelace", "negotiation");
        AcceptExpectedSeasonTarget(shell);
        for (var day = 0; day < 30; day++)
        {
            shell.BeginDay();
            var pool = shell.Session.World.Section<Paddock.Domain.Pool.TalentPoolSection>(Paddock.Domain.Pool.TalentPoolSection.SectionName);
            Assert.Null(pool?.FocusOf(OrganizationId.Real("ferrari")));
            var contracts = shell.Modules.Require<Paddock.Application.Contracts.ContractBook>().Section;
            Assert.DoesNotContain(contracts.Negotiations, negotiation => negotiation.Proposer.Value == "ferrari");
            shell.Ready(shell.Player);
            if (shell.Advance() is AdvanceResult.Refused)
            {
                break;
            }
        }
    }

    [Fact]
    public void TwoHumansAdvanceOnlyWhenBothAreReady()
    {
        var shell = OpenFerrari();
        TakeOver(shell, "Ada", "Lovelace", "negotiation");
        AcceptExpectedSeasonTarget(shell);
        shell.BeginDay();
        var other = shell.RegisterHuman("Bram");
        shell.Ready(shell.Player);
        var held = shell.Advance();
        var refused = Assert.IsType<AdvanceResult.Refused>(held);
        Assert.Equal(AdvanceRefusalKind.HumansNotReady, refused.Refusal.Kind);
        Assert.Equal(shell.Date, new GameDate(1955, 1, 1));

        shell.Ready(shell.Player);
        shell.Ready(other);
        var moved = shell.Advance();
        Assert.IsType<AdvanceResult.Advanced>(moved);
        Assert.Equal(new GameDate(1955, 1, 2), shell.Date);
    }

    [Fact]
    public void TakeOverInstallsAPrincipalWithTheChosenTiltAndRefusesFounding()
    {
        var shell = OpenFerrari();
        var before = shell.Session.World.Organizations.Count;
        var founded = shell.Submit(new TakeOverTeamCommand
        {
            ManagerId = shell.Player,
            IssuedOn = new DateOnly(1955, 1, 1),
            OrganizationId = CareerConfig.NewTeam,
            GivenName = "Ada",
            FamilyName = "Lovelace",
            Nationality = "GBR",
            Tilt = "negotiation",
        });
        var rejection = Assert.IsType<CommandResult.Rejected>(founded);
        Assert.Equal(BoardKeys.TakeOverOwnTeam, rejection.Reason.Key);
        Assert.Equal(before, shell.Session.World.Organizations.Count);

        TakeOver(shell, "Ada", "Lovelace", "negotiation");
        var team = shell.TeamOf(shell.Player);
        Assert.Equal("ferrari", team?.Value);
        var person = shell.Session.World.Persons.Single(candidate => candidate.FamilyName == "Lovelace");
        Assert.Equal(PlayerEstimates.Average + PlayerEstimates.TiltBonus, person.Truth.Value("negotiation"));
        Assert.Equal(PlayerEstimates.Average, person.Truth.Value("politics"));
        Assert.Equal(person.Truth.Value("negotiation"), person.Truth.PotentialValue("negotiation"));
        var belief = shell.Session.World.KnowledgeOf(team!.Value, person.Id);
        Assert.NotNull(belief);
        var negotiation = Assert.Single(belief.Value.Attributes, attribute => attribute.Key == "negotiation");
        Assert.Equal(14, negotiation.Band.Low);
        Assert.Equal(14, negotiation.Band.High);
        Assert.Null(shell.Session.World.KnowledgeOf(shell.Session.World.Organizations.First(org => org.Id.Value != "ferrari" && org.Kind == OrganizationKind.Team).Id, person.Id));

        var split = shell.Submit(new SetDevelopmentSplitCommand
        {
            ManagerId = shell.Player,
            IssuedOn = new DateOnly(1955, 1, 1),
            OrganizationId = "ferrari",
            CurrentPercent = 50,
            AccountPercent = 25,
            NextYearPercent = 25,
        });
        Assert.IsType<CommandResult.Accepted>(split);
        var plan = shell.Development.View(Paddock.Application.Access.AccessContext.ForManager(new Paddock.Application.Access.ManagerId(shell.Player.Value))).Own.Single();
        Assert.Equal(25, plan.Next.SharePercent);
    }

    [Fact]
    public void TakeOverOnNewYearOffersTheSeasonTargetAndAnsweringGrantsOnlyThatOne()
    {
        var shell = OpenFerrari();
        TakeOver(shell, "Ada", "Lovelace", "negotiation");
        shell.BeginDay();

        var team = shell.TeamOf(shell.Player)!.Value;
        var board = shell.Modules.Require<BoardBook>();
        Assert.Equal(BoardEstimates.InitialConfidenceTenths, board.Section.Board(team)!.ConfidenceTenths);
        Assert.DoesNotContain(
            board.Objectives.OwnedBy(team),
            objective => objective.IsOpen && objective.KindKey == BoardKeys.ObjectiveSeason);
        var decision = Assert.Single(OpenSeasonTargets(shell));
        Assert.Equal(BoardKeys.SeasonTargetSubject, decision.SubjectKey);
        Assert.Equal(SeasonTarget.Expected, decision.DefaultOptionId);

        var answered = shell.Submit(new ResolveInboxItemCommand
        {
            ManagerId = shell.Player,
            IssuedOn = new DateOnly(shell.Date.Year, shell.Date.Month, shell.Date.Day),
            ItemId = decision.Id,
            OptionId = SeasonTarget.Ambitious,
        });
        Assert.IsType<CommandResult.Accepted>(answered);

        var season = Assert.Single(
            board.Objectives.OwnedBy(team),
            objective => objective.IsOpen && objective.KindKey == BoardKeys.ObjectiveSeason);
        Assert.Equal(SeasonTarget.Ambitious, season.EffectOnMet.Arguments["ambition"]);
        Assert.DoesNotContain(board.Objectives.OwnedBy(team), objective => objective.Status == ObjectiveStatus.Withdrawn);
        Assert.Empty(OpenSeasonTargets(shell));
        Assert.Equal(BoardEstimates.InitialConfidenceTenths, board.Section.Board(team)!.ConfidenceTenths);
    }

    [Fact]
    public void ACareerWithAWithdrawnObjectiveSavesAndLoads()
    {
        // Regression (review of #223): the objectives table only allowed Open, Met and Failed, so saving after a take-over
        // that withdrew the AI's season objective failed on the CHECK constraint.
        var shell = OpenFerrari();
        LiveOneDay(shell);
        TakeOverOn(shell);
        var config = CareerConfig.FromPreset(CareerPreset.Chaos).WithStartYear(1955).WithPlayerTeam("ferrari");
        var path = Path.Combine(_directory, "withdrawn.paddock");

        CareerSaveWriter.Write(path, shell.Session, config, "ferrari", "test-data-hash", "withdrawn", shell.HostState);

        var loaded = CareerSaveReader.Read(path);
        Assert.Contains(
            loaded.Session.World.Section<Paddock.Domain.Objectives.ObjectivesSection>(Paddock.Domain.Objectives.ObjectivesSection.SectionName)!.Objectives,
            objective => objective.Status == ObjectiveStatus.Withdrawn);
    }

    [Fact]
    public void TakeOverBeforeTheFirstRaceWithdrawsTheAiObjectiveWithoutEffects()
    {
        var shell = OpenFerrari();
        LiveOneDay(shell);
        var team = OrganizationId.Real("ferrari");
        var board = shell.Modules.Require<BoardBook>();
        var granted = Assert.Single(
            board.Objectives.OwnedBy(team),
            objective => objective.IsOpen && objective.KindKey == BoardKeys.ObjectiveSeason);

        TakeOverOn(shell);

        Assert.Equal(ObjectiveStatus.Withdrawn, board.Objectives.Find(granted.Id)!.Status);
        Assert.Equal(BoardEstimates.InitialConfidenceTenths, board.Section.Board(team)!.ConfidenceTenths);
        Assert.DoesNotContain(
            board.Objectives.OwnedBy(team),
            objective => objective.IsOpen && objective.KindKey == BoardKeys.ObjectiveSeason);
        var decision = Assert.Single(OpenSeasonTargets(shell));
        Assert.Equal(BoardKeys.SeasonTargetSubject, decision.SubjectKey);
    }

    [Fact]
    public void TakeOverAfterTheFirstRaceKeepsTheExistingObjective()
    {
        var shell = OpenFerrari();
        LiveOneDay(shell);
        var team = OrganizationId.Real("ferrari");
        var contracts = shell.Modules.Require<ContractBook>();
        contracts.UseWorld(contracts.World.WithSection(ChampionshipSection.Create(
            shell.Date.Year,
            7,
            1,
            false,
            [new ChampionshipLedger("driver", [0m], [1])],
            [new ChampionshipLedger(team.Value, [0m], [1])])));
        var board = shell.Modules.Require<BoardBook>();
        var granted = Assert.Single(
            board.Objectives.OwnedBy(team),
            objective => objective.IsOpen && objective.KindKey == BoardKeys.ObjectiveSeason);

        TakeOverOn(shell);

        var kept = Assert.Single(
            board.Objectives.OwnedBy(team),
            objective => objective.KindKey == BoardKeys.ObjectiveSeason);
        Assert.Equal(granted.Id, kept.Id);
        Assert.Equal(ObjectiveStatus.Open, kept.Status);
        Assert.Empty(OpenSeasonTargets(shell));
    }

    private static void TakeOver(CareerShell shell, string given, string family, string tilt)
    {
        var result = shell.Submit(new TakeOverTeamCommand
        {
            ManagerId = shell.Player,
            IssuedOn = new DateOnly(1955, 1, 1),
            OrganizationId = "ferrari",
            GivenName = given,
            FamilyName = family,
            Nationality = "GBR",
            Tilt = tilt,
        });
        Assert.IsType<CommandResult.Accepted>(result);
    }

    private static void TakeOverOn(CareerShell shell)
    {
        var today = new DateOnly(shell.Date.Year, shell.Date.Month, shell.Date.Day);
        var result = shell.Submit(new TakeOverTeamCommand
        {
            ManagerId = shell.Player,
            IssuedOn = today,
            OrganizationId = "ferrari",
            GivenName = "Ada",
            FamilyName = "Lovelace",
            Nationality = "GBR",
            Tilt = "negotiation",
        });
        Assert.IsType<CommandResult.Accepted>(result);
    }

    private static void LiveOneDay(CareerShell shell)
    {
        shell.BeginDay();
        shell.Ready(shell.Player);
        Assert.IsType<AdvanceResult.Advanced>(shell.Advance());
    }

    private static void AcceptExpectedSeasonTarget(CareerShell shell)
    {
        var decision = Assert.Single(OpenSeasonTargets(shell));
        var answered = shell.Submit(new ResolveInboxItemCommand
        {
            ManagerId = shell.Player,
            IssuedOn = new DateOnly(shell.Date.Year, shell.Date.Month, shell.Date.Day),
            ItemId = decision.Id,
            OptionId = SeasonTarget.Expected,
        });
        Assert.IsType<CommandResult.Accepted>(answered);
    }

    private static IReadOnlyList<InboxItem> OpenSeasonTargets(CareerShell shell) =>
        shell.Modules.Require<InboxBook>().Section.ItemsOf(shell.Player.Value)
            .Where(item => item.Kind == BoardEngine.SeasonTargetKind && item.IsOpen)
            .ToArray();

    private static CareerShell OpenFerrari()
    {
        var config = CareerConfig.FromPreset(CareerPreset.Chaos).WithStartYear(1955).WithPlayerTeam("ferrari");
        var data = AuthoredDataLoader.Load(Path.Combine(RepoPaths.Root(), "data"));
        var created = WorldInitializer.Create(config, data, EmptyPeopleProvider.Instance, 7UL);
        var arrivals = TalentIntakeSchedule.AfterStart(config, EmptyPeopleProvider.Instance, created.World, 7UL);
        var session = new CareerSession(created.World, 7UL, created.TalentPool, arrivals);
        return CareerShell.Open(session, new CareerRunOptions { Inputs = Paddock.Career.CareerInputsLoader.Load(Path.Combine(RepoPaths.Root(), "data"), data) }, "Ada Lovelace");
    }

    private static string Script(string save) =>
        "preset Chaos\n"
        + "axis history 0\n"
        + "year 1955\n"
        + "fatality Off\n"
        + "tilt negotiation\n"
        + "team own\n"
        + "team NewTeam\n"
        + "team found\n"
        + "teams\n"
        + "team ferrari\n"
        + "player Ada Lovelace GBR\n"
        + "start\n"
        + "state\n"
        + "market driver\n"
        + "negotiate open first driver\n"
        + "negotiate list\n"
        + "dev split 50 25 25\n"
        + "car\n"
        + "sponsors\n"
        + "finance\n"
        + "board\n"
        + "pool\n"
        + "scout all\n"
        + "inbox\n"
        + "why\n"
        + "ready\n"
        + "advance\n"
        + "hash\n"
        + "save " + save + "\n"
        + "quit\n";

    private (int Code, string Output, string Error) Run(string script, string language, string[]? args = null)
    {
        args ??= ["play", "--lang", language, "--seed", "7"];
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = PlayCommand.Execute(args, new StringReader(script), stdout, stderr);
        return (code, stdout.ToString(), stderr.ToString());
    }

    private static string HashLine(string output)
    {
        foreach (var line in output.Split('\n'))
        {
            if (line.StartsWith("World hash ", StringComparison.Ordinal))
            {
                return line.Trim();
            }
        }

        throw new InvalidOperationException("No hash line.");
    }
}
