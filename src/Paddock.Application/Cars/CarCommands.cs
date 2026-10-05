using Paddock.Application.Commands;
using Paddock.Application.Contracts;
using Paddock.Application.Managers;
using Paddock.Domain.Cars;
using Paddock.Domain.Random;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Cars;

/// <summary>The live world as the car commands see it. The host owns the world; commands only replace the cars section.</summary>
public sealed class CarBook
{
    private readonly Func<WorldState> _read;
    private readonly Action<WorldState> _write;

    public CarBook(Func<WorldState> read, Action<WorldState> write, ulong masterSeed)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(write);
        _read = read;
        _write = write;
        MasterSeed = masterSeed;
    }

    public ulong MasterSeed { get; }

    public WorldState World => _read();

    public CarsSection Section => World.Section<CarsSection>(CarsSection.SectionName) ?? CarsSection.Empty;

    public void Replace(CarsSection section) => _write(World.WithSection(section));
}

/// <summary>
/// PP-050 keeps customer chassis out of the MVP. When that changes, a regulation of
/// <c>constructor_must_build_own_car</c> still refuses. A null reason means the purchase could proceed;
/// the purchase itself is not implemented.
/// </summary>
public static class CustomerChassisRules
{
    public const bool MvpAllowsCustomerChassis = false;

    public static string? Refusal(bool mvpAllows, bool regulationAllows)
    {
        if (!mvpAllows)
        {
            return CarKeys.CustomerNotInMvp;
        }

        return regulationAllows ? null : CarKeys.CustomerForbidden;
    }
}

/// <summary>Approve an own-design concept for the manager's team. Both cars take it; drivers stay in their cars.</summary>
public sealed record ApproveConceptCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string OrganizationId { get; init; }

    public int AeroMilli { get; init; }

    public int PhilosophyMilli { get; init; }

    public int WindowMilli { get; init; }

    public int CoolingMilli { get; init; }

    public int TyreMilli { get; init; }

    public int IntegrationMilli { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

/// <summary>Buy another constructor's chassis. Refused in the MVP (PP-050) and wherever the rule forbids it.</summary>
public sealed record AcquireCustomerCarCommand : ICommand
{
    public required ManagerId ManagerId { get; init; }

    public long SubmissionNumber { get; init; }

    public required DateOnly IssuedOn { get; init; }

    public required string OrganizationId { get; init; }

    public required string SellerId { get; init; }

    public ICommand WithSubmissionNumber(long submissionNumber) => this with { SubmissionNumber = submissionNumber };
}

public sealed record ConceptApproved(ManagerId ManagerId, DateOnly OccurredOn, string OrganizationId) : IDomainEvent
{
    public string TypeId => "car.concept.approved";
}

public static class CarCommands
{
    public static void Register(CommandDispatcher dispatcher, CarBook book, IOrganizationControl control)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        dispatcher.Register(new ApproveConceptHandler(book, control));
        dispatcher.Register(new AcquireCustomerCarHandler(book, control));
    }
}

internal static class CarCommandSupport
{
    public static TranslationMessage? Team(IOrganizationControl control, WorldState world, ManagerId manager, string organizationId, out OrganizationId organization)
    {
        organization = default;
        if (!TryOrganization(organizationId, out organization))
        {
            return TranslationMessage.Of(CarKeys.UnknownOrganization);
        }

        if (!control.Controls(manager, organization))
        {
            return TranslationMessage.Of(CarKeys.NoControl);
        }

        Organization? found = null;
        foreach (var candidate in world.Organizations)
        {
            if (candidate.Id == organization)
            {
                found = candidate;
                break;
            }
        }

        if (found is null)
        {
            return TranslationMessage.Of(CarKeys.UnknownOrganization);
        }

        return found.Kind == OrganizationKind.Team ? null : TranslationMessage.Of(CarKeys.NotATeam);
    }

    public static bool TryOrganization(string text, out OrganizationId organization)
    {
        organization = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            organization = text.StartsWith("org:", StringComparison.Ordinal)
                ? OrganizationId.Generated(long.Parse(text.AsSpan(4), System.Globalization.CultureInfo.InvariantCulture))
                : OrganizationId.Real(text);
            return organization.Value == text;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            return false;
        }
    }

    public static bool TryConcept(ApproveConceptCommand command, out CarConcept concept)
    {
        concept = default;
        int[] axes = [command.AeroMilli, command.PhilosophyMilli, command.WindowMilli, command.CoolingMilli, command.TyreMilli, command.IntegrationMilli];
        foreach (var axis in axes)
        {
            if (axis is < -1000 or > 1000)
            {
                return false;
            }
        }

        concept = new CarConcept(
            command.AeroMilli / 1000d,
            command.PhilosophyMilli / 1000d,
            command.WindowMilli / 1000d,
            command.CoolingMilli / 1000d,
            command.TyreMilli / 1000d,
            command.IntegrationMilli / 1000d);
        return true;
    }
}

