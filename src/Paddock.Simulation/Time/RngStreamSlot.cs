namespace Paddock.Simulation.Time;

/// <summary>
/// One saved RNG stream for a championship season. Day handlers do not pass a round; race rounds are not drawn here.
/// </summary>
public readonly record struct RngStreamSlot : IComparable<RngStreamSlot>
{
    public RngStreamSlot(string name, int season)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Season = season;
    }

    public string Name { get; } = "";

    public int Season { get; }

    public int CompareTo(RngStreamSlot other)
    {
        var season = Season.CompareTo(other.Season);
        if (season != 0)
        {
            return season;
        }

        return string.CompareOrdinal(Name, other.Name);
    }
}
