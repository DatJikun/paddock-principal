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
/// points, the narrower the band that one observation produces. A scout's judgement decides how far an observation may be off
/// centre. A scout of judgement 17 or better never misses the truth. A poor one sometimes does, and then the new observation is
/// ignored if it contradicts what the organization already believes. Bands only ever shrink (inside the previous band), and
/// observation alone never makes a value exact. All numbers are ESTIMATES in <see cref="PoolEstimates"/>.
/// </para>
/// </summary>
public static class ScoutingModel
{
    /// <summary>Half the width of a band that rests on <paramref name="milliPoints"/> of observation, for a current attribute.</summary>
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

    /// <summary>The share of the half width a scout's centre may be off by. At most 1 means he cannot miss the truth.</summary>
    public static double NoiseShare(int talentJudgement)
    {
        var worst = PoolEstimates.WorstScoutNoiseShare;
        var best = PoolEstimates.BestScoutNoiseShare;
        return best + ((worst - best) * (GenerationEstimates.AttributeMax - talentJudgement) / (GenerationEstimates.AttributeMax - GenerationEstimates.AttributeMin));
    }

    /// <summary>
    /// One observation of a person: the new belief of <paramref name="observer"/>. Draws exactly one number for each true attribute
    /// (in key order) and one for the potential, however the bands turn out, so the draw sequence does not depend on the result.
    /// </summary>
    /// <param name="previous">What the observer believed before, or null for a first look.</param>
    /// <param name="totalMilli">Observation points the observer has on this person, counting this observation.</param>
    public static PersonKnowledge Observe(
        PersonKnowledge? previous,
        OrganizationId observer,
        PersonId subject,
        PersonTruth truth,
        ScoutProfile scout,
        long totalMilli,
        Xoshiro256StarStar rng)
    {
        ArgumentNullException.ThrowIfNull(truth);
        ArgumentNullException.ThrowIfNull(rng);
        var half = HalfWidth(totalMilli);
        var share = NoiseShare(scout.TalentJudgement);
        var bands = new List<KnownAttribute>(truth.Attributes.Count);
        foreach (var attribute in truth.Attributes)
        {
            AttributeBand? before = null;
            if (previous is not null)
            {
                foreach (var known in previous.Attributes)
                {
                    if (known.Key == attribute.Key)
                    {
                        before = known.Band;
                    }
                }
            }

            bands.Add(new KnownAttribute(attribute.Key, Narrow(before, attribute.Value, half, share, rng)));
        }

        var potential = Narrow(previous?.Potential, MeanPotential(truth), half * PoolEstimates.PotentialWidthFactor, share, rng);
        return new PersonKnowledge(observer, subject, bands, potential);
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

    private static AttributeBand Narrow(AttributeBand? previous, int truth, double half, double share, Xoshiro256StarStar rng)
    {
        var draw = rng.NextDouble();
        var centre = truth + (((2 * draw) - 1) * half * share);
        var low = Math.Max(GenerationEstimates.AttributeMin, (int)Math.Floor(centre - half));
        var high = Math.Min(GenerationEstimates.AttributeMax, (int)Math.Ceiling(centre + half));
        var floor = previous?.Low ?? GenerationEstimates.AttributeMin;
        var ceiling = previous?.High ?? GenerationEstimates.AttributeMax;
        low = Math.Max(low, floor);
        high = Math.Min(high, ceiling);
        if (low > high)
        {
            // The observation contradicts what is already believed (a poor scout missed). Keep the earlier belief.
            return previous ?? new AttributeBand(floor, ceiling);
        }

        while (high - low < PoolEstimates.MinBandSpan)
        {
            if (high < ceiling)
            {
                high++;
            }
            else if (low > floor)
            {
                low--;
            }
            else
            {
                break;
            }
        }

        return new AttributeBand(low, high);
    }
}
