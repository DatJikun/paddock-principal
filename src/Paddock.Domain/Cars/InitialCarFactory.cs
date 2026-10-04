using System.Globalization;
using Paddock.Domain.People;
using Paddock.Domain.Random;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Cars;

/// <summary>
/// Constructor-season car strength on 0..100, from the ratings car effect (T22) when a caller has it.
/// Null means the factory uses the tier fallback. The AI never reads this source (PP-050): it is world setup only.
/// </summary>
public interface ICarStrengthSource
{
    bool TryGet(string constructorId, int season, out double strength);
}

/// <summary>
/// ESTIMATE stand-in until a ratings run supplies the car effect. Mercedes is strong in 1954–1955 (PP-050).
/// </summary>
public sealed class EstimateCarStrength : ICarStrengthSource
{
    public static EstimateCarStrength Shared { get; } = new();

    public bool TryGet(string constructorId, int season, out double strength)
    {
        if (string.Equals(constructorId, "mercedes", StringComparison.Ordinal) && season is >= 1954 and <= 1955)
        {
            strength = season == 1955 ? 78 : 74;
            return true;
        }

        strength = 0;
        return false;
    }
}

/// <summary>A table of constructor-season strengths, for tests and for a future ratings load.</summary>
public sealed class MapCarStrengthSource : ICarStrengthSource
{
    private readonly Dictionary<string, double> _values;

    public MapCarStrengthSource(params (string Constructor, int Season, double Strength)[] rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        _values = new Dictionary<string, double>(rows.Length, StringComparer.Ordinal);
        foreach (var (constructor, season, strength) in rows)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(constructor);
            if (strength is < 0d or > 100d)
            {
                throw new ArgumentOutOfRangeException(nameof(rows));
            }

            _values.Add(Key(constructor, season), strength);
        }
    }

    public bool TryGet(string constructorId, int season, out double strength) =>
        _values.TryGetValue(Key(constructorId, season), out strength);

    private static string Key(string constructor, int season) =>
        constructor + "|" + season.ToString(CultureInfo.InvariantCulture);
}

/// <summary>A band around a true rating. The width is knowledge; the centre is not returned as its own field.</summary>
public readonly record struct RatingBand(double Low, double High);

/// <summary>
/// Bands an organization's engineers would give. Wider for a weaker technical director and aero head.
/// Pure and deterministic, so a query can call it without RNG (INV-005). The band never collapses to a point.
/// </summary>
public static class CarKnowledgeBands
{
    public static RatingBand Around(double truth, int technicalDirectorVision, int aeroHead)
    {
        var skill = (ClampStaff(technicalDirectorVision) + ClampStaff(aeroHead) - 2d) / 38d;
        var half = CarEstimates.MaxHalfWidth + ((CarEstimates.MinHalfWidth - CarEstimates.MaxHalfWidth) * skill);
        var low = CarEstimates.ClampRating(truth - half);
        var high = CarEstimates.ClampRating(truth + half);
        if (high <= low)
        {
            high = CarEstimates.ClampRating(low + 1d);
        }

        return new RatingBand(low, high);
    }

    private static int ClampStaff(int value) => Math.Clamp(value, 1, 20);
}

/// <summary>Staff attributes the car commands read. Missing chairs use <see cref="CarEstimates.DefaultStaffAttribute"/>.</summary>
public static class TeamEngineers
{
    public static int Attribute(WorldState world, OrganizationId organization, GameDate on, StaffRole role, string key)
    {
        ArgumentNullException.ThrowIfNull(world);
        foreach (var contract in world.Contracts)
        {
            if (contract.OrganizationId != organization || !contract.Role.IsStaff || contract.Role.StaffRole != role)
            {
                continue;
            }

            if (contract.Start > on || contract.End < on)
            {
                continue;
            }

            foreach (var attribute in world.TruthOf(contract.PersonId).Attributes)
            {
                if (attribute.Key == key)
                {
                    return attribute.Value;
                }
            }
        }

        return CarEstimates.DefaultStaffAttribute;
    }

