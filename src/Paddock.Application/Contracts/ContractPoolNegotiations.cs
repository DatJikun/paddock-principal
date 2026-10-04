using Paddock.Application.Commands;
using Paddock.Application.Managers;
using Paddock.Application.Pool;
using Paddock.Domain.Contracts;
using Paddock.Domain.Pool;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Contracts;

/// <summary>
/// <see cref="IPoolNegotiations"/> on top of the contract engine. A pool signing opens a driver-seat negotiation and puts the
/// first offer on the table. ESTIMATE: a test role is a reserve seat and a junior role is a number-two seat, both for one season
/// at the era reference salary. T44 replaces the offer.
/// </summary>
public sealed class ContractPoolNegotiations : IPoolNegotiations
{
    /// <summary>ESTIMATE: seasons of the first offer made to a pool driver.</summary>
    public const int OfferYears = 1;

    private readonly ContractEngine _engine;

    public ContractPoolNegotiations(ContractEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    public TranslationMessage? ValidateStart(OrganizationId organization, PersonId person, PoolSigningRole role, GameDate on)
    {
        if (!Enum.IsDefined(role))
        {
            return TranslationMessage.Of(PoolKeys.PersonUnknown);
        }

        if (Controller(organization) is not ManagerId manager)
        {
            return TranslationMessage.Of(ContractKeys.NotController);
        }

        return _engine.ValidateOpen(manager, organization, person, NegotiationSubject.DriverSeat, on, deadline: null, renewalOf: null);
    }

    public IReadOnlyList<IDomainEvent> Start(ManagerId manager, OrganizationId organization, PersonId person, PoolSigningRole role, GameDate on)
    {
        var (negotiation, opened) = _engine.Open(manager, organization, person, NegotiationSubject.DriverSeat, on, deadline: null, renewalOf: null);
        var salary = _engine.Book.ReferenceSalary(organization, person, NegotiationSubject.DriverSeat, on);
        var terms = new OfferTerms(salary, 0, 0, 0, OfferYears, SeatOf(role), option: null, exit: null);
        var events = new List<IDomainEvent>(opened);
        events.AddRange(_engine.Submit(manager, negotiation.Id, terms, on));
        return events;
    }

    private ManagerId? Controller(OrganizationId organization)
    {
        var managers = _engine.Book.Environment.Control.ManagersOf(organization);
        return managers.Count == 0 ? null : managers[0];
    }

    private static SeatStatus SeatOf(PoolSigningRole role) =>
        role == PoolSigningRole.Test ? SeatStatus.Reserve : SeatStatus.NumberTwo;
}
