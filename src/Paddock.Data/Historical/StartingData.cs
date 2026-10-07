using System.Globalization;
using System.Text.Json;
using Paddock.Domain.Cars;
using Paddock.Domain.Finance;
using Paddock.Domain.World;

namespace Paddock.Data.Historical;

/// <summary>
/// The starting data of every season (#271), written by the DataPipeline <c>starting-data</c> step to
/// <c>data/cache/reports/starting_data.json</c>. It stays local like the rest of the Jolpica cache (PP-041). Every number in it is an
/// ESTIMATE derived from the ratings car effect and the results, not a measured figure.
/// </summary>
public sealed record StartingDataReport(
    int SchemaVersion,
    string Notes,
    int FromYear,
    int ToYear,
    IReadOnlyList<StartingSeason> Seasons);

/// <summary>
/// One season: the car of each constructor the season knows (<see cref="Constructors"/>: every one that raced, and every team the
/// authored engine book lists for the season), the strength the step gave a team with no car effect (<see cref="NewEntrantStrength"/>),
/// and the constructors' final order of the season (<see cref="Standings"/>), which sets the budget tier of the season after it.
/// </summary>
public sealed record StartingSeason(
    int Season,
    double NewEntrantStrength,
    IReadOnlyList<StartingConstructor> Constructors,
    IReadOnlyList<StartingStanding> Standings);

/// <summary>
/// One constructor's start in a season. <see cref="StrengthSource"/> says where the strength came from (<see cref="StartingStrengthSources"/>);
/// <see cref="CarEffect"/> is the fitted effect when there is one. The engine is read from the authored engine book and is null when
/// the book has no row for the constructor in that season.
/// </summary>
public sealed record StartingConstructor(
    string ConstructorId,
    double Strength,
    string StrengthSource,
    double? CarEffect,
    string? EngineSupplier,
    string? EngineName);

public sealed record StartingStanding(string ConstructorId, int Position, decimal Points);

/// <summary>Where a constructor's starting strength came from.</summary>
public static class StartingStrengthSources
{
    /// <summary>The ratings car effect of that constructor (its lineage) in that season.</summary>
    public const string CarEffect = "car-effect";

    /// <summary>No effect that season, so the effect of the same lineage in the latest earlier season.</summary>
    public const string EarlierSeason = "earlier-season";

    /// <summary>No effect at all (a team new to the grid, or one in the engine book that never raced): the season's new-entrant strength.</summary>
    public const string NewEntrant = "new-entrant";
}

/// <summary>Reads <see cref="StartingDataReport"/>. A missing file is not an error: the world then uses the authored data alone.</summary>
public static class StartingDataLoader
{
    public const int SchemaVersion = 1;

    public const string FileName = "starting_data.json";

    /// <summary>The path the pipeline writes under a data root: <c>cache/reports/starting_data.json</c>.</summary>
    public static string DefaultPath(string dataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        return Path.Combine(Path.GetFullPath(dataRoot), "cache", "reports", FileName);
    }

    /// <summary>The report at <see cref="DefaultPath"/>, or null when the local cache has none.</summary>
    public static StartingDataReport? TryLoad(string dataRoot)
    {
        var path = DefaultPath(dataRoot);
        return File.Exists(path) ? Load(path) : null;
    }

    public static StartingDataReport Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Parse(File.ReadAllText(path));
    }

    public static StartingDataReport Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var report = JsonSerializer.Deserialize<StartingDataReport>(json, HistoricalJson.Options)
            ?? throw new JsonException("The starting data file is empty.");
        if (report.SchemaVersion != SchemaVersion)
        {
            throw new JsonException(
                "The starting data file has schema version " + report.SchemaVersion.ToString(CultureInfo.InvariantCulture)
                + ", expected " + SchemaVersion.ToString(CultureInfo.InvariantCulture) + ". Run the starting-data step again.");
        }

        return report;
    }
}

/// <summary>
/// The car strengths and budget tiers a career that starts in one year reads. The authored files win: an authored strength of a
/// constructor replaces the generated one, and an authored constructors' order of the previous season replaces the whole generated
/// order. Only the start season is taken from the generated data (strengths of the start season, the order of the season before),
/// so a later season of the career never reads how the real one went (PP-062).
/// </summary>
public sealed record StartingSources(int StartYear, ICarStrengthSource? CarStrength, ITeamTierSource? Tiers, bool FromGeneratedData)
{
    /// <summary>
    /// Layers <paramref name="generated"/> under the authored sources for <paramref name="startYear"/>. Without generated data this
    /// is the authored sources unchanged.
    /// </summary>
    public static StartingSources For(int startYear, StartingDataReport? generated, ICarStrengthSource? authoredStrength, ITeamTierSource? authoredTiers)
    {
        var season = generated?.Seasons.FirstOrDefault(entry => entry.Season == startYear);
        var previous = generated?.Seasons.FirstOrDefault(entry => entry.Season == startYear - 1);
        if (season is null && previous is null)
        {
            return new StartingSources(startYear, authoredStrength, authoredTiers, false);
        }

        ICarStrengthSource? strength = season is null ? authoredStrength : new LayeredCarStrength(authoredStrength, season);
        var tiers = authoredTiers;
        if (previous is not null && !Covers(authoredTiers, previous, startYear))
        {
            tiers = new PreviousSeasonStandingTier(previous.Standings.Select(standing =>
                new ConstructorStandingFact(previous.Season, standing.ConstructorId, standing.Position)));
        }

        return new StartingSources(startYear, strength, tiers, true);
    }

    /// <summary>True when the authored order knows the place of any constructor of the generated previous season.</summary>
    private static bool Covers(ITeamTierSource? authored, StartingSeason previous, int startYear) =>
        authored is not null
        && previous.Standings.Any(standing => authored.PreviousPlaceOf(OrganizationId.Real(standing.ConstructorId), startYear) is not null);

    /// <summary>
    /// The authored strength first, then the generated one of the start season. An id the season does not list (an engine maker
    /// that is not a constructor, for example) is unknown, so its reader keeps its own fallback. Reliability comes only from the
    /// authored rows.
    /// </summary>
    private sealed class LayeredCarStrength : ICarStrengthSource
    {
        private readonly ICarStrengthSource? _authored;
        private readonly int _season;
        private readonly Dictionary<string, double> _generated;

        public LayeredCarStrength(ICarStrengthSource? authored, StartingSeason season)
        {
            _authored = authored;
            _season = season.Season;
            _generated = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var constructor in season.Constructors)
            {
                _generated[constructor.ConstructorId] = constructor.Strength;
            }
        }

        public bool TryGet(string constructorId, int season, out double strength)
        {
            if (_authored is not null && _authored.TryGet(constructorId, season, out strength))
            {
                return true;
            }

            strength = 0;
            return season == _season && _generated.TryGetValue(constructorId, out strength);
        }

        public bool TryGetReliability(string constructorId, int season, out double reliability)
        {
            if (_authored is not null && _authored.TryGet(constructorId, season, out _))
            {
                return _authored.TryGetReliability(constructorId, season, out reliability);
            }

            reliability = 0;
            return false;
        }
    }
}
