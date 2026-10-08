namespace Paddock.Domain.Infrastructure;

/// <summary>How the team reaches a round: lorries on the same continent, a ship overseas (Argentina in the 1950s).</summary>
public enum LogisticsMode
{
    Lorry = 0,
    Ship = 1,
}

/// <summary>ESTIMATE quote of one race's transport: mode, days on the road or at sea, and cents.</summary>
public sealed record LogisticsQuote(LogisticsMode Mode, int Days, long CostCents);

/// <summary>
/// Pure transport math (PP-064). Distance is continent vs continent, not a map. A race result is never read from this.
/// </summary>
public static class LogisticsMath
{
    public const string Argentina = "ARG";

    private static readonly HashSet<string> Europe = new(StringComparer.Ordinal)
    {
        "AUT", "AZE", "BEL", "CHE", "DEU", "ESP", "FRA", "GBR", "HUN", "ITA", "MCO", "NLD", "PRT", "RUS", "SWE", "TUR",
    };

    private static readonly HashSet<string> Americas = new(StringComparer.Ordinal)
    {
        "ARG", "BRA", "CAN", "MEX", "USA",
    };

    private static readonly HashSet<string> Asia = new(StringComparer.Ordinal)
    {
        "ARE", "BHR", "CHN", "IND", "JPN", "KOR", "MYS", "QAT", "SAU", "SGP",
    };

    private static readonly HashSet<string> Africa = new(StringComparer.Ordinal)
    {
        "MAR", "ZAF",
    };

    private static readonly HashSet<string> Oceania = new(StringComparer.Ordinal)
    {
        "AUS",
    };

    public static string Of(LogisticsMode mode) => mode switch
    {
        LogisticsMode.Lorry => "lorry",
        LogisticsMode.Ship => "ship",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown logistics mode."),
    };

    /// <summary>
    /// The quote of one round (#268). The season's transport costs <see cref="InfrastructureEstimates.LogisticsSeasonShare"/> of the era's typical
    /// budget and each round gets its part by its weight among the rounds of the season (<paramref name="seasonCircuits"/>, the countries of the rounds
    /// of the season being raced, one per round). Without a calendar the round is priced as one round of a usual
    /// season (<see cref="InfrastructureEstimates.LogisticsFallbackRounds"/> rounds, the others lorry trips). Pure: the team's cash is not an input.
    /// </summary>
    public static LogisticsQuote Quote(string? homeCountry, string? circuitCountry, long typicalBudgetCents, IReadOnlyList<string?>? seasonCircuits = null)
    {
        var home = Normalize(homeCountry);
        var circuit = Normalize(circuitCountry);
        var mode = ModeOf(home, circuit);
        var days = DaysOf(home, circuit, mode);
        var own = WeightOf(home, circuit);
        var total = 0L;
        if (seasonCircuits is not null)
        {
            foreach (var other in seasonCircuits)
            {
                total += WeightOf(home, Normalize(other));
            }
        }

        if (total < own)
        {
            // No calendar, or one that does not hold this round: a season of the usual size in which the other rounds are lorry trips.
            total = own + ((long)(InfrastructureEstimates.LogisticsFallbackRounds - 1) * InfrastructureEstimates.LogisticsLorryWeight);
        }

        var cents = typicalBudgetCents <= 0
            ? 0L
            : Math.Max(1L, (long)Math.Round(typicalBudgetCents * InfrastructureEstimates.LogisticsSeasonShare * own / total, MidpointRounding.AwayFromZero));
        return new LogisticsQuote(mode, days, cents);
    }

    public static long CostCents(string? homeCountry, string? circuitCountry, long typicalBudgetCents, IReadOnlyList<string?>? seasonCircuits = null) =>
        Quote(homeCountry, circuitCountry, typicalBudgetCents, seasonCircuits).CostCents;

    /// <summary>How much of the season's transport a round takes for a team from <paramref name="home"/>: a ship more than a lorry, a home round the least.</summary>
    private static int WeightOf(string home, string circuit) =>
        ModeOf(home, circuit) == LogisticsMode.Ship
            ? InfrastructureEstimates.LogisticsShipWeight
            : SamePlace(home, circuit)
                ? InfrastructureEstimates.LogisticsHomeWeight
                : InfrastructureEstimates.LogisticsLorryWeight;

    public static LogisticsMode ModeOf(string? homeCountry, string? circuitCountry)
    {
        var home = Normalize(homeCountry);
        var circuit = Normalize(circuitCountry);
        if (SamePlace(home, circuit) || SameContinent(home, circuit))
        {
            return LogisticsMode.Lorry;
        }

        return LogisticsMode.Ship;
    }

    private static int DaysOf(string home, string circuit, LogisticsMode mode)
    {
        if (mode == LogisticsMode.Ship)
        {
            return InfrastructureEstimates.LogisticsShipDays;
        }

        return SamePlace(home, circuit)
            ? InfrastructureEstimates.LogisticsHomeDays
            : InfrastructureEstimates.LogisticsLorryDays;
    }

    private static bool SamePlace(string home, string circuit) =>
        home.Length > 0 && string.Equals(home, circuit, StringComparison.Ordinal);

    private static bool SameContinent(string home, string circuit)
    {
        var left = ContinentOf(home);
        var right = ContinentOf(circuit);
        return left is not null && left == right;
    }

    private static string? ContinentOf(string country)
    {
        if (Europe.Contains(country))
        {
            return "europe";
        }

        if (Americas.Contains(country))
        {
            return "americas";
        }

        if (Asia.Contains(country))
        {
            return "asia";
        }

        if (Africa.Contains(country))
        {
            return "africa";
        }

        if (Oceania.Contains(country))
        {
            return "oceania";
        }

        return country.Length == 0 ? "europe" : null;
    }

    private static string Normalize(string? country) =>
        string.IsNullOrWhiteSpace(country) ? "" : country.Trim().ToUpperInvariant();
}
