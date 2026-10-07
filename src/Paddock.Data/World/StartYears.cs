using Paddock.Data.Authored;
using Paddock.Data.Historical;
using Paddock.Domain.Career;

namespace Paddock.Data.World;

/// <summary>
/// One year a new career could start in, for the wizard (#271). An unavailable year carries a translation key with the reason; the
/// copy lives in <c>strings/pl.json</c> and <c>strings/en.json</c>.
/// </summary>
public sealed record StartYearOption(int Year, bool Available, string? ReasonKey);

/// <summary>
/// Which start years have the data a fair start needs: a starting car strength for the teams of that season, from the local starting
/// data or from the authored files. A year without it would still build, but every car would start at the same tier fallback, so it
/// is offered as unavailable with a reason. Pure: no I/O and no RNG.
/// </summary>
public static class StartYears
{
    /// <summary>The last season with historical data. The season after it is the first generated one, out of scope for now.</summary>
    public const int LastHistorical = 2025;

    public const string NoStartingData = "start.year.unavailable.no_data";

    public const string Procedural = "start.year.unavailable.procedural";

    /// <summary>Every year from 1950 to the first generated season, in order.</summary>
    public static IReadOnlyList<StartYearOption> Of(AuthoredData data, StartingDataReport? generated)
    {
        ArgumentNullException.ThrowIfNull(data);
        var seasons = generated?.Seasons.Select(season => season.Season).ToHashSet() ?? [];
        var options = new List<StartYearOption>();
        for (var year = CareerConfig.MinStartYear; year <= LastHistorical + 1; year++)
        {
            if (year > LastHistorical)
            {
                options.Add(new StartYearOption(year, false, Procedural));
                continue;
            }

            var known = seasons.Contains(year) || AuthoredStrengthCovers(data, year);
            options.Add(new StartYearOption(year, known, known ? null : NoStartingData));
        }

        return options;
    }

    private static bool AuthoredStrengthCovers(AuthoredData data, int year) =>
        data.CarStrength is { } strength
        && data.Engines.Entries.Any(entry => entry.Year == year && strength.TryGet(entry.ConstructorId, year, out _));
}
