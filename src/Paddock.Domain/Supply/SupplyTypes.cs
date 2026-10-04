using System.Globalization;

namespace Paddock.Domain.Supply;

/// <summary>What a supplier delivers (PP-030). Part-by-part suppliers (brakes, gearbox) are out of scope.</summary>
public enum SupplyItem
{
    Engine = 0,
    Tyres = 1,
    Fuel = 2,
}

/// <summary>
/// How a deal is struck (PP-030, DESIGN section 5.4). It sets which version of the supplier's product the customer receives
/// (<see cref="SupplyEstimates.LagSeasons"/>), what it costs (<see cref="SupplyEstimates.PriceMilli"/>) and the support that comes with it.
/// <see cref="Works"/>: the customer's own organization builds the item; the price is nil. <see cref="Partner"/>: factory supply, with
/// factory support. <see cref="Customer"/>: an ordinary supply. <see cref="LastYearEngine"/>: the supplier's superseded engine, cheap
/// and slow; engines only.
/// </summary>
public enum SupplyKind
{
    Works = 0,
    Partner = 1,
    Customer = 2,
    LastYearEngine = 3,
}

/// <summary>Where a deal stands. <see cref="Voided"/> is for a deal that a rule change or the end of a programme cancelled (T42 extension point).</summary>
public enum SupplyDealStatus
{
    Active = 0,
    Ended = 1,
    Voided = 2,
}

/// <summary>Stable ids of the supply section. Numbers only move forward (INV-009).</summary>
public static class SupplyIds
{
    public const string DealPrefix = "supply:";

    public const string NegotiationPrefix = "sneg:";

    public static string Deal(long number) => DealPrefix + number.ToString(CultureInfo.InvariantCulture);

    public static string Negotiation(long number) => NegotiationPrefix + number.ToString(CultureInfo.InvariantCulture);

    public static bool TryParseDeal(string? id, out long number) => TryParse(id, DealPrefix, out number);

    public static bool TryParseNegotiation(string? id, out long number) => TryParse(id, NegotiationPrefix, out number);

    private static bool TryParse(string? id, string prefix, out long number)
    {
        number = 0;
        if (id is null || !id.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var tail = id.AsSpan(prefix.Length);
        if (tail.Length == 0 || (tail.Length > 1 && tail[0] == '0'))
        {
            return false;
        }

        return long.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out number) && number >= 1;
    }
}

/// <summary>Reason keys a supplier states, on top of the shared <c>negotiation.reason.*</c> keys. Copy lives in both string files.</summary>
public static class SupplyReasons
{
    public const string PriceTooLow = "supply.reason.price_too_low";

    public const string NoCapacity = "supply.reason.no_capacity";

    public const string ExclusiveTaken = "supply.reason.exclusive_taken";

    public const string ExclusiveUnavailable = "supply.reason.exclusive_unavailable";

    /// <summary>Ledger reason of the fee a customer pays.</summary>
    public const string Fee = "supply.reason.fee";

    /// <summary>Ledger reason of the income a supplier books.</summary>
    public const string Sale = "supply.reason.sale";
}
