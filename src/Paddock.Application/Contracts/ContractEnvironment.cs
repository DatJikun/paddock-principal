using Paddock.Application.Managers;
using Paddock.Domain.Contracts;
using Paddock.Domain.People;
using Paddock.Domain.Spy;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Application.Contracts;

/// <summary>
/// Which organizations a manager runs. The core never assumes one human (INV-001, PP-045): every contract command names a
/// manager, and the command is refused unless that manager runs the organization. Who runs what is host configuration, not
/// world truth, so it is not part of the world hash.
/// </summary>
public interface IOrganizationControl
{
    bool Controls(ManagerId manager, OrganizationId organization);

    /// <summary>The managers who run the organization, in ordinal order of their ids.</summary>
    IReadOnlyList<ManagerId> ManagersOf(OrganizationId organization);
}

/// <summary>A simple table of who runs what.</summary>
public sealed class ControlTable : IOrganizationControl
{
    private readonly SortedDictionary<string, SortedSet<string>> _byOrganization = new(StringComparer.Ordinal);

    public ControlTable Assign(ManagerId manager, OrganizationId organization)
    {
        if (!_byOrganization.TryGetValue(organization.Value, out var managers))
        {
            managers = new SortedSet<string>(StringComparer.Ordinal);
            _byOrganization.Add(organization.Value, managers);
        }

        managers.Add(manager.Value);
        return this;
    }

    public bool Controls(ManagerId manager, OrganizationId organization) =>
        manager.IsAssigned
        && organization.IsAssigned
        && _byOrganization.TryGetValue(organization.Value, out var managers)
        && managers.Contains(manager.Value);

    public IReadOnlyList<ManagerId> ManagersOf(OrganizationId organization) =>
        organization.IsAssigned && _byOrganization.TryGetValue(organization.Value, out var managers)
            ? managers.Select(id => new ManagerId(id)).ToArray()
            : [];
}

/// <summary>
/// What an organization can pay. Finance (T37, issue #102) is not available yet, so this is the minimal port the contract
/// system needs: can the organization carry a new salary, can it pay a termination, and record that it did. When T37 lands, its
/// ledger implements this interface; nothing else here changes.
/// </summary>
public interface IPayrollLedger
{
    /// <summary>True when the organization can commit to <paramref name="annualSalary"/> per season for <paramref name="years"/> seasons.</summary>
    bool CanCommit(OrganizationId organization, long annualSalary, int years, GameDate on);

    /// <summary>True when the organization can pay <paramref name="amount"/> now.</summary>
    bool CanPay(OrganizationId organization, long amount, GameDate on);

    /// <summary>Records that the organization paid compensation to a person for an early termination.</summary>
    void RecordCompensation(OrganizationId payer, PersonId payee, long amount, GameDate on);
}

/// <summary>Every organization can afford everything and nothing is recorded. The default until finance exists.</summary>
public sealed class UnlimitedPayroll : IPayrollLedger
{
    public bool CanCommit(OrganizationId organization, long annualSalary, int years, GameDate on) => true;

    public bool CanPay(OrganizationId organization, long amount, GameDate on) => true;

    public void RecordCompensation(OrganizationId payer, PersonId payee, long amount, GameDate on)
    {
    }
}

/// <summary>
/// Constructors' standings after a race, for the exit-clause check. The standings system is not wired to contracts yet; a pure
/// query (INV-005).
/// </summary>
public interface IConstructorStandings
{
    /// <summary>The position (1 is best) of the organization in the season's constructors' championship, or null when unknown.</summary>
    int? Position(OrganizationId organization, int season);
}

/// <summary>No standings are known, so no exit clause triggers.</summary>
public sealed class NoStandings : IConstructorStandings
{
    public int? Position(OrganizationId organization, int season) => null;
}

/// <summary>The negotiation skill of an organization's team principal (DESIGN section 6.2, PP-044), which sets how many talks it can run.</summary>
public interface IPrincipalSkills
{
    /// <summary>The negotiation attribute of the organization's principal as the organization knows it, 0 when there is none.</summary>
    int Negotiation(WorldState world, OrganizationId organization);
}

/// <summary>
/// Reads the middle of the band the organization believes for its own principal's <c>negotiation</c> attribute. Only the
/// organization's belief is read, never the true value (INV-003). Zero when it has no principal or no belief.
/// </summary>
public sealed class KnownPrincipalSkills : IPrincipalSkills
{
    private const string NegotiationKey = "negotiation";

    public int Negotiation(WorldState world, OrganizationId organization)
    {
        ArgumentNullException.ThrowIfNull(world);
        var today = world.CurrentDate;
        foreach (var contract in world.Contracts)
        {
            if (contract.OrganizationId != organization
                || !contract.IsActiveOn(today)
                || !contract.Role.IsStaff
                || contract.Role.StaffRole != StaffRole.TeamPrincipal)
            {
                continue;
            }

            var knowledge = world.KnowledgeOf(organization, contract.PersonId);
            if (knowledge is PersonKnowledgeView view)
            {
                foreach (var attribute in view.Attributes)
                {
                    if (attribute.Key == NegotiationKey)
                    {
                        return (attribute.Band.Low + attribute.Band.High) / 2;
                    }
                }
            }
        }

        return 0;
    }
}

/// <summary>
/// The ports of the contract system, with a neutral default for each. A host replaces the ones it has a real system for
/// (standings, finance, trust, personality). <see cref="Personality"/> and <see cref="Pay"/> have no sensible neutral and are required.
/// </summary>
public sealed class ContractEnvironment
{
    public ContractEnvironment(
        IPersonalitySource personality,
        IPayBenchmark pay,
        IOrganizationControl? control = null,
        IOrganizationAppealSource? appeal = null,
        ITrustSource? trust = null,
        IPayrollLedger? payroll = null,
        IConstructorStandings? standings = null,
        IPrincipalSkills? principal = null,
        ITraceSink? trace = null)
    {
        ArgumentNullException.ThrowIfNull(personality);
        ArgumentNullException.ThrowIfNull(pay);
        Personality = personality;
        Pay = pay;
        Control = control ?? new ControlTable();
        Appeal = appeal ?? new NeutralAppealSource();
        Trust = trust ?? new NeutralTrustSource();
        Payroll = payroll ?? new UnlimitedPayroll();
        Standings = standings ?? new NoStandings();
        Principal = principal ?? new KnownPrincipalSkills();
        Trace = trace ?? NullSink.Instance;
    }

    public IPersonalitySource Personality { get; }

    public IPayBenchmark Pay { get; }

    public IOrganizationControl Control { get; }

    public IOrganizationAppealSource Appeal { get; }

    public ITrustSource Trust { get; }

    public IPayrollLedger Payroll { get; }

    public IConstructorStandings Standings { get; }

    public IPrincipalSkills Principal { get; }

    /// <summary>Receives a trace of every decision a person makes. A passive observer (INV-006).</summary>
    public ITraceSink Trace { get; }
}
