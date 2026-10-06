using System.Globalization;
using System.Text.Json;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Data.Historical;

/// <summary>
/// Reads race dates from the local Jolpica cache (<c>cache/jolpica/normalized/races.json</c>, PP-041: local only, never committed).
/// A missing file is not an error: the book is empty and every season keeps the even spacing (#229).
/// </summary>
public static class RaceDateLoader
{
    /// <summary>The path of the races file under a data root, whether or not it exists.</summary>
    public static string RacesPath(string dataRoot) =>
        Path.Combine(Path.GetFullPath(dataRoot), "cache", "jolpica", "normalized", "races.json");

    public static RaceDateBook Load(string dataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        var path = RacesPath(dataRoot);
        return File.Exists(path) ? Parse(File.ReadAllText(path)) : RaceDateBook.Empty;
    }

    /// <summary>Reads the text of a normalized races file. A race whose date is not a calendar date is left out.</summary>
    public static RaceDateBook Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var document = JsonSerializer.Deserialize<HistoricalRacesDocument>(json, HistoricalJson.Options)
            ?? throw new JsonException("The races file is empty.");
        var races = new List<(int Season, int Round, GameDate Date)>(document.Races.Count);
        foreach (var race in document.Races)
        {
            if (DateOnly.TryParseExact(race.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                races.Add((race.Season, race.Round, new GameDate(date.Year, date.Month, date.Day)));
            }
        }

        return RaceDateBook.Create(races);
    }
}
