using System.Globalization;
using Paddock.Domain.World;

namespace Paddock.Data.Authored;

/// <summary>
/// Checks authored regulations, tracks, technologies, teams, staff, and eras.
/// Every violation is returned; the first failure does not stop the rest.
/// The covered window is 1950–2026. A timeline or lineage <c>to</c> of null stays open through 2026.
/// Number dimensions have no catalog value list (the files use both kilograms and the token "unknown"), so only
/// enum and bool dimensions are checked against that list.
/// Era and CPI sources must be absolute https URLs. <c>cpi_us.json</c> lists every year from 1950 through
/// <see cref="CpiBook.BaseYear"/> exactly once. <see cref="LastSeason"/> is allowed only when <c>partial</c> is true.
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
    public const string EraUnknownDimension = "era-unknown-dimension";
    public const string EraEnumValue = "era-enum-value";
    public const string EraGap = "era-gap";
    public const string EraOverlap = "era-overlap";
    public const string EraInvertedPeriod = "era-inverted-period";
    public const string EraSourceUrl = "era-source-url";
    public const string EraCpiYear = "era-cpi-year";
    public const string EraCpiPartial = "era-cpi-partial";

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
            AppendCoverage(errors, dimension.Id, periods ?? [], InvertedPeriod, Gap, Overlap, "dimension");
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
        AppendEraErrors(errors, data);
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

    private static void AppendEraErrors(List<AuthoredDataError> errors, AuthoredData data)
    {
        var catalogById = IndexCatalog(data.EraCatalog);

        foreach (var period in data.EraTimeline)
        {
            if (!catalogById.ContainsKey(period.Dimension))
            {
                errors.Add(new AuthoredDataError(
                    EraUnknownDimension,
                    $"era timeline period for '{period.Dimension}' ({Span(period.From, period.To)}) uses a dimension that is not in the era catalog"));
            }
        }

        foreach (var period in data.EraTimeline)
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
                EraEnumValue,
                $"era dimension '{period.Dimension}' value '{period.Value}' in {Span(period.From, period.To)} is not in the era catalog value list"));
        }

        var periodsByDimension = GroupPeriods(data.EraTimeline);
        foreach (var dimension in data.EraCatalog)
        {
            periodsByDimension.TryGetValue(dimension.Id, out var periods);
            AppendCoverage(errors, dimension.Id, periods ?? [], EraInvertedPeriod, EraGap, EraOverlap, "era dimension");
        }

        foreach (var period in data.EraTimeline)
        {
            if (IsAbsoluteHttps(period.Source))
            {
                continue;
            }

            errors.Add(new AuthoredDataError(
                EraSourceUrl,
                $"era timeline period for '{period.Dimension}' ({Span(period.From, period.To)}) source '{period.Source}' is not an absolute https URL"));
        }

        foreach (var row in data.CpiYears)
        {
            if (IsAbsoluteHttps(row.Source))
            {
                continue;
            }

            errors.Add(new AuthoredDataError(
                EraSourceUrl,
                $"cpi year {row.Year.ToString(CultureInfo.InvariantCulture)} source '{row.Source}' is not an absolute https URL"));
        }

        AppendCpiYears(errors, data.CpiYears);
    }

    private static void AppendCpiYears(List<AuthoredDataError> errors, IReadOnlyList<CpiYear> years)
    {
        var counts = new Dictionary<int, int>();
        foreach (var row in years)
        {
            counts.TryGetValue(row.Year, out var count);
            counts[row.Year] = count + 1;
        }

        AppendMissingCpiYears(errors, counts);

        var duplicateYears = new List<int>();
        var outsideYears = new List<int>();
        foreach (var (year, count) in counts)
        {
            if (year >= FirstSeason && year <= CpiBook.BaseYear)
            {
                if (count > 1)
                {
                    duplicateYears.Add(year);
                }

                continue;
            }

            if (year == LastSeason && year > CpiBook.BaseYear)
            {
                if (count > 1)
                {
                    duplicateYears.Add(year);
                }

                continue;
            }

            outsideYears.Add(year);
        }

        duplicateYears.Sort();
        outsideYears.Sort();

        foreach (var year in duplicateYears)
        {
            errors.Add(new AuthoredDataError(
                EraCpiYear,
                $"cpi_us.json lists {year.ToString(CultureInfo.InvariantCulture)} more than once"));
        }

        foreach (var year in outsideYears)
        {
            errors.Add(new AuthoredDataError(
                EraCpiYear,
                $"cpi_us.json lists {year.ToString(CultureInfo.InvariantCulture)}, outside {FirstSeason.ToString(CultureInfo.InvariantCulture)}-{LastSeason.ToString(CultureInfo.InvariantCulture)}"));
        }

        foreach (var row in years)
        {
            if (row.Year != LastSeason || row.Year <= CpiBook.BaseYear || row.Partial)
            {
                continue;
            }

            errors.Add(new AuthoredDataError(
                EraCpiPartial,
                $"cpi_us.json year {row.Year.ToString(CultureInfo.InvariantCulture)} must have partial set to true"));
        }
    }

    private static void AppendMissingCpiYears(List<AuthoredDataError> errors, Dictionary<int, int> counts)
    {
        var rangeStart = -1;
        for (var year = FirstSeason; year <= CpiBook.BaseYear + 1; year++)
        {
            var missing = year <= CpiBook.BaseYear && !counts.ContainsKey(year);
            if (missing)
            {
                if (rangeStart < 0)
                {
                    rangeStart = year;
                }

                continue;
            }

            if (rangeStart < 0)
            {
                continue;
            }

            var from = rangeStart;
            var to = year - 1;
            var span = from == to
                ? from.ToString(CultureInfo.InvariantCulture)
                : $"{from.ToString(CultureInfo.InvariantCulture)}-{to.ToString(CultureInfo.InvariantCulture)}";
            errors.Add(new AuthoredDataError(EraCpiYear, $"cpi_us.json does not list {span}"));
            rangeStart = -1;
        }
    }

    private static void AppendCoverage(
        List<AuthoredDataError> errors,
        string dimensionId,
        List<TimelinePeriod> periods,
        string invertedCode,
        string gapCode,
        string overlapCode,
        string subject)
    {
        var counts = new int[LastSeason - FirstSeason + 1];
        foreach (var period in periods)
        {
            if (period.To is int toYear && period.From > toYear)
            {
                errors.Add(new AuthoredDataError(
                    invertedCode,
                    $"{subject} '{dimensionId}' period {period.From.ToString(CultureInfo.InvariantCulture)}-{toYear.ToString(CultureInfo.InvariantCulture)} ends before it starts"));
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

        AppendRanges(errors, dimensionId, counts, missing: true, gapCode, subject);
        AppendRanges(errors, dimensionId, counts, missing: false, overlapCode, subject);
    }

    private static void AppendRanges(
        List<AuthoredDataError> errors,
        string dimensionId,
        int[] counts,
        bool missing,
        string code,
        string subject)
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
                ? new AuthoredDataError(code, $"{subject} '{dimensionId}' does not cover {span}")
                : new AuthoredDataError(code, $"{subject} '{dimensionId}' overlaps in {span}"));
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
