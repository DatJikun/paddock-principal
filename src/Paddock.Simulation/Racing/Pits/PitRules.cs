using System.Globalization;
using Paddock.Domain.World;
using Paddock.Simulation.Racing.Tyres;

namespace Paddock.Simulation.Racing.Pits;

/// <summary>
/// What the regulations of a season say about pit stops. Read from the regulation catalog by <see cref="For"/>;
/// the numbers that are not in the data (lane speed limit, minimum stop time, crew size) are ESTIMATE
/// (see <see cref="PitConstants"/>).
/// </summary>
/// <param name="Season">The season the rules are for.</param>
/// <param name="TyreChangeAllowed">False only where the catalog dimension <c>dry_tyre_rule</c> forbids tyre changes (one season in the data).</param>
/// <param name="RefuellingAllowed">Catalog dimension <c>refuelling</c>.</param>
/// <param name="DriverChangeAllowed">True where a shared drive scores points (catalog dimension <c>shared_drive_points</c> is <c>shared_equally</c>), that is while cars are shared; later a swap is legal but worthless, so it is not offered.</param>
/// <param name="RequiredDistinctDryCompounds">Different dry compounds a dry race must be run on; 1 where there is no mandatory mix.</param>
/// <param name="PitLaneSpeedLimitKph">The pit-lane speed limit; null before <see cref="PitConstants.PitLaneLimitFirstSeason"/>.</param>
/// <param name="MinimumStopSeconds">Least time a stop may keep the car stationary; 0 for none.</param>
/// <param name="StandardCrewSize">People working on the car in a stop, in a typical team of the era.</param>
public sealed record PitRules(
    int Season,
    bool TyreChangeAllowed,
    bool RefuellingAllowed,
    bool DriverChangeAllowed,
    int RequiredDistinctDryCompounds,
    double? PitLaneSpeedLimitKph,
    double MinimumStopSeconds,
    int StandardCrewSize)
{
    /// <summary>Dimension id of the dry tyre rule in the regulation catalog.</summary>
    public const string DryTyreRuleDimension = "dry_tyre_rule";

    /// <summary>Dimension id of the shared-drive points rule in the regulation catalog.</summary>
    public const string SharedDriveDimension = "shared_drive_points";

    /// <summary>
    /// Some stop has a purpose: a tyre change, fuel or a driver change. Stops themselves are never banned in the data.
    /// </summary>
    public bool StopsAllowed => TyreChangeAllowed || RefuellingAllowed || DriverChangeAllowed;

    /// <summary>
    /// Reads the pit rules of a season from its rule set. A dimension the rule set does not have is not an error:
    /// no <c>dry_tyre_rule</c> means free tyre changes and no mandatory mix, no <c>refuelling</c> means banned (the
    /// cautious choice), no <c>shared_drive_points</c> means no driver changes. An unknown value throws.
    /// </summary>
    /// <exception cref="InvalidOperationException">A dimension has a value that is not understood.</exception>
    public static PitRules For(RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var season = rules.Season;

        var tyreChange = true;
        var requiredCompounds = 1;
        if (rules.Values.TryGetValue(DryTyreRuleDimension, out var tyreRule))
        {
            switch (tyreRule)
            {
                case "no_mandatory_compound_mix":
                    break;
                case "no_tyre_changes":
                    tyreChange = false;
                    break;
                case "two_compounds_both_mandatory":
                case "three_nominated_two_mandatory":
                    requiredCompounds = 2;
                    break;
                default:
                    throw NotUnderstood(DryTyreRuleDimension, tyreRule);
            }
        }

        var refuelling = false;
        if (rules.Values.TryGetValue(FuelRegime.RefuellingDimension, out var refuellingValue))
        {
            refuelling = refuellingValue switch
            {
                "allowed" => true,
                "banned" => false,
                _ => throw NotUnderstood(FuelRegime.RefuellingDimension, refuellingValue),
            };
        }

        var driverChange = false;
        if (rules.Values.TryGetValue(SharedDriveDimension, out var sharedValue))
        {
            driverChange = sharedValue switch
            {
                "shared_equally" => true,
                "not_shared" => false,
                _ => throw NotUnderstood(SharedDriveDimension, sharedValue),
            };
        }

        double? limit = season >= PitConstants.PitLaneLimitFirstSeason ? PitConstants.PitLaneLimitKph : null;
        var minimumStop = season >= PitConstants.MinimumStopFirstSeason ? PitConstants.MinimumStopSeconds : 0;
        var crew = (int)Math.Round(EraCurve.Interpolate(PitConstants.StandardCrewSizeAnchors, season));
        return new PitRules(season, tyreChange, refuelling, driverChange, requiredCompounds, limit, minimumStop, crew);
    }

    /// <summary>
    /// Stops a race must contain. A mandatory mix of two dry compounds needs one tyre change in a dry race; a wet race
    /// waives it, and so does a rule set where tyres cannot be changed.
    /// </summary>
    public int MinimumStops(bool dryRace) =>
        dryRace && TyreChangeAllowed && RequiredDistinctDryCompounds > 1 ? RequiredDistinctDryCompounds - 1 : 0;

    /// <summary>
    /// Whether the compounds a car has run satisfy the mandatory mix. Always true in a wet race and where there is no
    /// mandatory mix; otherwise at least <see cref="RequiredDistinctDryCompounds"/> different dry compounds are needed.
    /// </summary>
    /// <param name="usedCompoundIds">Compounds the car has used, any order, duplicates allowed.</param>
    /// <param name="isWetCompound">Whether a compound id is a wet-weather compound.</param>
    /// <param name="wetRace">True if the race is wet.</param>
    public bool MixSatisfied(IEnumerable<string> usedCompoundIds, Func<string, bool> isWetCompound, bool wetRace)
    {
        ArgumentNullException.ThrowIfNull(usedCompoundIds);
        ArgumentNullException.ThrowIfNull(isWetCompound);
        if (wetRace || RequiredDistinctDryCompounds <= 1 || !TyreChangeAllowed)
        {
            return true;
        }

        return usedCompoundIds.Where(id => !isWetCompound(id)).Distinct(StringComparer.Ordinal).Count() >= RequiredDistinctDryCompounds;
    }

    /// <summary>
    /// Seconds a stop costs in the lane against staying on the track, not counting the time the car stands: the
    /// lane time (length over the lane speed) less the time the same stretch takes at racing speed, plus
    /// <see cref="PitConstants.EntryExitSeconds"/>. With a speed limit the lane speed is the limit.
    /// </summary>
    /// <param name="laneLengthMetres">Length of the pit lane, entry to exit.</param>
    /// <param name="racingSpeedKph">Average speed on the track over the same stretch.</param>
    public double PitLaneTimeLossSeconds(
        double laneLengthMetres = PitConstants.DefaultLaneLengthMetres,
        double racingSpeedKph = PitConstants.DefaultRacingSpeedKph)
    {
        if (!(double.IsFinite(laneLengthMetres) && laneLengthMetres > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(laneLengthMetres), "The lane length must be positive.");
        }

        if (!(double.IsFinite(racingSpeedKph) && racingSpeedKph > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(racingSpeedKph), "The racing speed must be positive.");
        }

        var laneKph = PitLaneSpeedLimitKph ?? PitConstants.UnlimitedLaneKph;
        var laneSeconds = laneLengthMetres / (laneKph / 3.6);
        var trackSeconds = laneLengthMetres / (racingSpeedKph / 3.6);
        return Math.Max(0, laneSeconds - trackSeconds) + PitConstants.EntryExitSeconds;
    }

    private static InvalidOperationException NotUnderstood(string dimension, string value) =>
        new(string.Create(CultureInfo.InvariantCulture, $"Dimension '{dimension}' has the value '{value}', which is not understood."));
}
