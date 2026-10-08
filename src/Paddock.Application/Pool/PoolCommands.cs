using Paddock.Application.Commands;
using Paddock.Application.Managers;
using Paddock.Application.Objectives;
using Paddock.Domain.Pool;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Career;

namespace Paddock.Application.Pool;

/// <summary>
/// The live world as the pool commands see it: they read the world and put the changed one back. The host decides where the
/// world lives; <see cref="ForSession"/> is the career session's.
/// </summary>
public sealed class PoolBook
{
    private readonly Func<WorldState> _read;
    private readonly Action<WorldState> _write;

    public PoolBook(Func<WorldState> read, Action<WorldState> write)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(write);
        _read = read;
        _write = write;
    }

    public WorldState World => _read();

    public TalentPoolSection Section => World.Section<TalentPoolSection>(TalentPoolSection.SectionName) ?? TalentPoolSection.Empty;

    public static PoolBook ForSession(CareerSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new PoolBook(() => session.World, session.StoreWorld);
    }

    internal void Replace(TalentPoolSection section) => _write(World.WithSection(section));

    internal static GameDate ToGameDate(DateOnly date) => new(date.Year, date.Month, date.Day);

    internal static DateOnly ToDateOnly(GameDate date) => new(date.Year, date.Month, date.Day);
}

