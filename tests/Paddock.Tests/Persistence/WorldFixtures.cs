using System.Globalization;
using Paddock.Data.Authored;
using Paddock.Domain.People;
using Paddock.Domain.Random;
using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Persistence;

namespace Paddock.Tests.Persistence;

/// <summary>
/// Worlds for persistence tests. Real staff and the team lineage come from the authored files in the repo.
/// Attribute values, salaries, budgets and the fixture drivers are SYNTHETIC ESTIMATES that exist to exercise
/// storage. They are not historical facts and not calibrated numbers.
/// </summary>
internal static class WorldFixtures
{
    public static readonly GameDate Opening = new(1955, 1, 1);

    private static readonly Lazy<AuthoredData> Authored = new(
        () => AuthoredDataLoader.Load(Path.Combine(RepoPaths.Root(), "data")));

    public static SaveMeta Meta(DateOnly? date = null) => new(
        careerName: "Fixture career",
        managerName: "Fixture manager",
        playerTeamId: "tyrrell",
        currentGameDate: date ?? new DateOnly(1955, 1, 1),
        worldDataHash: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
        masterSeed: 42UL,
        careerConfig: Paddock.Domain.Career.CareerConfig.FromPreset(Paddock.Domain.Career.CareerPreset.Balanced));

    /// <summary>
    /// A world with real staff from the authored data, fixture drivers, drivers and staff from the T13 generators,
    /// real teams in the authored Tyrrell-to-Mercedes lineage, a generated team, contracts with options and
    /// release clauses, beliefs, and a removed person and contract (whose ids stay burned).
    /// </summary>
    public static WorldState Small()
    {
        var world = WorldState.At(Opening);

        var staff = Authored.Value.Staff
            .Where(member => member.Born is not null && member.Nationality is not null)
            .Take(12)
            .ToArray();
        var index = 0;
        foreach (var member in staff)
        {
            var (given, family) = SplitName(member.Name);
            var role = index % 4 == 0 ? StaffRole.TechnicalDirector : StaffRole.ChiefDesigner;
            var roles = index % 3 == 0
                ? new[] { PersonRole.Staff(role), PersonRole.TeamPrincipal }
                : [PersonRole.Staff(role)];
            (world, _) = world.AddPerson(new PersonSpec(
                given,
                family,
                ParseDate(member.Born!),
                member.Nationality!,
                true,
                member.Id,
                roles,
                SyntheticStaffTruth(role, index)));
            index++;
        }

        for (var i = 1; i <= 4; i++)
        {
            (world, _) = world.AddPerson(new PersonSpec(
                "Fixture",
                "Driver" + i.ToString(CultureInfo.InvariantCulture),
                new GameDate(1925 + i, 3, 14),
                "GBR",
                true,
                "fixture_driver_" + i.ToString(CultureInfo.InvariantCulture),
                [PersonRole.Driver],
                DriverTruth(6 + i, 12 + i)));
        }

        var ids = new StableIdAllocator();
        var names = new FixtureNameSource();
        var people = RngStream.Derive(7UL, RngStreamName.People, 1955);
        var request = GenerationRequest.ForNew(
            QualityBand.Solid,
            [new NationalityWeight("GBR", 3), new NationalityWeight("ITA", 2), new NationalityWeight("FRA", 1)],
            1955);
        var drivers = new DriverGenerator(ids, names);
        var generatedDrivers = new List<PersonId>();
        for (var i = 0; i < 8; i++)
        {
            var driver = drivers.Generate(people, 1955, request);
            PersonId id;
            (world, id) = world.AddPerson(new PersonSpec(
                driver.GivenName,
                driver.FamilyName,
                new GameDate(driver.BirthDate.Year, driver.BirthDate.Month, driver.BirthDate.Day),
                driver.Nationality,
                false,
                null,
                [PersonRole.Driver],
                PersonTruth.FromDriver(driver.Attributes, driver.PotentialAttributes)));
            generatedDrivers.Add(id);
        }

        var staffGenerator = new StaffGenerator(ids, names);
        var generatedStaff = staffGenerator.Generate(people, 1955, StaffRole.Scout, request);
        var attributes = generatedStaff.Attributes.ToArray();
        var ceilings = attributes.Select(attribute => new NamedAttribute(attribute.Key, Math.Min(20, attribute.Value + 1))).ToArray();
        (world, _) = world.AddPerson(new PersonSpec(
            generatedStaff.GivenName,
            generatedStaff.FamilyName,
            new GameDate(generatedStaff.BirthDate.Year, generatedStaff.BirthDate.Month, generatedStaff.BirthDate.Day),
            generatedStaff.Nationality,
            false,
            null,
            [PersonRole.Staff(generatedStaff.Role)],
            new PersonTruth(attributes, ceilings)));

        // The real team lineage, from data/authored/teams/lineage.json.
        var chain = TeamLineage.Chain("tyrrell-mercedes", Authored.Value.LineageSpans);
        var teams = new List<OrganizationId>();
        foreach (var step in chain)
        {
            OrganizationId id;
            var founded = new GameDate(step.FromYear, 1, 1);
            var spans = step.ToYear is int last
                ? new[] { new OrganizationNameSpan("Fixture " + step.ConstructorId, founded, new GameDate(last, 12, 31)) }
                : [new OrganizationNameSpan("Fixture " + step.ConstructorId, founded, null)];
            (world, id) = world.AddOrganization(new OrganizationSpec(
                OrganizationKind.Team,
                true,
                step.ConstructorId,
                founded,
                step.ToYear is int end ? new GameDate(end, 12, 31) : null,
                3_000_000 + (teams.Count * 250_000),
                spans));
            teams.Add(id);
        }

        for (var i = 1; i < teams.Count; i++)
        {
            var after = chain[i];
            world = world.LinkLineage(teams[i - 1], teams[i], new GameDate(after.FromYear, 1, 1), after.ToYear is int to ? new GameDate(to, 12, 31) : null);
        }

        // A generated team with a renamed second span, and an engine supplier.
        OrganizationId generatedTeam;
        (world, generatedTeam) = world.AddOrganization(new OrganizationSpec(
            OrganizationKind.Team,
            false,
            null,
            new GameDate(1955, 1, 1),
            null,
            2_000_000,
            [new OrganizationNameSpan("Generated Racing", new GameDate(1955, 1, 1), new GameDate(1956, 12, 31)), new OrganizationNameSpan("Generated Racing GP", new GameDate(1957, 1, 1), null)]));
        OrganizationId supplier;
        (world, supplier) = world.AddOrganization(new OrganizationSpec(
            OrganizationKind.EngineSupplier,
            true,
            "fixture_engine_co",
            new GameDate(1948, 1, 1),
            null,
            900_000,
            [new OrganizationNameSpan("Fixture Engine Co", new GameDate(1948, 1, 1), null)]));

        // Contracts. Drivers sign for a team for 1955, and some have a follow-up contract. Staff sign non-exclusively.
        var contractIndex = 0;
        foreach (var person in world.Persons.Where(candidate => candidate.Roles.Any(role => role.IsDriver)).ToArray())
        {
            var team = teams[contractIndex % teams.Count];
            if (world.GetOrganization(team).Dissolved is GameDate)
            {
                team = generatedTeam;
            }

            (world, _) = world.AddContract(new ContractSpec(
                person.Id,
                team,
                ContractRole.Driver((SeatStatus)(contractIndex % 4)),
                new GameDate(1955, 1, 1),
                new GameDate(1955, 12, 31),
                100_000 + (contractIndex * 10_000),
                true,
                contractIndex % 2 == 0 ? new ContractOption(new GameDate(1955, 9, 1), 1 + (contractIndex % 3)) : null,
                contractIndex % 3 == 0 ? new ReleaseClause(500_000 + contractIndex) : null));
            if (contractIndex % 2 == 1)
            {
                (world, _) = world.AddContract(new ContractSpec(
                    person.Id,
                    generatedTeam,
                    ContractRole.Driver(SeatStatus.Reserve),
                    new GameDate(1956, 1, 1),
                    new GameDate(1957, 12, 31),
                    80_000,
                    true,
                    null,
                    null));
            }

            contractIndex++;
        }

        foreach (var person in world.Persons.Where(candidate => candidate.Roles.Any(role => role.IsStaff)).Take(6).ToArray())
        {
            var role = person.Roles.First(candidate => candidate.IsStaff).StaffRole;
            (world, _) = world.AddContract(new ContractSpec(
                person.Id,
                supplier,
                ContractRole.Staff(role),
                new GameDate(1954, 1, 1),
                new GameDate(1956, 12, 31),
                60_000,
                false,
                null,
                null));
        }

        // Beliefs: bands only, one with a potential band and one without.
        var observer = teams[0];
        var subjects = world.Persons.Where(candidate => candidate.Roles.Any(role => role.IsDriver)).Take(3).ToArray();
        world = world.SetKnowledge(new PersonKnowledge(
            observer,
            subjects[0].Id,
            [new KnownAttribute("cornering", new AttributeBand(8, 12)), new KnownAttribute("braking", new AttributeBand(10, 16))],
            new AttributeBand(12, 18)));
        world = world.SetKnowledge(new PersonKnowledge(observer, subjects[1].Id, [new KnownAttribute("cornering", new AttributeBand(5, 9))], null));
        world = world.SetKnowledge(new PersonKnowledge(generatedTeam, subjects[0].Id, [], null));

        // Removed entities keep their ids burned (INV-009).
        world = world.RemovePerson(generatedDrivers[^1]);
        world = world.RemoveContract(world.Contracts[^1].Id);
        return world;
    }

