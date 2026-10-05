using Paddock.Domain.People;
using Paddock.Domain.Time;

namespace Paddock.Domain.World;

/// <summary>
/// Input for <see cref="WorldState.AddPerson"/>. The world assigns the id.
/// </summary>
public sealed class PersonSpec
{
    public PersonSpec(
        string givenName,
        string familyName,
        GameDate birthDate,
        string nationality,
        bool isReal,
        string? realId,
        IReadOnlyList<PersonRole> roles,
        PersonTruth truth)
    {
        GivenName = DisplayText.Require(givenName, nameof(givenName));
        FamilyName = DisplayText.Require(familyName, nameof(familyName));
        if (birthDate.Year < GenerationEstimates.MinBirthYear || birthDate.Year > GenerationEstimates.MaxBirthYear)
        {
            throw new ArgumentOutOfRangeException(nameof(birthDate), birthDate, "Birth year is outside the supported range.");
        }

        BirthDate = birthDate;
        Nationality = DisplayText.Require(nationality, nameof(nationality));
        if (isReal == string.IsNullOrWhiteSpace(realId))
        {
            throw new ArgumentException(
                "A real person needs an authored id, and a generated person must not have one.",
                nameof(realId));
        }

        IsReal = isReal;
        RealId = isReal ? IdText.RequireReal(realId!, nameof(realId)) : null;
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(truth);
        Roles = roles;
        Truth = truth;
    }

    public string GivenName { get; }

    public string FamilyName { get; }

    public GameDate BirthDate { get; }

    public string Nationality { get; }

    public bool IsReal { get; }

    public string? RealId { get; }

    public IReadOnlyList<PersonRole> Roles { get; }

    public PersonTruth Truth { get; }
}

/// <summary>
/// A person in the world: identity, roles held, and simulation truth.
/// Knowledge about this person is stored per organization, not here.
/// A person who has left the sport keeps the record and gets <see cref="RetiredOn"/>; they are never deleted
/// (TECH §6.2: real people always stay).
/// </summary>
public sealed class Person
{
    public Person(
        PersonId id,
        string givenName,
        string familyName,
        GameDate birthDate,
        string nationality,
        bool isReal,
        IReadOnlyList<PersonRole> roles,
        PersonTruth truth,
        GameDate? retiredOn = null,
        GameDate? injuredUntil = null)
    {
        if (!id.IsAssigned)
        {
            throw new ArgumentException("Person id is unassigned.", nameof(id));
        }

        if (id.IsReal != isReal)
        {
            throw new ArgumentException("IsReal does not match the id.", nameof(isReal));
        }

        Id = id;
        GivenName = DisplayText.Require(givenName, nameof(givenName));
        FamilyName = DisplayText.Require(familyName, nameof(familyName));
        if (birthDate.Year < GenerationEstimates.MinBirthYear || birthDate.Year > GenerationEstimates.MaxBirthYear)
        {
            throw new ArgumentOutOfRangeException(nameof(birthDate), birthDate, "Birth year is outside the supported range.");
        }

        BirthDate = birthDate;
        Nationality = DisplayText.Require(nationality, nameof(nationality));
        IsReal = isReal;
        Roles = CanonicalRoles(roles);
        Truth = truth ?? throw new ArgumentNullException(nameof(truth));
        if (retiredOn is GameDate left && left < birthDate)
        {
            throw new ArgumentOutOfRangeException(nameof(retiredOn), retiredOn, "A person cannot retire before they are born.");
        }

        if (injuredUntil is GameDate injured && injured < birthDate)
        {
            throw new ArgumentOutOfRangeException(nameof(injuredUntil), injuredUntil, "A person cannot be injured before they are born.");
        }

        RetiredOn = retiredOn;
        InjuredUntil = injuredUntil;
    }

    public PersonId Id { get; }

    public string GivenName { get; }

    public string FamilyName { get; }

    public string Name => GivenName + " " + FamilyName;

    public GameDate BirthDate { get; }

    public string Nationality { get; }

    public bool IsReal { get; }

    public IReadOnlyList<PersonRole> Roles { get; }

    public PersonTruth Truth { get; }

    /// <summary>The day the person left the sport, or null while they are active.</summary>
    public GameDate? RetiredOn { get; }

    public bool IsRetired => RetiredOn is not null;

    /// <summary>The date the person is injured until (unable to race), or null while they are healthy (PP-061).</summary>
    public GameDate? InjuredUntil { get; }

    public bool IsInjured(GameDate on) => InjuredUntil is not null && on <= InjuredUntil;

    /// <summary>The same person with other simulation truth (development moves it). Everything else, retirement and injury included, is kept.</summary>
    internal Person WithTruth(PersonTruth truth) =>
        new(Id, GivenName, FamilyName, BirthDate, Nationality, IsReal, Roles, truth, RetiredOn, InjuredUntil);

    internal Person Retire(GameDate on)
    {
        if (RetiredOn is not null)
        {
            throw new InvalidOperationException($"Person '{Id}' has already retired.");
        }

        return new Person(Id, GivenName, FamilyName, BirthDate, Nationality, IsReal, Roles, Truth, on, InjuredUntil);
    }

    internal Person Injure(GameDate until) =>
        new(Id, GivenName, FamilyName, BirthDate, Nationality, IsReal, Roles, Truth, RetiredOn, until);

    internal Person ClearInjury() =>
        new(Id, GivenName, FamilyName, BirthDate, Nationality, IsReal, Roles, Truth, RetiredOn, null);

    private static PersonRole[] CanonicalRoles(IReadOnlyList<PersonRole> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        if (roles.Count == 0)
        {
            throw new ArgumentException("A person holds at least one role.", nameof(roles));
        }

        var copy = new PersonRole[roles.Count];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < roles.Count; i++)
        {
            var role = roles[i];
            if (!role.IsAssigned)
            {
                throw new ArgumentException("A role is unassigned.", nameof(roles));
            }

            if (!seen.Add(role.ToString()))
            {
                throw new ArgumentException("Duplicate role '" + role + "'.", nameof(roles));
            }

            copy[i] = role;
        }

        Array.Sort(copy, static (left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));
        return copy;
    }
}
