using System.Globalization;
using Paddock.Domain.Time;

namespace Paddock.Domain.World;

/// <summary>
/// The dates races were held on, by season and round. A host fills it from local data when it has some (the Jolpica cache stays
/// local, PP-041); an empty book means every season falls back to the even spacing of the placeholder calendar.
/// </summary>
public sealed class RaceDateBook
{
    private readonly Dictionary<(int Season, int Round), GameDate> _dates;

    private RaceDateBook(Dictionary<(int Season, int Round), GameDate> dates)
    {
        _dates = dates;
    }

    public static RaceDateBook Empty { get; } = new([]);

    public int Count => _dates.Count;

    public static RaceDateBook Create(IEnumerable<(int Season, int Round, GameDate Date)> races)
    {
        ArgumentNullException.ThrowIfNull(races);
        var dates = new Dictionary<(int Season, int Round), GameDate>();
        foreach (var (season, round, date) in races)
        {
            if (!dates.TryAdd((season, round), date))
            {
                throw new ArgumentException(
                    $"Race {season.ToString(CultureInfo.InvariantCulture)} round {round.ToString(CultureInfo.InvariantCulture)} has two dates.",
                    nameof(races));
            }
        }

        return new RaceDateBook(dates);
    }

    public bool TryGet(int season, int round, out GameDate date) => _dates.TryGetValue((season, round), out date);
}
