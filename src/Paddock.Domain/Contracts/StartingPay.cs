using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Contracts;

/// <summary>
/// The salary of a contract the world writes itself, with no negotiation behind it: the contracts of a career's first morning and
/// a chair filled from the generated pool (#318). Before this every such contract was paid 0 ("not modelled"), so a 2017 Ferrari
/// driver earned nothing until the player renewed him. The salary is the era benchmark (<see cref="IPayBenchmark"/>, the same
/// scale the negotiations use) at the stars of the person's true attributes. That is the world being built, the way a real
/// contract was signed before the career began; no actor reads the truth here, and nobody sees it except as the salary a
/// contract already carries. The rounding is an ESTIMATE.
/// </summary>
public static class StartingPay
{
    /// <summary>ESTIMATE: a starting salary keeps this many significant digits, so a list does not show 33 241 517.</summary>
    public const int SignificantDigits = 2;

    /// <summary>
    /// Every unpaid driver or staff contract that runs on or after <paramref name="on"/> gets the era salary of its holder.
    /// A contract that already has a salary is left alone, and so is the team principal (the board pays the principal).
    /// Pure and order-independent: no RNG, the same world gives the same salaries.
    /// </summary>
    public static WorldState PayUnpaid(WorldState world, IPayBenchmark pay, GameDate on)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(pay);
        var unpaid = world.Contracts
            .Where(contract => contract.Salary == 0 && contract.End >= on && Subject(contract) is not null)
            .OrderBy(contract => contract.Id.Value, StringComparer.Ordinal)
            .ToArray();
        foreach (var contract in unpaid)
        {
            var subject = Subject(contract)!.Value;
            var salary = For(pay, Math.Max(on.Year, contract.Start.Year), subject, world.GetPerson(contract.PersonId).Truth);
            world = world.WithContractSalary(contract.Id, salary);
        }

        return world;
    }

    /// <summary>The era salary for one person in one role: the benchmark at the stars of the true attributes the role uses.</summary>
    public static long For(IPayBenchmark pay, int season, NegotiationSubject subject, PersonTruth truth)
    {
        ArgumentNullException.ThrowIfNull(pay);
        ArgumentNullException.ThrowIfNull(truth);
        IReadOnlyList<string> keys = subject.Kind == NegotiationSubjectKind.DriverSeat
            ? GenerationEstimates.DriverAttributeKeys
            : StaffCatalogue.AttributeKeys(subject.StaffRole);
        var values = truth.Attributes.Where(attribute => keys.Contains(attribute.Key)).Select(attribute => (double)attribute.Value).ToArray();
        var stars = values.Length == 0 ? NegotiationEstimates.UnknownStars : values.Average() / 4.0;
        return Round(pay.Reference(season, subject, stars));
    }

    /// <summary>Keeps <see cref="SignificantDigits"/> significant digits, at least 1.</summary>
    public static long Round(long amount)
    {
        if (amount <= 0)
        {
            return 1;
        }

        var step = 1L;
        while (amount / step >= Pow10(SignificantDigits))
        {
            step *= 10;
        }

        return Math.Max(1L, (long)Math.Round(amount / (double)step, MidpointRounding.AwayFromZero) * step);
    }

    private static long Pow10(int digits)
    {
        var value = 1L;
        for (var i = 0; i < digits; i++)
        {
            value *= 10;
        }

        return value;
    }

    private static NegotiationSubject? Subject(Contract contract)
    {
        if (contract.Role.IsDriver)
        {
            return NegotiationSubject.DriverSeat;
        }

        return contract.Role.IsStaff && contract.Role.StaffRole != StaffRole.TeamPrincipal
            ? NegotiationSubject.Staff(contract.Role.StaffRole)
            : null;
    }
}
