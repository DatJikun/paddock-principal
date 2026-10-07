using System.Text.Json;

namespace Paddock.Data.Historical;

/// <summary>One podium place of a real past race: the driver's display name and demonym, the constructor id and name.</summary>
public sealed record PastPodiumPlace(int Position, string DriverName, string Nationality, string ConstructorId, string ConstructorName);

/// <summary>A real past race at a circuit (Jolpica circuit id) with its podium, best place first.</summary>
public sealed record PastPodium(int Season, int Round, string CircuitId, IReadOnlyList<PastPodiumPlace> Places);

/// <summary>
/// Reads the podiums of real past races from the local Jolpica cache (<c>cache/jolpica/normalized</c>, PP-041: local only,
/// never committed). A missing file is not an error: the list is empty and a circuit page simply has no real history.
/// Only seasons before <c>beforeSeason</c> are read, so a career never sees results of its own period (PP-062).
/// </summary>
public static class PastPodiumLoader
{
    public static IReadOnlyList<PastPodium> Load(string dataRoot, int beforeSeason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        var folder = Path.Combine(Path.GetFullPath(dataRoot), "cache", "jolpica", "normalized");
        var races = Path.Combine(folder, "races.json");
        var results = Path.Combine(folder, "results.json");
        var drivers = Path.Combine(folder, "drivers.json");
        var constructors = Path.Combine(folder, "constructors.json");
        if (!File.Exists(races) || !File.Exists(results) || !File.Exists(drivers) || !File.Exists(constructors))
        {
            return [];
        }

        try
        {
            return Parse(
                File.ReadAllText(races),
                File.ReadAllText(results),
                File.ReadAllText(drivers),
                File.ReadAllText(constructors),
                beforeSeason);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Joins the four normalized documents. A race without a classified podium place is left out.</summary>
    public static IReadOnlyList<PastPodium> Parse(string racesJson, string resultsJson, string driversJson, string constructorsJson, int beforeSeason)
    {
        var races = JsonSerializer.Deserialize<HistoricalRacesDocument>(racesJson, HistoricalJson.Options)?.Races ?? [];
        var results = JsonSerializer.Deserialize<HistoricalResultsDocument>(resultsJson, HistoricalJson.Options)?.Results ?? [];
        var driverBook = (JsonSerializer.Deserialize<HistoricalDriversDocument>(driversJson, HistoricalJson.Options)?.Drivers ?? [])
            .ToDictionary(driver => driver.DriverId, StringComparer.Ordinal);
        var constructorBook = (JsonSerializer.Deserialize<HistoricalConstructorsDocument>(constructorsJson, HistoricalJson.Options)?.Constructors ?? [])
            .ToDictionary(constructor => constructor.ConstructorId, StringComparer.Ordinal);

        var podiums = results
            .Where(result => result.Season < beforeSeason && result.IsClassified && !result.IsDisqualified && result.Position is >= 1 and <= 3)
            .GroupBy(result => (result.Season, result.Round))
            .ToDictionary(group => group.Key, group => group.OrderBy(result => result.Position).ToList());

        var list = new List<PastPodium>();
        foreach (var race in races.Where(race => race.Season < beforeSeason).OrderBy(race => race.Season).ThenBy(race => race.Round))
        {
            if (!podiums.TryGetValue((race.Season, race.Round), out var rows))
            {
                continue;
            }

            var places = new List<PastPodiumPlace>();
            foreach (var row in rows)
            {
                if (places.Any(place => place.Position == row.Position))
                {
                    continue;
                }

                driverBook.TryGetValue(row.DriverId, out var driver);
                constructorBook.TryGetValue(row.ConstructorId, out var constructor);
                places.Add(new PastPodiumPlace(
                    row.Position,
                    driver is null ? row.DriverId : (driver.GivenName + " " + driver.FamilyName).Trim(),
                    driver?.Nationality ?? "",
                    row.ConstructorId,
                    constructor?.Name ?? row.ConstructorId));
            }

            list.Add(new PastPodium(race.Season, race.Round, race.CircuitId, places));
        }

        return list;
    }
}
