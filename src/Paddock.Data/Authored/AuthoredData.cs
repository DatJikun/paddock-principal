using Paddock.Domain.Cars;
using Paddock.Domain.Finance;
using Paddock.Domain.Infrastructure;
using Paddock.Domain.World;

namespace Paddock.Data.Authored;

/// <summary>
/// Authored regulations, tracks, technologies, teams, staff, and eras loaded from <c>data/authored</c>.
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
        IReadOnlyList<StaffMember> staff,
        IReadOnlyList<CatalogDimension> eraCatalog,
        IReadOnlyList<TimelinePeriod> eraTimeline,
        IReadOnlyList<CpiYear> cpiYears,
        IReadOnlyList<TrackGeometryFile>? trackGeometries = null,
        ICarStrengthSource? carStrength = null,
        ITeamTierSource? teamTiers = null,
        FacilityCatalog? facilities = null,
        BannedRulesFile? bannedRules = null,
        DriverRolesFile? driverRoles = null)
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
        ArgumentNullException.ThrowIfNull(eraCatalog);
        ArgumentNullException.ThrowIfNull(eraTimeline);
        ArgumentNullException.ThrowIfNull(cpiYears);

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
        EraCatalog = eraCatalog;
        EraTimeline = eraTimeline;
        CpiYears = cpiYears;
        DimensionIds = catalog.Select(dimension => dimension.Id).ToArray();
        Periods = timeline
            .Select(period => new RulePeriod(period.Dimension, period.Value, period.From, period.To))
            .ToArray();
        EraDimensionIds = eraCatalog.Select(dimension => dimension.Id).ToArray();
        EraPeriods = eraTimeline
            .Select(period => new RulePeriod(period.Dimension, period.Value, period.From, period.To))
            .ToArray();
        CpiBook = new CpiBook(cpiYears.Select(row => new CpiObservation(row.Year, row.Cpi)).ToArray());
        TrackGeometries = trackGeometries ?? [];
        CarStrength = carStrength;
        TeamTiers = teamTiers;
        Layouts = circuits.Circuits
            .SelectMany(circuit => circuit.Layouts.Select(layout => new TrackLayout(
                layout.LayoutId,
                circuit.CircuitId,
                layout.LengthKm,
                ProfileWeights(layout.ProfileGuess),
                layout.Character,
                circuit.Country)))
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
        Facilities = facilities ?? FacilityCatalog.Empty;
        BannedRulesFile = bannedRules;
        BannedRules = bannedRules is null ? Paddock.Domain.Racing.BannedRules.None : BannedRulesLoader.ToBannedRules(bannedRules);
        DriverRolesFile = driverRoles;
        DriverRoles = driverRoles is null ? DriverRoleOverrides.None : new DriverRoleOverrides(driverRoles.Roles);
    }

    /// <summary>
    /// The authored first/second driver file as it was read, or null when a fixture has none (the default applies to every team).
    /// Kept so the validator can check what was written (#325).
    /// </summary>
    public DriverRolesFile? DriverRolesFile { get; }

    /// <summary>The authored seat roles by start year and driver id, which win over the default (#325).</summary>
    public DriverRoleOverrides DriverRoles { get; }

    /// <summary>
    /// The authored banned list as it was read, or null when a fixture has none (nothing is banned then). Kept so the validator can
    /// check what was written, not only what the domain type holds.
    /// </summary>
    public BannedRulesFile? BannedRulesFile { get; }

    /// <summary>The rules and mechanics that can never be proposed or voted on, per series (#275).</summary>
    public Paddock.Domain.Racing.BannedRules BannedRules { get; }

    /// <summary>The authored ESTIMATE of constructor car strength, or null when a fixture has none (the tier fallback applies).</summary>
    public ICarStrengthSource? CarStrength { get; }

    /// <summary>
    /// The authored ESTIMATE of the previous season's constructors' order (<c>team_tiers_estimates.json</c>), or null when a fixture
    /// has none. It sets the opening budget of every team, and finance opens its books from the same tier (#234).
    /// </summary>
    public ITeamTierSource? TeamTiers { get; }

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

    public IReadOnlyList<CatalogDimension> EraCatalog { get; }

    public IReadOnlyList<TimelinePeriod> EraTimeline { get; }

    public IReadOnlyList<CpiYear> CpiYears { get; }

    public IReadOnlyList<TrackGeometryFile> TrackGeometries { get; }

    public IReadOnlyList<string> DimensionIds { get; }

    public IReadOnlyList<RulePeriod> Periods { get; }

    public IReadOnlyList<string> EraDimensionIds { get; }

    public IReadOnlyList<RulePeriod> EraPeriods { get; }

    public CpiBook CpiBook { get; }

    public IReadOnlyList<TrackLayout> Layouts { get; }

    public IReadOnlyList<RaceAssignment> RaceAssignments { get; }

    public IReadOnlyList<EngineSupply> EngineSupplies { get; }

    public IReadOnlyList<LineageSpan> LineageSpans { get; }

    public IReadOnlyList<StaffAssignment> StaffAssignments { get; }

    public FacilityCatalog Facilities { get; }

    public RuleSet RuleSetFor(int season) => RuleSet.For(season, DimensionIds, Periods);

    public EraSet EraSetFor(int season) => EraSet.For(season, EraDimensionIds, EraPeriods);

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
