using Paddock.Domain.Cars;
using Paddock.Domain.Random;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Domain.People;

/// <summary>A world after empty staff chairs were filled, and the people who were added.</summary>
public sealed record StaffFill(WorldState World, IReadOnlyList<PersonId> Hired);

/// <summary>
/// Fills empty team chairs and keeps the race-engineer pairing (PP-059). Draws come from children of the People
/// stream tagged with the organization, the role and the season, so one team's hire does not move another's (INV-004).
/// </summary>
public static class StaffRoster
{
    public static bool HasVacancy(WorldState world, GameDate on)
    {
        ArgumentNullException.ThrowIfNull(world);
        foreach (var team in ActiveTeams(world))
        {
            foreach (var role in StaffCatalogue.TeamRoster)
            {
                if (role == StaffRole.RaceEngineer)
                {
                    continue;
                }

                if (Held(world, team.Id, role, on) == 0)
                {
                    return true;
                }
            }

            if (Held(world, team.Id, StaffRole.RaceEngineer, on) < Math.Max(DriverCount(world, team.Id, on), StaffEstimates.RaceEngineersPerTeam))
            {
                return true;
            }
        }

        return false;
    }

    public static StaffFill Fill(
        WorldState world,
        RngStream people,
        INameSource names,
        INameBlocklist? blocklist,
        GameDate on,
        Func<OrganizationId, string?>? homeCountry = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(people);
        ArgumentNullException.ThrowIfNull(names);
        var hired = new List<PersonId>();
        var teams = ActiveTeams(world);
        foreach (var team in teams)
        {
            var band = StaffEstimates.BandForRank(StaffEstimates.BudgetRank(teams, team.Id), teams.Count);
            var weights = StaffEstimates.NationalityWeights(homeCountry?.Invoke(team.Id));
            foreach (var role in StaffCatalogue.TeamRoster)
            {
                if (role == StaffRole.RaceEngineer)
                {
                    continue;
                }

                if (Held(world, team.Id, role, on) == 0)
                {
                    (world, var person) = Hire(world, people, names, blocklist, team.Id, role, on, 0, band, weights);
                    hired.Add(person);
                }
            }

            var drivers = DriverCount(world, team.Id, on);
            var needed = Math.Max(drivers, StaffEstimates.RaceEngineersPerTeam);
            var have = Held(world, team.Id, StaffRole.RaceEngineer, on);
            for (var seat = have; seat < needed; seat++)
            {
                (world, var person) = Hire(world, people, names, blocklist, team.Id, StaffRole.RaceEngineer, on, seat, band, weights);
                hired.Add(person);
            }
        }

        return new StaffFill(world, hired);
    }

    /// <summary>
    /// Attributes for a real person who has no authored rating, drawn in the organization's band.
    /// The child is tagged by the person and the role, so it does not depend on who else was generated.
    /// </summary>
    public static IReadOnlyList<NamedAttribute> AttributesForKnown(
        RngStream people,
        string personId,
        IReadOnlyList<StaffRole> roles,
        QualityBand band)
    {
        ArgumentNullException.ThrowIfNull(people);
        ArgumentException.ThrowIfNullOrWhiteSpace(personId);
        ArgumentNullException.ThrowIfNull(roles);
        var attributes = new List<NamedAttribute>();
        foreach (var role in roles)
        {
            var rng = PeopleSampling.Child(people, "staff-attr", personId + "|" + role);
            foreach (var key in StaffCatalogue.AttributeKeys(role))
            {
                if (attributes.All(attribute => !string.Equals(attribute.Key, key, StringComparison.Ordinal)))
                {
                    attributes.Add(new NamedAttribute(key, PeopleSampling.PickStaffAttribute(rng, band)));
                }
            }
        }

        if (attributes.All(attribute => !string.Equals(attribute.Key, StaffCatalogue.InnovationKey, StringComparison.Ordinal)))
        {
            var rng = PeopleSampling.Child(people, "staff-attr", personId + "|innovation");
            attributes.Add(new NamedAttribute(StaffCatalogue.InnovationKey, PeopleSampling.PickStaffAttribute(rng, band)));
        }

        return attributes;
    }

