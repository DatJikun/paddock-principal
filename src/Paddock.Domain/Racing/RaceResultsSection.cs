using System.Globalization;
using Paddock.Domain.World;

namespace Paddock.Domain.Racing;

/// <summary>One classified or unclassified car. <see cref="RetirementKey"/> is a translation key, empty when the car was classified.</summary>
public sealed record RaceResultRow(int Position, bool Classified, string DriverId, string TeamId, string Points, string RetirementKey)
{
    public int Position { get; } = Position < 1
        ? throw new ArgumentOutOfRangeException(nameof(Position))
        : Position;

    public string DriverId { get; } = string.IsNullOrWhiteSpace(DriverId)
        ? throw new ArgumentException("A result row needs a driver.", nameof(DriverId))
        : DriverId;

    public string TeamId { get; } = string.IsNullOrWhiteSpace(TeamId)
        ? throw new ArgumentException("A result row needs a team.", nameof(TeamId))
        : TeamId;

    public string Points { get; } = Points ?? throw new ArgumentNullException(nameof(Points));

    public string RetirementKey { get; } = RetirementKey ?? throw new ArgumentNullException(nameof(RetirementKey));
}

/// <summary>One report argument. Values that name a person or a team keep the id, not a display name.</summary>
public sealed record RaceReportArg(string Name, string Value)
{
    public string Name { get; } = string.IsNullOrWhiteSpace(Name)
        ? throw new ArgumentException("A report argument needs a name.", nameof(Name))
        : Name;

    public string Value { get; } = Value ?? throw new ArgumentNullException(nameof(Value));
}

/// <summary>One report line: a translation key, its arguments, and a plural count when the line has one.</summary>
public sealed record StoredReportLine(string Key, string? Count, IReadOnlyList<RaceReportArg> Args)
{
    public string Key { get; } = string.IsNullOrWhiteSpace(Key)
        ? throw new ArgumentException("A report line needs a key.", nameof(Key))
        : Key;

    public IReadOnlyList<RaceReportArg> Args { get; } = Args?.ToArray() ?? throw new ArgumentNullException(nameof(Args));
}

/// <summary>A titled group of report lines. The title is itself a line.</summary>
public sealed record StoredReportSection(StoredReportLine Title, IReadOnlyList<StoredReportLine> Lines)
{
    public StoredReportLine Title { get; } = Title ?? throw new ArgumentNullException(nameof(Title));

    public IReadOnlyList<StoredReportLine> Lines { get; } = Lines?.ToArray() ?? throw new ArgumentNullException(nameof(Lines));
}

/// <summary>One finished round: the classification and the report, as keys and arguments.</summary>
public sealed record StoredRace(int Season, int Round, string LayoutId, IReadOnlyList<RaceResultRow> Rows, IReadOnlyList<StoredReportSection> Sections)
{
    public int Season { get; } = Season < 1950 ? throw new ArgumentOutOfRangeException(nameof(Season)) : Season;

    public int Round { get; } = Round < 1 ? throw new ArgumentOutOfRangeException(nameof(Round)) : Round;

    public string LayoutId { get; } = string.IsNullOrWhiteSpace(LayoutId)
        ? throw new ArgumentException("A race needs a layout.", nameof(LayoutId))
        : LayoutId;

    public IReadOnlyList<RaceResultRow> Rows { get; } = Rows?.ToArray() ?? throw new ArgumentNullException(nameof(Rows));

    public IReadOnlyList<StoredReportSection> Sections { get; } = Sections?.ToArray() ?? throw new ArgumentNullException(nameof(Sections));
}

/// <summary>
/// The <c>race-results</c> world section: every finished round of this career, so a result can be read after the day
/// and after a save. Absent until the first race, so a world that has not raced keeps its hash.
/// <para>
/// Canonical text (schema 1), after the section header:
/// <code>
/// races &lt;count&gt;
/// race &lt;season&gt; &lt;round&gt; &lt;len&gt;:&lt;layout&gt;
/// rows &lt;count&gt;
/// row &lt;position&gt; &lt;0|1&gt; &lt;len&gt;:&lt;driver&gt; &lt;len&gt;:&lt;team&gt; &lt;len&gt;:&lt;points&gt; &lt;len&gt;:&lt;retirement key&gt;
/// sections &lt;count&gt;
/// section
/// line &lt;len&gt;:&lt;key&gt; &lt;count or -&gt; &lt;arg count&gt;
/// arg &lt;len&gt;:&lt;name&gt; &lt;len&gt;:&lt;value&gt;
/// </code>
/// The first line of a section is its title. Later lines are the body, in order.
/// </para>
/// </summary>
public sealed class RaceResultsSection : IWorldSection
{
    public const string SectionName = "race-results";

