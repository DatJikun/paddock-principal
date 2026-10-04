using Paddock.Domain.People;
using Paddock.Domain.Random;
using Paddock.Domain.World;

namespace Paddock.Domain.Pool;

/// <summary>A scout as the observation sees him: how well he judges talent and how wide his contacts reach, both 1-20.</summary>
public readonly record struct ScoutProfile
{
    public ScoutProfile(int talentJudgement, int contactNetwork)
    {
        Check(talentJudgement, nameof(talentJudgement));
        Check(contactNetwork, nameof(contactNetwork));
        TalentJudgement = talentJudgement;
        ContactNetwork = contactNetwork;
    }

    public int TalentJudgement { get; }

    public int ContactNetwork { get; }

    /// <summary>An organization with no scout of its own (<see cref="PoolEstimates.DefaultScoutJudgement"/>).</summary>
    public static ScoutProfile Unscouted { get; } = new(PoolEstimates.DefaultScoutJudgement, PoolEstimates.DefaultScoutNetwork);

    /// <summary>The profile of a scout from his true attributes, or null when he has no scout attributes.</summary>
    public static ScoutProfile? From(PersonTruth truth)
    {
        ArgumentNullException.ThrowIfNull(truth);
        int? judgement = null;
        int? network = null;
        foreach (var attribute in truth.Attributes)
        {
            if (attribute.Key == "talent_judgement")
            {
                judgement = attribute.Value;
            }
            else if (attribute.Key == "contact_network")
            {
                network = attribute.Value;
            }
        }

        return judgement is int j && network is int n ? new ScoutProfile(j, n) : null;
    }

    private static void Check(int value, string name)
    {
        if (value < GenerationEstimates.AttributeMin || value > GenerationEstimates.AttributeMax)
        {
            throw new ArgumentOutOfRangeException(name, value, "Scout attributes are from 1 to 20.");
        }
    }
}

