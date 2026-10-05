using Paddock.Domain.Cars;
using Paddock.Domain.World;

namespace Paddock.Domain.Supply;

/// <summary>
/// One version of a supplier's engine, each rating 0 to 100. Simulation truth: a manager reads bands, never this (INV-003).
/// Efficiency has no consumer yet: the fuel-load model (T30) does not read it, so it only shows in the supplier's profile.
/// </summary>
public readonly record struct EngineVersion(double Power, double Reliability, double Efficiency);

/// <summary>
/// What an engine adds to a car's performance vector, as offsets to the car's own power and reliability levels. Zero offsets mean
/// "no engine deal": the vector is exactly the T41 one.
/// </summary>
public readonly record struct EngineContribution(double PowerOffset, double ReliabilityOffset)
{
    public static EngineContribution None => default;
}

/// <summary>
/// The supplier's engine by season. <paramref name="versionSeason"/> is the season of the version, not the season of the race: a
/// customer with a lag gets an older version (<see cref="SupplyEstimates.LagSeasons"/>). A pure query (INV-005).
/// </summary>
public interface ISupplierProfiles
{
    EngineVersion EngineOf(OrganizationId supplier, int versionSeason);
}

/// <summary>
/// ESTIMATE stand-in for the designer and progress (no data, R13/R14): the base of a supplier at the anchor season is the car strength
/// the ratings give its constructor (so Mercedes is strong in 1955, PP-050), or the tier fallback, spread by a stable hash of the id;
/// the engine then gains <see cref="SupplyEstimates.ProgressPerSeason"/> a season. No RNG: the same supplier gives the same engine.
/// The own-engine programme (T42) replaces this for its own organization by wrapping it (see <see cref="IEngineProgrammes"/>).
/// </summary>
public sealed class EstimateSupplierProfiles : ISupplierProfiles
{
    private const string SupplierPrefix = "supplier:";

    private readonly int _anchorSeason;
    private readonly ICarStrengthSource? _strength;

    public EstimateSupplierProfiles(int anchorSeason, ICarStrengthSource? strength = null)
    {
        _anchorSeason = anchorSeason;
        _strength = strength;
    }

    public EngineVersion EngineOf(OrganizationId supplier, int versionSeason)
    {
        if (!supplier.IsAssigned)
        {
            throw new ArgumentException("Supplier id is unassigned.", nameof(supplier));
        }

        var key = supplier.Value.StartsWith(SupplierPrefix, StringComparison.Ordinal) ? supplier.Value[SupplierPrefix.Length..] : supplier.Value;
        var baseline = _strength is not null && _strength.TryGet(key, _anchorSeason, out var strength) ? strength : CarEstimates.TierFallback;
        var progress = SupplyEstimates.ProgressPerSeason * (versionSeason - _anchorSeason);
        return new EngineVersion(
            CarEstimates.ClampRating(baseline + Spread(supplier.Value, "power", SupplyEstimates.PowerSpread) + progress),
            CarEstimates.ClampRating(baseline + Spread(supplier.Value, "reliability", SupplyEstimates.ReliabilitySpread) + progress),
            CarEstimates.ClampRating(50 + Spread(supplier.Value, "efficiency", SupplyEstimates.EfficiencySpread) + progress));
    }

    /// <summary>A stable value from -width to +width for an id and a facet (FNV-1a, so it is the same on every platform).</summary>
    public static double Spread(string id, string facet, double width)
    {
        ulong hash = 14695981039346656037UL;
        foreach (var character in id + "|" + facet)
        {
            hash ^= character;
            hash *= 1099511628211UL;
        }

        var unit = (hash >> 11) / (double)(1UL << 53);
        return (unit * 2 - 1) * width;
    }
}

/// <summary>
/// The extension point of the own-engine programme (PP-019). NOT IMPLEMENTED in T43: an engine programme is a T42 development
/// project with its own budget category, so <c>EngineProgramme</c>, <c>StartEngineProgramme</c> and <c>OfferEngineToCustomers</c> wait
/// for T42 (#107). When it lands: (1) implement this port; (2) wrap <see cref="ISupplierProfiles"/> so the programme's organization
/// answers with its programme's version; (3) a team that <see cref="Sells"/> may then be named as supplier of an engine in
/// <c>ProposeSupplyDeal</c> and signs deals of the same <see cref="SupplyDeal"/> type; (4) <see cref="Voids"/> a programme when
/// <c>engine_formula</c> changes, ending its deals as <see cref="SupplyDealStatus.Voided"/>.
/// </summary>
public interface IEngineProgrammes
{
    /// <summary>True when the organization has an engine to sell in <paramref name="season"/> (first engine ready, programme not voided).</summary>
    bool Sells(OrganizationId organization, int season);

    /// <summary>True when the programme of the organization is voided by the rule in force in <paramref name="season"/>.</summary>
    bool Voids(OrganizationId organization, int season);
}

/// <summary>No organization runs an engine programme. The default until T42.</summary>
public sealed class NoEngineProgrammes : IEngineProgrammes
{
    public static NoEngineProgrammes Instance { get; } = new();

    public bool Sells(OrganizationId organization, int season) => false;

    public bool Voids(OrganizationId organization, int season) => false;
}

/// <summary>Turns a deal into what the car gets. Pure (INV-005).</summary>
public static class SupplyContribution
{
    /// <summary>The season of the engine version a deal delivers on <paramref name="raceSeason"/>.</summary>
    public static int VersionSeason(SupplyKind kind, int raceSeason) => raceSeason - SupplyEstimates.LagSeasons(kind);

    public static EngineContribution Of(SupplyDeal deal, ISupplierProfiles profiles, int raceSeason)
    {
        ArgumentNullException.ThrowIfNull(deal);
        ArgumentNullException.ThrowIfNull(profiles);
        if (deal.Item != SupplyItem.Engine)
        {
            return EngineContribution.None;
        }

        var version = profiles.EngineOf(deal.Supplier, VersionSeason(deal.Kind, raceSeason));
        var support = deal.Kind is SupplyKind.Partner or SupplyKind.Works ? SupplyEstimates.PartnerReliabilitySupport : 0;
        return new EngineContribution(
            (version.Power - 50) * SupplyEstimates.EnginePowerWeight,
            (version.Reliability + support - 50) * SupplyEstimates.EngineReliabilityWeight);
    }
}
