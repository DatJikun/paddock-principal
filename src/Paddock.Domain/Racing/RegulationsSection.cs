using System.Globalization;
using Paddock.Domain.World;

namespace Paddock.Domain.Racing;

/// <summary>A proposal the teams rejected, so it is not brought back at once. <see cref="Season"/> is the season of the vote.</summary>
public sealed record RejectedRegulation
{
    public RejectedRegulation(string dimensionId, string value, int season)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimensionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentOutOfRangeException.ThrowIfLessThan(season, 1950);
        DimensionId = dimensionId;
        Value = value;
        Season = season;
    }

    public string DimensionId { get; }

    public string Value { get; }

    public int Season { get; }
}

/// <summary>
/// The <c>regulations</c> world section (T47, T23): the rule set in force for <see cref="Season"/> when the career votes
/// its rules (<see cref="Paddock.Domain.Career.RulesSource.VotedEachSeason"/>). A historical career does not write this
/// section; it reads the authored timeline. Absent means "use the authored rules".
/// <para>
/// Canonical text (schema 1):
/// <code>
/// regulations &lt;season&gt;
/// values &lt;count&gt;
/// value &lt;len&gt;:&lt;dimension&gt; &lt;len&gt;:&lt;value&gt;
/// rejected &lt;count&gt;
/// rejected &lt;len&gt;:&lt;dimension&gt; &lt;len&gt;:&lt;value&gt; &lt;season&gt;
/// </code>
/// </para>
/// </summary>
public sealed class RegulationsSection : IWorldSection
{
    public const string SectionName = "regulations";

    private readonly (string Dimension, string Value)[] _values;
    private readonly RejectedRegulation[] _rejected;

    private RegulationsSection(int season, (string Dimension, string Value)[] values, RejectedRegulation[] rejected)
    {
        Season = season;
        _values = values;
        _rejected = rejected;
    }

    public string Name => SectionName;

    public int SchemaVersion => 1;

    /// <summary>The season these values apply to (the season after the vote that produced them).</summary>
    public int Season { get; }

    public IReadOnlyList<(string Dimension, string Value)> Values => _values;

    public IReadOnlyList<RejectedRegulation> Rejected => _rejected;

    public static RegulationsSection Create(int season, IReadOnlyDictionary<string, string> values, IReadOnlyList<RejectedRegulation> rejected)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(season, 1950);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(rejected);
        if (values.Count == 0)
        {
            throw new ArgumentException("A voted rule set needs at least one dimension.", nameof(values));
        }

        var copy = new (string Dimension, string Value)[values.Count];
        var index = 0;
        foreach (var (dimension, value) in values)
        {
            if (string.IsNullOrWhiteSpace(dimension) || string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A rule dimension and its value are both required.", nameof(values));
            }

            copy[index++] = (dimension, value);
        }

        Array.Sort(copy, static (left, right) => string.CompareOrdinal(left.Dimension, right.Dimension));
        for (var i = 1; i < copy.Length; i++)
        {
            if (string.Equals(copy[i].Dimension, copy[i - 1].Dimension, StringComparison.Ordinal))
            {
                throw new ArgumentException("Dimension '" + copy[i].Dimension + "' is listed twice.", nameof(values));
            }
        }

        var memory = new RejectedRegulation[rejected.Count];
        var seen = new HashSet<string>(rejected.Count, StringComparer.Ordinal);
        for (var i = 0; i < rejected.Count; i++)
        {
            var item = rejected[i] ?? throw new ArgumentException("A rejected proposal is missing.", nameof(rejected));
            var key = item.DimensionId + "\u001f" + item.Value + "\u001f" + item.Season.ToString(CultureInfo.InvariantCulture);
            if (!seen.Add(key))
            {
                throw new ArgumentException("Rejected proposal '" + item.DimensionId + "' is listed twice.", nameof(rejected));
            }

            memory[i] = item;
        }

        Array.Sort(memory, static (left, right) =>
        {
            var dimension = string.CompareOrdinal(left.DimensionId, right.DimensionId);
            if (dimension != 0)
            {
                return dimension;
            }

            var value = string.CompareOrdinal(left.Value, right.Value);
            return value != 0 ? value : left.Season.CompareTo(right.Season);
        });
        return new RegulationsSection(season, copy, memory);
    }

    public RuleSet ToRuleSet()
    {
        var values = new Dictionary<string, string>(_values.Length, StringComparer.Ordinal);
        foreach (var (dimension, value) in _values)
        {
            values.Add(dimension, value);
        }

        return RuleSet.Restore(Season, values);
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Line("regulations " + Season.ToString(CultureInfo.InvariantCulture));
        writer.Count("values", _values.Length);
        foreach (var (dimension, value) in _values)
        {
            writer.Begin("value");
            writer.Field(dimension);
            writer.Space();
            writer.Field(value);
            writer.End();
        }

        writer.Count("rejected", _rejected.Length);
        foreach (var item in _rejected)
        {
            writer.Begin("rejected");
            writer.Field(item.DimensionId);
            writer.Space();
            writer.Field(item.Value);
            writer.Raw(" " + item.Season.ToString(CultureInfo.InvariantCulture));
            writer.End();
        }
    }
}
