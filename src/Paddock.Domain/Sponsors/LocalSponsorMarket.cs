using System.Globalization;

namespace Paddock.Domain.Sponsors;

/// <summary>
/// The yearly local backers (#254): fictional small sponsors made from the year and an index, never stored and never drawn
/// from a random stream, so every run and every save make the same ones. A sponsor id says what it is
/// (<c>local_1956_t07</c>: year, family, index), so a deal or a talk that holds the id finds the sponsor again
/// from the id alone. Every number is an ESTIMATE. The names are invented and match no real company.
/// <para>
/// Why they exist: the authored file has a few sponsors, one team at a time backs each, and a team that renews keeps its sponsor
/// for good. Over a few seasons the AI teams held all of them and the player found none. A new cohort every season, which also
/// leaves after <see cref="SponsorEstimates.LocalBackerSeasons"/> seasons, keeps the market alive and lets old deals free their teams.
/// </para>
/// </summary>
public static class LocalSponsorMarket
{
    private const string Prefix = "local_";

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, SponsorDefinition[]> Cohorts = new();

    private static readonly string[] TechnicalIndustries =
        [SponsorIndustries.Fuel, SponsorIndustries.Oil, SponsorIndustries.Tyres, SponsorIndustries.Patron, SponsorIndustries.MotorClub, SponsorIndustries.Automotive];

    private static readonly string[] LiveryIndustries =
        [SponsorIndustries.Consumer, SponsorIndustries.Finance, SponsorIndustries.Electronics];

    private static readonly string[] Stems =
    [
        "Aldermoor", "Brenwick", "Calderfen", "Dunhollow", "Eastmere", "Farrowdale", "Glenhurst", "Harrowgate", "Ivermoor", "Jessamond",
        "Kingsholt", "Larkmoor", "Marrowby", "Northcote", "Oakhollow", "Pendrake", "Quillon", "Ravensford", "Stanmoor", "Thornfield",
        "Ulverley", "Valbrook", "Wexcombe", "Yarrowby", "Zennor", "Ashgrove", "Bramwell", "Corrimoor", "Dalwick", "Elmstead",
        "Fenwarden", "Grayloch", "Holmbury", "Inglemere", "Kestlow", "Lymbrook", "Morvale", "Nethercombe", "Orsley", "Pelham Rise",
    ];

    private static readonly Dictionary<string, string[]> Suffixes = new(StringComparer.Ordinal)
    {
        [SponsorIndustries.Fuel] = ["Petroleum", "Fuels", "Spirit Co.", "Oil Depot", "Energy"],
        [SponsorIndustries.Oil] = ["Lubricants", "Oils", "Works", "Refining", "Lube Co."],
        [SponsorIndustries.Tyres] = ["Tyres", "Rubber", "Tyre Works", "Treads", "Tyre Co."],
        [SponsorIndustries.Patron] = ["Patronage", "Estates", "Holdings", "Family Trust", "Syndicate"],
        [SponsorIndustries.MotorClub] = ["Motor Club", "Racing Club", "Automobile Society", "Drivers' Circle", "Speed Club"],
        [SponsorIndustries.Automotive] = ["Motors", "Coachworks", "Engineering", "Garages", "Carburettors"],
        [SponsorIndustries.Consumer] = ["Foods", "Biscuits", "Housewares", "Soap Works", "Stores"],
        [SponsorIndustries.Finance] = ["Savings Bank", "Assurance", "Credit", "Building Society", "Trust"],
        [SponsorIndustries.Electronics] = ["Electric", "Radio", "Instruments", "Electronics", "Telegraph"],
    };

    /// <summary>The local backers that can start a deal in this year (this season's cohort and the earlier ones that have not left yet).</summary>
    public static IReadOnlyList<SponsorDefinition> ActiveIn(int year)
    {
        var result = new List<SponsorDefinition>();
        for (var season = year - SponsorEstimates.LocalBackerSeasons + 1; season <= year; season++)
        {
            if (season < 1)
            {
                continue;
            }

            result.AddRange(Cohorts.GetOrAdd(season, Build));
        }

        return result;
    }

    private static SponsorDefinition[] Build(int season)
    {
        var cohort = new List<SponsorDefinition>();
        for (var index = 0; index < SponsorEstimates.LocalBackersPerSeason; index++)
        {
            cohort.Add(Make(season, technical: true, index));
            cohort.Add(Make(season, technical: false, index));
        }

        return [.. cohort];
    }

    /// <summary>The backer an id names, or null when the id is not one of the local ids.</summary>
    public static SponsorDefinition? Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (!id.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var parts = id[Prefix.Length..].Split('_');
        if (parts.Length != 2 || parts[1].Length < 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(parts[1].AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var index)
            || index >= SponsorEstimates.LocalBackersPerSeason)
        {
            return null;
        }

        var sponsor = parts[1][0] switch
        {
            't' => Make(year, technical: true, index),
            'l' => Make(year, technical: false, index),
            _ => null,
        };
        return sponsor is not null && string.Equals(sponsor.Id, id, StringComparison.Ordinal) ? sponsor : null;
    }

    private static SponsorDefinition Make(int year, bool technical, int index)
    {
        var industries = technical ? TechnicalIndustries : LiveryIndustries;
        var industry = industries[index % industries.Length];
        var suffixes = Suffixes[industry];
        var round = index / industries.Length;
        var stem = Stems[((index * 7) + (year * 3)) % Stems.Length];
        var suffix = suffixes[(round + year) % suffixes.Length];
        var id = Prefix + year.ToString(CultureInfo.InvariantCulture) + "_" + (technical ? "t" : "l")
            + index.ToString("00", CultureInfo.InvariantCulture);
        SponsorObjectiveSpec? objective = (index % 3) switch
        {
            0 => new SponsorObjectiveSpec(SponsorObjectiveSpec.PointsAtLeast, "2", 364),
            1 => new SponsorObjectiveSpec(SponsorObjectiveSpec.PodiumsAtLeast, "1", 364),
            _ => null,
        };
        return new SponsorDefinition(
            id,
            stem + " " + suffix,
            industry,
            "GBR",
            PrestigeNeed: (index % 6) * 0.05,
            BudgetLevel: (technical ? 0.025 : 0.035) + ((index % 5) * 0.008),
            FromYear: year,
            ToYear: year + SponsorEstimates.LocalBackerSeasons - 1,
            Slots: technical ? [SlotKind.Technical] : [SlotKind.Main, SlotKind.Secondary],
            Objective: objective,
            Local: true);
    }
}
