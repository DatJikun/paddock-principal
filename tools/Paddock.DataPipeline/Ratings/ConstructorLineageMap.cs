using System.Text.Json;

namespace Paddock.DataPipeline;

public sealed record LineageEntry(string LineageId, string ConstructorId, int From, int? To);

/// <summary>
/// Resolves a (Jolpica constructorId, season) to a lineage key so that a renamed or sold team keeps one
/// car-effect series (data/authored/teams/lineage.json). Constructors without a lineage entry for that
/// season resolve to their own constructorId.
/// </summary>
public sealed class ConstructorLineageMap
{
    public static ConstructorLineageMap Empty { get; } = new([]);

    private readonly Dictionary<string, List<LineageEntry>> _byConstructor;

    public ConstructorLineageMap(IEnumerable<LineageEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _byConstructor = entries
            .GroupBy(e => e.ConstructorId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
    }

    public string Resolve(string constructorId, int season)
    {
        if (_byConstructor.TryGetValue(constructorId, out var list))
        {
            foreach (var e in list)
            {
                if (season >= e.From && (e.To is null || season <= e.To))
                {
                    return e.LineageId;
                }
            }
        }

        return constructorId;
    }

    public static ConstructorLineageMap Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var doc = JsonDocument.Parse(json);
        var entries = new List<LineageEntry>();
        if (!doc.RootElement.TryGetProperty("lineages", out var lineages))
        {
            return Empty;
        }

        foreach (var lineage in lineages.EnumerateArray())
        {
            var id = lineage.GetProperty("lineage_id").GetString();
            if (string.IsNullOrEmpty(id) || !lineage.TryGetProperty("entries", out var list))
            {
                continue;
            }

            foreach (var e in list.EnumerateArray())
            {
                var constructorId = e.GetProperty("constructorId").GetString();
                if (string.IsNullOrEmpty(constructorId))
                {
                    continue;
                }

                var from = e.GetProperty("from").GetInt32();
                int? to = e.TryGetProperty("to", out var toEl) && toEl.ValueKind == JsonValueKind.Number
                    ? toEl.GetInt32()
                    : null;
                entries.Add(new LineageEntry(id, constructorId, from, to));
            }
        }

        return new ConstructorLineageMap(entries);
    }
}