    private readonly StoredRace[] _races;

    private RaceResultsSection(StoredRace[] races) => _races = races;

    public static RaceResultsSection Empty { get; } = new([]);

    public string Name => SectionName;

    public int SchemaVersion => 1;

    public bool IsEmpty => _races.Length == 0;

    public IReadOnlyList<StoredRace> Races => _races;

    public static RaceResultsSection Restore(IEnumerable<StoredRace> races)
    {
        ArgumentNullException.ThrowIfNull(races);
        var list = new List<StoredRace>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var race in races)
        {
            ArgumentNullException.ThrowIfNull(race);
            var key = race.Season.ToString(CultureInfo.InvariantCulture) + ":" + race.Round.ToString(CultureInfo.InvariantCulture);
            if (!seen.Add(key))
            {
                throw new ArgumentException("Round " + key + " is listed twice.", nameof(races));
            }

            list.Add(race);
        }

        list.Sort(static (left, right) =>
        {
            var season = left.Season.CompareTo(right.Season);
            return season != 0 ? season : left.Round.CompareTo(right.Round);
        });
        return new RaceResultsSection(list.ToArray());
    }

    public StoredRace? Find(int season, int round)
    {
        foreach (var race in _races)
        {
            if (race.Season == season && race.Round == round)
            {
                return race;
            }
        }

        return null;
    }

    public StoredRace? Latest() => _races.Length == 0 ? null : _races[^1];

    public RaceResultsSection With(StoredRace race)
    {
        ArgumentNullException.ThrowIfNull(race);
        var next = new List<StoredRace>(_races.Length + 1);
        var replaced = false;
        foreach (var existing in _races)
        {
            if (existing.Season == race.Season && existing.Round == race.Round)
            {
                next.Add(race);
                replaced = true;
            }
            else
            {
                next.Add(existing);
            }
        }

        if (!replaced)
        {
            next.Add(race);
        }

        return Restore(next);
    }

    public void WriteCanonical(CanonicalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Count("races", _races.Length);
        foreach (var race in _races)
        {
            writer.Begin("race");
            writer.Raw(race.Season.ToString(CultureInfo.InvariantCulture));
            writer.Raw(" ");
            writer.Raw(race.Round.ToString(CultureInfo.InvariantCulture));
            writer.Raw(" ");
            writer.Field(race.LayoutId);
            writer.End();
            writer.Count("rows", race.Rows.Count);
            foreach (var row in race.Rows)
            {
                writer.Begin("row");
                writer.Raw(row.Position.ToString(CultureInfo.InvariantCulture));
                writer.Raw(row.Classified ? " 1 " : " 0 ");
                writer.Field(row.DriverId);
                writer.Raw(" ");
                writer.Field(row.TeamId);
                writer.Raw(" ");
                writer.Field(row.Points);
                writer.Raw(" ");
                writer.Field(row.RetirementKey);
                writer.End();
            }

            writer.Count("sections", race.Sections.Count);
            foreach (var section in race.Sections)
            {
                writer.Line("section");
                WriteLine(writer, section.Title);
                writer.Count("lines", section.Lines.Count);
                foreach (var line in section.Lines)
                {
                    WriteLine(writer, line);
                }
            }
        }
    }

    private static void WriteLine(CanonicalWriter writer, StoredReportLine line)
    {
        writer.Begin("line");
        writer.Field(line.Key);
        writer.Raw(" ");
        writer.Raw(line.Count ?? "-");
        writer.Raw(" ");
        writer.Raw(line.Args.Count.ToString(CultureInfo.InvariantCulture));
        writer.End();
        foreach (var arg in line.Args)
        {
            writer.Begin("arg");
            writer.Field(arg.Name);
            writer.Raw(" ");
            writer.Field(arg.Value);
            writer.End();
        }
    }
}
