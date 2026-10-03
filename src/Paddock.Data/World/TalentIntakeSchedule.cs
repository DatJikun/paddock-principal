using Paddock.Domain.Career;
using Paddock.Domain.People;
using Paddock.Domain.Random;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Data.World;

/// <summary>
/// Real drivers who join the talent pool after the opening season (T12).
/// Fully generated careers have no schedule. Skills are rolled once here, from the People stream
/// of the entry season, and admitting the person later does not roll again.
/// Drivers the world already holds, or who have no usable birth date, are left out.
/// </summary>
public static class TalentIntakeSchedule
{
    public static IReadOnlyList<ScheduledArrival> AfterStart(
        CareerConfig config,
        IPeopleProvider provider,
        WorldState world,
        ulong masterSeed,
        WorldInitOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(world);
        if (config.PeopleSource == PeopleSource.FullyGenerated)
        {
            return [];
        }

        options ??= new WorldInitOptions();
        var names = options.Names ?? new FixtureNameSource();
        var blocklist = options.Blocklist ?? EmptyNameBlocklist.Instance;
        var start = world.CurrentDate.Year;
        var arrivals = new List<ScheduledArrival>();
        foreach (var record in provider.Drivers.OrderBy(driver => driver.DriverId, StringComparer.Ordinal))
        {
            if (record.PoolEntryYear is not int entry || entry <= start)
            {
                continue;
            }

            if (world.Ids.WasIssued(record.DriverId))
            {
                continue;
            }

            if (!TryBirth(record.BirthDate, record.BornYear, out var birth))
            {
                continue;
            }

            var nationality = string.IsNullOrWhiteSpace(record.Nationality)
                ? WorldInitEstimates.UnknownNationality
                : record.Nationality.Trim();
            var identity = new KnownPersonIdentity(record.DriverId, record.GivenName, record.FamilyName, birth, nationality);
            var truth = Truth(config, provider, names, blocklist, masterSeed, entry, identity, record.DriverId);
            var spec = new PersonSpec(
                record.GivenName,
                record.FamilyName,
                new GameDate(birth.Year, birth.Month, birth.Day),
                nationality,
                true,
                record.DriverId,
                [PersonRole.Driver],
                truth);
            var on = new GameDate(entry, CareerDayEstimates.PoolEntryMonth, CareerDayEstimates.PoolEntryDay);
            arrivals.Add(new ScheduledArrival(on, spec));
        }

        return arrivals;
    }

    private static PersonTruth Truth(
        CareerConfig config,
        IPeopleProvider provider,
        INameSource names,
        INameBlocklist blocklist,
        ulong masterSeed,
        int season,
        KnownPersonIdentity identity,
        string driverId)
    {
        if (config.PeopleSource != PeopleSource.RealNamesRandomSkills)
        {
            var rating = provider.RatingFor(driverId, season);
            if (rating is not null)
            {
                return PersonTruth.FromDriver(rating.Current, rating.Potential);
            }
        }

        var strength = config.PeopleSource == PeopleSource.RealNamesRandomSkills
            ? WorldInitEstimates.RandomizedSkillStrength
            : WorldInitEstimates.UnratedFallbackStrength;
        var people = RngStream.Derive(masterSeed, RngStreamName.People, season);
        var generator = new DriverGenerator(new StableIdAllocator(1), names, blocklist);
        var driver = generator.RandomizeKnownPerson(people, season, identity, strength, WorldInitEstimates.PoolKnownQuality);
        return PersonTruth.FromDriver(driver.Attributes, driver.PotentialAttributes);
    }

    private static bool TryBirth(DateOnly? date, int? year, out DateOnly birth)
    {
        if (date is DateOnly exact)
        {
            birth = exact;
            return true;
        }

        if (year is int known && known >= GenerationEstimates.MinBirthYear && known <= GenerationEstimates.MaxBirthYear)
        {
            birth = new DateOnly(known, WorldInitEstimates.EstimatedBirthMonth, WorldInitEstimates.EstimatedBirthDay);
            return true;
        }

        birth = default;
        return false;
    }
}
