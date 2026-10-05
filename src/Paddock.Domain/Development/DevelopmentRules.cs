using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Development;

/// <summary>What development reads of the regulations of one season.</summary>
/// <param name="InSeasonTesting">Value of <c>in_season_testing</c>, or null when unknown.</param>
/// <param name="AeroTesting">Value of <c>aero_testing_restrictions</c>, or null when unknown.</param>
/// <param name="Fingerprint">Values of the technical dimensions that make old knowledge obsolete when they change.</param>
public sealed record DevelopmentEra(string? InSeasonTesting, string? AeroTesting, IReadOnlyDictionary<string, string> Fingerprint)
{
    public static DevelopmentEra Unrestricted { get; } = new(null, null, new Dictionary<string, string>());

    public double UnderstandingCap => DevelopmentMath.UnderstandingCap(InSeasonTesting, AeroTesting);

    /// <summary>How many fingerprint dimensions differ from <paramref name="previous"/>, and how many there are in total.</summary>
    public (int Changed, int Total) ChangedSince(DevelopmentEra previous)
    {
        ArgumentNullException.ThrowIfNull(previous);
        var keys = new SortedSet<string>(Fingerprint.Keys, StringComparer.Ordinal);
        keys.UnionWith(previous.Fingerprint.Keys);
        var changed = 0;
        foreach (var key in keys)
        {
            Fingerprint.TryGetValue(key, out var now);
            previous.Fingerprint.TryGetValue(key, out var before);
            if (!string.Equals(now, before, StringComparison.Ordinal))
            {
                changed++;
            }
        }

        return (changed, keys.Count);
    }
}

/// <summary>The era rules development reads, by year. A host backs it with the authored regulation periods (T23).</summary>
public interface IDevelopmentRules
{
    DevelopmentEra Era(int year);
}

/// <summary>Reads <see cref="DevelopmentEra"/> from authored rule periods.</summary>
public sealed class PeriodDevelopmentRules : IDevelopmentRules
{
    public const string InSeasonTestingDimension = "in_season_testing";

    public const string AeroTestingDimension = "aero_testing_restrictions";

    /// <summary>ESTIMATE: the technical dimensions whose change devalues stored knowledge. Equal weights.</summary>
    public static readonly IReadOnlyList<string> FingerprintDimensions =
    [
        "drs", "active_aero", "engine_formula", "forced_induction", "hybrid_system", "aero_package", "minimum_weight_kg",
        "tyre_supplier_model", "tyre_tread", "active_suspension", "traction_control", "anti_lock_brakes",
        "semi_automatic_gearbox", "fully_automatic_gearbox", "fuel_specification", "power_unit_allocation", "cockpit_protection",
    ];

    private readonly IReadOnlyList<RulePeriod> _periods;

    public PeriodDevelopmentRules(IReadOnlyList<RulePeriod> periods)
    {
        ArgumentNullException.ThrowIfNull(periods);
        _periods = periods;
    }

    public DevelopmentEra Era(int year)
    {
        var fingerprint = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var dimension in FingerprintDimensions)
        {
            if (ValueIn(dimension, year) is { } value)
            {
                fingerprint[dimension] = value;
            }
        }

        return new DevelopmentEra(ValueIn(InSeasonTestingDimension, year), ValueIn(AeroTestingDimension, year), fingerprint);
    }

    private string? ValueIn(string dimension, int year)
    {
        foreach (var period in _periods)
        {
            if (string.Equals(period.DimensionId, dimension, StringComparison.Ordinal)
                && period.FromYear <= year && (period.ToYear is null || year <= period.ToYear.Value))
            {
                return period.Value;
            }
        }

        return null;
    }
}

/// <summary>Reads the key engineers of a team from the world on one day. Truth, for the simulation only (INV-003).</summary>
public static class EngineerRoster
{
    private static readonly StaffRole[] Roles =
    [
        StaffRole.TechnicalDirector, StaffRole.ChiefDesigner, StaffRole.HeadOfAerodynamics, StaffRole.HeadOfVehicleDynamics,
    ];

    /// <summary>How many of the four development chairs exist in <paramref name="year"/>.</summary>
    public static int ChairsIn(int year) => Roles.Count(role => StaffCatalogue.AvailableFrom(role) <= year);

    /// <summary>
    /// The engineers in post on <paramref name="on"/>, in role order (ties by person id). A team with nobody in post gets one
    /// generic engineer with default attributes, so a team without key staff still develops, slowly and without flair.
    /// </summary>
    public static IReadOnlyList<Engineer> Of(WorldState world, OrganizationId organization, GameDate on)
    {
        ArgumentNullException.ThrowIfNull(world);
        var found = new List<(StaffRole Role, string Person, Engineer Engineer)>();
        foreach (var contract in world.Contracts)
        {
            if (contract.OrganizationId != organization || !contract.Role.IsStaff || contract.Start > on || contract.End < on)
            {
                continue;
            }

            var role = contract.Role.StaffRole;
            if (!Roles.Contains(role))
            {
                continue;
            }

            var person = world.GetPerson(contract.PersonId);
            var attributes = person.Truth.Attributes.ToDictionary(attribute => attribute.Key, attribute => attribute.Value, StringComparer.Ordinal);
            found.Add((
                role,
                contract.PersonId.Value,
                new Engineer(
                    contract.PersonId.Value,
                    role,
                    attributes,
                    DevelopmentMath.ExperienceOf(person.BirthDate, on),
                    DevelopmentMath.AdaptationOf(TenureStart(world, contract), on))));
        }

        var ordered = found
            .OrderBy(entry => entry.Role)
            .ThenBy(entry => entry.Person, StringComparer.Ordinal)
            .Select(entry => entry.Engineer)
            .ToList();
        if (ordered.Count == 0)
        {
            ordered.Add(new Engineer("staff:" + organization.Value, null, new Dictionary<string, int>(), 0.5d, 0.5d));
        }

        return ordered;
    }

    /// <summary>
    /// The start of unbroken service: walk back over contracts of the same person and organization that meet end-to-end.
    /// A renewal is a new contract starting the day after the previous one ends, and it does not reset adaptation.
    /// </summary>
    private static GameDate TenureStart(WorldState world, Contract contract)
    {
        var start = contract.Start;
        var guard = 0;
        while (guard++ < world.Contracts.Count + 1)
        {
            Contract? earlier = null;
            foreach (var candidate in world.Contracts)
            {
                if (candidate.PersonId != contract.PersonId || candidate.OrganizationId != contract.OrganizationId)
                {
                    continue;
                }

                if (candidate.End.AddDays(1) != start)
                {
                    continue;
                }

                if (earlier is null || candidate.Start < earlier.Start)
                {
                    earlier = candidate;
                }
            }

            if (earlier is null)
            {
                return start;
            }

            start = earlier.Start;
        }

        return start;
    }
}