    /// <summary>Pairs each team's race engineers with its car drivers. Engineers and cars are taken in id order.</summary>
    public static StaffSection Pair(WorldState world, GameDate on)
    {
        ArgumentNullException.ThrowIfNull(world);
        var section = StaffSection.Empty;
        var cars = world.Section<CarsSection>(CarsSection.SectionName) ?? CarsSection.Empty;
        foreach (var team in ActiveTeams(world))
        {
            var engineers = Holders(world, team.Id, StaffRole.RaceEngineer, on);
            var drivers = cars.Of(team.Id)
                .Where(car => car.Driver is not null)
                .OrderBy(car => car.Id, StringComparer.Ordinal)
                .Select(car => car.Driver!.Value)
                .ToArray();
            var count = Math.Min(engineers.Count, drivers.Length);
            for (var i = 0; i < count; i++)
            {
                section = section.With(new RaceEngineerLink(
                    team.Id,
                    engineers[i],
                    drivers[i],
                    StaffEstimates.RelationshipStart,
                    on.Year));
            }
        }

        return section;
    }

    /// <summary>
    /// On a new season, a pair that is still at the same team grows. A seat that lost its engineer or its driver is paired again.
    /// </summary>
    public static StaffSection Advance(WorldState world, StaffSection current, GameDate on)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(current);
        var kept = StaffSection.Empty;
        foreach (var link in current.Links)
        {
            if (!StillTogether(world, link, on))
            {
                continue;
            }

            var relationship = link.Relationship;
            if (on.Year > link.Season)
            {
                relationship = Math.Min(
                    StaffEstimates.RelationshipMax,
                    link.Relationship + ((on.Year - link.Season) * StaffEstimates.RelationshipPerSeason));
            }

            kept = kept.With(new RaceEngineerLink(link.Team, link.Engineer, link.Driver, relationship, on.Year));
        }

        var cars = world.Section<CarsSection>(CarsSection.SectionName) ?? CarsSection.Empty;
        foreach (var team in ActiveTeams(world))
        {
            var takenEngineers = new HashSet<string>(StringComparer.Ordinal);
            var takenDrivers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var link in kept.Links)
            {
                if (link.Team != team.Id)
                {
                    continue;
                }

                takenEngineers.Add(link.Engineer.Value);
                takenDrivers.Add(link.Driver.Value);
            }

