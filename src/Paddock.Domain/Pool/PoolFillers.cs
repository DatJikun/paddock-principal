using System.Globalization;
using Paddock.Domain.People;
using Paddock.Domain.Random;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.Pool;

/// <summary>A fictional pool entrant, ready for <see cref="WorldState.AddPerson"/>, and the id the generator expects the world to give.</summary>
public sealed record PoolFiller(PersonSpec Spec, string ExpectedId);

/// <summary>
/// The generator side of the pool (T13 generator, DESIGN 2.1): fictional drivers who fill the pool so it is not a list of future
/// champions and so nobody can tell the real from the invented. A filler is built with the same code as any generated driver,
/// from the <c>People</c> stream only, with an entry age instead of the band's own age range.
/// How many fillers a season needs is the caller's decision (the pool is topped up to <see cref="PoolEstimates.TargetSize"/>).
/// </summary>
public static class PoolFillers
{
    /// <summary>
    /// The <paramref name="index"/>-th filler of <paramref name="year"/>. The band is drawn from a child of the stream tagged with
    /// the year and index, and the driver from a child tagged with the id, so a different number of fillers in another year, or an
    /// extra draw somewhere else, never changes this one (INV-004).
    /// </summary>
    public static PoolFiller Create(
        RngStream people,
        int year,
        int index,
        long nextPersonSequence,
        INameSource names,
        INameBlocklist blocklist,
        IReadOnlyList<(QualityBand Band, int Weight)> qualityWeights,
        IReadOnlyList<NationalityWeight> nationalities)
    {
        ArgumentNullException.ThrowIfNull(people);
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(blocklist);
        ArgumentNullException.ThrowIfNull(qualityWeights);
        ArgumentNullException.ThrowIfNull(nationalities);
        var band = PickBand(people, year, index, qualityWeights);
        var generator = new DriverGenerator(new StableIdAllocator(nextPersonSequence), names, blocklist);
        var request = GenerationRequest.ForNewAtAge(
            band,
            nationalities,
            year,
            PoolEstimates.FillerAgeMin,
            PoolEstimates.FillerAgeMaxExclusive);
        var driver = generator.Generate(people, year, request);
        var spec = new PersonSpec(
            driver.GivenName,
            driver.FamilyName,
            new GameDate(driver.BirthDate.Year, driver.BirthDate.Month, driver.BirthDate.Day),
            driver.Nationality,
            false,
            null,
            [PersonRole.Driver],
            PersonTruth.FromDriver(driver.Attributes, driver.PotentialAttributes),
            driver.IsFemale == true);
        return new PoolFiller(spec, driver.Id);
    }

    private static QualityBand PickBand(
        RngStream people,
        int year,
        int index,
        IReadOnlyList<(QualityBand Band, int Weight)> weights)
    {
        var total = 0;
        foreach (var (_, weight) in weights)
        {
            total += weight;
        }

        var tag = "pool-filler-band:v1:"
            + year.ToString(CultureInfo.InvariantCulture)
            + ":"
            + index.ToString(CultureInfo.InvariantCulture);
        var roll = people.DeriveChild(tag).NextInt(0, total);
        var cursor = 0;
        foreach (var (band, weight) in weights)
        {
            cursor += weight;
            if (roll < cursor)
            {
                return band;
            }
        }

        throw new InvalidOperationException("Quality weights did not cover the roll.");
    }
}
