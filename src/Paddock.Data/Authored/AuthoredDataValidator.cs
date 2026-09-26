using System.Globalization;

namespace Paddock.Data.Authored;

/// <summary>
/// Checks authored regulations, tracks, technologies, teams and staff.
/// Every violation is returned; the first failure does not stop the rest.
/// The covered window is 1950–2026. A timeline or lineage <c>to</c> of null stays open through 2026.
/// Number dimensions have no catalog value list (the files use both kilograms and the token "unknown"), so only
/// enum and bool dimensions are checked against that list.
/// </summary>
public static partial class AuthoredDataValidator
{
    public const int FirstSeason = 1950;
    public const int LastSeason = 2026;
    public const double ProfileWeightTolerance = 0.01;

    public const string UnknownDimension = "unknown-dimension";
    public const string EnumValue = "enum-value";
    public const string Gap = "gap";
    public const string Overlap = "overlap";
    public const string InvertedPeriod = "inverted-period";
    public const string ProfileWeights = "profile-weights";
    public const string UnknownLayout = "unknown-layout";

    public static IReadOnlyList<AuthoredDataError> Validate(AuthoredData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var errors = new List<AuthoredDataError>();
        var catalogById = IndexCatalog(data.Catalog);

        foreach (var period in data.Timeline)
        {
            if (!catalogById.ContainsKey(period.Dimension))
            {
                errors.Add(new AuthoredDataError(
                    UnknownDimension,
                    $"timeline period for '{period.Dimension}' ({Span(period.From, period.To)}) uses a dimension that is not in the catalog"));
            }
        }

        foreach (var period in data.Timeline)
        {
            if (!catalogById.TryGetValue(period.Dimension, out var dimension))
            {
                continue;
            }

            if (dimension.Type is not ("enum" or "bool"))
            {
                continue;
            }

            if (ContainsOrdinal(dimension.Values, period.Value))
            {
                continue;
            }

            errors.Add(new AuthoredDataError(
                EnumValue,
                $"dimension '{period.Dimension}' value '{period.Value}' in {Span(period.From, period.To)} is not in the catalog value list"));
        }

        var periodsByDimension = GroupPeriods(data.Timeline);
        foreach (var dimension in data.Catalog)
        {
            periodsByDimension.TryGetValue(dimension.Id, out var periods);
            AppendCoverage(errors, dimension.Id, periods ?? []);
        }

        foreach (var circuit in data.Circuits.Circuits)
        {
            foreach (var layout in circuit.Layouts)
            {
                var sum = layout.ProfileGuess.Straights
                    + layout.ProfileGuess.HighSpeed
                    + layout.ProfileGuess.LowSpeed
                    + layout.ProfileGuess.Braking;
                if (Math.Abs(sum - 1d) > ProfileWeightTolerance)
                {
                    errors.Add(new AuthoredDataError(
                        ProfileWeights,
                        $"layout '{layout.LayoutId}' profile weights sum to {sum.ToString("0.000", CultureInfo.InvariantCulture)}, expected 1 +/- {ProfileWeightTolerance.ToString("0.00", CultureInfo.InvariantCulture)}"));
                }
            }
        }

        var layoutIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var circuit in data.Circuits.Circuits)
        {
            foreach (var layout in circuit.Layouts)
            {
                layoutIds.Add(layout.LayoutId);
            }
        }

        foreach (var entry in data.RaceLayoutMap)
        {
            if (layoutIds.Contains(entry.LayoutId))
            {
                continue;
            }

            errors.Add(new AuthoredDataError(
                UnknownLayout,
                $"race {entry.Season.ToString(CultureInfo.InvariantCulture)} round {entry.Round.ToString(CultureInfo.InvariantCulture)} points at missing layout '{entry.LayoutId}'"));
        }

        AppendTechnologiesTeamsAndStaff(errors, data);
        return errors;
    }

    private static Dictionary<string, CatalogDimension> IndexCatalog(IReadOnlyList<CatalogDimension> catalog)
    {
        var catalogById = new Dictionary<string, CatalogDimension>(catalog.Count, StringComparer.Ordinal);
        foreach (var dimension in catalog)
        {
            catalogById.TryAdd(dimension.Id, dimension);
        }

        return catalogById;
    }

    private static Dictionary<string, List<TimelinePeriod>> GroupPeriods(IReadOnlyList<TimelinePeriod> timeline)
    {
        var periodsByDimension = new Dictionary<string, List<TimelinePeriod>>(StringComparer.Ordinal);
        foreach (var period in timeline)
        {
            if (!periodsByDimension.TryGetValue(period.Dimension, out var periods))
            {
                periods = [];
                periodsByDimension.Add(period.Dimension, periods);
            }

            periods.Add(period);
        }

        return periodsByDimension;
    }

    private static void AppendCoverage(
        List<AuthoredDataError> errors,
        string dimensionId,
        List<TimelinePeriod> periods)
    {
        var counts = new int[LastSeason - FirstSeason + 1];
        foreach (var period in periods)
        {
            if (period.To is int toYear && period.From > toYear)
            {
                errors.Add(new AuthoredDataError(
                    InvertedPeriod,
                    $"dimension '{dimensionId}' period {period.From.ToString(CultureInfo.InvariantCulture)}-{toYear.ToString(CultureInfo.InvariantCulture)} ends before it starts"));
                continue;
            }

            var end = period.To ?? LastSeason;
            var start = Math.Max(FirstSeason, period.From);
            var stop = Math.Min(LastSeason, end);
            if (start > stop)
            {
                continue;
            }

            for (var year = start; year <= stop; year++)
            {
                counts[year - FirstSeason]++;
            }
        }

        AppendRanges(errors, dimensionId, counts, missing: true);
        AppendRanges(errors, dimensionId, counts, missing: false);
    }

    private static void AppendRanges(List<AuthoredDataError> errors, string dimensionId, int[] counts, bool missing)
    {
        var rangeStart = -1;
        for (var index = 0; index <= counts.Length; index++)
        {
            var inRange = index < counts.Length && (missing ? counts[index] == 0 : counts[index] > 1);
            if (inRange)
            {
                if (rangeStart < 0)
                {
                    rangeStart = index;
                }

                continue;
            }

            if (rangeStart < 0)
            {
                continue;
            }

            var from = FirstSeason + rangeStart;
            var to = FirstSeason + index - 1;
            var span = from == to
                ? from.ToString(CultureInfo.InvariantCulture)
                : $"{from.ToString(CultureInfo.InvariantCulture)}-{to.ToString(CultureInfo.InvariantCulture)}";
            errors.Add(missing
                ? new AuthoredDataError(Gap, $"dimension '{dimensionId}' does not cover {span}")
                : new AuthoredDataError(Overlap, $"dimension '{dimensionId}' overlaps in {span}"));
            rangeStart = -1;
        }
    }

    private static bool ContainsOrdinal(IReadOnlyList<string>? values, string candidate)
    {
        if (values is null)
        {
            return false;
        }

        foreach (var value in values)
        {
            if (string.Equals(value, candidate, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string Span(int from, int? to)
    {
        if (to is null)
        {
            return $"{from.ToString(CultureInfo.InvariantCulture)}-open";
        }

        if (from == to.Value)
        {
            return from.ToString(CultureInfo.InvariantCulture);
        }

        return $"{from.ToString(CultureInfo.InvariantCulture)}-{to.Value.ToString(CultureInfo.InvariantCulture)}";
    }
}
