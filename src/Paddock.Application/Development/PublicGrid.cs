using System.Globalization;
using Paddock.Domain.Racing;
using Paddock.Domain.World;

namespace Paddock.Application.Development;

/// <summary>
/// What the paddock knows of who is strong: the order of the teams by the points of the latest season with results. Public results only,
/// never a hidden rating (INV-003). Empty before any result, and the grid comparison then falls back to the public bands.
/// </summary>
internal static class PublicGrid
{
    /// <summary>Organization ids, best first, from the points of <paramref name="season"/> or, with no race yet, of the season before it.</summary>
    public static List<string> Ranking(WorldState world, int season)
    {
        ArgumentNullException.ThrowIfNull(world);
        var archive = world.Section<RaceResultsSection>(RaceResultsSection.SectionName);
        if (archive is null || archive.IsEmpty)
        {
            return [];
        }

        var wanted = archive.Races.Any(race => race.Season == season) ? season : archive.Races.Max(race => race.Season);
        if (wanted > season)
        {
            return [];
        }

        var points = new SortedDictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var race in archive.Races.Where(race => race.Season == wanted))
        {
            foreach (var row in race.Rows)
            {
                if (!decimal.TryParse(row.Points, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                {
                    value = 0m;
                }

                points[row.TeamId] = points.GetValueOrDefault(row.TeamId) + value;
            }
        }

        return points
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => entry.Key)
            .ToList();
    }
}
