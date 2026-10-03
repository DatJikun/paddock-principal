namespace Paddock.Data.World;

/// <summary>
/// The last season each real driver was active, read from the seats of the T12 schedule.
/// A driver whose last seat is in the final season of the data is left out: the data simply stops there, so nothing
/// says they quit, and the age curve decides. Drivers with no seat at all are left out too.
/// </summary>
public static class LastSeasons
{
    public static IReadOnlyDictionary<string, int> From(IPeopleProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var last = new Dictionary<string, int>(StringComparer.Ordinal);
        var dataEnd = 0;
        foreach (var record in provider.Drivers)
        {
            if (record.Seats.Count == 0)
            {
                continue;
            }

            var season = record.Seats.Max(seat => seat.Season);
            last[record.DriverId] = season;
            dataEnd = Math.Max(dataEnd, season);
        }

        foreach (var id in last.Where(pair => pair.Value >= dataEnd).Select(pair => pair.Key).ToArray())
        {
            last.Remove(id);
        }

        return last;
    }
}
