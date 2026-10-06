using System.Globalization;
using Paddock.Domain.World;

namespace Paddock.Domain.Racing;

/// <summary>
/// What the race tape and the weekend result say about one car beyond its place: where it started, how many laps it
/// completed, its race time at the flag (null when it did not finish) and its best lap (null when it set none).
/// </summary>
public sealed record RaceRowDetail(int GridPosition, int LapsCompleted, long? TimeMs, long? FastestLapMs)
{
    public int GridPosition { get; } = GridPosition < 1
        ? throw new ArgumentOutOfRangeException(nameof(GridPosition))
        : GridPosition;

    public int LapsCompleted { get; } = LapsCompleted < 0
        ? throw new ArgumentOutOfRangeException(nameof(LapsCompleted))
        : LapsCompleted;

    public long? TimeMs { get; } = TimeMs is < 0 ? throw new ArgumentOutOfRangeException(nameof(TimeMs)) : TimeMs;

    public long? FastestLapMs { get; } = FastestLapMs is < 0 ? throw new ArgumentOutOfRangeException(nameof(FastestLapMs)) : FastestLapMs;
}

/// <summary>
/// One classified or unclassified car. <see cref="RetirementKey"/> is a translation key, empty when the car was classified.
/// <see cref="Detail"/> is null for a round stored before the details were kept.
/// </summary>
public sealed record RaceResultRow(int Position, bool Classified, string DriverId, string TeamId, string Points, string RetirementKey, RaceRowDetail? Detail = null)
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

/// <summary>
/// The facts of one race as a whole: laps the leader ran, the lap length at the time, the pole sitter and the fastest lap.
/// Stored with the round so a past race reads the same after the layout data changes.
/// </summary>
/// <param name="Laps">Laps the leader ran (fewer than scheduled after a red flag that ended the race).</param>
/// <param name="LapLengthMeters">The layout's lap length in whole metres.</param>
/// <param name="PoleDriverId">The driver who took pole, null when the grid names none.</param>
/// <param name="PoleTimeMs">His qualifying time in milliseconds, null when not known.</param>
/// <param name="FastestLapDriverId">The driver of the fastest race lap, null when no lap was run.</param>
/// <param name="FastestLapMs">The fastest race lap in milliseconds.</param>
public sealed record RaceFacts(int Laps, int LapLengthMeters, string? PoleDriverId, long? PoleTimeMs, string? FastestLapDriverId, long? FastestLapMs)
{
    public int Laps { get; } = Laps < 0 ? throw new ArgumentOutOfRangeException(nameof(Laps)) : Laps;

    public int LapLengthMeters { get; } = LapLengthMeters < 0 ? throw new ArgumentOutOfRangeException(nameof(LapLengthMeters)) : LapLengthMeters;

    public string? PoleDriverId { get; } = PoleDriverId is { Length: 0 } ? throw new ArgumentException("An empty id is no driver.", nameof(PoleDriverId)) : PoleDriverId;

    public long? PoleTimeMs { get; } = PoleTimeMs is < 0 ? throw new ArgumentOutOfRangeException(nameof(PoleTimeMs)) : PoleTimeMs;

    public string? FastestLapDriverId { get; } = FastestLapDriverId is { Length: 0 } ? throw new ArgumentException("An empty id is no driver.", nameof(FastestLapDriverId)) : FastestLapDriverId;

    public long? FastestLapMs { get; } = FastestLapMs is < 0 ? throw new ArgumentOutOfRangeException(nameof(FastestLapMs)) : FastestLapMs;
}

/// <summary>One finished round: the classification and the report, as keys and arguments. <see cref="Facts"/> is null for a round stored before they were kept.</summary>
public sealed record StoredRace(int Season, int Round, string LayoutId, IReadOnlyList<RaceResultRow> Rows, IReadOnlyList<StoredReportSection> Sections, RaceFacts? Facts = null)
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
/// Canonical text (schema 2), after the section header. The <c>facts</c> and <c>detail</c> lines are written only for a
/// round that has them, so a round stored under schema 1 keeps the text it had:
/// <code>
/// races &lt;count&gt;
/// race &lt;season&gt; &lt;round&gt; &lt;len&gt;:&lt;layout&gt;
/// facts &lt;laps&gt; &lt;lap metres&gt; &lt;len&gt;:&lt;pole driver&gt; &lt;pole ms or -&gt; &lt;len&gt;:&lt;fastest driver&gt; &lt;fastest ms or -&gt;
/// rows &lt;count&gt;
/// row &lt;position&gt; &lt;0|1&gt; &lt;len&gt;:&lt;driver&gt; &lt;len&gt;:&lt;team&gt; &lt;len&gt;:&lt;points&gt; &lt;len&gt;:&lt;retirement key&gt;
/// detail &lt;grid&gt; &lt;laps&gt; &lt;time ms or -&gt; &lt;fastest lap ms or -&gt;
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

    public int SchemaVersion => 2;

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
            if (race.Facts is { } facts)
            {
                writer.Begin("facts");
                writer.Raw(facts.Laps.ToString(CultureInfo.InvariantCulture));
                writer.Raw(" ");
                writer.Raw(facts.LapLengthMeters.ToString(CultureInfo.InvariantCulture));
                writer.Raw(" ");
                writer.Field(facts.PoleDriverId ?? "");
                writer.Raw(" ");
                writer.Raw(Number(facts.PoleTimeMs));
                writer.Raw(" ");
                writer.Field(facts.FastestLapDriverId ?? "");
                writer.Raw(" ");
                writer.Raw(Number(facts.FastestLapMs));
                writer.End();
            }

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
                if (row.Detail is { } detail)
                {
                    writer.Begin("detail");
                    writer.Raw(detail.GridPosition.ToString(CultureInfo.InvariantCulture));
                    writer.Raw(" ");
                    writer.Raw(detail.LapsCompleted.ToString(CultureInfo.InvariantCulture));
                    writer.Raw(" ");
                    writer.Raw(Number(detail.TimeMs));
                    writer.Raw(" ");
                    writer.Raw(Number(detail.FastestLapMs));
                    writer.End();
                }
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

    private static string Number(long? value) => value is long number ? number.ToString(CultureInfo.InvariantCulture) : "-";

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