public sealed class ApproveConceptHandler : CommandHandler<ApproveConceptCommand>
{
    private readonly CarBook _book;
    private readonly IOrganizationControl _control;

    public ApproveConceptHandler(CarBook book, IOrganizationControl control)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(control);
        _book = book;
        _control = control;
    }

    protected override TranslationMessage? ValidateTyped(ApproveConceptCommand command, CommandContext context)
    {
        if (!CarCommandSupport.TryConcept(command, out _))
        {
            return TranslationMessage.Of(CarKeys.AxisRange);
        }

        return CarCommandSupport.Team(_control, _book.World, command.ManagerId, command.OrganizationId, out _);
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(ApproveConceptCommand command, CommandContext context)
    {
        if (!CarCommandSupport.TryConcept(command, out var concept)
            || CarCommandSupport.Team(_control, _book.World, command.ManagerId, command.OrganizationId, out var organization) is not null)
        {
            throw new InvalidOperationException("Execute ran for a command that should have been rejected.");
        }

        var today = new GameDate(command.IssuedOn.Year, command.IssuedOn.Month, command.IssuedOn.Day);
        var section = _book.Section;
        var stream = RngStream.Derive(_book.MasterSeed, RngStreamName.Development, today.Year);
        var child = stream.DeriveChild(CeilingTag(organization, today.Year, concept));
        var quality = TeamEngineers.ExecutionQuality(_book.World, organization, today);
        var (ceiling, _) = ConceptMapping.DrawPotential(child, concept.Philosophy, quality);
        var effects = ConceptMapping.Effects(concept, ceiling);
        var levels = ConceptMapping.StartingLevels(concept, ceiling);
        var existing = section.Of(organization);
        if (existing.Count == 0)
        {
            var drivers = InitialCarFactory.RaceDrivers(_book.World, organization, today);
            for (var seat = 0; seat < CarEstimates.CarsPerTeam; seat++)
            {
                PersonId? driver = seat < drivers.Count ? drivers[seat] : null;
                var created = new TeamCar(
                    CarIds.Format(section.NextCar),
                    organization,
                    today.Year,
                    concept,
                    levels,
                    ceiling,
                    CarEstimates.NewConceptUnderstanding,
                    effects.TyreWearMultiplier,
                    effects.SupplierChangeCost,
                    null,
                    driver);
                section = section.Add(created);
            }
        }
        else
        {
            foreach (var car in existing)
            {
                section = section.Replace(car.WithDesign(
                    today.Year,
                    concept,
                    levels,
                    ceiling,
                    CarEstimates.NewConceptUnderstanding,
                    effects.TyreWearMultiplier,
                    effects.SupplierChangeCost));
            }
        }

        _book.Replace(section);
        return [new ConceptApproved(command.ManagerId, command.IssuedOn, organization.Value)];
    }

    /// <summary>
    /// The ceiling draw is a child of Development tagged with the organization, the season and the concept axes.
    /// The same concept therefore draws the same ceiling; approving again cannot fish for a better one.
    /// </summary>
    private static string CeilingTag(OrganizationId organization, int season, CarConcept concept) =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{organization.Value}|ceiling|{season}|{CarEstimates.Milli(concept.Aero)}|{CarEstimates.Milli(concept.Philosophy)}|{CarEstimates.Milli(concept.Window)}|{CarEstimates.Milli(concept.Cooling)}|{CarEstimates.Milli(concept.TyreKindness)}|{CarEstimates.Milli(concept.Integration)}");
}

public sealed class AcquireCustomerCarHandler : CommandHandler<AcquireCustomerCarCommand>
{
    private readonly CarBook _book;
    private readonly IOrganizationControl _control;

    public AcquireCustomerCarHandler(CarBook book, IOrganizationControl control)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(control);
        _book = book;
        _control = control;
    }

    protected override TranslationMessage? ValidateTyped(AcquireCustomerCarCommand command, CommandContext context)
    {
        var rejection = CarCommandSupport.Team(_control, _book.World, command.ManagerId, command.OrganizationId, out _);
        if (rejection is not null)
        {
            return rejection;
        }

        if (string.IsNullOrWhiteSpace(command.SellerId))
        {
            return TranslationMessage.Of(CarKeys.UnknownOrganization);
        }

        var reason = CustomerChassisRules.Refusal(CustomerChassisRules.MvpAllowsCustomerChassis, regulationAllows: true);
        return TranslationMessage.Of(reason ?? CarKeys.CustomerForbidden);
    }

    protected override IReadOnlyList<IDomainEvent> ExecuteTyped(AcquireCustomerCarCommand command, CommandContext context) =>
        throw new InvalidOperationException("A customer car is refused before execution.");
}
