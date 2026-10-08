using Paddock.Application.Cars;
using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Finance;
using Paddock.Application.Managers;
using Paddock.Domain.Development;
using Paddock.Domain.Finance;
using Paddock.Domain.Infrastructure;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Infrastructure;

/// <summary>
/// The principal spends money and time to raise one facility toward the moving state of the art (PP-026). The facility is
/// degraded until the build ends. Refusals name a reason key.
/// </summary>
public sealed record UpgradeFacilityCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string OrganizationId { get; init; }

    public required string Kind { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>
/// The principal books one private test (PP-064). It runs <see cref="InfrastructureEstimates.TestLeadDays"/> later; the rental is
/// charged and the car learns on that day, so a booking cancelled before it costs nothing and gives nothing (#264).
/// </summary>
public sealed record BookTestCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string OrganizationId { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>Cancels a booked private test that has not happened yet. The booking is named by its day.</summary>
public sealed record CancelTestCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string OrganizationId { get; init; }

    public required DateOnly TestOn { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

public sealed record TestCancelled(
    ManagerId ManagerId,
    DateOnly OccurredOn,
    string OrganizationId,
    DateOnly TestOn) : IDomainEvent
{
    public string TypeId => InfrastructureEventTypes.TestCancelled;
}

public sealed record FacilityUpgradeStarted(
    ManagerId ManagerId,
    DateOnly OccurredOn,
    string OrganizationId,
    string Kind,
    DateOnly EndsOn) : IDomainEvent
{
    public string TypeId => InfrastructureEventTypes.UpgradeStarted;
}

public sealed record TestBooked(
    ManagerId ManagerId,
    DateOnly OccurredOn,
    string OrganizationId,
    long CostCents) : IDomainEvent
{
    public string TypeId => InfrastructureEventTypes.TestBooked;
}

internal static class InfrastructureCommandSupport
{
    public static TranslationMessage? Team(
        InfrastructureEnvironment environment,
        WorldState world,
        ManagerId manager,
        string organizationId,
        out OrganizationId organization)
    {
        if (!CarCommandSupport.TryOrganization(organizationId, out organization))
        {
            return TranslationMessage.Of(InfrastructureKeys.UnknownOrganization);
        }

        if (!environment.Control.Controls(manager, organization))
        {
            return TranslationMessage.Of(InfrastructureKeys.NoControl);
        }

        var target = organization;
        var found = world.Organizations.FirstOrDefault(candidate => candidate.Id == target);
        if (found is null)
        {
            return TranslationMessage.Of(InfrastructureKeys.UnknownOrganization);
        }

        return found.Kind == OrganizationKind.Team ? null : TranslationMessage.Of(InfrastructureKeys.NotATeam);
    }

    public static GameDate Day(DateOnly date) => new(date.Year, date.Month, date.Day);
}

public sealed class UpgradeFacilityHandler : CommandHandler<UpgradeFacilityCommand>
{
    private readonly InfrastructureBook _book;
    private readonly InfrastructureEnvironment _environment;

    public UpgradeFacilityHandler(InfrastructureBook book, InfrastructureEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
    }

    protected override TranslationMessage? ValidateTyped(UpgradeFacilityCommand command, CommandContext context)
    {
        var gate = InfrastructureCommandSupport.Team(
            _environment,
            _book.World,
            command.ManagerId,
            command.OrganizationId,
            out var organization);
        if (gate is not null)
        {
            return gate;
        }

        if (!FacilityKindIds.TryParse(command.Kind, out var kind))
        {
            return TranslationMessage.Of(InfrastructureKeys.UnknownKind);
        }

        var spec = _environment.Catalog.SpecOf(kind);
        if (spec is null)
        {
            return TranslationMessage.Of(InfrastructureKeys.UnknownKind);
        }

        var today = InfrastructureCommandSupport.Day(command.IssuedOn);
        if (today.Year < spec.UnlockYear)
        {
            return TranslationMessage.Of(InfrastructureKeys.EraNotReached);
        }

        var existing = _book.Section.Find(organization, kind);
        if (existing?.IsBuilding == true)
        {
            return TranslationMessage.Of(InfrastructureKeys.AlreadyBuilding);
        }

        var quality = existing?.QualityMilli ?? 0;
        var cost = InfrastructureMath.UpgradeCostCents(quality, today.Year, _book.Finance.TypicalCents);
        if (!_book.Finance.HasBook(organization) || _book.Finance.BalanceOf(organization) < cost)
        {
            return TranslationMessage.Of(InfrastructureKeys.NoMoney);
        }

        return null;
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(UpgradeFacilityCommand command, CommandContext context)
    {
        if (ValidateTyped(command, context) is not null
            || !CarCommandSupport.TryOrganization(command.OrganizationId, out var organization)
            || !FacilityKindIds.TryParse(command.Kind, out var kind))
        {
            throw new InvalidOperationException("Execute ran for a command that should have been rejected.");
        }

        var today = InfrastructureCommandSupport.Day(command.IssuedOn);
        var existing = _book.Section.Find(organization, kind)
            ?? new Facility(organization, kind, 0, null, null, null, 0);
        var target = InfrastructureMath.AfterUpgradeMilli(existing.QualityMilli, today.Year);
        var cost = InfrastructureMath.UpgradeCostCents(existing.QualityMilli, today.Year, _book.Finance.TypicalCents);
        var days = InfrastructureMath.UpgradeDays(existing.QualityMilli, today.Year);
        var ends = today.AddDays(days);
        var updated = existing.StartBuild(target, cost, today, ends);
        var finance = _book.Finance.Post(
            organization,
            today,
            LedgerCategories.Infrastructure,
            FacilityKindIds.Of(kind),
            -cost,
            InfrastructureKeys.LedgerUpgrade);
        _book.Write(_book.Section.Upsert(updated), finance);
        return
        [
            new FacilityUpgradeStarted(
                command.ManagerId,
                command.IssuedOn,
                organization.Value,
                FacilityKindIds.Of(kind),
                new DateOnly(ends.Year, ends.Month, ends.Day)),
        ];
    }
}

public sealed class BookTestHandler : CommandHandler<BookTestCommand>
{
    private readonly InfrastructureBook _book;
    private readonly InfrastructureEnvironment _environment;

    public BookTestHandler(InfrastructureBook book, InfrastructureEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
    }

    protected override TranslationMessage? ValidateTyped(BookTestCommand command, CommandContext context)
    {
        var gate = InfrastructureCommandSupport.Team(
            _environment,
            _book.World,
            command.ManagerId,
            command.OrganizationId,
            out var organization);
        if (gate is not null)
        {
            return gate;
        }

        var today = InfrastructureCommandSupport.Day(command.IssuedOn);
        var allowed = InfrastructureMath.TestsAllowed(_environment.TestingRule(today.Year));
        if (allowed <= 0)
        {
            return TranslationMessage.Of(InfrastructureKeys.TestsBanned);
        }

        if (_book.Section.TestsUsed(organization, today.Year) >= allowed)
        {
            return TranslationMessage.Of(InfrastructureKeys.NoTestsLeft);
        }

        if (_book.Cars.Of(organization).Count == 0)
        {
            return TranslationMessage.Of(InfrastructureKeys.NoCars);
        }

        var cost = InfrastructureMath.TestRentalCents(_book.Finance.TypicalCents);
        if (!_book.Finance.HasBook(organization) || _book.Finance.BalanceOf(organization) < cost)
        {
            return TranslationMessage.Of(InfrastructureKeys.NoMoneyTest);
        }

        return null;
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(BookTestCommand command, CommandContext context)
    {
        if (ValidateTyped(command, context) is not null
            || !CarCommandSupport.TryOrganization(command.OrganizationId, out var organization))
        {
            throw new InvalidOperationException("Execute ran for a command that should have been rejected.");
        }

        var today = InfrastructureCommandSupport.Day(command.IssuedOn);
        var cost = InfrastructureMath.TestRentalCents(_book.Finance.TypicalCents);
        var booking = new TestBooking(organization, InfrastructureMath.TestDate(today), cost);
        _book.Write(_book.Section.AddTest(booking));
        return
        [
            new TestBooked(command.ManagerId, command.IssuedOn, organization.Value, cost),
        ];
    }
}

public sealed class CancelTestHandler : CommandHandler<CancelTestCommand>
{
    private readonly InfrastructureBook _book;
    private readonly InfrastructureEnvironment _environment;

    public CancelTestHandler(InfrastructureBook book, InfrastructureEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(environment);
        _book = book;
        _environment = environment;
    }

    protected override TranslationMessage? ValidateTyped(CancelTestCommand command, CommandContext context)
    {
        var gate = InfrastructureCommandSupport.Team(
            _environment,
            _book.World,
            command.ManagerId,
            command.OrganizationId,
            out var organization);
        if (gate is not null)
        {
            return gate;
        }

        return Find(organization, command) is null ? TranslationMessage.Of(InfrastructureKeys.NoSuchTest) : null;
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(CancelTestCommand command, CommandContext context)
    {
        if (ValidateTyped(command, context) is not null
            || !CarCommandSupport.TryOrganization(command.OrganizationId, out var organization)
            || Find(organization, command) is not { } booking)
        {
            throw new InvalidOperationException("Execute ran for a command that should have been rejected.");
        }

        _book.Write(_book.Section.RemoveTest(booking));
        return [new TestCancelled(command.ManagerId, command.IssuedOn, organization.Value, command.TestOn)];
    }

    /// <summary>The booking on that day that has not happened yet. A test on the day itself, or earlier, is no longer cancellable.</summary>
    private TestBooking? Find(OrganizationId organization, CancelTestCommand command)
    {
        var today = InfrastructureCommandSupport.Day(command.IssuedOn);
        var on = InfrastructureCommandSupport.Day(command.TestOn);
        if (on <= today)
        {
            return null;
        }

        return _book.Section.Tests.FirstOrDefault(test => test.Organization == organization && test.Date == on);
    }
}

public static class InfrastructureRegistration
{
    public static void Register(CommandDispatcher dispatcher, InfrastructureBook book, InfrastructureEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        dispatcher.Register(new UpgradeFacilityHandler(book, environment));
        dispatcher.Register(new BookTestHandler(book, environment));
        dispatcher.Register(new CancelTestHandler(book, environment));
    }
}
