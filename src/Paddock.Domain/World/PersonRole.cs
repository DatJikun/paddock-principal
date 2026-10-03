using Paddock.Domain.People;

namespace Paddock.Domain.World;

/// <summary>
/// A role a person can hold. Staff roles are DESIGN §6.2.
/// Team principal is <see cref="StaffRole.TeamPrincipal"/>, also exposed as <see cref="TeamPrincipal"/>.
/// The default value is unassigned.
/// </summary>
public readonly record struct PersonRole
{
    private readonly bool _assigned;
    private readonly bool _driver;
    private readonly StaffRole _staff;

    private PersonRole(bool driver, StaffRole staff)
    {
        _assigned = true;
        _driver = driver;
        _staff = staff;
    }

    public static PersonRole Driver => new(true, default);

    public static PersonRole TeamPrincipal => Staff(StaffRole.TeamPrincipal);

    public bool IsAssigned => _assigned;

    public bool IsDriver => _assigned && _driver;

    public bool IsStaff => _assigned && !_driver;

    public StaffRole StaffRole => IsStaff
        ? _staff
        : throw new InvalidOperationException("This role is not a staff role.");

    public static PersonRole Staff(StaffRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown staff role.");
        }

        return new PersonRole(false, role);
    }

    public override string ToString()
    {
        if (!_assigned)
        {
            return string.Empty;
        }

        return _driver ? "driver" : "staff:" + _staff.ToString();
    }
}
