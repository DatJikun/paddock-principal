using Paddock.Domain.World;

namespace Paddock.Domain.People;

/// <summary>
/// ESTIMATE values for filling a staff chair and for the race engineer who works with one driver (PP-059).
/// Not measured facts. A richer team draws a higher band; the relationship grows while the pair stays together.
/// </summary>
public static class StaffEstimates
{
    /// <summary>ESTIMATE: seasons of the contract written when a chair is filled.</summary>
    public const int SeatContractSeasons = 1;

    /// <summary>ESTIMATE: race engineers a two-car team starts with when it has fewer drivers than cars.</summary>
    public const int RaceEngineersPerTeam = 2;

    /// <summary>ESTIMATE: relationship of a race engineer and a driver in the season they are first paired, 1–100.</summary>
    public const int RelationshipStart = 40;

    /// <summary>ESTIMATE: relationship gained for each season the same engineer and driver stay at the same team.</summary>
    public const int RelationshipPerSeason = 4;

    /// <summary>ESTIMATE: relationship does not rise past this.</summary>
    public const int RelationshipMax = 100;

    /// <summary>ESTIMATE: weight of a home country among the nationalities offered to a generated staff member.</summary>
    public const int HomeCountryWeight = 3;

    /// <summary>ESTIMATE: nationalities offered when a chair is filled and no home country is known.</summary>
    public static readonly string[] Nationalities = ["GBR", "ITA", "DEU", "FRA", "BRA", "USA"];

    /// <summary>
    /// ESTIMATE: the public budget rank (1 is the richest; ties break by organization id) maps onto a quality band.
    /// The richest team draws contenders, the top third solid people, and the rest fillers. Future stars stay a driver band.
    /// </summary>
    public static QualityBand BandForRank(int rank, int field)
    {
        if (field < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(field), field, "A field has at least one team.");
        }

        if (rank < 1 || rank > field)
        {
            throw new ArgumentOutOfRangeException(nameof(rank), rank, "Rank is between 1 and the size of the field.");
        }

        if (rank == 1)
        {
            return QualityBand.Contender;
        }

        var topThird = Math.Max(1, (field + 2) / 3);
        return rank <= topThird ? QualityBand.Solid : QualityBand.Filler;
    }

    /// <summary>1 is the largest budget. Equal budgets break by organization id, the same way the board ranks a field.</summary>
    public static int BudgetRank(IReadOnlyList<Organization> teams, OrganizationId organization)
    {
        ArgumentNullException.ThrowIfNull(teams);
        Organization? own = null;
        foreach (var team in teams)
        {
            if (team.Id == organization)
            {
                own = team;
                break;
            }
        }

        if (own is null)
        {
            throw new ArgumentException("The organization is not in the field.", nameof(organization));
        }

        var rank = 1;
        foreach (var team in teams)
        {
            if (team.Id == organization)
            {
                continue;
            }

            if (team.Budget > own.Budget
                || (team.Budget == own.Budget && string.CompareOrdinal(team.Id.Value, own.Id.Value) < 0))
            {
                rank++;
            }
        }

        return rank;
    }

    public static NationalityWeight[] NationalityWeights(string? homeCountry)
    {
        var home = string.IsNullOrWhiteSpace(homeCountry) ? null : homeCountry.Trim().ToUpperInvariant();
        var weights = new List<NationalityWeight>(Nationalities.Length + 1);
        foreach (var code in Nationalities)
        {
            weights.Add(new NationalityWeight(code, string.Equals(code, home, StringComparison.Ordinal) ? HomeCountryWeight : 1));
        }

        if (home is not null && !Nationalities.Contains(home, StringComparer.Ordinal))
        {
            weights.Add(new NationalityWeight(home, HomeCountryWeight));
        }

        return weights.ToArray();
    }
}
