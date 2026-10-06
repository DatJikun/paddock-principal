using System.Globalization;
using Paddock.Domain.World;

namespace Paddock.Domain.Sponsors;

/// <summary>Where on the car a sponsor appears. Which kinds exist in a season is set by the era (<see cref="SponsorEra"/>).</summary>
public enum SlotKind
{
    /// <summary>A technical or patron backer: fuel, oil, tyres, a motor club, a wealthy patron. The only kind before commercial liveries.</summary>
    Technical = 0,

    /// <summary>The main livery sponsor. Needs <c>commercial_liveries = commercial_sponsorship_allowed</c>.</summary>
    Main = 1,

    /// <summary>A secondary livery sponsor. Needs commercial liveries.</summary>
    Secondary = 2,
}

public static class SlotKinds
{
    public static string KeyOf(SlotKind kind) => kind switch
    {
        SlotKind.Technical => "technical",
        SlotKind.Main => "main",
        SlotKind.Secondary => "secondary",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown slot kind."),
    };

    public static bool TryParse(string? key, out SlotKind kind)
    {
        switch (key)
        {
            case "technical":
                kind = SlotKind.Technical;
                return true;
            case "main":
                kind = SlotKind.Main;
                return true;
            case "secondary":
                kind = SlotKind.Secondary;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    /// <summary>True for the kinds that put a brand on the car and so need commercial liveries.</summary>
    public static bool IsLivery(SlotKind kind) => kind is SlotKind.Main or SlotKind.Secondary;
}

/// <summary>The closed set of industries a sponsor can belong to. Two sponsors of one industry cannot share a team.</summary>
public static class SponsorIndustries
{
    public const string Fuel = "fuel";
    public const string Oil = "oil";
    public const string Tyres = "tyres";
    public const string Patron = "patron";
    public const string MotorClub = "motor_club";
    public const string Automotive = "automotive";
    public const string Tobacco = "tobacco";
    public const string Alcohol = "alcohol";
    public const string Consumer = "consumer";
    public const string Finance = "finance";
    public const string Electronics = "electronics";

    private static readonly string[] Technical = [Fuel, Oil, Tyres, Patron, MotorClub, Automotive];
    private static readonly string[] Livery = [Tobacco, Alcohol, Consumer, Finance, Electronics];

    public static IReadOnlyList<string> All { get; } = [.. Technical, .. Livery];

    public static bool IsKnown(string? industry) => industry is not null && All.Contains(industry);

    /// <summary>A technical industry can back a team without a livery; a livery industry cannot.</summary>
    public static bool IsTechnical(string industry) => Technical.Contains(industry);
}

/// <summary>What a sponsor asks of a team in return, as authored data. Becomes a T36 objective when a deal is signed.</summary>
public sealed record SponsorObjectiveSpec(string Kind, string Value, int WithinDays)
{
    public const string PodiumsAtLeast = "podiums_at_least";
    public const string PointsAtLeast = "points_at_least";
    public const string ChampionshipPositionAtMost = "championship_position_at_most";
    public const string DriverNationalityInLineup = "driver_nationality_in_lineup";

    public static IReadOnlyList<string> Kinds { get; } =
        [PodiumsAtLeast, PointsAtLeast, ChampionshipPositionAtMost, DriverNationalityInLineup];
}

/// <summary>
/// One fictional sponsor from <c>data/authored/commercial/sponsors_estimates.json</c>. Every number is an uncalibrated ESTIMATE.
/// <paramref name="PrestigeNeed"/> is the lowest team prestige (0 to 1) the sponsor accepts.
/// <paramref name="BudgetLevel"/> is the share of the era's typical team budget it pays a year before popularity and slot factors.
/// <paramref name="ToYear"/> null means open through the end of the covered window.
/// <paramref name="Local"/> marks a backer made by <see cref="LocalSponsorMarket"/> instead of read from the authored file.
/// </summary>
public sealed record SponsorDefinition(
    string Id,
    string Name,
    string Industry,
    string Nationality,
    double PrestigeNeed,
    double BudgetLevel,
    int FromYear,
    int? ToYear,
    IReadOnlyList<SlotKind> Slots,
    SponsorObjectiveSpec? Objective,
    bool Local = false)
{
    public bool ActiveIn(int year) => FromYear <= year && (ToYear is null || year <= ToYear.Value);

    public bool FitsSlot(SlotKind kind) => Slots.Contains(kind);
}

/// <summary>
/// What the era allows, read from the era catalog dimensions. Before 1968 cars carried national racing colours, so there is
/// no livery slot and the three slots are technical or patron backers. From 1968 a team has a main, a secondary and a technical slot.
/// The number of slots per team (three) is the owner's decision (PP-050); the kinds per era are an ESTIMATE of how to split them.
/// </summary>
public sealed record SponsorEra(bool Livery, bool Tobacco, bool Alcohol)
{
    public const string LiveriesDimension = "commercial_liveries";
    public const string LiveriesAllowed = "commercial_sponsorship_allowed";
    public const string TobaccoDimension = "tobacco_advertising";
    public const string AlcoholDimension = "alcohol_advertising";

    /// <summary>Slot number (1 to 3) to kind, for this era.</summary>
    public IReadOnlyList<SlotKind> Slots => Livery
        ? [SlotKind.Main, SlotKind.Secondary, SlotKind.Technical]
        : [SlotKind.Technical, SlotKind.Technical, SlotKind.Technical];

    public static SponsorEra ForYear(IReadOnlyList<RulePeriod> periods, int year)
    {
        ArgumentNullException.ThrowIfNull(periods);
        var livery = ValueIn(periods, LiveriesDimension, year) == LiveriesAllowed;
        var tobacco = ValueIn(periods, TobaccoDimension, year) is "unrestricted" or "national_restrictions_mosaic";
        var alcohol = ValueIn(periods, AlcoholDimension, year) is "unrestricted" or "national_restrictions";
        return new SponsorEra(livery, tobacco, alcohol);
    }

    /// <summary>True when the era lets this industry sponsor a team in a slot of this kind.</summary>
    public bool Allows(string industry, SlotKind kind)
    {
        if (SlotKinds.IsLivery(kind) && !Livery)
        {
            return false;
        }

        return industry switch
        {
            SponsorIndustries.Tobacco => Livery && Tobacco,
            SponsorIndustries.Alcohol => Livery && Alcohol,
            _ => SponsorIndustries.IsTechnical(industry) || Livery,
        };
    }

    private static string? ValueIn(IReadOnlyList<RulePeriod> periods, string dimension, int year)
    {
        foreach (var period in periods)
        {
            if (string.Equals(period.DimensionId, dimension, StringComparison.Ordinal)
                && period.FromYear <= year && (period.ToYear is null || year <= period.ToYear.Value))
            {
                return period.Value;
            }
        }

        return null;
    }
}

/// <summary>The sponsors of the game, in id order.</summary>
public sealed class SponsorCatalog
{
    private readonly SortedDictionary<string, SponsorDefinition> _byId;
    private readonly bool _localMarket;

    /// <param name="sponsors">The authored sponsors.</param>
    /// <param name="localMarket">
    /// True to add the yearly local backers of <see cref="LocalSponsorMarket"/>, so the market does not run dry when every authored
    /// sponsor is under contract with a team that keeps renewing.
    /// </param>
    public SponsorCatalog(IEnumerable<SponsorDefinition> sponsors, bool localMarket = false)
    {
        ArgumentNullException.ThrowIfNull(sponsors);
        _localMarket = localMarket;
        _byId = new SortedDictionary<string, SponsorDefinition>(StringComparer.Ordinal);
        foreach (var sponsor in sponsors)
        {
            ArgumentNullException.ThrowIfNull(sponsor);
            if (!_byId.TryAdd(sponsor.Id, sponsor))
            {
                throw new ArgumentException("Sponsor '" + sponsor.Id + "' appears twice.", nameof(sponsors));
            }
        }
    }

    public IReadOnlyList<SponsorDefinition> All => _byId.Values.ToArray();

    public SponsorDefinition? Find(string id)
    {
        if (id is null)
        {
            return null;
        }

        if (_byId.TryGetValue(id, out var sponsor))
        {
            return sponsor;
        }

        return _localMarket ? LocalSponsorMarket.Find(id) : null;
    }

    /// <summary>Sponsors that could fill a slot of this kind in this year: active, fitting, and allowed by the era.</summary>
    public IReadOnlyList<SponsorDefinition> Candidates(int year, SlotKind kind, SponsorEra era)
    {
        ArgumentNullException.ThrowIfNull(era);
        var authored = _byId.Values.AsEnumerable();
        var all = _localMarket ? authored.Concat(LocalSponsorMarket.ActiveIn(year)) : authored;
        return all
            .Where(sponsor => sponsor.ActiveIn(year) && sponsor.FitsSlot(kind) && era.Allows(sponsor.Industry, kind))
            .ToArray();
    }

    public override string ToString() => _byId.Count.ToString(CultureInfo.InvariantCulture) + " sponsors";
}
