using Paddock.Application.Commands;
using Paddock.Application.Managers;
using Paddock.Domain.Pool;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Pool;

/// <summary>
/// How a junior programme is paid for. The finance ledger (T37) is not built yet, so the pool talks to this small port and T37
/// supplies the real one: the cost of a programme, whether the organization can afford it, and the ledger entry.
/// <see cref="Validate"/> is a query (INV-005); <see cref="Charge"/> runs only inside an accepted command (INV-001).
/// </summary>
public interface IJuniorFunding
{
    /// <summary>The price of one season of the programme, in the units of the economy.</summary>
    long Cost(JuniorProgramme programme);

    /// <summary>Null when <paramref name="payer"/> may pay <paramref name="amount"/> on that date, otherwise the reason they may not.</summary>
    TranslationMessage? Validate(OrganizationId payer, long amount, GameDate on);

    /// <summary>Takes the money. <paramref name="reasonKey"/> is the ledger text key (<see cref="PoolKeys.FundingReason"/>).</summary>
    void Charge(OrganizationId payer, long amount, string reasonKey, GameDate on);
}

/// <summary>
/// The stand-in until T37: prices come from <see cref="PoolEstimates"/>, everyone can pay, and nothing is recorded.
/// A junior programme is therefore free in play until finance exists. Replace it, do not extend it.
/// </summary>
public sealed class UnmeteredJuniorFunding : IJuniorFunding
{
    public static UnmeteredJuniorFunding Instance { get; } = new();

    public long Cost(JuniorProgramme programme) => PoolEstimates.CostOf(programme);

    public TranslationMessage? Validate(OrganizationId payer, long amount, GameDate on) => null;

    public void Charge(OrganizationId payer, long amount, string reasonKey, GameDate on)
    {
    }
}

/// <summary>
/// Where signing a pool driver goes: it starts a contract negotiation with a test or a junior role. The negotiation and the
/// contract belong to T39, which implements this port; the pool does not look inside it. When a contract is signed, the pool
/// notices on the next day (a member with a contract has left the pool).
/// <see cref="ValidateStart"/> is a query (INV-005).
/// </summary>
public interface IPoolNegotiations
{
    /// <summary>Null when a negotiation may start, otherwise the reason it may not.</summary>
    TranslationMessage? ValidateStart(OrganizationId organization, PersonId person, PoolSigningRole role, GameDate on);

    /// <summary>Starts the negotiation and returns the events it produced.</summary>
    IReadOnlyList<IDomainEvent> Start(ManagerId manager, OrganizationId organization, PersonId person, PoolSigningRole role, GameDate on);
}

/// <summary>The stand-in until T39: nobody can start a negotiation.</summary>
public sealed class UnavailableNegotiations : IPoolNegotiations
{
    public static UnavailableNegotiations Instance { get; } = new();

    public TranslationMessage? ValidateStart(OrganizationId organization, PersonId person, PoolSigningRole role, GameDate on) =>
        TranslationMessage.Of(PoolKeys.SigningUnavailable);

    public IReadOnlyList<IDomainEvent> Start(ManagerId manager, OrganizationId organization, PersonId person, PoolSigningRole role, GameDate on) =>
        throw new InvalidOperationException("Execute ran for a command that should have been rejected.");
}
