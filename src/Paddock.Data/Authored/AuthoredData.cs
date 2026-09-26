using Paddock.Domain.World;

namespace Paddock.Data.Authored;

/// <summary>
/// Authored regulations and tracks loaded from <c>data/authored</c>.
/// </summary>
public sealed class AuthoredData
{
    public AuthoredData(
        IReadOnlyList<CatalogDimension> catalog,
        IReadOnlyList<TimelinePeriod> timeline,
        IReadOnlyList<OtherSeriesIdea> otherSeriesIdeas,
        CircuitsFile circuits,
        IReadOnlyList<RaceLayoutEntry> raceLayoutMap)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(otherSeriesIdeas);
        ArgumentNullException.ThrowIfNull(circuits);
        ArgumentNullException.ThrowIfNull(raceLayoutMap);

        Catalog = catalog;
        Timeline = timeline;
        OtherSeriesIdeas = otherSeriesIdeas;
        Circuits = circuits;
        RaceLayoutMap = raceLayoutMap;
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
    }

    public IReadOnlyList<CatalogDimension> Catalog { get; }

    public IReadOnlyList<TimelinePeriod> Timeline { get; }

    public IReadOnlyList<OtherSeriesIdea> OtherSeriesIdeas { get; }

    public CircuitsFile Circuits { get; }

    public IReadOnlyList<RaceLayoutEntry> RaceLayoutMap { get; }

    public IReadOnlyList<string> DimensionIds { get; }

    public IReadOnlyList<RulePeriod> Periods { get; }

    public IReadOnlyList<TrackLayout> Layouts { get; }

    public IReadOnlyList<RaceAssignment> RaceAssignments { get; }

    public RuleSet RuleSetFor(int season) => RuleSet.For(season, DimensionIds, Periods);

    public TrackLayout LayoutFor(int season, int round) =>
        RaceCalendar.LayoutFor(season, round, Layouts, RaceAssignments);

    private static Dictionary<string, double> ProfileWeights(LayoutProfile profile) =>
        new(StringComparer.Ordinal)
        {
            ["straights"] = profile.Straights,
            ["high_speed"] = profile.HighSpeed,
            ["low_speed"] = profile.LowSpeed,
            ["braking"] = profile.Braking,
        };
}
