namespace Paddock.Domain.Sponsors;

/// <summary>What an industry adds to a deal on top of the price (#268). Some industries give something extra; the rest give nothing.</summary>
public enum IndustryBonusKind
{
    None = 0,

    /// <summary>The sponsor supplies goods (fuel, oil, tyres, parts), worth a share of the annual amount, paid with every instalment.</summary>
    InKind = 1,

    /// <summary>The sponsor pays a share of the annual amount once, with the first instalment.</summary>
    Signing = 2,
}

/// <summary>The bonus of an industry and its size in thousandths of the annual amount. Every number is an ESTIMATE in <see cref="SponsorEstimates"/>.</summary>
public static class SponsorIndustryBonus
{
    public static IndustryBonusKind KindOf(string industry) => industry switch
    {
        SponsorIndustries.Fuel or SponsorIndustries.Oil or SponsorIndustries.Tyres or SponsorIndustries.Automotive => IndustryBonusKind.InKind,
        SponsorIndustries.Finance or SponsorIndustries.Consumer or SponsorIndustries.Electronics => IndustryBonusKind.Signing,
        _ => IndustryBonusKind.None,
    };

    /// <summary>Thousandths of the annual amount the bonus is worth in all (zero for an industry with none).</summary>
    public static int Milli(string industry) => KindOf(industry) switch
    {
        IndustryBonusKind.InKind => SponsorEstimates.InKindMilli,
        IndustryBonusKind.Signing => SponsorEstimates.SigningBonusMilli,
        _ => 0,
    };

    public static string KeyOf(IndustryBonusKind kind) => kind switch
    {
        IndustryBonusKind.None => "none",
        IndustryBonusKind.InKind => "inKind",
        IndustryBonusKind.Signing => "signing",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown industry bonus."),
    };
}

/// <summary>
/// A sponsor's wish for a driver of one nationality (#268). It is a bonus, never a requirement: a missed wish costs no trust and ends nothing.
/// <paramref name="RaceSeat"/> is true for a big sponsor, which wants the driver in a race seat; a small one accepts a reserve.
/// </summary>
public sealed record SponsorWish(string Nationality, bool RaceSeat);

/// <summary>Nationality codes of the sponsor file and of the people files differ for a few countries; this reads them as one.</summary>
public static class SponsorNationality
{
    public static string Normalize(string code) => code.Trim().ToUpperInvariant() switch
    {
        "GER" => "DEU",
        "NED" => "NLD",
        "SUI" => "CHE",
        "SPA" => "ESP",
        var other => other,
    };

    public static bool Same(string left, string right) => string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);
}