    public static PersonSpec GeneratedSpec() => new(
        "Newcomer",
        "Fixture",
        new GameDate(1935, 5, 5),
        "ITA",
        false,
        null,
        [PersonRole.Driver],
        DriverTruth(7, 14));

    public static (string Given, string Family) SplitName(string name)
    {
        var trimmed = name.Trim();
        var cut = trimmed.LastIndexOf(' ');
        return cut < 0 ? (trimmed, trimmed) : (trimmed[..cut], trimmed[(cut + 1)..]);
    }

    public static GameDate ParseDate(string text) =>
        new(
            int.Parse(text[..4], CultureInfo.InvariantCulture),
            int.Parse(text.AsSpan(5, 2), CultureInfo.InvariantCulture),
            int.Parse(text.AsSpan(8, 2), CultureInfo.InvariantCulture));

    public static PersonTruth DriverTruth(int current, int potential)
    {
        var now = new DriverAttributes(current, current, current, current, current, current, current, current, current, current, current);
        var ceiling = new DriverAttributes(potential, potential, potential, potential, potential, potential, potential, potential, potential, potential, potential);
        return PersonTruth.FromDriver(now, ceiling);
    }

    private static PersonTruth SyntheticStaffTruth(StaffRole role, int seed)
    {
        var keys = StaffCatalogue.AttributeKeys(role);
        var attributes = new List<NamedAttribute>();
        var ceilings = new List<NamedAttribute>();
        for (var i = 0; i < keys.Count; i++)
        {
            var value = 4 + ((seed + (i * 3)) % 12);
            attributes.Add(new NamedAttribute(keys[i], value));
            ceilings.Add(new NamedAttribute(keys[i], Math.Min(20, value + 3)));
        }

        return new PersonTruth(attributes, ceilings);
    }
}
