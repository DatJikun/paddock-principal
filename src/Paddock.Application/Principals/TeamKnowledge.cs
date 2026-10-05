using Paddock.Application.Access;
using Paddock.Application.Contracts;
using Paddock.Application.Finance;
using Paddock.Domain.Finance;
using Paddock.Domain.People;
using Paddock.Domain.Principals;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Simulation.Ai;
using HostManagerId = Paddock.Application.Managers.ManagerId;

namespace Paddock.Application.Principals;

/// <summary>
/// What one AI principal knows about its team on one day, assembled for the pure deciders of <c>Paddock.Simulation.Ai</c>. This is the
/// seam of INV-003: everything here is read through the queries a manager uses (<see cref="AccessContext.ForAi"/>), the organization's own
/// beliefs (<see cref="WorldState.KnowledgeOf"/>), its own books, or public facts (standings, the price list, who supplies whom). It never reads
/// <see cref="WorldState.TruthOf"/> or a person's hidden traits, and the types it hands on cannot carry them. Built once per review.
/// </summary>
internal sealed partial class TeamKnowledge
{
    private readonly PrincipalEnvironment _env;
    private AiFunds? _funds;
    private IReadOnlyList<Contract>? _contracts;

    public TeamKnowledge(PrincipalEnvironment env, WorldState world, OrganizationId organization, GameDate today)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(world);
        _env = env;
        World = world;
        Organization = organization;
        Today = today;
        Day = InboxBookDates.ToDateOnly(today);
        Manager = PrincipalKeys.ManagerOf(organization);
        Access = AccessContext.ForAi(new Paddock.Application.Access.ManagerId(Manager.Value));
    }

    public WorldState World { get; }

    public OrganizationId Organization { get; }

    public GameDate Today { get; }

    public DateOnly Day { get; }

    public HostManagerId Manager { get; }

    public AccessContext Access { get; }

    /// <summary>Every contract the team has ever signed that is still in the world (ended ones stay), in id order.</summary>
    public IReadOnlyList<Contract> Contracts => _contracts ??= World.Contracts
        .Where(contract => contract.OrganizationId == Organization)
        .OrderBy(contract => contract.Id.Value, StringComparer.Ordinal)
        .ToArray();

    // ---------------------------------------------------------------- principal

    /// <summary>The team principal under contract today, or null.</summary>
    public Person? PrincipalPerson()
    {
        foreach (var contract in Contracts)
        {
            if (contract.IsActiveOn(Today) && contract.Role.IsStaff && contract.Role.StaffRole == StaffRole.TeamPrincipal)
            {
                return World.GetPerson(contract.PersonId);
            }
        }

        return null;
    }

    /// <summary>The four attributes of the principal as the team believes them (bands), or none when it has no principal or no belief.</summary>
    public PrincipalAttributes PrincipalBeliefs(Person? principal)
    {
        if (principal is null || World.KnowledgeOf(Organization, principal.Id) is not PersonKnowledgeView belief)
        {
            return PrincipalAttributes.Unknown;
        }

        return new PrincipalAttributes(
            Band(belief, "negotiation"),
            Band(belief, "people_management"),
            Band(belief, "politics"),
            Band(belief, "business"));
    }

    private static BandOfBelief? Band(PersonKnowledgeView belief, string key)
    {
        foreach (var attribute in belief.Attributes)
        {
            if (attribute.Key == key)
            {
                return new BandOfBelief(attribute.Band.Low, attribute.Band.High);
            }
        }

        return null;
    }

    // ---------------------------------------------------------------- money

    /// <summary>The team's own books, in whole dollars. A world without finance gives a team that can pay for anything it can name.</summary>
    public AiFunds Funds()
    {
        if (_funds is not null)
        {
            return _funds;
        }

        var organization = World.GetOrganization(Organization);
        var finance = World.Section<FinanceSection>(FinanceSection.SectionName);
        var payroll = 0L;
        foreach (var contract in Contracts)
        {
            if (contract.IsActiveOn(Today))
            {
                payroll += contract.Salary;
            }
        }

        if (finance is null || !finance.HasBook(Organization))
        {
            var budget = organization.Budget > 0 ? organization.Budget : 1_000_000L;
            _funds = new AiFunds(budget * 4, 0, 0, budget, payroll);
            return _funds;
        }

        var typical = finance.TypicalCents / 100;
        if (typical <= 0)
        {
            typical = organization.Budget > 0 ? organization.Budget : 1_000_000L;
        }

        var view = FinanceQuery.Read(Access, Organization, World, Today, _env.PrimaryControl);
        if (view is not FinanceView.Own own)
        {
            _funds = new AiFunds(0, 0, 0, typical, payroll);
            return _funds;
        }

        var owed = Math.Max(0L, own.ObligationsCents - own.CertainIncomeCents) / 100;
        var allowance = (long)(typical * AiEstimates.OverdraftAllowanceShare);
        _funds = new AiFunds(own.CashCents / 100, allowance, owed, typical, payroll);
        return _funds;
    }

    // ---------------------------------------------------------------- results

    /// <summary>The constructors' position of the team in <paramref name="season"/> as the public standings give it, or null.</summary>
    public int? Position(int season) => _env.StandingsOrDefault.Position(Organization, season);

    /// <summary>The number of teams in the table.</summary>
    public int TeamCount() => Math.Max(1, World.Organizations.Count(item => item.Kind == OrganizationKind.Team && item.Dissolved is null));

    // ---------------------------------------------------------------- people as the team believes them

    public PersonView ViewOf(Person person, IReadOnlyList<string> qualityKeys)
    {
        var belief = World.KnowledgeOf(Organization, person.Id);
        return new PersonView(
            person.Id.Value,
            Today.Year - person.BirthDate.Year,
            Quality(belief, qualityKeys),
            Potential(belief));
    }

    internal static Believed Quality(PersonKnowledgeView? belief, IReadOnlyList<string> keys)
    {
        if (belief is not PersonKnowledgeView known)
        {
            return Believed.Unknown;
        }

        double low = 0;
        double high = 0;
        var count = 0;
        foreach (var attribute in known.Attributes)
        {
            if (keys.Contains(attribute.Key))
            {
                low += attribute.Band.Low;
                high += attribute.Band.High;
                count++;
            }
        }

        if (count == 0)
        {
            return Believed.Unknown;
        }

        return new Believed(
            Math.Clamp(low / count / 4.0, 0.0, 5.0),
            Math.Clamp(high / count / 4.0, 0.0, 5.0));
    }

    private static Believed Potential(PersonKnowledgeView? belief) =>
        belief is { Potential: { } band }
            ? new Believed(Math.Clamp(band.Low / 4.0, 0.0, 5.0), Math.Clamp(band.High / 4.0, 0.0, 5.0))
            : Believed.Unknown;

    internal static IReadOnlyList<string> QualityKeys(string subject) =>
        MarketSubjects.IsDriver(subject)
            ? GenerationEstimates.DriverAttributeKeys
            : StaffCatalogue.AttributeKeys(Paddock.Domain.Contracts.NegotiationSubject.Parse(subject).StaffRole);
}

/// <summary>Date conversions the principals share with the inbox book (which keeps its own internal).</summary>
internal static class InboxBookDates
{
    public static DateOnly ToDateOnly(GameDate date) => new(date.Year, date.Month, date.Day);

    public static GameDate ToGameDate(DateOnly date) => new(date.Year, date.Month, date.Day);
}
