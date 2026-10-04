using System.Globalization;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Pool;

/// <summary>The two programmes an organization can pay for to develop a junior "somewhere lower" (DESIGN 2.1).</summary>
public enum JuniorProgramme
{
    CheapSlow = 0,
    ExpensiveFast = 1,
}

/// <summary>The role a pool driver is offered when a negotiation is started for him. Contracts themselves belong to T39.</summary>
public enum PoolSigningRole
{
    Test = 0,
    Junior = 1,
}

/// <summary>What an organization's scouts concentrate on.</summary>
public enum ScoutFocusKind
{
    /// <summary>Everyone in the pool, slowly.</summary>
    Pool = 0,

    /// <summary>One person, faster.</summary>
    Person = 1,
}

/// <summary>A season of a junior programme that an organization has paid for. It acts on the next season-start development roll.</summary>
public sealed record JuniorFunding
{
    public JuniorFunding(OrganizationId funder, JuniorProgramme programme, int season)
    {
        if (!funder.IsAssigned)
        {
            throw new ArgumentException("The funder is unassigned.", nameof(funder));
        }

        if (!Enum.IsDefined(programme))
        {
            throw new ArgumentOutOfRangeException(nameof(programme), programme, "Unknown junior programme.");
        }

        Funder = funder;
        Programme = programme;
        Season = season;
    }

    public OrganizationId Funder { get; }

    public JuniorProgramme Programme { get; }

    /// <summary>The season (calendar year) the money was paid in.</summary>
    public int Season { get; }
}

/// <summary>
/// One person in the pool. <see cref="Handle"/> is the opaque name a manager or an AI uses for him: it comes from the
/// section's own counter, so it says nothing about whether the person is a real driver or a filler (INV-003, PP-018).
/// </summary>
public sealed record PoolMember
{
    public PoolMember(PersonId id, long handle, GameDate enteredOn, JuniorFunding? funding)
    {
        if (!id.IsAssigned)
        {
            throw new ArgumentException("The person id is unassigned.", nameof(id));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(handle, 1);
        Id = id;
        Handle = handle;
        EnteredOn = enteredOn;
        Funding = funding;
    }

    public PersonId Id { get; }

    public long Handle { get; }

    public GameDate EnteredOn { get; }

    public JuniorFunding? Funding { get; }

    public string HandleText => TalentPoolSection.HandleOf(Handle);
}

/// <summary>The focus one organization has set for its scouts.</summary>
public sealed record ScoutFocus
{
    public ScoutFocus(OrganizationId organization, ScoutFocusKind kind, PersonId? person)
    {
        if (!organization.IsAssigned)
        {
            throw new ArgumentException("The organization is unassigned.", nameof(organization));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown focus kind.");
        }

        if ((kind == ScoutFocusKind.Person) != (person is PersonId subject && subject.IsAssigned))
        {
            throw new ArgumentException("A person focus names one person, and a pool focus names none.", nameof(person));
        }

        Organization = organization;
        Kind = kind;
        Person = person;
    }

    public OrganizationId Organization { get; }

    public ScoutFocusKind Kind { get; }

    public PersonId? Person { get; }
}

/// <summary>Observation points (thousandths) one organization has accumulated on one pool member.</summary>
public sealed record ObservationRow
{
    public ObservationRow(OrganizationId organization, PersonId person, long milli)
    {
        if (!organization.IsAssigned || !person.IsAssigned)
        {
            throw new ArgumentException("An observation needs an organization and a person.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(milli);
        Organization = organization;
        Person = person;
        Milli = milli;
    }

    public OrganizationId Organization { get; }

    public PersonId Person { get; }

    public long Milli { get; }
}

/// <summary>A pool member who aged out without a contract. Kept so the chronicle can find him and so he never re-enters.</summary>
public sealed record LapsedCareer(PersonId Id, GameDate On);

/// <summary>Helper for the canonical text of the pool.</summary>
internal static class PoolText
{
    public static string Invariant(long value) => value.ToString(CultureInfo.InvariantCulture);
}
