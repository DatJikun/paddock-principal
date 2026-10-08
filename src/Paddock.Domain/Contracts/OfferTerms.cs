using System.Globalization;
using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Contracts;

/// <summary>Who may use an extension option (DESIGN section 10: "team or person option").</summary>
public enum OptionHolder
{
    Team = 0,
    Person = 1,
}

/// <summary>
/// Exit clause by constructors' position: the person may leave when the team stands worse than
/// <see cref="PositionWorseThan"/> after a race (for example 5 means "below fifth place").
/// </summary>
public readonly record struct ExitClause
{
    public ExitClause(int positionWorseThan)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(positionWorseThan, 1);
        PositionWorseThan = positionWorseThan;
    }

    public int PositionWorseThan { get; }

    /// <summary>True when a team in <paramref name="position"/> (1 is best) triggers the clause.</summary>
    public bool IsTriggeredBy(int position) => position > PositionWorseThan;
}

/// <summary>An extension option as offered: who holds it and for how many extra seasons.</summary>
public readonly record struct OfferOption
{
    public OfferOption(OptionHolder holder, int extraYears)
    {
        if (!Enum.IsDefined(holder))
        {
            throw new ArgumentOutOfRangeException(nameof(holder), holder, "Unknown option holder.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(extraYears, 1);
        Holder = holder;
        ExtraYears = extraYears;
    }

    public OptionHolder Holder { get; }

    public int ExtraYears { get; }
}

/// <summary>
/// The terms one side puts on the table, which become a T15 <see cref="Contract"/> plus <see cref="ContractTerms"/> when
/// signed. Amounts are nominal currency units (a placeholder estimate, see <see cref="WorldText.SalaryExplanation"/>).
/// <see cref="Seat"/> is set for a driver seat and null for key staff. A value type in spirit: two equal terms are equal.
/// </summary>
public sealed record OfferTerms
{
    public OfferTerms(
        long salary,
        long pointsBonus,
        long winBonus,
        long titleBonus,
        int years,
        SeatStatus? seat,
        OfferOption? option,
        ExitClause? exit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(salary);
        ArgumentOutOfRangeException.ThrowIfNegative(pointsBonus);
        ArgumentOutOfRangeException.ThrowIfNegative(winBonus);
        ArgumentOutOfRangeException.ThrowIfNegative(titleBonus);
        ArgumentOutOfRangeException.ThrowIfLessThan(years, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(years, NegotiationEstimates.MaxYears);
        if (seat is SeatStatus status && !Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(seat), seat, "Unknown seat status.");
        }

        Salary = salary;
        PointsBonus = pointsBonus;
        WinBonus = winBonus;
        TitleBonus = titleBonus;
        Years = years;
        Seat = seat;
        Option = option;
        Exit = exit;
    }

    /// <summary>Salary per season.</summary>
    public long Salary { get; }

    /// <summary>Bonus for every championship point. Retired (#265): the bridge always offers 0 and no form shows it; the field stays so saves keep their shape.</summary>
    public long PointsBonus { get; }

    /// <summary>Bonus for every win.</summary>
    public long WinBonus { get; }

    /// <summary>Bonus for a title.</summary>
    public long TitleBonus { get; }

    /// <summary>Length in seasons.</summary>
    public int Years { get; }

    public SeatStatus? Seat { get; }

    public OfferOption? Option { get; }

    public ExitClause? Exit { get; }

    /// <summary>
    /// True when this offer is a real change from <paramref name="previous"/>: any structural difference (status, length,
    /// bonuses, option, exit clause) or a salary rise of at least
    /// <see cref="NegotiationEstimates.MinMeaningfulImprovementPercent"/> percent. A smaller change is nudging.
    /// </summary>
    public bool IsMeaningfulChangeFrom(OfferTerms previous)
    {
        ArgumentNullException.ThrowIfNull(previous);
        var structural = Seat != previous.Seat
            || Years != previous.Years
            || PointsBonus != previous.PointsBonus
            || WinBonus != previous.WinBonus
            || TitleBonus != previous.TitleBonus
            || Option != previous.Option
            || Exit != previous.Exit;
        if (structural)
        {
            return true;
        }

        var rise = Salary - previous.Salary;
        return rise > 0 && rise * 100 >= previous.Salary * NegotiationEstimates.MinMeaningfulImprovementPercent;
    }

    /// <summary>True when these terms fit <paramref name="subject"/>: a seat status for a driver seat, none for key staff.</summary>
    public bool Fits(NegotiationSubject subject) => subject.Kind == NegotiationSubjectKind.DriverSeat == Seat.HasValue;

    /// <summary>Writes the terms as one canonical line starting with <paramref name="label"/>.</summary>
    public void WriteCanonical(CanonicalWriter writer, string label)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentException.ThrowIfNullOrEmpty(label);
        writer.Line(
            label
            + " " + Salary.ToString(CultureInfo.InvariantCulture)
            + " " + PointsBonus.ToString(CultureInfo.InvariantCulture)
            + " " + WinBonus.ToString(CultureInfo.InvariantCulture)
            + " " + TitleBonus.ToString(CultureInfo.InvariantCulture)
            + " " + Years.ToString(CultureInfo.InvariantCulture)
            + " " + (Seat is SeatStatus seat ? seat.ToString() : "-")
            + " " + (Option is OfferOption option ? option.Holder + ":" + option.ExtraYears.ToString(CultureInfo.InvariantCulture) : "-")
            + " " + (Exit is ExitClause exit ? exit.PositionWorseThan.ToString(CultureInfo.InvariantCulture) : "-"));
    }
}

/// <summary>
/// What a signed contract carries beyond the T15 fields: bonuses, who holds the option, and the exit clause. Kept by
/// contract id in the <c>contracts</c> section. A contract with no entry (for example one the world initializer made)
/// has no bonuses and no clauses. The seat status is part of the T15 <see cref="ContractRole"/>.
/// </summary>
public sealed record ContractTerms
{
    public ContractTerms(
        ContractId contractId,
        long pointsBonus,
        long winBonus,
        long titleBonus,
        OptionHolder? optionHolder,
        ExitClause? exit)
    {
        if (!contractId.IsAssigned)
        {
            throw new ArgumentException("Contract id is unassigned.", nameof(contractId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(pointsBonus);
        ArgumentOutOfRangeException.ThrowIfNegative(winBonus);
        ArgumentOutOfRangeException.ThrowIfNegative(titleBonus);
        if (optionHolder is OptionHolder holder && !Enum.IsDefined(holder))
        {
            throw new ArgumentOutOfRangeException(nameof(optionHolder), optionHolder, "Unknown option holder.");
        }

        ContractId = contractId;
        PointsBonus = pointsBonus;
        WinBonus = winBonus;
        TitleBonus = titleBonus;
        OptionHolder = optionHolder;
        Exit = exit;
    }

    public ContractId ContractId { get; }

    public long PointsBonus { get; }

    public long WinBonus { get; }

    public long TitleBonus { get; }

    /// <summary>Who holds the extension option of the contract. Null when the contract has no option.</summary>
    public OptionHolder? OptionHolder { get; }

    public ExitClause? Exit { get; }

    /// <summary>The option for a contract that starts on <paramref name="start"/> and ends on <paramref name="end"/>, or null.</summary>
    public static ContractOption? OptionFor(OfferOption? option, GameDate start, GameDate end)
    {
        if (option is not OfferOption offered)
        {
            return null;
        }

        var deadline = end.AddDays(-NegotiationEstimates.OptionNoticeDays);
        if (deadline < start)
        {
            deadline = start;
        }

        return new ContractOption(deadline, offered.ExtraYears);
    }
}

/// <summary>What a negotiation is about. A driver seat or a key staff role; supplier deals (T43) add kinds.</summary>
public enum NegotiationSubjectKind
{
    DriverSeat = 0,
    StaffRole = 1,
}

/// <summary>The subject of a negotiation: a driver seat, or one key staff role (DESIGN section 6.2).</summary>
public readonly record struct NegotiationSubject
{
    private readonly StaffRole _staff;

    private NegotiationSubject(NegotiationSubjectKind kind, StaffRole staff)
    {
        Kind = kind;
        _staff = staff;
    }

    public NegotiationSubjectKind Kind { get; }

    public StaffRole StaffRole => Kind == NegotiationSubjectKind.StaffRole
        ? _staff
        : throw new InvalidOperationException("This subject is not a staff role.");

    /// <summary>Stable text form: <c>driver</c> or <c>staff:Role</c>.</summary>
    public string Key => Kind == NegotiationSubjectKind.DriverSeat ? "driver" : "staff:" + _staff;

    public static NegotiationSubject DriverSeat => new(NegotiationSubjectKind.DriverSeat, default);

    public static NegotiationSubject Staff(StaffRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown staff role.");
        }

        return new NegotiationSubject(NegotiationSubjectKind.StaffRole, role);
    }

    public static NegotiationSubject Parse(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key == "driver")
        {
            return DriverSeat;
        }

        const string prefix = "staff:";
        if (key.StartsWith(prefix, StringComparison.Ordinal)
            && Enum.TryParse<StaffRole>(key.AsSpan(prefix.Length), out var role)
            && Enum.IsDefined(role)
            && role.ToString() == key[prefix.Length..])
        {
            return Staff(role);
        }

        throw new ArgumentException($"'{key}' is not a negotiation subject.", nameof(key));
    }

    /// <summary>The T15 role a contract for these terms carries.</summary>
    public ContractRole ToContractRole(OfferTerms terms)
    {
        ArgumentNullException.ThrowIfNull(terms);
        return Kind == NegotiationSubjectKind.DriverSeat
            ? ContractRole.Driver(terms.Seat ?? throw new ArgumentException("A driver seat needs a seat status.", nameof(terms)))
            : ContractRole.Staff(_staff);
    }

    /// <summary>True when <paramref name="roles"/> includes the role this subject needs.</summary>
    public bool IsHeldBy(IReadOnlyList<PersonRole> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        foreach (var role in roles)
        {
            if (Kind == NegotiationSubjectKind.DriverSeat ? role.IsDriver : role.IsStaff && role.StaffRole == _staff)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when a contract of <paramref name="role"/> is about this subject.</summary>
    public bool Matches(ContractRole role) =>
        Kind == NegotiationSubjectKind.DriverSeat ? role.IsDriver : role.IsStaff && role.StaffRole == _staff;

    public override string ToString() => Key;
}
