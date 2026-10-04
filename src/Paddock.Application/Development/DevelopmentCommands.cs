using System.Globalization;
using Paddock.Application.Cars;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Inbox;
using Paddock.Application.Managers;
using Paddock.Domain.Development;
using Paddock.Domain.Inbox;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Development;

/// <summary>
/// The principal sets the split of resources between the current car, the development account and next year's car, and the
/// priority of each area (PP-043, path A). It is the only thing the principal sets; the engineers choose the projects.
/// </summary>
public sealed record SetDevelopmentSplitCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string OrganizationId { get; init; }

    public int CurrentPercent { get; init; }

    public int AccountPercent { get; init; }

    public int NextYearPercent { get; init; }

    public int AeroPriority { get; init; } = DevelopmentEstimates.DefaultPriority;

    public int ChassisPriority { get; init; } = DevelopmentEstimates.DefaultPriority;

    public int ReliabilityPriority { get; init; } = DevelopmentEstimates.DefaultPriority;

    public int TyresPriority { get; init; } = DevelopmentEstimates.DefaultPriority;

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>Chooses when a concept is deployed: <c>WhenReady</c>, <c>AfterRaces</c> (with <see cref="Races"/>), <c>NextSeason</c> or <c>Hold</c> (stay ready across rollovers).</summary>
public sealed record DeployConceptCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string OrganizationId { get; init; }

    public required string ProjectId { get; init; }

    public required string Timing { get; init; }

    public int Races { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>
/// Commits a ready concept to production (T42c): the principal has looked at the state of the work and decides to build now
/// rather than keep waiting. The old car keeps racing; the concept goes live at the first race after production ends.
/// </summary>
public sealed record CommitConceptCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string OrganizationId { get; init; }

    public required string ProjectId { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>Closes an active project early for a proportional gain and a proportional cost.</summary>
public sealed record CutProjectCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string OrganizationId { get; init; }

    public required string ProjectId { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

internal static class DevelopmentCommandSupport
{
    public const int MaxRaces = 30;

    public static TranslationMessage? Team(
        DevelopmentEnvironment environment,
        WorldState world,
        ManagerId manager,
        string organizationId,
        out OrganizationId organization)
    {
        if (!CarCommandSupport.TryOrganization(organizationId, out organization))
        {
            return TranslationMessage.Of(DevelopmentKeys.UnknownOrganization);
        }

        if (!environment.Control.Controls(manager, organization))
        {
            return TranslationMessage.Of(DevelopmentKeys.NoControl);
        }

        var target = organization;
        var found = world.Organizations.FirstOrDefault(candidate => candidate.Id == target);
        if (found is null)
        {
            return TranslationMessage.Of(DevelopmentKeys.UnknownOrganization);
        }

        return found.Kind == OrganizationKind.Team ? null : TranslationMessage.Of(DevelopmentKeys.NotATeam);
    }

    public static bool TryTiming(string text, out ConceptTiming timing) =>
        Enum.TryParse(text, ignoreCase: false, out timing) && Enum.IsDefined(timing);

    public static GameDate Day(DateOnly date) => new(date.Year, date.Month, date.Day);

    public static DevProject? Own(DevelopmentBook book, OrganizationId organization, string projectId) =>
        book.Section.Find(projectId) is { } project && project.Organization == organization ? project : null;
}

public sealed class SetDevelopmentSplitHandler : CommandHandler<SetDevelopmentSplitCommand>
{
    private readonly DevelopmentBook _book;
    private readonly DevelopmentEnvironment _environment;

    public SetDevelopmentSplitHandler(DevelopmentBook book, DevelopmentEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
    }

    protected override TranslationMessage? ValidateTyped(SetDevelopmentSplitCommand command, CommandContext context)
    {
        var rejection = DevelopmentCommandSupport.Team(_environment, _book.World, command.ManagerId, command.OrganizationId, out _);
        if (rejection is not null)
        {
            return rejection;
        }

        if (!DevelopmentPlan.IsValidSplit(command.CurrentPercent, command.AccountPercent, command.NextYearPercent))
        {
            return TranslationMessage.Of(DevelopmentKeys.BadSplit);
        }

        int[] priorities = [command.AeroPriority, command.ChassisPriority, command.ReliabilityPriority, command.TyresPriority];
        return priorities.All(DevelopmentPlan.IsValidPriority) ? null : TranslationMessage.Of(DevelopmentKeys.BadPriority);
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(SetDevelopmentSplitCommand command, CommandContext context)
    {
        if (ValidateTyped(command, context) is not null
            || !CarCommandSupport.TryOrganization(command.OrganizationId, out var organization))
        {
            throw new InvalidOperationException("Execute ran for a command that should have been rejected.");
        }

        var today = DevelopmentCommandSupport.Day(command.IssuedOn);
        var section = _book.Section;
        var before = section.PlanOf(organization) ?? DevelopmentPlan.Default(organization);
        var after = before with
        {
            CurrentPercent = command.CurrentPercent,
            AccountPercent = command.AccountPercent,
            NextYearPercent = command.NextYearPercent,
            AeroPriority = command.AeroPriority,
            ChassisPriority = command.ChassisPriority,
            ReliabilityPriority = command.ReliabilityPriority,
            TyresPriority = command.TyresPriority,
            ChangedOn = today,
        };
        _book.Write(new DevelopmentOutcome(_book.Cars, section.SetPlan(after), _book.Finance, true));
        var events = new List<IDomainEvent> { new DevelopmentSplitSet(command.ManagerId, command.IssuedOn, organization.Value) };
        var splitMoved = before.CurrentPercent != after.CurrentPercent
            || before.AccountPercent != after.AccountPercent
            || before.NextYearPercent != after.NextYearPercent;
        if (splitMoved && Reply(section, organization, today) is { } close)
        {
            var draft = new InboxItemDraft(
                DevelopmentKeys.InboxKind,
                DevelopmentKeys.ReplySubject,
                [
                    new KeyValuePair<string, string>(DevelopmentKeys.ProjectArgument, close.Project.Id),
                    new KeyValuePair<string, string>("weeks", close.Weeks.ToString(CultureInfo.InvariantCulture)),
                ],
                [
                    new InboxOption(DevelopmentKeys.OptionKeep, DevelopmentKeys.ReplyKeepLabel, DevelopmentKeys.ReplyKeepConsequence),
                    new InboxOption(DevelopmentKeys.OptionCut, DevelopmentKeys.ReplyCutLabel, DevelopmentKeys.ReplyCutConsequence),
                ],
                today.AddDays(DevelopmentEstimates.ReplyDays),
                DevelopmentKeys.OptionKeep);
            events.Add(context.Inbox.Post(context.Managers, command.ManagerId, draft, today));
        }

        return events;
    }

    /// <summary>
    /// The engineers answer a changed split about the active project that is furthest along, once it is past
    /// <see cref="DevelopmentEstimates.CloseProgress"/>: "a few more weeks, we are close". The player keeps the plan or cuts.
    /// </summary>
    private static (DevProject Project, int Weeks)? Reply(DevelopmentSection section, OrganizationId organization, GameDate today)
    {
        _ = today;
        var close = section.ProjectsOf(organization)
            .Where(project => project.IsActive && project.Progress >= DevelopmentEstimates.CloseProgress && project.ProgressDays < project.DurationDays)
            .OrderByDescending(project => project.Progress)
            .ThenBy(project => project.Number)
            .FirstOrDefault();
        if (close is null)
        {
            return null;
        }

        var weeks = Math.Max(1, (int)Math.Ceiling((close.DurationDays - close.ProgressDays) / 7d));
        return (close, weeks);
    }
}

public sealed class DeployConceptHandler : CommandHandler<DeployConceptCommand>
{
    private readonly DevelopmentBook _book;
    private readonly DevelopmentEnvironment _environment;

    public DeployConceptHandler(DevelopmentBook book, DevelopmentEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
    }

    protected override TranslationMessage? ValidateTyped(DeployConceptCommand command, CommandContext context)
    {
        var rejection = DevelopmentCommandSupport.Team(_environment, _book.World, command.ManagerId, command.OrganizationId, out var organization);
        if (rejection is not null)
        {
            return rejection;
        }

        if (DevelopmentCommandSupport.Own(_book, organization, command.ProjectId) is not { } project)
        {
            return TranslationMessage.Of(DevelopmentKeys.UnknownProject);
        }

        if (project.Kind != DevKind.Concept)
        {
            return TranslationMessage.Of(DevelopmentKeys.NotConcept);
        }

        if (project.Status is not (ProjectStatus.Active or ProjectStatus.Ready))
        {
            return TranslationMessage.Of(DevelopmentKeys.NotDeployable);
        }

        if (!DevelopmentCommandSupport.TryTiming(command.Timing, out var timing))
        {
            return TranslationMessage.Of(DevelopmentKeys.BadTiming);
        }

        var racesOk = timing == ConceptTiming.AfterRaces
            ? command.Races is >= 1 and <= DevelopmentCommandSupport.MaxRaces
            : command.Races == 0;
        return racesOk ? null : TranslationMessage.Of(DevelopmentKeys.BadTiming);
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(DeployConceptCommand command, CommandContext context)
    {
        if (ValidateTyped(command, context) is not null
            || !CarCommandSupport.TryOrganization(command.OrganizationId, out var organization)
            || !DevelopmentCommandSupport.TryTiming(command.Timing, out var timing))
        {
            throw new InvalidOperationException("Execute ran for a command that should have been rejected.");
        }

        var today = DevelopmentCommandSupport.Day(command.IssuedOn);
        var project = DevelopmentCommandSupport.Own(_book, organization, command.ProjectId)!;
        var updated = project with { Timing = timing, TimingRaces = command.Races, RacesWaited = 0 };
        var section = _book.Section.ReplaceProject(updated);
        var outcome = new DevelopmentOutcome(_book.Cars, section, _book.Finance, true);
        if (updated.Status == ProjectStatus.Ready && timing == ConceptTiming.WhenReady)
        {
            // WhenReady is the automatic path: commit as soon as ready. For a concept that already is, that is now.
            var inputs = _book.Inputs(today, _environment) with { Development = section };
            outcome = DevelopmentEngine.StartProduction(inputs, updated.Id);
        }

        _book.Write(outcome);
        return [new ConceptTimingSet(command.ManagerId, command.IssuedOn, updated.Id, timing.ToString())];
    }
}

public sealed class CommitConceptHandler : CommandHandler<CommitConceptCommand>
{
    private readonly DevelopmentBook _book;
    private readonly DevelopmentEnvironment _environment;

    public CommitConceptHandler(DevelopmentBook book, DevelopmentEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
    }

    protected override TranslationMessage? ValidateTyped(CommitConceptCommand command, CommandContext context)
    {
        var rejection = DevelopmentCommandSupport.Team(_environment, _book.World, command.ManagerId, command.OrganizationId, out var organization);
        if (rejection is not null)
        {
            return rejection;
        }

        return DevelopmentCommandSupport.Own(_book, organization, command.ProjectId) switch
        {
            null => TranslationMessage.Of(DevelopmentKeys.UnknownProject),
            { Kind: not DevKind.Concept } => TranslationMessage.Of(DevelopmentKeys.NotConcept),
            { Status: not ProjectStatus.Ready } => TranslationMessage.Of(DevelopmentKeys.NotReady),
            _ => null,
        };
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(CommitConceptCommand command, CommandContext context)
    {
        if (ValidateTyped(command, context) is not null)
        {
            throw new InvalidOperationException("Execute ran for a command that should have been rejected.");
        }

        var today = DevelopmentCommandSupport.Day(command.IssuedOn);
        _book.Write(DevelopmentEngine.StartProduction(_book.Inputs(today, _environment), command.ProjectId));
        return [Committed(_book, command.ManagerId, command.IssuedOn, command.ProjectId)];
    }

    internal static ConceptCommitted Committed(DevelopmentBook book, ManagerId manager, DateOnly on, string projectId)
    {
        var ends = book.Section.Find(projectId)!.ProductionEnds!.Value;
        return new ConceptCommitted(manager, on, projectId, new DateOnly(ends.Year, ends.Month, ends.Day));
    }
}

public sealed class CutProjectHandler : CommandHandler<CutProjectCommand>
{
    private readonly DevelopmentBook _book;
    private readonly DevelopmentEnvironment _environment;

    public CutProjectHandler(DevelopmentBook book, DevelopmentEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
    }

    protected override TranslationMessage? ValidateTyped(CutProjectCommand command, CommandContext context)
    {
        var rejection = DevelopmentCommandSupport.Team(_environment, _book.World, command.ManagerId, command.OrganizationId, out var organization);
        if (rejection is not null)
        {
            return rejection;
        }

        return DevelopmentCommandSupport.Own(_book, organization, command.ProjectId) switch
        {
            null => TranslationMessage.Of(DevelopmentKeys.UnknownProject),
            { IsActive: false } => TranslationMessage.Of(DevelopmentKeys.NotActive),
            _ => null,
        };
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(CutProjectCommand command, CommandContext context)
    {
        if (ValidateTyped(command, context) is not null)
        {
            throw new InvalidOperationException("Execute ran for a command that should have been rejected.");
        }

        var today = DevelopmentCommandSupport.Day(command.IssuedOn);
        _book.Write(DevelopmentEngine.Cut(_book.Inputs(today, _environment), command.ProjectId));
        return [new DevelopmentProjectCut(command.ManagerId, command.IssuedOn, command.ProjectId)];
    }
}

/// <summary>
/// The engineers' reply to a changed split: keep the plan, or cut the project that is nearly done. Cutting runs through
/// the same engine as the command, so the inbox answer and the command give the same result.
/// </summary>
public sealed class DevelopmentReplyResolver : IInboxResolver
{
    private readonly DevelopmentBook _book;
    private readonly DevelopmentEnvironment _environment;

    public DevelopmentReplyResolver(DevelopmentBook book, DevelopmentEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
    }

    public string Kind => DevelopmentKeys.InboxKind;

    public TranslationMessage? Validate(InboxItem item, string optionId, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(item);
        switch (optionId)
        {
            case DevelopmentKeys.OptionKeep:
                return null;
            case DevelopmentKeys.OptionCut:
                return _book.Section.Find(item.Arguments[DevelopmentKeys.ProjectArgument]) is { IsActive: true }
                    ? null
                    : TranslationMessage.Of(DevelopmentKeys.NotActive);
            default:
                return TranslationMessage.Of(InboxKeys.OptionUnknown);
        }
    }

    public IReadOnlyList<IDomainEvent> Execute(InboxItem item, string optionId, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        if (optionId != DevelopmentKeys.OptionCut)
        {
            return [];
        }

        var today = InboxBook.ToGameDate(context.World.CurrentDate);
        var id = item.Arguments[DevelopmentKeys.ProjectArgument];
        _book.Write(DevelopmentEngine.Cut(_book.Inputs(today, _environment), id));
        return [new DevelopmentProjectCut(new ManagerId(item.ManagerId), InboxBook.ToDateOnly(today), id)];
    }
}

/// <summary>
/// The answer to "commit now or keep developing?" (T42c). Commit runs the same engine as <see cref="CommitConceptCommand"/>;
/// keep developing changes nothing, so the concept stays ready with whatever timing it has.
/// </summary>
public sealed class ConceptDecisionResolver : IInboxResolver
{
    private readonly DevelopmentBook _book;
    private readonly DevelopmentEnvironment _environment;

    public ConceptDecisionResolver(DevelopmentBook book, DevelopmentEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
    }

    public string Kind => DevelopmentKeys.ConceptInboxKind;

    public TranslationMessage? Validate(InboxItem item, string optionId, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(item);
        switch (optionId)
        {
            case DevelopmentKeys.OptionWait:
                return null;
            case DevelopmentKeys.OptionCommit:
                return _book.Section.Find(item.Arguments[DevelopmentKeys.ProjectArgument]) is { Status: ProjectStatus.Ready }
                    ? null
                    : TranslationMessage.Of(DevelopmentKeys.NotReady);
            default:
                return TranslationMessage.Of(InboxKeys.OptionUnknown);
        }
    }

    public IReadOnlyList<IDomainEvent> Execute(InboxItem item, string optionId, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        if (optionId != DevelopmentKeys.OptionCommit)
        {
            return [];
        }

        var today = InboxBook.ToGameDate(context.World.CurrentDate);
        var id = item.Arguments[DevelopmentKeys.ProjectArgument];
        _book.Write(DevelopmentEngine.StartProduction(_book.Inputs(today, _environment), id));
        return [CommitConceptHandler.Committed(_book, new ManagerId(item.ManagerId), InboxBook.ToDateOnly(today), id)];
    }
}

/// <summary>
/// Registers the development commands and the inbox resolver of the engineers' reply. One call; it does not touch the
/// career loop. The day handler (<see cref="DevelopmentDayHandler"/>) and the post-race hook
/// (<see cref="DevelopmentRaceHook"/>) are wired by the host separately.
/// </summary>
public static class DevelopmentRegistration
{
    public static void Register(
        CommandDispatcher dispatcher,
        InboxResolvers resolvers,
        DevelopmentBook book,
        DevelopmentEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(resolvers);
        dispatcher.Register(new SetDevelopmentSplitHandler(book, environment));
        dispatcher.Register(new DeployConceptHandler(book, environment));
        dispatcher.Register(new CommitConceptHandler(book, environment));
        dispatcher.Register(new CutProjectHandler(book, environment));
        resolvers.Register(new DevelopmentReplyResolver(book, environment));
        resolvers.Register(new ConceptDecisionResolver(book, environment));
    }
}
