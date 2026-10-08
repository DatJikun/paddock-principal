namespace Paddock.Domain.Sponsors;

/// <summary>
/// How hard the condition of a deal is (#268): the player can ask an easier one for less money or accept a harder one for more.
/// <see cref="Standard"/> is first, so it is the default of every stored row and of every command that names no ambition.
/// </summary>
public enum SponsorAmbition
{
    Standard = 0,
    Lighter = 1,
    Harder = 2,
}

public static class SponsorAmbitions
{
    public static IReadOnlyList<SponsorAmbition> All { get; } = [SponsorAmbition.Lighter, SponsorAmbition.Standard, SponsorAmbition.Harder];

    public static string KeyOf(SponsorAmbition ambition) => ambition switch
    {
        SponsorAmbition.Lighter => "lighter",
        SponsorAmbition.Standard => "standard",
        SponsorAmbition.Harder => "harder",
        _ => throw new ArgumentOutOfRangeException(nameof(ambition), ambition, "Unknown sponsor ambition."),
    };

    public static bool TryParse(string? key, out SponsorAmbition ambition)
    {
        switch (key)
        {
            case "lighter":
                ambition = SponsorAmbition.Lighter;
                return true;
            case "standard":
                ambition = SponsorAmbition.Standard;
                return true;
            case "harder":
                ambition = SponsorAmbition.Harder;
                return true;
            default:
                ambition = default;
                return false;
        }
    }

    /// <summary>Thousandths of the standard target the ambition asks for.</summary>
    public static int TargetMilli(SponsorAmbition ambition) => ambition switch
    {
        SponsorAmbition.Lighter => SponsorEstimates.LighterTargetMilli,
        SponsorAmbition.Standard => 1000,
        SponsorAmbition.Harder => SponsorEstimates.HarderTargetMilli,
        _ => throw new ArgumentOutOfRangeException(nameof(ambition), ambition, "Unknown sponsor ambition."),
    };

    /// <summary>Thousandths of the standard price the ambition pays.</summary>
    public static int PayMilli(SponsorAmbition ambition) => ambition switch
    {
        SponsorAmbition.Lighter => SponsorEstimates.LighterPayMilli,
        SponsorAmbition.Standard => 1000,
        SponsorAmbition.Harder => SponsorEstimates.HarderPayMilli,
        _ => throw new ArgumentOutOfRangeException(nameof(ambition), ambition, "Unknown sponsor ambition."),
    };
}

/// <summary>
/// What the player negotiates with a sponsor: how many years the deal runs (1 to 3) and how hard its condition is. The price a year follows
/// from the condition alone (<see cref="PayMilli"/>): a longer deal pays the same a year as a short one (owner decision) and cannot be lost
/// to a missed renewal, and a sponsor that is open to it raises the amount for good years (<see cref="SponsorPartnership"/>). A harder
/// condition pays more and fails more often. Pure: no state, no random number. Every figure is an ESTIMATE in <see cref="SponsorEstimates"/>.
/// </summary>
public readonly record struct SponsorTerms(int Years, SponsorAmbition Ambition)
{
    public static SponsorTerms Default => new(SponsorEstimates.MinYears, SponsorAmbition.Standard);

    public bool IsDefault => Years == SponsorEstimates.MinYears && Ambition == SponsorAmbition.Standard;

    public bool IsValid => Years >= SponsorEstimates.MinYears && Years <= SponsorEstimates.MaxYears && Enum.IsDefined(Ambition);

    /// <summary>Thousandths of the one-year, standard price a year of this deal pays: the length of the deal does not change it.</summary>
    public int PayMilli => SponsorAmbitions.PayMilli(Ambition);

    /// <summary>The days a deal of this length runs, from its first day to its last.</summary>
    public int Days => Years * SponsorEstimates.DealDays;

    /// <summary>The cents a year of this deal pays when the one-year, standard price (after waiting) is <paramref name="baseCents"/>.</summary>
    public long AnnualCents(long baseCents) => baseCents * PayMilli / 1000;
}
