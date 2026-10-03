using System.Globalization;

namespace Paddock.Domain.World;

/// <summary>
/// Looks up which lineage a constructor belongs to in one season.
/// </summary>
public static class TeamLineage
{
    /// <summary>
    /// The lineage id covering <paramref name="constructorId"/> in <paramref name="season"/>, or null when none does.
    /// Two different lineages covering the same constructor-season throw.
    /// </summary>
    public static string? LineageOf(
        string constructorId,
        int season,
        IReadOnlyList<LineageSpan> spans)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(constructorId);
        ArgumentNullException.ThrowIfNull(spans);

        string? found = null;
        foreach (var span in spans)
        {
            if (!string.Equals(span.ConstructorId, constructorId, StringComparison.Ordinal) || !Covers(span, season))
            {
                continue;
            }

            if (found is null)
            {
                found = span.LineageId;
                continue;
            }

            if (string.Equals(found, span.LineageId, StringComparison.Ordinal))
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Constructor '{constructorId}' belongs to more than one lineage in {season.ToString(CultureInfo.InvariantCulture)}.");
        }

        return found;
    }

    /// <summary>
    /// Constructor steps of one lineage, earliest season first.
    /// Spans that start in the same year keep their input order, which is the authored file order.
    /// An unknown lineage id returns an empty list.
    /// </summary>
    public static IReadOnlyList<LineageSpan> Chain(string lineageId, IReadOnlyList<LineageSpan> spans)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lineageId);
        ArgumentNullException.ThrowIfNull(spans);

        var indexed = new List<(LineageSpan Span, int Index)>();
        for (var i = 0; i < spans.Count; i++)
        {
            if (string.Equals(spans[i].LineageId, lineageId, StringComparison.Ordinal))
            {
                indexed.Add((spans[i], i));
            }
        }

        indexed.Sort(static (left, right) =>
        {
            var year = left.Span.FromYear.CompareTo(right.Span.FromYear);
            return year != 0 ? year : left.Index.CompareTo(right.Index);
        });

        var chain = new LineageSpan[indexed.Count];
        for (var i = 0; i < indexed.Count; i++)
        {
            chain[i] = indexed[i].Span;
        }

        return chain;
    }

    private static bool Covers(LineageSpan span, int season)
    {
        if (span.ToYear is int toYear && span.FromYear > toYear)
        {
            return false;
        }

        return span.FromYear <= season && (span.ToYear is null || season <= span.ToYear.Value);
    }
}
