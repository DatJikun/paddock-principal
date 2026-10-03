using Paddock.Domain.People;
using Paddock.Domain.Time;

namespace Paddock.Domain.World;

/// <summary>Seat status on a driver contract (DESIGN §10).</summary>
public enum SeatStatus
{
    NumberOne = 0,
    Equal = 1,
    NumberTwo = 2,
    Reserve = 3,
}

/// <summary>
/// The job a contract covers: a driver seat, or a staff role from DESIGN §6.2
/// (team principal is <see cref="StaffRole.TeamPrincipal"/>).
/// The default value is unassigned.
/// </summary>
public readonly record struct ContractRole
{
    private readonly bool _assigned;
    private readonly bool _driver;
    private readonly SeatStatus _seat;
    private readonly StaffRole _staff;

    private ContractRole(bool driver, SeatStatus seat, StaffRole staff)
    {
        _assigned = true;
        _driver = driver;
        _seat = seat;
        _staff = staff;
    }

    public bool IsAssigned => _assigned;

    public bool IsDriver => _assigned && _driver;

    public bool IsStaff => _assigned && !_driver;

    public SeatStatus Seat => IsDriver
        ? _seat
        : throw new InvalidOperationException("This contract is not a driver seat.");

    public StaffRole StaffRole => IsStaff
        ? _staff
        : throw new InvalidOperationException("This contract is not a staff role.");

    public static ContractRole Driver(SeatStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown seat status.");
        }

        return new ContractRole(true, status, default);
    }

    public static ContractRole Staff(StaffRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown staff role.");
        }

        return new ContractRole(false, default, role);
    }

    public override string ToString()
    {
        if (!_assigned)
        {
            return string.Empty;
        }

        return _driver ? "driver:" + _seat.ToString() : "staff:" + _staff.ToString();
    }
}

/// <summary>
/// Optional extension option. <see cref="ExtraYears"/> is a whole number of seasons, an estimate of length, not a price.
/// </summary>
public readonly record struct ContractOption
{
    public ContractOption(GameDate deadline, int extraYears)
    {
        if (extraYears < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(extraYears), extraYears, "An option extends the contract by at least one year.");
        }

        Deadline = deadline;
        ExtraYears = extraYears;
    }

    public GameDate Deadline { get; }

    public int ExtraYears { get; }
}

/// <summary>
/// Optional buyout. <see cref="Amount"/> is a nominal whole number of currency units (a placeholder estimate).
/// </summary>
public readonly record struct ReleaseClause
{
    public ReleaseClause(long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "A release amount cannot be negative.");
        }

        Amount = amount;
    }

    public long Amount { get; }
}

/// <summary>
/// Input for <see cref="WorldState.AddContract"/>. The world assigns the id.
/// </summary>
public sealed class ContractSpec
{
    public ContractSpec(
        PersonId personId,
        OrganizationId organizationId,
        ContractRole role,
        GameDate start,
        GameDate end,
        long salary,
        bool exclusive,
        ContractOption? option,
        ReleaseClause? releaseClause)
    {
        if (!personId.IsAssigned)
        {
            throw new ArgumentException("Person id is unassigned.", nameof(personId));
        }

        if (!organizationId.IsAssigned)
        {
            throw new ArgumentException("Organization id is unassigned.", nameof(organizationId));
        }

        if (!role.IsAssigned)
        {
            throw new ArgumentException("Contract role is unassigned.", nameof(role));
        }

        if (end < start)
        {
            throw new ArgumentException("A contract ends before it starts.", nameof(end));
        }

        if (salary < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(salary), salary, "Salary cannot be negative.");
        }

        if (option is ContractOption clause && (clause.Deadline < start || clause.Deadline > end))
        {
            throw new ArgumentException("An option deadline falls outside the contract.", nameof(option));
        }

        PersonId = personId;
        OrganizationId = organizationId;
        Role = role;
        Start = start;
        End = end;
        Salary = salary;
        Exclusive = exclusive;
        Option = option;
        ReleaseClause = releaseClause;
    }

    public PersonId PersonId { get; }

    public OrganizationId OrganizationId { get; }

    public ContractRole Role { get; }

    public GameDate Start { get; }

    public GameDate End { get; }

    /// <summary>Nominal currency units. A placeholder estimate. See <see cref="WorldText.SalaryExplanation"/>.</summary>
    public long Salary { get; }

    public bool Exclusive { get; }

    public ContractOption? Option { get; }

    public ReleaseClause? ReleaseClause { get; }
}

/// <summary>
/// A contract between a person and an organization.
/// Salary is a nominal placeholder. Option and release clause are optional.
/// </summary>
public sealed class Contract
{
    public Contract(
        ContractId id,
        PersonId personId,
        OrganizationId organizationId,
        ContractRole role,
        GameDate start,
        GameDate end,
        long salary,
        bool exclusive,
        ContractOption? option,
        ReleaseClause? releaseClause)
    {
        if (!id.IsAssigned)
        {
            throw new ArgumentException("Contract id is unassigned.", nameof(id));
        }

        if (!personId.IsAssigned)
        {
            throw new ArgumentException("Person id is unassigned.", nameof(personId));
        }

        if (!organizationId.IsAssigned)
        {
            throw new ArgumentException("Organization id is unassigned.", nameof(organizationId));
        }

        if (!role.IsAssigned)
        {
            throw new ArgumentException("Contract role is unassigned.", nameof(role));
        }

        if (end < start)
        {
            throw new ArgumentException("A contract ends before it starts.", nameof(end));
        }

        if (salary < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(salary), salary, "Salary cannot be negative.");
        }

        if (option is ContractOption clause && (clause.Deadline < start || clause.Deadline > end))
        {
            throw new ArgumentException("An option deadline falls outside the contract.", nameof(option));
        }

        Id = id;
        PersonId = personId;
        OrganizationId = organizationId;
        Role = role;
        Start = start;
        End = end;
        Salary = salary;
        Exclusive = exclusive;
        Option = option;
        ReleaseClause = releaseClause;
    }

    public ContractId Id { get; }

    public PersonId PersonId { get; }

    public OrganizationId OrganizationId { get; }

    public ContractRole Role { get; }

    public GameDate Start { get; }

    public GameDate End { get; }

    /// <summary>Nominal currency units. A placeholder estimate. See <see cref="WorldText.SalaryExplanation"/>.</summary>
    public long Salary { get; }

    public bool Exclusive { get; }

    public ContractOption? Option { get; }

    public ReleaseClause? ReleaseClause { get; }

    public bool IsActiveOn(GameDate date) => Start <= date && date <= End;

    /// <summary>
    /// True when both contracts are exclusive, bind the same person, and share at least one day.
    /// A person may not hold two such contracts. Touching only at a boundary day counts as an overlap.
    /// </summary>
    public bool ExclusivelyOverlaps(Contract other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (Id == other.Id || PersonId != other.PersonId || !Exclusive || !other.Exclusive)
        {
            return false;
        }

        return Start <= other.End && other.Start <= End;
    }
}
