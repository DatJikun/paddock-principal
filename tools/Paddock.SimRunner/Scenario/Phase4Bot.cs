using Paddock.Application.Access;
using Paddock.Application.Board;
using Paddock.Application.Career;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Development;
using Paddock.Application.Finance;
using Paddock.Application.Inbox;
using Paddock.Application.Infrastructure;
using Paddock.Application.Managers;
using Paddock.Application.Objectives;
using Paddock.Application.Racing;
using Paddock.Application.Sponsors;
using Paddock.Career;
using Paddock.Data.Authored;
using Paddock.Data.Historical;
using Paddock.Data.World;
using Paddock.Domain.Board;
using Paddock.Domain.Career;
using Paddock.Domain.Contracts;
using Paddock.Domain.Finance;
using Paddock.Domain.Inbox;
using Paddock.Domain.Infrastructure;
using Paddock.Domain.Objectives;
using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;
using Paddock.Simulation.Career;
using AccessManagerId = Paddock.Application.Access.ManagerId;
using HostManagerId = Paddock.Application.Managers.ManagerId;

namespace Paddock.SimRunner.Scenario;

/// <summary>How the bot spends the player's split and which market move it makes. Same Application commands as a human.</summary>
public sealed record Phase4Policy(
    string SeasonTargetOption,
    int CurrentPercent,
    int AccountPercent,
    int NextYearPercent,
    Phase4MarketMove Market,
    Phase4SponsorMove Sponsor)
{
    public static Phase4Policy Default { get; } = new(SeasonTarget.Expected, 50, 25, 25, Phase4MarketMove.None, Phase4SponsorMove.None);

    public static Phase4Policy CurrentCar { get; } = Default with { CurrentPercent = 90, AccountPercent = 5, NextYearPercent = 5 };

    public static Phase4Policy NextYearCar { get; } = Default with { CurrentPercent = 5, AccountPercent = 5, NextYearPercent = 90 };

    public static Phase4Policy StarDriver { get; } = Default with { Market = Phase4MarketMove.Star };

    public static Phase4Policy OtherDriver { get; } = Default with { Market = Phase4MarketMove.Other };

    public static Phase4Policy AcceptSponsor { get; } = Default with { Sponsor = Phase4SponsorMove.Accept };

    public static Phase4Policy DeclineSponsor { get; } = Default with { Sponsor = Phase4SponsorMove.Decline };

    public static Phase4Policy Ambitious { get; } = Default with { SeasonTargetOption = SeasonTarget.Ambitious };
}

public enum Phase4MarketMove
{
    None,
    Star,
    Other,
}

public enum Phase4SponsorMove
{
    None,
    Accept,
    Decline,
}

/// <summary>One lived career: the bot files the same commands a human files (INV-001).</summary>
public sealed record Phase4Play(
    CareerShell Shell,
    CareerConfig Config,
    string TeamId,
    ulong Seed,
    GameDate Reached,
    string WorldHash,
    bool ReachedUntil,
    string? SoftLock,
    string? KnownIssue,
    int RacesEntered,
    int RacesSkipped,
    bool Dismissed,
    bool Insolvent,
    TimeSpan Wall,
    long SaveBytes,
    IReadOnlyList<string> Notes);