            var engineers = Holders(world, team.Id, StaffRole.RaceEngineer, on)
                .Where(id => !takenEngineers.Contains(id.Value))
                .ToArray();
            var drivers = cars.Of(team.Id)
                .Where(car => car.Driver is not null && !takenDrivers.Contains(car.Driver.Value.Value))
                .OrderBy(car => car.Id, StringComparer.Ordinal)
                .Select(car => car.Driver!.Value)
                .ToArray();
            var count = Math.Min(engineers.Length, drivers.Length);
            for (var i = 0; i < count; i++)
            {
                kept = kept.With(new RaceEngineerLink(
                    team.Id,
                    engineers[i],
                    drivers[i],
                    StaffEstimates.RelationshipStart,
                    on.Year));
            }
        }

        return kept;
    }

    public static QualityBand BandOf(WorldState world, OrganizationId organization)
    {
        var teams = ActiveTeams(world);
        if (teams.All(team => team.Id != organization))
        {
            return QualityBand.Solid;
        }

        return StaffEstimates.BandForRank(StaffEstimates.BudgetRank(teams, organization), teams.Count);
    }

    private static (WorldState World, PersonId Person) Hire(
        WorldState world,
        RngStream people,
        INameSource names,
        INameBlocklist? blocklist,
        OrganizationId organization,
        StaffRole role,
        GameDate on,
        int seat,
        QualityBand band,
        NationalityWeight[] weights)
    {
        var generator = new StaffGenerator(new StableIdAllocator(world.Ids.NextPerson), names, blocklist);
        var request = GenerationRequest.ForNew(band, weights, on.Year);
        var drawKey = organization.Value + "|" + role + "|" + on.Year.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + seat.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var staff = generator.Generate(people, on.Year, role, request, drawKey);
        var attributes = staff.Attributes.ToList();
        if (attributes.All(attribute => !string.Equals(attribute.Key, StaffCatalogue.InnovationKey, StringComparison.Ordinal)))
        {
            attributes.Add(new NamedAttribute(StaffCatalogue.InnovationKey, staff.Innovation));
        }

        var spec = new PersonSpec(
            staff.GivenName,
            staff.FamilyName,
            new GameDate(staff.BirthDate.Year, staff.BirthDate.Month, staff.BirthDate.Day),
            staff.Nationality,
            false,
            null,
            [PersonRole.Staff(role)],
            new PersonTruth(attributes, attributes));
        (world, var id) = world.AddPerson(spec);
        if (!string.Equals(id.Value, staff.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Generated person id " + staff.Id + " does not match the world id " + id.Value + ".");
        }

        var contract = new ContractSpec(
            id,
            organization,
            ContractRole.Staff(role),
            on,
            GameDate.SeasonEnd(on.Year + StaffEstimates.SeatContractSeasons - 1),
            0,
            false,
            null,
            null);
        (world, _) = world.AddContract(contract);
        world = world.SetKnowledge(new PersonKnowledge(organization, id, Exact(attributes), null));
        return (world, id);
    }

    private static bool StillTogether(WorldState world, RaceEngineerLink link, GameDate on)
    {
        if (world.GetPerson(link.Engineer).IsRetired || world.GetPerson(link.Driver).IsRetired)
        {
            return false;
        }

        var engineer = false;
        var driver = false;
        foreach (var contract in world.Contracts)
        {
            if (contract.OrganizationId != link.Team || !contract.IsActiveOn(on))
            {
                continue;
            }

            if (contract.PersonId == link.Engineer && contract.Role.IsStaff && contract.Role.StaffRole == StaffRole.RaceEngineer)
            {
                engineer = true;
            }

            if (contract.PersonId == link.Driver && contract.Role.IsDriver)
            {
                driver = true;
            }
        }

        return engineer && driver;
    }

    private static List<Organization> ActiveTeams(WorldState world)
    {
        var teams = new List<Organization>();
        foreach (var organization in world.Organizations)
        {
            if (organization.Kind == OrganizationKind.Team && organization.Dissolved is null)
            {
                teams.Add(organization);
            }
        }

        teams.Sort(static (left, right) => string.CompareOrdinal(left.Id.Value, right.Id.Value));
        return teams;
    }

    private static int Held(WorldState world, OrganizationId organization, StaffRole role, GameDate on) =>
        Holders(world, organization, role, on).Count;

    private static List<PersonId> Holders(WorldState world, OrganizationId organization, StaffRole role, GameDate on)
    {
        var holders = new List<PersonId>();
        foreach (var contract in world.Contracts)
        {
            if (contract.OrganizationId != organization || !contract.IsActiveOn(on) || !contract.Role.IsStaff || contract.Role.StaffRole != role)
            {
                continue;
            }

            if (world.GetPerson(contract.PersonId).IsRetired)
            {
                continue;
            }

            holders.Add(contract.PersonId);
        }

        holders.Sort(static (left, right) => string.CompareOrdinal(left.Value, right.Value));
        return holders;
    }

    private static int DriverCount(WorldState world, OrganizationId organization, GameDate on)
    {
        var count = 0;
        foreach (var contract in world.Contracts)
        {
            if (contract.OrganizationId == organization && contract.Role.IsDriver && contract.IsActiveOn(on) && !world.GetPerson(contract.PersonId).IsRetired)
            {
                count++;
            }
        }

        return count;
    }

    private static KnownAttribute[] Exact(IReadOnlyList<NamedAttribute> attributes)
    {
        var known = new KnownAttribute[attributes.Count];
        for (var i = 0; i < attributes.Count; i++)
        {
            known[i] = new KnownAttribute(attributes[i].Key, new AttributeBand(attributes[i].Value, attributes[i].Value));
        }

        return known;
    }
}