    /// <summary>0..1 from chief-designer precision and technical-director vision. ESTIMATE scaling.</summary>
    public static double ExecutionQuality(WorldState world, OrganizationId organization, GameDate on)
    {
        var precision = Attribute(world, organization, on, StaffRole.ChiefDesigner, "precision");
        var vision = Attribute(world, organization, on, StaffRole.TechnicalDirector, "vision");
        return Math.Clamp((precision + vision) / 40d, 0d, 1d);
    }
}

/// <summary>
/// Builds the opening cars: two per racing team, each with a regular driver and no later swap (PP-050).
/// Historical strength comes from <see cref="ICarStrengthSource"/> when it knows the constructor; otherwise a tier.
/// A fully generated world ignores history and draws tiers from <see cref="RngStreamName.Development"/>.
/// </summary>
public static class InitialCarFactory
{
    public static WorldState Install(
        WorldState world,
        ulong masterSeed,
        int season,
        bool fullyGenerated,
        ICarStrengthSource? strength,
        IReadOnlyList<DriverFitProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(profiles);
        var source = strength ?? EstimateCarStrength.Shared;
        var cars = new List<TeamCar>();
        long next = 1;
        var opening = GameDate.SeasonStart(season);
        foreach (var organization in world.Organizations.OrderBy(org => org.Id.Value, StringComparer.Ordinal))
        {
            if (!Races(organization, opening))
            {
                continue;
            }

            var ceiling = CeilingOf(organization, season, fullyGenerated, masterSeed, source);
            var concept = CarConcept.Neutral;
            var effects = ConceptMapping.Effects(concept, ceiling);
            var levels = ConceptMapping.StartingLevels(concept, ceiling);
            var drivers = RaceDrivers(world, organization.Id, season);
            for (var seat = 0; seat < CarEstimates.CarsPerTeam; seat++)
            {
                PersonId? driver = seat < drivers.Count ? drivers[seat] : null;
                cars.Add(new TeamCar(
                    CarIds.Format(next),
                    organization.Id,
                    season,
                    concept,
                    levels,
                    ceiling,
                    CarEstimates.InitialUnderstanding,
                    effects.TyreWearMultiplier,
                    effects.SupplierChangeCost,
                    null,
                    driver));
                next++;
            }
        }

        if (cars.Count == 0 && profiles.Count == 0)
        {
            return world;
        }

        return world.WithSection(CarsSection.Restore(next, 1, cars, profiles));
    }

    public static IReadOnlyList<PersonId> RaceDrivers(WorldState world, OrganizationId organization, int season)
    {
        ArgumentNullException.ThrowIfNull(world);
        var opening = GameDate.SeasonStart(season);
        return world.Contracts
            .Where(contract => contract.OrganizationId == organization
                && contract.Role.IsDriver
                && contract.Role.Seat != SeatStatus.Reserve
                && contract.Start <= opening
                && contract.End >= opening)
            .OrderBy(contract => (int)contract.Role.Seat)
            .ThenBy(contract => contract.PersonId.Value, StringComparer.Ordinal)
            .Take(CarEstimates.CarsPerTeam)
            .Select(contract => contract.PersonId)
            .ToArray();
    }

    private static bool Races(Organization organization, GameDate opening) =>
        organization.Kind == OrganizationKind.Team
        && (organization.Dissolved is not GameDate dissolved || dissolved >= opening);

    private static double CeilingOf(
        Organization organization,
        int season,
        bool fullyGenerated,
        ulong masterSeed,
        ICarStrengthSource source)
    {
        if (fullyGenerated)
        {
            // ESTIMATE tier means. The spread is one uniform, not the philosophy draw, so a generated grid
            // is not a copy of the historical table.
            var rng = RngStream.Derive(masterSeed, RngStreamName.Development, season).DeriveChild("init:" + organization.Id.Value);
            var roll = rng.NextDouble();
            var mean = roll < 0.25d ? 42d : roll < 0.75d ? 55d : 72d;
            var noise = (rng.NextDouble() - 0.5d) * 8d;
            return CarEstimates.ClampRating(mean + noise);
        }

        if (organization.Id.IsReal && source.TryGet(organization.Id.Value, season, out var strength))
        {
            return CarEstimates.ClampRating(strength);
        }

        return CarEstimates.TierFallback;
    }
}