/// <summary>
/// A scripted manager. It takes over any 1955 team, answers inbox items, and lives days until the target date,
/// a classified hang, or an unclassified soft lock.
/// </summary>
public static class Phase4Bot
{
    public static Phase4Play Play(string dataRoot, string teamId, ulong seed, GameDate until, Phase4Policy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(teamId);
        var started = DateTime.UtcNow;
        var data = AuthoredDataLoader.Load(dataRoot);
        var teams = WorldInitializer.PublicTeams(data, 1955);
        if (!teams.Any(team => string.Equals(team.Id, teamId, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Team '" + teamId + "' is not in the 1955 field.", nameof(teamId));
        }

        var config = CareerConfig.FromPreset(CareerPreset.Chaos).WithStartYear(1955).WithPlayerTeam(teamId);
        var provider = EmptyPeopleProvider.Instance;
        var created = WorldInitializer.Create(config, data, provider, seed);
        var arrivals = TalentIntakeSchedule.AfterStart(config, provider, created.World, seed);
        var session = new CareerSession(
            created.World,
            seed,
            created.TalentPool,
            arrivals,
            new CareerSessionOptions { LastSeasons = LastSeasons.From(provider) });
        var options = new CareerRunOptions
        {
            Inputs = CareerInputsLoader.Load(dataRoot, data, created.EngineSupplies, config, RaceDateBook.Empty),
        };
        var shell = CareerShell.Open(session, options, "Gate Bot");
        var take = shell.Submit(new TakeOverTeamCommand
        {
            ManagerId = shell.Player,
            IssuedOn = new DateOnly(1955, 1, 1),
            OrganizationId = teamId,
            GivenName = "Gate",
            FamilyName = "Bot",
            Nationality = "GBR",
            Tilt = "negotiation",
        });
        if (take is CommandResult.Rejected refused)
        {
            throw new InvalidOperationException(refused.Reason.Key);
        }

        shell.BeginDay();
        ApplyOpening(shell, policy);
        var notes = new List<string>();
        var races = 0;
        var skipped = 0;
        string? known = null;
        string? lockReason = null;
        var days = 0;
        var cap = Phase4Estimates.Start.DaysUntil(until) + 8;
        while (shell.Date < until && days < cap)
        {
            days++;
            shell.BeginDay();
            var acted = ResolveHumanDecisions(shell, notes);
            var blocked = ClassifyBlock(shell, out known);
            if (blocked is not null)
            {
                lockReason = blocked;
                break;
            }

            if (!acted && shell.Date >= until)
            {
                break;
            }

            WatchRace(shell, teamId, ref races, ref skipped, notes);
            shell.Ready(shell.Player);
            var before = shell.Date;
            var advance = shell.Advance();
            if (advance is AdvanceResult.Refused hold)
            {
                ResolveHumanDecisions(shell, notes);
                shell.Ready(shell.Player);
                advance = shell.Advance();
                if (advance is AdvanceResult.Refused)
                {
                    lockReason = ClassifyBlock(shell, out known) ?? hold.Refusal.Reason.Key;
                    break;
                }
            }

            if (shell.Date == before)
            {
                lockReason = "calendar.didNotAdvance";
                break;
            }
        }

        WatchRace(shell, teamId, ref races, ref skipped, notes);
        var team = OrganizationId.Real(teamId);
        var board = shell.Modules.Require<BoardBook>();
        var dismissed = board.Section.OrganizationOf(shell.Player.Value) is null
            && board.Section.UnemployedManager(shell.Player.Value) is not null;
        var finance = shell.Session.World.Section<FinanceSection>(FinanceSection.SectionName);
        var insolvent = finance is not null && finance.IsInsolvent(team);
        var saveBytes = MeasureSave(dataRoot, shell, config, teamId);
        var reached = shell.Date >= until && lockReason is null;
        if (lockReason is not null && known is null)
        {
            notes.Add("Unclassified stop on " + shell.Date + ": " + lockReason);
        }
        else if (known is not null)
        {
            notes.Add("Known issue " + known + " on " + shell.Date + " (not fixed in #113).");
        }

        return new Phase4Play(
            shell,
            config,
            teamId,
            seed,
            shell.Date,
            shell.WorldHash,
            reached,
            lockReason,
            known,
            races,
            skipped,
            dismissed,
            insolvent,
            DateTime.UtcNow - started,
            saveBytes,
            notes);
    }

    private static void ApplyOpening(CareerShell shell, Phase4Policy policy)
    {
        var team = shell.TeamOf(shell.Player) ?? throw new InvalidOperationException("Take-over left the player without a team.");
        var today = Today(shell);
        var target = OpenDecisions(shell).FirstOrDefault(item => item.Kind == BoardEngine.SeasonTargetKind);
        if (target is not null)
        {
            Submit(shell, new ResolveInboxItemCommand
            {
                ManagerId = shell.Player,
                IssuedOn = today,
                ItemId = target.Id,
                OptionId = policy.SeasonTargetOption,
            });
        }

        Submit(shell, new SetDevelopmentSplitCommand
        {
            ManagerId = shell.Player,
            IssuedOn = today,
            OrganizationId = team.Value,
            CurrentPercent = policy.CurrentPercent,
            AccountPercent = policy.AccountPercent,
            NextYearPercent = policy.NextYearPercent,
        });
        ApproachMarket(shell, policy.Market);
        ApproachSponsor(shell, policy.Sponsor);
    }

    private static void ApproachMarket(CareerShell shell, Phase4MarketMove move)
    {
        if (move == Phase4MarketMove.None)
        {
            return;
        }

        var team = shell.TeamOf(shell.Player)!.Value;
        var access = AccessOf(shell);
        var people = new FreeAgentQuery(shell.Modules.Require<ContractBook>())
            .List(access, team, shell.Date, NegotiationSubject.DriverSeat)
            .Where(person => person.IsDriver)
            .OrderBy(person => person.Person.Value, StringComparer.Ordinal)
            .ToArray();
        if (people.Length == 0)
        {
            return;
        }

        var ranked = people
            .Select(person => (person, score: person.KnownAttributes.FirstOrDefault(attribute => attribute.Key == "cornering") is { } band
                ? band.Low + band.High
                : 0))
            .OrderByDescending(row => row.score)
            .ThenBy(row => row.person.Person.Value, StringComparer.Ordinal)
            .Select(row => row.person)
            .ToArray();
        var pick = move == Phase4MarketMove.Star ? ranked[0] : ranked[^1];
        var opened = shell.Submit(new OpenNegotiationCommand
        {
            ManagerId = shell.Player,
            IssuedOn = Today(shell),
            Organization = team,
            Person = pick.Person,
            Subject = NegotiationSubject.DriverSeat,
        });
        if (opened is CommandResult.Rejected)
        {
            return;
        }

        var talks = new NegotiationQuery(shell.Modules.Require<ContractBook>()).View(access);
        var talk = talks.Items.LastOrDefault(item => item.Person == pick.Person);
        if (talk is null)
        {
            return;
        }

        var salary = shell.Modules.Require<ContractBook>().ReferenceSalary(team, pick.Person, NegotiationSubject.DriverSeat, shell.Date);
        Submit(shell, new SubmitOfferCommand
        {
            ManagerId = shell.Player,
            IssuedOn = Today(shell),
            NegotiationId = talk.Id,
            Terms = new OfferTerms(Math.Max(1, salary), 0, 0, 0, 1, SeatStatus.Equal, null, null),
        });
    }

    private static void ApproachSponsor(CareerShell shell, Phase4SponsorMove move)
    {
        if (move == Phase4SponsorMove.None)
        {
            return;
        }

        var team = shell.TeamOf(shell.Player)!.Value;
        if (shell.Modules.TryGet<SponsorBook>() is not { } book || shell.Modules.TryGet<SponsorEnvironment>() is not { } environment)
        {
            return;
        }

        var objectives = new ObjectiveQuery(shell.Modules.Require<IObjectiveFacts>(), shell.Modules.Require<IManagerOrganizations>());
        var view = SponsorQuery.Read(AccessOf(shell), team, book, environment, objectives, shell.Date);
        if (view is not SponsorView.Own own)
        {
            return;
        }

        var slot = own.Slots.FirstOrDefault(item => item.TalkId is null && item.DealId is null && item.Candidates.Any(candidate => candidate.Blocked is null));
        if (slot is null)
        {
            return;
        }

        var sponsor = slot.Candidates.First(candidate => candidate.Blocked is null);
        var began = shell.Submit(new BeginSponsorTalksCommand
        {
            ManagerId = shell.Player,
            IssuedOn = Today(shell),
            OrganizationId = team.Value,
            SponsorId = sponsor.SponsorId,
            Slot = slot.Slot,
        });
        if (began is CommandResult.Rejected)
        {
            return;
        }

        view = SponsorQuery.Read(AccessOf(shell), team, book, environment, objectives, shell.Date);
        if (view is not SponsorView.Own after || after.Talks.Count == 0)
        {
            return;
        }

        var talk = after.Talks[0];
        if (move == Phase4SponsorMove.Decline)
        {
            Submit(shell, new WalkAwayFromTalksCommand
            {
                ManagerId = shell.Player,
                IssuedOn = Today(shell),
                OrganizationId = team.Value,
                TalkId = talk.Id,
            });
            return;
        }

        Submit(shell, new SignAtCurrentTermsCommand
        {
            ManagerId = shell.Player,
            IssuedOn = Today(shell),
            OrganizationId = team.Value,
            TalkId = talk.Id,
        });
    }

    private static bool ResolveHumanDecisions(CareerShell shell, List<string> notes)
    {
        var acted = false;
        for (var i = 0; i < 32; i++)
        {
            var open = OpenDecisions(shell);
            if (open.Count == 0)
            {
                return acted;
            }

            var item = open[0];
            var option = PickOption(shell, item);
            var result = shell.Submit(new ResolveInboxItemCommand
            {
                ManagerId = shell.Player,
                IssuedOn = Today(shell),
                ItemId = item.Id,
                OptionId = option,
            });
            if (result is CommandResult.Rejected rejected
                && item.Kind == ContractEngine.RenewalKind
                && option == ContractEngine.OptionRenew)
            {
                notes.Add("Renewal " + item.Id + " refused (" + rejected.Reason.Key + "); releasing (known " + Phase4KnownIssues.JulyRenewalFlood + ").");
                result = shell.Submit(new ResolveInboxItemCommand
                {
                    ManagerId = shell.Player,
                    IssuedOn = Today(shell),
                    ItemId = item.Id,
                    OptionId = ContractEngine.OptionRelease,
                });
            }

            if (result is CommandResult.Rejected still)
            {
                notes.Add("Could not resolve " + item.Kind + " " + item.Id + ": " + still.Reason.Key);
                return acted;
            }

            acted = true;
        }

        return acted;
    }

    private static string PickOption(CareerShell shell, InboxItemView item)
    {
        if (item.Kind == BoardEngine.SeasonTargetKind)
        {
            return SeasonTarget.Expected;
        }

        if (item.Kind == ContractEngine.RenewalKind)
        {
            var talks = shell.Modules.Require<ContractBook>().Section.ActiveOf(shell.TeamOf(shell.Player)!.Value).Count;
            var cap = shell.Modules.Require<ContractBook>().Capacity(shell.TeamOf(shell.Player)!.Value);
            return talks >= cap ? ContractEngine.OptionRelease : ContractEngine.OptionRenew;
        }

        if (item.Kind == DevelopmentKeys.ConceptInboxKind)
        {
            return DevelopmentKeys.OptionWait;
        }

        if (!string.IsNullOrEmpty(item.DefaultOptionId))
        {
            return item.DefaultOptionId;
        }

        return item.Options[0].Id;
    }

    private static string? ClassifyBlock(CareerShell shell, out string? known)
    {
        known = null;
        var player = shell.Player;
        var inbox = new InboxQuery(shell.Modules.Require<InboxBook>()).View(AccessOf(shell));
        var playerDecision = inbox.OpenDecisionCount > 0;
        var otherHold = false;
        foreach (var manager in shell.Modules.Managers.All)
        {
            if (manager.Id == player)
            {
                continue;
            }

            if (manager.BlockingItem is not null)
            {
                otherHold = true;
            }
        }

        if (!playerDecision && otherHold)
        {
            known = Phase4KnownIssues.AiInboxHoldsClock;
            return "soft-lock.other-manager";
        }

        var renewals = inbox.Items.Any(item => item.Status == InboxStatus.Open && item.Kind == ContractEngine.RenewalKind);
        var sponsors = inbox.Items.Any(item => item.Status == InboxStatus.Open && item.Kind.StartsWith("sponsor.", StringComparison.Ordinal));
        known = Phase4KnownIssues.Classify(shell.Date, playerDecision, otherHold, renewals, sponsors);
        if (playerDecision)
        {
            return null;
        }

        foreach (var manager in shell.Modules.Managers.All)
        {
            if (manager.Kind == ManagerKind.Human && manager.Id == player && manager.BlockingItem is not null && inbox.OpenDecisionCount == 0)
            {
                return "soft-lock.no-action";
            }
        }

        return null;
    }

    private static void WatchRace(CareerShell shell, string teamId, ref int races, ref int skipped, List<string> notes)
    {
        if (shell.Modules.TryGet<RaceWatch>() is not { } watch
            || !watch.TryTake(out _, out _, out _, out _, out var lines, out var skippedIds))
        {
            return;
        }

        if (lines.Any(line => line.TeamId == teamId))
        {
            races++;
            return;
        }

        if (skippedIds.Contains(teamId, StringComparer.Ordinal))
        {
            skipped++;
            notes.Add("Skipped a race (running and transport) on " + shell.Date + ".");
        }
    }

    private static long MeasureSave(string dataRoot, CareerShell shell, CareerConfig config, string teamId)
    {
        var path = Path.Combine(Path.GetTempPath(), "paddock-gate-" + Guid.NewGuid().ToString("N") + ".paddock");
        try
        {
            CareerSaveWriter.Write(path, shell.Session, config, teamId, RunCommand.HashWorldData(dataRoot, null), "phase4-1955", shell.HostState);
            return new FileInfo(path).Length;
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static IReadOnlyList<InboxItemView> OpenDecisions(CareerShell shell) =>
        new InboxQuery(shell.Modules.Require<InboxBook>()).View(AccessOf(shell)).Items
            .Where(item => item.Status == InboxStatus.Open && item.NeedsDecision)
            .ToArray();

    private static AccessContext AccessOf(CareerShell shell) => AccessContext.ForManager(new AccessManagerId(shell.Player.Value));

    private static DateOnly Today(CareerShell shell) => new(shell.Date.Year, shell.Date.Month, shell.Date.Day);

    private static void Submit(CareerShell shell, ICommand command) => _ = shell.Submit(command);

    /// <summary>Spends cash until the books are in the red, then lives one season so insolvency can fire.</summary>
    public static Phase4Play Collapse(string dataRoot, ulong seed)
    {
        var play = Play(dataRoot, "gordini", seed, new GameDate(1955, 3, 1), Phase4Policy.Default);
        var shell = play.Shell;
        var team = shell.TeamOf(shell.Player);
        if (team is not OrganizationId organization)
        {
            return play;
        }

        for (var i = 0; i < 8; i++)
        {
            _ = shell.Submit(new BookTestCommand
            {
                ManagerId = shell.Player,
                IssuedOn = Today(shell),
                OrganizationId = organization.Value,
            });
            _ = shell.Submit(new UpgradeFacilityCommand
            {
                ManagerId = shell.Player,
                IssuedOn = Today(shell),
                OrganizationId = organization.Value,
                Kind = nameof(FacilityKind.Factory),
            });
        }

        return PlayFrom(shell, play, dataRoot, new GameDate(1956, 4, 1));
    }

    private static Phase4Play PlayFrom(CareerShell shell, Phase4Play previous, string dataRoot, GameDate until)
    {
        var notes = previous.Notes.ToList();
        string? known = previous.KnownIssue;
        string? lockReason = previous.SoftLock;
        var days = 0;
        while (shell.Date < until && days < 400 && lockReason is null)
        {
            days++;
            shell.BeginDay();
            ResolveHumanDecisions(shell, notes);
            lockReason = ClassifyBlock(shell, out known);
            if (lockReason is not null)
            {
                break;
            }

            shell.Ready(shell.Player);
            if (shell.Advance() is AdvanceResult.Refused)
            {
                ResolveHumanDecisions(shell, notes);
                shell.Ready(shell.Player);
                if (shell.Advance() is AdvanceResult.Refused)
                {
                    lockReason = ClassifyBlock(shell, out known) ?? "advance.refused";
                    break;
                }
            }
        }

        var team = OrganizationId.Real(previous.TeamId);
        var finance = shell.Session.World.Section<FinanceSection>(FinanceSection.SectionName);
        var insolvent = finance is not null && finance.IsInsolvent(team);
        var board = shell.Modules.Require<BoardBook>();
        var dismissed = board.Section.UnemployedManager(shell.Player.Value) is not null;
        return previous with
        {
            Reached = shell.Date,
            WorldHash = shell.WorldHash,
            ReachedUntil = shell.Date >= until && lockReason is null,
            SoftLock = lockReason,
            KnownIssue = known,
            Dismissed = dismissed,
            Insolvent = insolvent,
            Notes = notes,
            SaveBytes = MeasureSave(dataRoot, shell, previous.Config, previous.TeamId),
        };
    }
}
