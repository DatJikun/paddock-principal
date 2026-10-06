using System.Globalization;
using Paddock.Domain.World;

namespace Paddock.Domain.Sponsors;

/// <summary>
/// The sponsors of one team for one season (#254, PP-065): every team has its own pool, new and different each season, made from the team
/// and the year alone. No sponsor is shared between teams, so no team ever blocks another, and the player's offers do not depend on what any
/// AI team signs. Nothing is stored and no random stream is drawn: the same team and year always give the same backers, so every run and
/// every save make the same ones. A sponsor id says what it is (<c>local:1956:maserati:t02</c>: year, team, family, index), so a deal that
/// holds the id finds the sponsor again from the id alone. Every number is an ESTIMATE. The names are invented and match no real company.
/// </summary>
public static class LocalSponsorMarket
{
    private const string Prefix = "local:";

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

    /// <summary>The team's own backers for this season: <see cref="SponsorEstimates.LocalBackersPerSeason"/> technical and as many livery ones.</summary>
    public static IReadOnlyList<SponsorDefinition> For(int year, OrganizationId team)
    {
        var result = new List<SponsorDefinition>();
        for (var index = 0; index < SponsorEstimates.LocalBackersPerSeason; index++)
        {
            result.Add(Make(year, team.Value, technical: true, index));
            result.Add(Make(year, team.Value, technical: false, index));
        }

        return result;
    }

    /// <summary>The backer an id names, or null when the id is not one of the team backer ids.</summary>
    public static SponsorDefinition? Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (!id.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = id[Prefix.Length..];
        var firstColon = rest.IndexOf(':', StringComparison.Ordinal);
        var lastColon = rest.LastIndexOf(':');
        if (firstColon <= 0 || lastColon <= firstColon + 1 || rest.Length - lastColon < 3
            || !int.TryParse(rest.AsSpan(0, firstColon), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(rest.AsSpan(lastColon + 2), NumberStyles.None, CultureInfo.InvariantCulture, out var index)
            || index >= SponsorEstimates.LocalBackersPerSeason)
        {
            return null;
        }

        var team = rest[(firstColon + 1)..lastColon];
        var sponsor = rest[lastColon + 1] switch
        {
            't' => Make(year, team, technical: true, index),
            'l' => Make(year, team, technical: false, index),
            _ => null,
        };
        return sponsor is not null && string.Equals(sponsor.Id, id, StringComparison.Ordinal) ? sponsor : null;
    }

    /// <summary>A stable 32-bit hash of the text (FNV-1a), the same on every machine and run, unlike <see cref="string.GetHashCode()"/>.</summary>
    private static uint Hash(string text)
    {
        var hash = 2166136261u;
        foreach (var character in text)
        {
            hash = (hash ^ character) * 16777619u;
        }

        return hash;
    }

    private static SponsorDefinition Make(int year, string team, bool technical, int index)
    {
        var family = technical ? "t" : "l";
        var id = Prefix + year.ToString(CultureInfo.InvariantCulture) + ":" + team + ":" + family
            + index.ToString("00", CultureInfo.InvariantCulture);
        var seed = Hash(id);
        var industries = technical ? TechnicalIndustries : LiveryIndustries;
        var industry = industries[(int)((seed >> 3) % (uint)industries.Length)];
        var suffixes = Suffixes[industry];
        var stem = Stems[(int)((seed >> 7) % (uint)Stems.Length)];
        var suffix = suffixes[(int)((seed >> 13) % (uint)suffixes.Length)];
        SponsorObjectiveSpec? objective = (int)((seed >> 17) % 3) switch
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
            PrestigeNeed: (int)((seed >> 21) % 6) * 0.05,
            BudgetLevel: (technical ? 0.025 : 0.035) + ((int)((seed >> 25) % 5) * 0.008),
            FromYear: year,
            ToYear: year + SponsorEstimates.LocalBackerSeasons - 1,
            Slots: technical ? [SlotKind.Technical] : [SlotKind.Main, SlotKind.Secondary],
            Objective: objective,
            Team: team);
    }
}
