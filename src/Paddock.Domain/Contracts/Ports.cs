using System.Globalization;
using Paddock.Domain.People;
using Paddock.Domain.Random;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Contracts;

/// <summary>
/// How attractive an organization looks to a person, each part from 0 to 1. Built from what is public about the team
/// (results, history, the car it is expected to build), never from hidden simulation truth the person could not see.
/// </summary>
public readonly record struct OrganizationAppeal
{
    public OrganizationAppeal(double prestige, double expectedCar, double risk)
    {
        Prestige = Unit(prestige, nameof(prestige));
        ExpectedCar = Unit(expectedCar, nameof(expectedCar));
        Risk = Unit(risk, nameof(risk));
    }

    /// <summary>Standing of the organization: titles, history, name.</summary>
    public double Prestige { get; }

    /// <summary>The strength of the car the person expects to drive.</summary>
    public double ExpectedCar { get; }

    /// <summary>The danger that the team falls apart or falls behind (money, form, regulations).</summary>
    public double Risk { get; }

    /// <summary>The middle of every scale. Used when no source says otherwise.</summary>
    public static OrganizationAppeal Neutral { get; } = new(0.5, 0.5, 0.5);

    private static double Unit(double value, string name)
    {
        if (double.IsNaN(value) || value < 0.0 || value > 1.0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Appeal values run from 0 to 1.");
        }

        return value;
    }
}

/// <summary>
/// A source of <see cref="OrganizationAppeal"/>. Standings, finance and the car model are later systems; they register one.
/// A pure query: no state change, no RNG (INV-005).
/// </summary>
public interface IOrganizationAppealSource
{
    OrganizationAppeal Appeal(OrganizationId organization, GameDate on);
}

/// <summary>Every organization looks average.</summary>
public sealed class NeutralAppealSource : IOrganizationAppealSource
{
    public OrganizationAppeal Appeal(OrganizationId organization, GameDate on) => OrganizationAppeal.Neutral;
}

/// <summary>
/// The hidden personality of a person (DESIGN section 6.1). The world does not store it yet (T15 holds attributes only), so a
/// source supplies it. It is the person's own truth: only the person's decision reads it, and no query returns it.
/// </summary>
public interface IPersonalitySource
{
    PersonalityTraits TraitsOf(PersonId person);
}

/// <summary>
/// Stand-in until the world stores personality: derives the traits of a person from the career master seed and the person's id.
/// Pure and order-independent: the derivation uses a child stream of <see cref="RngStreamName.People"/> keyed by the id, so asking
/// about one person never changes the answer for another, and nothing is consumed. The values are ESTIMATES (flat 5 to 15).
/// </summary>
public sealed class DerivedPersonalitySource : IPersonalitySource
{
    private const int TraitMin = 5;
    private const int TraitMaxExclusive = 16;

    private readonly RngStream _people;

    public DerivedPersonalitySource(ulong masterSeed)
    {
        _people = RngStream.Derive(masterSeed, RngStreamName.People, 0);
    }

    public PersonalityTraits TraitsOf(PersonId person)
    {
        var rng = _people.DeriveChild("personality:" + person.Value);
        var primary = (PrimaryPersonality)rng.NextInt(0, 8);
        return new PersonalityTraits(
            primary,
            rng.NextInt(TraitMin, TraitMaxExclusive),
            rng.NextInt(TraitMin, TraitMaxExclusive),
            rng.NextInt(TraitMin, TraitMaxExclusive),
            rng.NextInt(TraitMin, TraitMaxExclusive),
            rng.NextInt(TraitMin, TraitMaxExclusive));
    }
}

/// <summary>
/// The reference pay for a subject at a given number of stars. This is not a market value (PP-035 has none): it is the era's
/// benchmark pay, scaled by the stars the offering team believes the person has.
/// </summary>
public interface IPayBenchmark
{
    /// <summary>Nominal pay per season. At least 1. <paramref name="stars"/> runs from 0 to 5.</summary>
    long Reference(int season, NegotiationSubject subject, double stars);
}

/// <summary>
/// The era benchmark: <c>driver_pay_midfield_nominal_usd</c> at 2.5 stars rising to <c>driver_pay_top_nominal_usd</c> at 5 stars,
/// falling linearly towards <see cref="NegotiationEstimates.MinPayShare"/> of the midfield below 2.5. Key staff earn
/// <see cref="NegotiationEstimates.StaffPayShare"/> of that. Both rules are ESTIMATES; the two dimensions come from the era catalog.
/// </summary>
public sealed class EraPayBenchmark : IPayBenchmark
{
    public const string MidfieldDimension = "driver_pay_midfield_nominal_usd";

    public const string TopDimension = "driver_pay_top_nominal_usd";

    private readonly Func<int, EraSet> _eras;

    public EraPayBenchmark(Func<int, EraSet> eras)
    {
        ArgumentNullException.ThrowIfNull(eras);
        _eras = eras;
    }

    public long Reference(int season, NegotiationSubject subject, double stars)
    {
        var era = _eras(season);
        var middle = Read(era, MidfieldDimension);
        var top = Math.Max(middle, Read(era, TopDimension));
        var clamped = Math.Clamp(stars, 0.0, 5.0);
        var pay = clamped <= NegotiationEstimates.MidfieldStars
            ? middle * (NegotiationEstimates.MinPayShare
                + ((1.0 - NegotiationEstimates.MinPayShare) * (clamped / NegotiationEstimates.MidfieldStars)))
            : middle + ((top - middle) * ((clamped - NegotiationEstimates.MidfieldStars) / (5.0 - NegotiationEstimates.MidfieldStars)));
        if (subject.Kind == NegotiationSubjectKind.StaffRole)
        {
            pay *= NegotiationEstimates.StaffPayShare;
        }

        return Math.Max(1L, (long)Math.Round(pay, MidpointRounding.AwayFromZero));
    }

    private static double Read(EraSet era, string dimension)
    {
        var text = era.Value(dimension);
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value) || value < 0.0)
        {
            throw new InvalidOperationException($"Era dimension '{dimension}' is '{text}', which is not a pay amount.");
        }

        return value;
    }
}

/// <summary>
/// Trust between a person and an organization, from 0 to 100 (DESIGN section 6.3). Trust and promises are a later system; this
/// port exists so the tie-break between equal offers already asks for it. A pure query (INV-005).
/// </summary>
public interface ITrustSource
{
    double Trust(PersonId person, OrganizationId organization);
}

/// <summary>Everyone trusts everyone the same. The default until trust exists.</summary>
public sealed class NeutralTrustSource : ITrustSource
{
    /// <summary>ESTIMATE: the neutral trust level.</summary>
    public const double Neutral = 50.0;

    public double Trust(PersonId person, OrganizationId organization) => Neutral;
}