/// <summary>
/// How scouting narrows what an organization believes about a pool member (PP-013, DESIGN 6.2 and 10). Pure functions of their
/// arguments and of the generator they are given: the caller supplies a child of the <c>Scouting</c> stream, so this type never
/// reaches for randomness of its own (INV-004) and a query never calls it (INV-005).
/// <para>
/// Observation points accumulate with time, with the scout's contact network and with races shared with the person. The more
/// points, the narrower the band. The scout's judgement does two things: it widens his bands (a poor judge is less sure) and it
/// sets how far his reading of a person is off. That error is a bias that stays for a season (the generator the caller passes is
/// tagged with the season, the organization and the person), so more looking narrows the band around his own reading, which is
/// right for a good scout and wrong for a poor one. With the ESTIMATES of the day, a scout of judgement 11 or better never misses the truth, however narrow his band gets, and a poorer one sometimes does. Because the centre
/// of the band stays put while its width shrinks, bands only ever nest inside the earlier ones, and observation alone never makes
/// a value exact. All numbers are ESTIMATES in <see cref="PoolEstimates"/>.
/// </para>
/// </summary>
public static class ScoutingModel
{
    /// <summary>Half the width of a band that rests on <paramref name="milliPoints"/> of observation, for the best scout and a current attribute.</summary>
    public static double HalfWidth(long milliPoints)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(milliPoints);
        var points = milliPoints / 1000.0;
        return PoolEstimates.MinHalfWidth
            + ((PoolEstimates.StartHalfWidth - PoolEstimates.MinHalfWidth) * PoolEstimates.HalfWidthTauPoints
                / (PoolEstimates.HalfWidthTauPoints + points));
    }

    /// <summary>Observation points (thousandths) from one month of focus, scaled by the scout's contacts, plus shared races.</summary>
    public static long MonthlyMilli(ScoutFocusKind focus, ScoutProfile scout, int sharedRaces)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sharedRaces);
        var basis = focus == ScoutFocusKind.Person ? PoolEstimates.PersonFocusMilliPerMonth : PoolEstimates.PoolFocusMilliPerMonth;
        var percent = PoolEstimates.NetworkBasePercent + (PoolEstimates.NetworkPercentPerPoint * scout.ContactNetwork);
        return (basis * percent / 100) + (PoolEstimates.SharedRaceMilli * sharedRaces);
    }

    /// <summary>How much wider than the best scout's a scout's bands are: 1 at judgement 20, up to <see cref="PoolEstimates.WorstScoutWidthFactor"/> at 1.</summary>
    public static double WidthFactor(int talentJudgement)
    {
        var worst = PoolEstimates.WorstScoutWidthFactor;
        return 1.0 + ((worst - 1.0) * Deficit(talentJudgement));
    }

    /// <summary>The most a scout's reading of an attribute is off, in attribute points. Bands are rounded outward, so an error below the half width plus one point cannot miss the truth.</summary>
    public static double BiasLimit(int talentJudgement)
    {
        var best = PoolEstimates.BestScoutBias;
        var worst = PoolEstimates.WorstScoutBias;
        return best + ((worst - best) * Deficit(talentJudgement));
    }

    /// <summary>
    /// The belief of <paramref name="observer"/> about <paramref name="subject"/> after observation. Draws exactly one number for
    /// each true attribute (in key order) and one for the potential, however the bands turn out, so the draw sequence does not
    /// depend on the result. Pass the same generator (same tag) for the same season, organization and person: that is what keeps
    /// the scout's reading, and with it the nesting of bands, stable.
    /// </summary>
    /// <param name="totalMilli">Observation points the observer has on this person, counting the latest observation.</param>
    public static PersonKnowledge Observe(
        OrganizationId observer,
        PersonId subject,
        PersonTruth truth,
        ScoutProfile scout,
        long totalMilli,
        Xoshiro256StarStar rng)
    {
        ArgumentNullException.ThrowIfNull(truth);
        ArgumentNullException.ThrowIfNull(rng);
        var half = HalfWidth(totalMilli) * WidthFactor(scout.TalentJudgement);
        var limit = BiasLimit(scout.TalentJudgement);
        var bands = new List<KnownAttribute>(truth.Attributes.Count);
        foreach (var attribute in truth.Attributes)
        {
            bands.Add(new KnownAttribute(attribute.Key, Band(attribute.Value, half, limit, rng)));
        }

        var potential = Band(MeanPotential(truth), half * PoolEstimates.PotentialWidthFactor, limit * PoolEstimates.PotentialWidthFactor, rng);
        return new PersonKnowledge(observer, subject, bands, potential);
    }

    /// <summary>
    /// What an organization believes about a person after he has developed for a season. The hidden numbers only ever rise, and by
    /// at most <see cref="PoolEstimates.MaxAnnualStep"/>, so the upper edge of every attribute band moves up by that fixed amount
    /// and the lower edge stays: a belief that held the truth still holds it. The widening does not depend on the true growth, so it
    /// tells the organization nothing about it. The potential does not change with development and is left alone.
    /// </summary>
    public static PersonKnowledge Stale(PersonKnowledge knowledge)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        var bands = new List<KnownAttribute>(knowledge.Attributes.Count);
        foreach (var known in knowledge.Attributes)
        {
            var high = Math.Min(GenerationEstimates.AttributeMax, known.Band.High + PoolEstimates.MaxAnnualStep);
            bands.Add(new KnownAttribute(known.Key, new AttributeBand(known.Band.Low, high)));
        }

        return new PersonKnowledge(knowledge.ObserverId, knowledge.SubjectId, bands, knowledge.Potential);
    }

    /// <summary>The potential on the 1-20 scale: the mean of the hidden per-attribute ceilings, rounded to the nearest whole number.</summary>
    public static int MeanPotential(PersonTruth truth)
    {
        ArgumentNullException.ThrowIfNull(truth);
        var sum = 0;
        foreach (var attribute in truth.Potential)
        {
            sum += attribute.Value;
        }

        var count = truth.Potential.Count;
        return ((2 * sum) + count) / (2 * count);
    }

    private static AttributeBand Band(int truth, double half, double biasLimit, Xoshiro256StarStar rng)
    {
        var bias = ((2 * rng.NextDouble()) - 1) * biasLimit;
        var centre = Math.Clamp(truth + bias, GenerationEstimates.AttributeMin, GenerationEstimates.AttributeMax);
        var low = Math.Max(GenerationEstimates.AttributeMin, (int)Math.Floor(centre - half));
        var high = Math.Min(GenerationEstimates.AttributeMax, (int)Math.Ceiling(centre + half));
        return new AttributeBand(low, high);
    }

    private static double Deficit(int talentJudgement) =>
        (double)(GenerationEstimates.AttributeMax - talentJudgement) / (GenerationEstimates.AttributeMax - GenerationEstimates.AttributeMin);
}