/// <summary>
/// The manager's organization sends its scouts after one person of the pool, or after the whole pool. A person is named by the
/// handle the pool query gave, which says nothing about whether he is real (INV-003). The focus replaces the earlier one.
/// </summary>
public sealed record AssignScoutFocusCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    /// <summary>The handle of one pool member, or null for the whole pool.</summary>
    public string? PersonHandle { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>
/// The manager's organization pays for one season of a junior programme "somewhere lower" for a pool member. It acts on his
/// development at the start of the next season. A member has one funded season at a time. The money goes through
/// <see cref="IJuniorFunding"/> (finance, T37).
/// </summary>
public sealed record FundJuniorCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string PersonHandle { get; init; }

    public required JuniorProgramme Programme { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>
/// The manager's organization starts a negotiation with a pool member for a test or a junior role. The negotiation is
/// <see cref="IPoolNegotiations"/> (contracts, T39).
/// </summary>
public sealed record SignPoolDriverCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string PersonHandle { get; init; }

    public required PoolSigningRole Role { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>
/// The manager's team takes a pool member into its academy (#268): the team recruits its own juniors, a limited number
/// (<see cref="PoolEstimates.AcademySlots"/>), and a recruited junior is on nobody else's list. Only a member the team's own scouts show it
/// this season (<see cref="PoolShortlist"/>) can be recruited.
/// </summary>
public sealed record RecruitJuniorCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string PersonHandle { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>The manager's team frees a place of its academy: the junior goes back to the pool and a programme paid for him is lost.</summary>
public sealed record ReleaseJuniorCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string PersonHandle { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>A junior joined an academy.</summary>
public sealed record JuniorRecruited(ManagerId ManagerId, DateOnly OccurredOn, string PersonHandle) : IDomainEvent
{
    public string TypeId => "pool.junior.recruited";
}

/// <summary>A junior left an academy at the team's wish.</summary>
public sealed record JuniorReleased(ManagerId ManagerId, DateOnly OccurredOn, string PersonHandle) : IDomainEvent
{
    public string TypeId => "pool.junior.released";
}

/// <summary>An organization's scouts have a new focus.</summary>
public sealed record ScoutFocusAssigned(ManagerId ManagerId, DateOnly OccurredOn, string? PersonHandle) : IDomainEvent
{
    public string TypeId => "pool.scoutFocus.assigned";
}

/// <summary>A junior season was paid for.</summary>
public sealed record JuniorFunded(ManagerId ManagerId, DateOnly OccurredOn, string PersonHandle, JuniorProgramme Programme) : IDomainEvent
{
    public string TypeId => "pool.junior.funded";
}

/// <summary>A negotiation with a pool member was started.</summary>
public sealed record PoolSigningStarted(ManagerId ManagerId, DateOnly OccurredOn, string PersonHandle, PoolSigningRole Role) : IDomainEvent
{
    public string TypeId => "pool.signing.started";
}

internal static class PoolHandlers
{
    /// <summary>The organization of the manager, or the reason there is none. An AI manager with no organization is refused the same way.</summary>
    public static (OrganizationId Organization, TranslationMessage? Rejection) OrganizationOf(
        IManagerOrganizations organizations,
        ManagerId manager)
    {
        var organization = organizations.OrganizationOf(manager.Value);
        return organization is OrganizationId own && own.IsAssigned
            ? (own, null)
            : (default, TranslationMessage.Of(PoolKeys.NoOrganization));
    }

    /// <summary>The member a handle names, or null. A handle that was never issued and one of a member who left look the same.</summary>
    public static PoolMember? Member(PoolBook book, string handle) => book.Section.FindByHandle(handle);
}

public sealed class AssignScoutFocusHandler : CommandHandler<AssignScoutFocusCommand>
{
    private readonly PoolBook _book;
    private readonly IManagerOrganizations _organizations;

    public AssignScoutFocusHandler(PoolBook book, IManagerOrganizations organizations)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(organizations);
        _book = book;
        _organizations = organizations;
    }

    protected override TranslationMessage? ValidateTyped(AssignScoutFocusCommand command, CommandContext context)
    {
        var (_, rejection) = PoolHandlers.OrganizationOf(_organizations, command.ManagerId);
        if (rejection is not null)
        {
            return rejection;
        }

        return command.PersonHandle is not null && PoolHandlers.Member(_book, command.PersonHandle) is null
            ? TranslationMessage.Of(PoolKeys.PersonUnknown)
            : null;
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(AssignScoutFocusCommand command, CommandContext context)
    {
        var (organization, _) = PoolHandlers.OrganizationOf(_organizations, command.ManagerId);
        var member = command.PersonHandle is null ? null : PoolHandlers.Member(_book, command.PersonHandle);
        var focus = member is null
            ? new ScoutFocus(organization, ScoutFocusKind.Pool, null)
            : new ScoutFocus(organization, ScoutFocusKind.Person, member.Id);
        _book.Replace(_book.Section.SetFocus(focus));
        return [new ScoutFocusAssigned(command.ManagerId, command.IssuedOn, command.PersonHandle)];
    }
}

public sealed class FundJuniorHandler : CommandHandler<FundJuniorCommand>
{
    private readonly PoolBook _book;
    private readonly IManagerOrganizations _organizations;
    private readonly IJuniorFunding _funding;

    public FundJuniorHandler(PoolBook book, IManagerOrganizations organizations, IJuniorFunding? funding = null)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(organizations);
        _book = book;
        _organizations = organizations;
        _funding = funding ?? UnmeteredJuniorFunding.Instance;
    }

    protected override TranslationMessage? ValidateTyped(FundJuniorCommand command, CommandContext context)
    {
        var (organization, rejection) = PoolHandlers.OrganizationOf(_organizations, command.ManagerId);
        if (rejection is not null)
        {
            return rejection;
        }

        if (!Enum.IsDefined(command.Programme) || PoolHandlers.Member(_book, command.PersonHandle) is not { } member)
        {
            return TranslationMessage.Of(PoolKeys.PersonUnknown);
        }

        if (member.Academy != organization)
        {
            return TranslationMessage.Of(PoolKeys.NotYourJunior);
        }

        if (member.Funding is not null)
        {
            return TranslationMessage.Of(PoolKeys.AlreadyFunded);
        }

        return _funding.Validate(organization, _funding.Cost(command.Programme), PoolBook.ToGameDate(command.IssuedOn));
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(FundJuniorCommand command, CommandContext context)
    {
        var (organization, _) = PoolHandlers.OrganizationOf(_organizations, command.ManagerId);
        var member = PoolHandlers.Member(_book, command.PersonHandle)
            ?? throw new InvalidOperationException("Execute ran for a command that should have been rejected.");
        var today = PoolBook.ToGameDate(command.IssuedOn);
        _funding.Charge(organization, _funding.Cost(command.Programme), PoolKeys.FundingReason, today);
        _book.Replace(_book.Section.Fund(member.Id, new JuniorFunding(organization, command.Programme, today.Year)));
        return [new JuniorFunded(command.ManagerId, command.IssuedOn, command.PersonHandle, command.Programme)];
    }
}

public sealed class RecruitJuniorHandler : CommandHandler<RecruitJuniorCommand>
{
    private readonly PoolBook _book;
    private readonly IManagerOrganizations _organizations;

    public RecruitJuniorHandler(PoolBook book, IManagerOrganizations organizations)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(organizations);
        _book = book;
        _organizations = organizations;
    }

    protected override TranslationMessage? ValidateTyped(RecruitJuniorCommand command, CommandContext context)
    {
        var (organization, rejection) = PoolHandlers.OrganizationOf(_organizations, command.ManagerId);
        if (rejection is not null)
        {
            return rejection;
        }

        if (PoolHandlers.Member(_book, command.PersonHandle) is not { } member)
        {
            return TranslationMessage.Of(PoolKeys.PersonUnknown);
        }

        if (member.Academy == organization)
        {
            return TranslationMessage.Of(PoolKeys.AlreadyRecruited);
        }

        if (member.Academy is not null)
        {
            return TranslationMessage.Of(PoolKeys.TakenByAnother);
        }

        if (!PoolShortlist.Offers(organization, command.IssuedOn.Year, member))
        {
            return TranslationMessage.Of(PoolKeys.NotOnYourList);
        }

        return _book.Section.AcademyCount(organization) >= PoolEstimates.AcademySlots
            ? TranslationMessage.Of(PoolKeys.AcademyFull)
            : null;
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(RecruitJuniorCommand command, CommandContext context)
    {
        var (organization, _) = PoolHandlers.OrganizationOf(_organizations, command.ManagerId);
        var member = PoolHandlers.Member(_book, command.PersonHandle)
            ?? throw new InvalidOperationException("Execute ran for a command that should have been rejected.");
        _book.Replace(_book.Section.Recruit(member.Id, organization));
        return [new JuniorRecruited(command.ManagerId, command.IssuedOn, command.PersonHandle)];
    }
}

public sealed class ReleaseJuniorHandler : CommandHandler<ReleaseJuniorCommand>
{
    private readonly PoolBook _book;
    private readonly IManagerOrganizations _organizations;

    public ReleaseJuniorHandler(PoolBook book, IManagerOrganizations organizations)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(organizations);
        _book = book;
        _organizations = organizations;
    }

    protected override TranslationMessage? ValidateTyped(ReleaseJuniorCommand command, CommandContext context)
    {
        var (organization, rejection) = PoolHandlers.OrganizationOf(_organizations, command.ManagerId);
        if (rejection is not null)
        {
            return rejection;
        }

        if (PoolHandlers.Member(_book, command.PersonHandle) is not { } member)
        {
            return TranslationMessage.Of(PoolKeys.PersonUnknown);
        }

        return member.Academy == organization ? null : TranslationMessage.Of(PoolKeys.NotYourJunior);
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(ReleaseJuniorCommand command, CommandContext context)
    {
        var member = PoolHandlers.Member(_book, command.PersonHandle)
            ?? throw new InvalidOperationException("Execute ran for a command that should have been rejected.");
        _book.Replace(_book.Section.Release(member.Id));
        return [new JuniorReleased(command.ManagerId, command.IssuedOn, command.PersonHandle)];
    }
}

public sealed class SignPoolDriverHandler : CommandHandler<SignPoolDriverCommand>
{
    private readonly PoolBook _book;
    private readonly IManagerOrganizations _organizations;
    private readonly IPoolNegotiations _negotiations;

    public SignPoolDriverHandler(PoolBook book, IManagerOrganizations organizations, IPoolNegotiations? negotiations = null)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(organizations);
        _book = book;
        _organizations = organizations;
        _negotiations = negotiations ?? UnavailableNegotiations.Instance;
    }

    protected override TranslationMessage? ValidateTyped(SignPoolDriverCommand command, CommandContext context)
    {
        var (organization, rejection) = PoolHandlers.OrganizationOf(_organizations, command.ManagerId);
        if (rejection is not null)
        {
            return rejection;
        }

        if (!Enum.IsDefined(command.Role) || PoolHandlers.Member(_book, command.PersonHandle) is not { } member)
        {
            return TranslationMessage.Of(PoolKeys.PersonUnknown);
        }

        if (member.Academy is { } owner && owner != organization)
        {
            return TranslationMessage.Of(PoolKeys.TakenByAnother);
        }

        return _negotiations.ValidateStart(organization, member.Id, command.Role, PoolBook.ToGameDate(command.IssuedOn));
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(SignPoolDriverCommand command, CommandContext context)
    {
        var (organization, _) = PoolHandlers.OrganizationOf(_organizations, command.ManagerId);
        var member = PoolHandlers.Member(_book, command.PersonHandle)
            ?? throw new InvalidOperationException("Execute ran for a command that should have been rejected.");
        var events = new List<IDomainEvent>
        {
            new PoolSigningStarted(command.ManagerId, command.IssuedOn, command.PersonHandle, command.Role),
        };
        events.AddRange(_negotiations.Start(command.ManagerId, organization, member.Id, command.Role, PoolBook.ToGameDate(command.IssuedOn)));
        return events;
    }
}
