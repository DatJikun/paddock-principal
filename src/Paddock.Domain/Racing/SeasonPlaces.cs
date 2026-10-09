namespace Paddock.Domain.Racing;

/// <summary>One driver's plain totals in one season: the points as raced (no counting rule), and the wins and podiums that break a tie.</summary>
public sealed record SeasonTotal(int Season, string DriverId, decimal Points, int Wins, int Podiums);

/// <summary>
/// The place of each driver in each season by plain totals: points, then wins, then podiums. Drivers equal on all three share a
/// place. Used where the official table of a past season is not stored (#331), so the era's counting rule is not applied.
/// </summary>
public static class SeasonPlaces
{
    public static IReadOnlyDictionary<(int Season, string DriverId), int> Of(IEnumerable<SeasonTotal> totals)
    {
        ArgumentNullException.ThrowIfNull(totals);
        var places = new Dictionary<(int Season, string DriverId), int>();
        foreach (var season in totals.GroupBy(total => total.Season))
        {
            var rows = season.ToArray();
            foreach (var row in rows)
            {
                var ahead = 0;
                foreach (var other in rows)
                {
                    if (Ahead(other, row))
                    {
                        ahead++;
                    }
                }

                places[(row.Season, row.DriverId)] = ahead + 1;
            }
        }

        return places;
    }

    private static bool Ahead(SeasonTotal other, SeasonTotal row)
    {
        if (other.Points != row.Points)
        {
            return other.Points > row.Points;
        }

        if (other.Wins != row.Wins)
        {
            return other.Wins > row.Wins;
        }

        return other.Podiums > row.Podiums;
    }
}
