namespace Paddock.Domain.Finance;

/// <summary>
/// Nominal USD stored as cents (the minor unit). Models may use doubles; the ledger stores only this integer,
/// rounded once at the boundary. One dollar is <see cref="CentsPerDollar"/> cents. Not a calibrated rate.
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    public const int CentsPerDollar = 100;

    public Money(long cents) => Cents = cents;

    public long Cents { get; }

    public static Money Zero { get; } = new(0);

    public static Money FromCents(long cents) => new(cents);

    public static Money FromDollars(long dollars) => new(checked(dollars * CentsPerDollar));

    /// <summary>Rounds a dollar amount (a double from a model) to cents, once.</summary>
    public static Money RoundDollars(double dollars) =>
        FromCents((long)Math.Round(checked(dollars * CentsPerDollar), MidpointRounding.AwayFromZero));

    /// <summary>Rounds a cent amount that a model computed as a double, once.</summary>
    public static long RoundCents(double cents) =>
        (long)Math.Round(cents, MidpointRounding.AwayFromZero);

    public long WholeDollars => Cents / CentsPerDollar;

    public int CompareTo(Money other) => Cents.CompareTo(other.Cents);

    public override string ToString() => Cents.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
