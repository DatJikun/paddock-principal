using Paddock.Domain.World;

namespace Paddock.Data.Authored;

/// <summary>
/// Authored regulations, tracks, technologies, teams and staff loaded from <c>data/authored</c>.
/// </summary>
public sealed class AuthoredData
{
    public AuthoredData(
        IReadOnlyList<CatalogDimension> catalog,
        IReadOnlyList<TimelinePeriod> timeline,
        IReadOnlyList<OtherSeriesIdea> otherSeriesIdeas,
        CircuitsFile circuits,
        IReadOnlyList<RaceLayoutEntry> raceLayoutMap,
        IReadOnlyList<Technology> technologies,
        EnginesFile engines,
        LineageFile lineage,
        FoundersFile founders,
        IReadOnlyList<StaffMember> staff)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(otherSeriesIdeas);
        ArgumentNullException.ThrowIfNull(circuits);
        ArgumentNullException.ThrowIfNull(raceLayoutMap);
        ArgumentNullException.ThrowIfNull(technologies);
        ArgumentNullException.ThrowIfNull(engines);
        ArgumentNullException.ThrowIfNull(lineage);
        ArgumentNullException.ThrowIfNull(founders);
        ArgumentNullException.ThrowIfNull(staff);

        Catalog = catalog;
        Timeline = timeline;
        OtherSeriesIdeas = otherSeriesIdeas;
        Circuits = circuits;
        RaceLayoutMap = raceLayoutMap;
        Technologies = technologies;
        Engines = engines;
        Lineage = lineage;
        Founders = founders;
        Staff = staff;
        DimensionIds = catalog.Select(dimension => dimension.Id).ToArray();
        Periods = timeline
            .Select(period => new RulePeriod(period.Dimension, period.Value, period.From, period.To))
            .ToArray();
        Layouts = circuits.Circuits
            .SelectMany(circuit => circuit.Layouts.Select(layout => new TrackLayout(
                layout.LayoutId,
                circuit.CircuitId,
                layout.LengthKm,
                ProfileWeights(layout.ProfileGuess),
                layout.Character)))
            .ToArray();
        RaceAssignments = raceLayoutMap
            .Select(entry => new RaceAssignment(entry.Season, entry.Round, entry.LayoutId))
            .ToArray();
        ConstructorIds = ConstructorIdsOf(engines);
        EngineSupplies = engines.Entries
            .Select(entry => new EngineSupply(
                entry.ConstructorId,
                entry.Year,
                entry.Supplier,
                entry.EngineName,
                entry.Type))
            .ToArray();
        LineageSpans = lineage.Lineages
            .SelectMany(group => group.Entries.Select(entry => new LineageSpan(
                group.LineageId,
                entry.ConstructorId,
                entry.From,
                entry.To)))
            .ToArray();
        StaffAssignments = staff
            .SelectMany(person => person.Career.Select(stint => new StaffAssignment(
                person.Id,
                stint.Org,
                stint.Role,
                stint.From,
                stint.To)))
            .ToArray();
    }

    public IReadOnlyList<CatalogDimension> Catalog { get; }

    public IReadOnlyList<TimelinePeriod> Timeline { get; }

    public IReadOnlyList<OtherSeriesIdea> OtherSeriesIdeas { get; }

    public CircuitsFile Circuits { get; }

    public IReadOnlyList<RaceLayoutEntry> RaceLayoutMap { get; }

    public IReadOnlyList<Technology> Technologies { get; }

    public EnginesFile Engines { get; }

    public LineageFile Lineage { get; }

    public FoundersFile Founders { get; }

    public IReadOnlyList<StaffMember> Staff { get; }

    public IReadOnlySet<string> ConstructorIds { get; }

    public IReadOnlyList<string> DimensionIds { get; }

    public IReadOnlyList<RulePeriod> Periods { get; }

    public IReadOnlyList<TrackLayout> Layouts { get; }

    public IReadOnlyList<RaceAssignment> RaceAssignments { get; }

    public IReadOnlyList<EngineSupply> EngineSupplies { get; }

    public IReadOnlyList<LineageSpan> LineageSpans { get; }

    public IReadOnlyList<StaffAssignment> StaffAssignments { get; }

    public RuleSet RuleSetFor(int season) => RuleSet.For(season, DimensionIds, Periods);

    public TrackLayout LayoutFor(int season, int round) =>
        RaceCalendar.LayoutFor(season, round, Layouts, RaceAssignments);

    public IReadOnlyList<EngineSupply> EnginesFor(string constructorId, int season) =>
        EngineBook.EnginesFor(constructorId, season, EngineSupplies);

    public string? LineageOf(string constructorId, int season) =>
        TeamLineage.LineageOf(constructorId, season, LineageSpans);

    public IReadOnlyList<StaffAssignment> StaffAt(string org, int season) =>
        StaffBook.StaffAt(org, season, StaffAssignments);

    private static HashSet<string> ConstructorIdsOf(EnginesFile engines)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in engines.Entries)
        {
            ids.Add(entry.ConstructorId);
        }

        return ids;
    }

    private static Dictionary<string, double> ProfileWeights(LayoutProfile profile) =>
        new(StringComparer.Ordinal)
        {
            ["straights"] = profile.Straights,
            ["high_speed"] = profile.HighSpeed,
            ["low_speed"] = profile.LowSpeed,
            ["braking"] = profile.Braking,
        };
}
