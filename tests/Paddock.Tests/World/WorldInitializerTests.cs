using System.Globalization;
using Paddock.Data.Authored;
using Paddock.Data.World;
using Paddock.Domain.Cars;
using Paddock.Domain.Career;
using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Tests.World;

public class WorldInitializerTests
{
    private const ulong Seed = 20_26UL;

    [Theory]
    [InlineData(PeopleSource.RealTrajectory)]
    [InlineData(PeopleSource.RealPotential)]
    public void RealSourcesBuildOrganizationsLineageSuppliersAndSeats(PeopleSource source)
    {
        var result = Create(source);
        var world = result.World;
        var report = result.Report;

        Assert.Equal(GameDate.SeasonStart(WorldInitFixtures.Year), world.CurrentDate);
        Assert.Equal(["alpha", "bravo", "bravo_old", "charlie", "echo", "foxtrot", "supplier:aurum", "supplier:ferro_motors"],
            world.Organizations.Select(organization => organization.Id.Value).ToArray());
        Assert.Equal(4, report.Counts.Teams);
        Assert.Equal(2, report.Counts.DissolvedTeams);
        Assert.Equal(2, report.Counts.EngineSuppliers);
        Assert.Equal(2, report.Counts.LineageLinks);

        var alpha = world.GetOrganization(OrganizationId.Real("alpha"));
        Assert.Equal(OrganizationKind.Team, alpha.Kind);
        Assert.Equal(new GameDate(1958, 1, 1), alpha.Founded);
        Assert.Null(alpha.Dissolved);
        Assert.Equal("Alpha", alpha.NameOn(world.CurrentDate));
        Assert.Equal(new GameDate(1970, 1, 1), world.GetOrganization(OrganizationId.Real("bravo")).Founded);

        var oldBravo = world.GetOrganization(OrganizationId.Real("bravo_old"));
        Assert.Equal(new GameDate(1960, 1, 1), oldBravo.Founded);
        Assert.Equal(new GameDate(1969, 12, 31), oldBravo.Dissolved);
        Assert.Equal(
            [OrganizationId.Real("bravo_old"), OrganizationId.Real("bravo")],
            world.LineageChain(OrganizationId.Real("bravo")));
        Assert.Equal(
            [OrganizationId.Real("foxtrot"), OrganizationId.Real("echo")],
            world.LineageChain(OrganizationId.Real("echo")));

        Assert.Equal(
            ["alpha>supplier:aurum", "bravo>supplier:aurum", "echo>supplier:ferro_motors"],
            result.EngineSupplies.Select(link => link.Constructor.Value + ">" + link.Supplier.Value).ToArray());
        Assert.Equal("Aurum V8", result.EngineSupplies[0].EngineName);
    }

    [Fact]
    public void RealSourcesPlaceKeyStaffWithMappedRolesAndReportTheRest()
    {
        var result = Create(PeopleSource.RealTrajectory);
        var world = result.World;

        var principal = world.GetPerson(PersonId.Real("pat_principal"));
        Assert.Equal("Pat", principal.GivenName);
        Assert.Equal([PersonRole.TeamPrincipal], principal.Roles);
        Assert.Equal(new GameDate(1920, 3, 1), principal.BirthDate);

        var designer = world.GetPerson(PersonId.Real("tess_designer"));
        Assert.Equal(2, designer.Roles.Count);
        var designerContracts = world.Contracts.Where(contract => contract.PersonId == designer.Id).ToArray();
        Assert.Equal(2, designerContracts.Length);
        Assert.All(designerContracts, contract => Assert.False(contract.Exclusive));
        Assert.All(designerContracts, contract => Assert.Equal(OrganizationId.Real("alpha"), contract.OrganizationId));

        var particle = world.GetPerson(PersonId.Real("andre_de_vries"));
        Assert.Equal("André", particle.GivenName);
        Assert.Equal("de Vries", particle.FamilyName);
        Assert.Equal(WorldInitEstimates.UnknownNationality, particle.Nationality);
        Assert.Equal(new GameDate(WorldInitFixtures.Year - WorldInitEstimates.EstimatedStaffAge, 1, 1), particle.BirthDate);

        var suffix = world.GetPerson(PersonId.Real("wilson_junior"));
        Assert.Equal("Wilson", suffix.GivenName);
        Assert.Equal("Fittipaldi Júnior", suffix.FamilyName);

        var engineer = world.Contracts.Single(contract => contract.PersonId == PersonId.Real("engine_wiz"));
        Assert.Equal(OrganizationId.Real("supplier:aurum"), engineer.OrganizationId);
        Assert.Equal(StaffRole.EngineDesigner, engineer.Role.StaffRole);

        Assert.True(result.Report.Counts.StaffPeople > 5);
        Assert.Empty(Subjects(result, WorldInitGapCodes.TeamsWithoutStaff));
        AssertRoster(world, GameDate.SeasonStart(WorldInitFixtures.Year));
        Assert.Throws<InvalidOperationException>(() => world.GetPerson(PersonId.Real("future_guy")));
        Assert.Throws<InvalidOperationException>(() => world.GetPerson(PersonId.Real("past_guy")));
        Assert.Throws<InvalidOperationException>(() => world.GetPerson(PersonId.Real("indy_guy")));
        Assert.Throws<InvalidOperationException>(() => world.GetPerson(PersonId.Real("ghost_writer")));

        Assert.Equal(["pat_principal:owner@alpha"], Subjects(result, WorldInitGapCodes.StaffUnmappedRole));
        Assert.Equal(["ghost_writer@zulu"], Subjects(result, WorldInitGapCodes.StaffOrganizationAbsent));
        Assert.Equal(["andre_de_vries"], Subjects(result, WorldInitGapCodes.StaffBirthEstimated));
        Assert.Equal(5, Subjects(result, WorldInitGapCodes.StaffWithoutRatings).Length);
    }

    [Fact]
    public void RealTrajectoryPutsRacingDriversUnderContractAndFutureDriversInThePool()
    {
        var result = Create(PeopleSource.RealTrajectory);
        var world = result.World;
        var alpha = OrganizationId.Real("alpha");

        Assert.Equal(9, result.Report.Counts.RacingDrivers);
        Assert.Equal(1, result.Report.Counts.PoolDrivers);
        Assert.Equal([PersonId.Real("d_pool")], result.TalentPool);

        var racer = world.GetPerson(PersonId.Real("d_racer1"));
        Assert.Equal("Racer One", racer.Name);
        Assert.Equal(new GameDate(1940, 5, 5), racer.BirthDate);
        Assert.True(racer.IsReal);
        Assert.Equal(14, world.TruthOf(racer.Id).Value("cornering"));
        Assert.Equal(17, world.TruthOf(racer.Id).PotentialValue("cornering"));

        var contract = world.Contracts.Single(candidate => candidate.PersonId == racer.Id);
        Assert.Equal(alpha, contract.OrganizationId);
        Assert.True(contract.Role.IsDriver);
        Assert.True(contract.Exclusive);
        Assert.Equal(GameDate.SeasonStart(WorldInitFixtures.Year), contract.Start);
        Assert.Equal(GameDate.SeasonEnd(WorldInitFixtures.Year), contract.End);

        var moved = world.Contracts.Single(candidate => candidate.PersonId == PersonId.Real("d_multi"));
        Assert.Equal(alpha, moved.OrganizationId);

        var substitute = world.Contracts.Single(candidate => candidate.PersonId == PersonId.Real("d_sub"));
        Assert.Equal(SeatStatus.Reserve, substitute.Role.Seat);

        Assert.DoesNotContain(world.Contracts, candidate => candidate.PersonId == PersonId.Real("d_pool"));
        Assert.Equal(6, world.TruthOf(PersonId.Real("d_pool")).Value("cornering"));
        Assert.Throws<InvalidOperationException>(() => world.GetPerson(PersonId.Real("d_later")));
        Assert.Throws<InvalidOperationException>(() => world.GetPerson(PersonId.Real("d_past")));
        Assert.Throws<InvalidOperationException>(() => world.GetPerson(PersonId.Real("d_nobirth")));

        Assert.Equal(["d_ghost@zulu"], Subjects(result, WorldInitGapCodes.DriverConstructorAbsent));
        Assert.Equal(["d_nobirth"], Subjects(result, WorldInitGapCodes.DriverSkippedNoBirth));
        Assert.Equal(["d_racer2"], Subjects(result, WorldInitGapCodes.DriverBirthEstimated));
        Assert.Equal(["andre_de_vries", "d_racer3"], Subjects(result, WorldInitGapCodes.NationalityMissing));
        Assert.Equal(["d_ghost", "d_racer2", "d_sub"], Subjects(result, WorldInitGapCodes.DriversWithoutRatings));
        Assert.Equal(new GameDate(1945, WorldInitEstimates.EstimatedBirthMonth, WorldInitEstimates.EstimatedBirthDay), world.GetPerson(PersonId.Real("d_racer2")).BirthDate);
    }

    [Fact]
    public void RealSourcesGiveTheSameWorld()
    {
        Assert.Equal(
            Create(PeopleSource.RealTrajectory).World.StateHash(),
            Create(PeopleSource.RealPotential).World.StateHash());
    }

    [Fact]
    public void RealNamesRandomSkillsKeepsIdentitiesButIgnoresTheRatings()
    {
        var provider = WorldInitFixtures.Provider();
        var result = WorldInitializer.Create(Config(PeopleSource.RealNamesRandomSkills), WorldInitFixtures.Data(), provider, Seed);
        var real = Create(PeopleSource.RealTrajectory);

        Assert.Equal(0, provider.RatingCalls);
        Assert.Empty(Subjects(result, WorldInitGapCodes.DriversWithoutRatings));
        var racer = result.World.GetPerson(PersonId.Real("d_racer1"));
        Assert.Equal("Racer One", racer.Name);
        Assert.Equal(new GameDate(1940, 5, 5), racer.BirthDate);
        Assert.Equal(real.World.GetPerson(PersonId.Real("d_racer1")).Name, racer.Name);

        var truth = result.World.TruthOf(racer.Id);
        var rated = real.World.TruthOf(racer.Id);
        Assert.NotEqual(
            rated.Attributes.Select(attribute => attribute.Value).ToArray(),
            truth.Attributes.Select(attribute => attribute.Value).ToArray());
        Assert.Equal(11, truth.Attributes.Count);
        for (var i = 0; i < truth.Attributes.Count; i++)
        {
            Assert.InRange(truth.Attributes[i].Value, GenerationEstimates.AttributeMin, GenerationEstimates.AttributeMax);
            Assert.True(truth.Potential[i].Value >= truth.Attributes[i].Value);
        }

        Assert.Equal(9, result.Report.Counts.RacingDrivers);
        Assert.Equal(1, result.Report.Counts.PoolDrivers);
        Assert.Equal(real.World.Persons.Count, result.World.Persons.Count);
    }

    [Fact]
    public void FullyGeneratedFillsTheGridAndThePoolWithoutRealDrivers()
    {
        var result = Create(PeopleSource.FullyGenerated);
        var world = result.World;

        Assert.DoesNotContain(world.Persons, person => person.Roles.Any(role => role.IsDriver) && person.IsReal);
        Assert.Throws<InvalidOperationException>(() => world.GetPerson(PersonId.Real("pat_principal")));

        // Exactly two race seats per team, however many drivers the provider lists for it (#235).
        var seats = world.Contracts
            .Where(contract => contract.Role.IsDriver)
            .GroupBy(contract => contract.OrganizationId.Value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        Assert.Equal(
            new Dictionary<string, int> { ["alpha"] = 2, ["bravo"] = 2, ["charlie"] = 2, ["echo"] = 2 },
            seats);
        Assert.Equal(8, result.Report.Counts.RacingDrivers);
        Assert.Equal(4, result.Report.Counts.PoolDrivers);
        Assert.Equal(4, result.TalentPool.Count);
        Assert.All(result.TalentPool, id => Assert.DoesNotContain(world.Contracts, contract => contract.PersonId == id));

        Assert.Equal(0, result.Report.Counts.RealPersons);
        Assert.Equal(result.Report.Counts.GeneratedPersons, world.Persons.Count);
        Assert.Empty(Subjects(result, WorldInitGapCodes.TeamsWithoutStaff));
        AssertRoster(world, GameDate.SeasonStart(WorldInitFixtures.Year));

        foreach (var person in world.Persons)
        {
            Assert.False(person.IsReal);
            Assert.StartsWith("gen:", person.Id.Value, StringComparison.Ordinal);
            var truth = world.TruthOf(person.Id);
            Assert.All(truth.Attributes, attribute => Assert.InRange(attribute.Value, GenerationEstimates.AttributeMin, GenerationEstimates.AttributeMax));
            Assert.True(person.BirthDate.Year < WorldInitFixtures.Year);
        }

        var persons = world.Persons.Select(person => person.Id.Value).ToArray();
        Assert.Equal(persons.Length, persons.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            Enumerable.Range(1, persons.Length).Select(n => "gen:" + n.ToString(CultureInfo.InvariantCulture)).Order(StringComparer.Ordinal),
            persons.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void FullyGeneratedWithoutAnyProviderDataUsesTheDefaultSeatCount()
    {
        var result = WorldInitializer.Create(
            Config(PeopleSource.FullyGenerated),
            WorldInitFixtures.Data(),
            EmptyPeopleProvider.Instance,
            Seed);

        Assert.Equal(4 * WorldInitEstimates.RaceSeatsPerTeam, result.Report.Counts.RacingDrivers);
        Assert.Equal(4, result.Report.Counts.PoolDrivers);
    }

    [Theory]
    [InlineData(PeopleSource.RealTrajectory)]
    [InlineData(PeopleSource.RealNamesRandomSkills)]
    [InlineData(PeopleSource.FullyGenerated)]
    public void SameSeedConfigAndDataGiveTheSameWorldInAnyInputOrderAndCulture(PeopleSource source)
    {
        var expected = Create(source).World.StateHash();
        var reversed = WorldInitializer.Create(Config(source), WorldInitFixtures.Data(), WorldInitFixtures.Provider().Reversed(), Seed);
        Assert.Equal(expected, reversed.World.StateHash());

        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            Assert.Equal(expected, Create(source).World.StateHash());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData(PeopleSource.RealNamesRandomSkills)]
    [InlineData(PeopleSource.FullyGenerated)]
    public void ADifferentSeedChangesWhatTheGeneratorProduces(PeopleSource source)
    {
        var first = WorldInitializer.Create(Config(source), WorldInitFixtures.Data(), WorldInitFixtures.Provider(), 1);
        var second = WorldInitializer.Create(Config(source), WorldInitFixtures.Data(), WorldInitFixtures.Provider(), 2);

        Assert.NotEqual(first.World.StateHash(), second.World.StateHash());
    }

    [Fact]
    public void RealTrajectoryDoesNotDependOnTheSeedBecauseNothingIsRandomisedForRatedPeople()
    {
        // Every driver here is rated, so the seed has nothing to change.
        var drivers = WorldInitFixtures.Provider().Drivers.Where(driver => driver.DriverId is "d_racer1" or "d_pool").ToArray();
        var ratings = new Dictionary<string, DriverRating>(StringComparer.Ordinal)
        {
            ["d_racer1"] = WorldInitFixtures.Rating(14, 17),
            ["d_pool"] = WorldInitFixtures.Rating(6, 16),
        };
        var provider = new FixtureProvider(drivers, ratings);

        var first = WorldInitializer.Create(Config(PeopleSource.RealTrajectory), WorldInitFixtures.Data(), provider, 1);
        var second = WorldInitializer.Create(Config(PeopleSource.RealTrajectory), WorldInitFixtures.Data(), provider, 2);

        // Rated people stay seed-independent. Empty chairs and driver-fit rolls use the People stream, so the worlds differ.
        AssertSameTruth(first.World, second.World, PersonId.Real("d_racer1"));
        AssertSameTruth(first.World, second.World, PersonId.Real("d_pool"));
        Assert.NotEqual(first.World.StateHash(), second.World.StateHash());
    }

    [Fact]
    public void ThePlayerCanTakeAnExistingTeamOrAPlaceholderOrganization()
    {
        var existing = WorldInitializer.Create(
            Config(PeopleSource.RealTrajectory).WithPlayerTeam("bravo"),
            WorldInitFixtures.Data(),
            WorldInitFixtures.Provider(),
            Seed);
        Assert.Equal(OrganizationId.Real("bravo"), existing.PlayerOrganization);
        Assert.Equal(4, existing.Report.Counts.Teams);

        var placeholder = WorldInitializer.Create(
            Config(PeopleSource.RealTrajectory).WithPlayerTeam(CareerConfig.NewTeam),
            WorldInitFixtures.Data(),
            WorldInitFixtures.Provider(),
            Seed);
        Assert.False(placeholder.PlayerOrganization.IsReal);
        Assert.Equal("org:1", placeholder.PlayerOrganization.Value);
        var created = placeholder.World.GetOrganization(placeholder.PlayerOrganization);
        Assert.Equal(OrganizationKind.Team, created.Kind);
        Assert.Equal(GameDate.SeasonStart(WorldInitFixtures.Year), created.Founded);
        Assert.Equal(CareerConfig.NewTeam, created.NameOn(placeholder.World.CurrentDate));
        Assert.Contains(
            placeholder.World.Contracts,
            contract => contract.OrganizationId == placeholder.PlayerOrganization && contract.Role.IsStaff);
        Assert.DoesNotContain(
            placeholder.World.Contracts,
            contract => contract.OrganizationId == placeholder.PlayerOrganization && contract.Role.IsDriver);
        Assert.Equal(5, placeholder.Report.Counts.Teams);
    }

    [Fact]
    public void RefusesAnInvalidConfigAndAPlayerTeamThatDidNotExist()
    {
        var invalid = Assert.Throws<WorldInitException>(() => WorldInitializer.Create(
            Config(PeopleSource.RealTrajectory).WithStartYear(2030),
            WorldInitFixtures.Data(),
            EmptyPeopleProvider.Instance,
            Seed));
        Assert.Equal(WorldInitErrorCodes.InvalidConfig, invalid.Code);
        Assert.Contains(CareerConfigCodes.LateStartNeedsGeneratedPeople, invalid.Arguments);

        var missing = Assert.Throws<WorldInitException>(() => WorldInitializer.Create(
            Config(PeopleSource.RealTrajectory).WithPlayerTeam("delta"),
            WorldInitFixtures.Data(),
            EmptyPeopleProvider.Instance,
            Seed));
        Assert.Equal(WorldInitErrorCodes.UnknownPlayerTeam, missing.Code);
        Assert.Equal(["delta", "1970"], missing.Arguments);
    }

    [Fact]
    public void AStartAfterTheLastAuthoredSeasonUsesThatSeasonsTeamsAndSaysSo()
    {
        var result = WorldInitializer.Create(
            Config(PeopleSource.FullyGenerated).WithStartYear(1980).WithPlayerTeam("delta"),
            WorldInitFixtures.Data(),
            EmptyPeopleProvider.Instance,
            Seed);

        Assert.Equal(1980, result.Report.StartYear);
        Assert.Equal(1972, result.Report.ReferenceSeason);
        Assert.Equal(["1980"], Subjects(result, WorldInitGapCodes.ReferenceSeasonClamped));
        Assert.Equal(new GameDate(1980, 1, 1), result.World.CurrentDate);
        Assert.Equal(["delta"], result.World.Organizations.Where(organization => organization.Kind == OrganizationKind.Team).Select(organization => organization.Id.Value).ToArray());
    }

    [Fact]
    public void ScheduleBackedProviderJoinsTheScheduleWithTheDriverTable()
    {
        var schedule = new Paddock.Data.Historical.PeopleScheduleReport(
            2.5m,
            1980,
            [
                new Paddock.Data.Historical.ScheduledDriver(
                    "sched",
                    1940,
                    "British",
                    1962,
                    1970,
                    1960,
                    [new Paddock.Data.Historical.DriverStint(1970, "alpha", 3, 10, 8, "race")],
                    []),
                new Paddock.Data.Historical.ScheduledDriver("unnamed", null, null, 1970, 1970, 1968, [], []),
            ],
            [],
            [],
            []);
        var table = new[]
        {
            new Paddock.Data.Historical.HistoricalDriver("sched", "Sched", "Uled", "1940-05-05", "British", null, null, null),
        };
        var rated = new ScheduleBackedPeopleProvider(
            schedule,
            table,
            (id, season) => id == "sched" && season == 1970 ? WorldInitFixtures.Rating(12, 15) : null);

        var record = Assert.Single(rated.Drivers);
        Assert.Equal("Sched", record.GivenName);
        Assert.Equal(new DateOnly(1940, 5, 5), record.BirthDate);
        Assert.Equal(1960, record.PoolEntryYear);
        Assert.Equal(new DriverSeat(1970, "alpha", 3, "race", 8), Assert.Single(record.Seats));
        Assert.NotNull(rated.RatingFor("sched", 1970));
        Assert.Null(rated.RatingFor("sched", 1971));

        var result = WorldInitializer.Create(Config(PeopleSource.RealPotential), WorldInitFixtures.Data(), rated, Seed);
        var contract = result.World.Contracts.Single(candidate => candidate.PersonId == PersonId.Real("sched"));
        Assert.Equal(OrganizationId.Real("alpha"), contract.OrganizationId);
        Assert.Equal(12, result.World.TruthOf(PersonId.Real("sched")).Value("braking"));
    }

    [Fact]
    public void InitializerUsesOnlyThePeopleStream()
    {
        var path = Path.Combine(RepoPaths.Root(), "src", "Paddock.Data", "World", "WorldInitializer.cs");
        var text = File.ReadAllText(path);

        Assert.Contains("RngStreamName.People", text, StringComparison.Ordinal);
        foreach (var name in Paddock.Domain.Random.RngStreamName.All.Where(name => name != Paddock.Domain.Random.RngStreamName.People))
        {
            Assert.DoesNotContain("RngStreamName." + name, text, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("System.Random", text, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Authored1950BuildsEveryConstructorOfTheSeason()
    {
        var data = LoadRealData();
        var result = WorldInitializer.Create(
            CareerConfig.FromPreset(CareerPreset.MostHistorical).WithPlayerTeam("ferrari"),
            data,
            EmptyPeopleProvider.Instance,
            Seed);

        // Indianapolis 500-only constructors are a gap, not a two-car team (PP-050).
        Assert.Equal(1950, result.Report.ReferenceSeason);
        Assert.Equal(WorldInitEstimates.RaceSeatsPerTeam * result.Report.Counts.Teams, result.Report.Counts.RacingDrivers);
        Assert.Equal(OrganizationId.Real("ferrari"), result.PlayerOrganization);
        var seasonConstructors = data.Engines.Entries.Where(entry => entry.Year == 1950).Select(entry => entry.ConstructorId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var indyOnly = Subjects(result, WorldInitGapCodes.IndianapolisOnly);
        var teams = result.World.Organizations.Where(organization => organization.Kind == OrganizationKind.Team && organization.Dissolved is null).Select(organization => organization.Id.Value).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(seasonConstructors, indyOnly.Concat(teams).Order(StringComparer.Ordinal));
        Assert.Contains("kurtis_kraft", indyOnly);
        Assert.DoesNotContain("ferrari", indyOnly);
        Assert.Contains("ferrari", teams);
        Assert.Equal(teams.Length, result.Report.Counts.Teams);

        var enzo = result.World.GetPerson(PersonId.Real("enzo_ferrari"));
        Assert.Equal([PersonRole.TeamPrincipal], enzo.Roles);
        Assert.Contains(result.World.Contracts, contract => contract.PersonId == enzo.Id && contract.OrganizationId == OrganizationId.Real("ferrari"));
        Assert.Contains("enzo_ferrari:owner@ferrari", Subjects(result, WorldInitGapCodes.StaffUnmappedRole));
    }

    [Fact]
    public void Authored1988HasMcLarenWithRonDennisAndGordonMurray()
    {
        var data = LoadRealData();
        var result = WorldInitializer.Create(
            CareerConfig.FromPreset(CareerPreset.MostHistorical).WithStartYear(1988).WithPlayerTeam("mclaren"),
            data,
            EmptyPeopleProvider.Instance,
            Seed);
        var world = result.World;

        Assert.Equal(18, result.Report.Counts.Teams);
        Assert.True(result.Report.Counts.StaffPeople > 13);
        Assert.True(result.Report.Counts.GeneratedPersons > 0);
        Assert.Equal(7, result.Report.Counts.EngineSuppliers);
        Assert.Empty(Subjects(result, WorldInitGapCodes.TeamsWithoutStaff));
        AssertRoster(world, new GameDate(1988, 1, 1));

        var mclaren = OrganizationId.Real("mclaren");
        var dennis = world.GetPerson(PersonId.Real("ron_dennis"));
        Assert.Equal("Ron Dennis", dennis.Name);
        Assert.Contains(world.Contracts, contract => contract.PersonId == dennis.Id && contract.OrganizationId == mclaren && contract.Role.StaffRole == StaffRole.TeamPrincipal);
        var murray = world.GetPerson(PersonId.Real("gordon_murray"));
        Assert.Contains(world.Contracts, contract => contract.PersonId == murray.Id && contract.OrganizationId == mclaren && contract.Role.StaffRole == StaffRole.TechnicalDirector);
        var nichols = world.GetPerson(PersonId.Real("steve_nichols"));
        Assert.Equal([PersonRole.Staff(StaffRole.ChiefDesigner), PersonRole.Staff(StaffRole.RaceEngineer)], nichols.Roles);

        Assert.Equal("Mclaren", world.GetOrganization(mclaren).NameOn(world.CurrentDate));
        Assert.Equal(
            [OrganizationId.Real("toleman"), OrganizationId.Real("benetton")],
            world.LineageChain(OrganizationId.Real("benetton")).Take(2).ToArray());
        Assert.Equal(new GameDate(1985, 12, 31), world.GetOrganization(OrganizationId.Real("toleman")).Dissolved);
        Assert.Contains(result.EngineSupplies, link => link.Constructor == mclaren && link.Supplier.Value == "supplier:honda");
    }

    [Fact]
    public void FullyGeneratedStillGivesAValidGridOnRealData()
    {
        var data = LoadRealData();
        var config = CareerConfig.FromPreset(CareerPreset.Chaos).WithStartYear(1988).WithPlayerTeam("mclaren");
        var result = WorldInitializer.Create(config, data, EmptyPeopleProvider.Instance, Seed);
        var world = result.World;

        Assert.Equal(18 * WorldInitEstimates.RaceSeatsPerTeam, result.Report.Counts.RacingDrivers);
        Assert.Equal(18, world.Contracts.Where(contract => contract.Role.IsDriver).Select(contract => contract.OrganizationId).Distinct().Count());
        Assert.Equal(0, result.Report.Counts.RealPersons);
        Assert.Equal(18, result.Report.Counts.Teams);
        Assert.Equal(result.Report.Counts.GeneratedPersons, world.Persons.Count);
        Assert.Equal(
            world.Persons.Count,
            world.Contracts.Select(contract => contract.PersonId).Concat(result.TalentPool).Distinct().Count());
        Assert.Equal(
            Create1988(config, data),
            Create1988(config, data));
    }

    [Theory]
    [InlineData(PeopleSource.RealTrajectory)]
    [InlineData(PeopleSource.RealPotential)]
    [InlineData(PeopleSource.RealNamesRandomSkills)]
    [InlineData(PeopleSource.FullyGenerated)]
    public void EveryTeamStartsWithExactlyTwoRaceDriversAndAtMostTwoReserves(PeopleSource source)
    {
        var provider = CrowdedProvider();
        var result = WorldInitializer.Create(Config(source), WorldInitFixtures.Data(), provider, Seed);
        var world = result.World;

        foreach (var team in new[] { "alpha", "bravo", "charlie", "echo" })
        {
            var contracts = world.Contracts
                .Where(contract => contract.Role.IsDriver && contract.OrganizationId == OrganizationId.Real(team))
                .ToArray();
            Assert.Equal(2, contracts.Count(contract => contract.Role.Seat != SeatStatus.Reserve));
            Assert.InRange(contracts.Count(contract => contract.Role.Seat == SeatStatus.Reserve), 0, 2);
            Assert.Equal(2, InitialCarFactory.RaceDrivers(world, OrganizationId.Real(team), GameDate.SeasonStart(WorldInitFixtures.Year)).Count);
        }

        // One driver contract per person; everyone else is a free agent (no contract).
        var driverContracts = world.Contracts.Where(contract => contract.Role.IsDriver).ToArray();
        Assert.Equal(driverContracts.Length, driverContracts.Select(contract => contract.PersonId).Distinct().Count());
        Assert.All(result.TalentPool, id => Assert.DoesNotContain(driverContracts, contract => contract.PersonId == id));

        var again = WorldInitializer.Create(Config(source), WorldInitFixtures.Data(), provider.Reversed(), Seed);
        Assert.Equal(result.World.StateHash(), again.World.StateHash());
    }

    [Fact]
    public void RaceSeatsGoToTheDriversWithTheMostStartsAndSubstitutesBecomeReserves()
    {
        var world = WorldInitializer.Create(Config(PeopleSource.RealTrajectory), WorldInitFixtures.Data(), CrowdedProvider(), Seed).World;
        var alpha = OrganizationId.Real("alpha");

        string[] Of(Func<SeatStatus, bool> seat) => world.Contracts
            .Where(contract => contract.OrganizationId == alpha && contract.Role.IsDriver && seat(contract.Role.Seat))
            .Select(contract => contract.PersonId.Value)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["a1", "a2"], Of(seat => seat != SeatStatus.Reserve));
        Assert.Equal(["s1", "s2"], Of(seat => seat == SeatStatus.Reserve));
        Assert.Contains(world.Contracts, contract => contract.PersonId == PersonId.Real("bravo_only") && contract.OrganizationId == OrganizationId.Real("bravo"));
    }

    private static FixtureProvider CrowdedProvider()
    {
        static DriverSeat Race(string team, int starts) => new(WorldInitFixtures.Year, team, 1, "race", starts);
        static DriverSeat Sub(string team, int starts) => new(WorldInitFixtures.Year, team, 1, "substitute", starts);
        static RealDriverRecord Driver(string id, params DriverSeat[] seats) =>
            new(id, "Given", id, new DateOnly(1940, 1, 1), null, "British", 1960, 1958, seats);
        return new FixtureProvider(
            [
                Driver("a4", Race("alpha", 1)),
                Driver("a1", Race("alpha", 10)),
                Driver("a3", Race("alpha", 5)),
                Driver("a2", Race("alpha", 8)),
                Driver("s3", Sub("alpha", 1)),
                Driver("s1", Sub("alpha", 2)),
                Driver("s2", Sub("alpha", 1)),
                Driver("bravo_only", Race("bravo", 7)),
            ],
            new Dictionary<string, DriverRating>(StringComparer.Ordinal));
    }

    private static string Create1988(CareerConfig config, AuthoredData data) =>
        WorldInitializer.Create(config, data, EmptyPeopleProvider.Instance, Seed).World.StateHash();

    private static AuthoredData LoadRealData() => AuthoredDataLoader.Load(Path.Combine(RepoPaths.Root(), "data"));

    private static CareerConfig Config(PeopleSource source) =>
        CareerConfig.FromPreset(CareerPreset.Balanced).WithStartYear(WorldInitFixtures.Year).WithPeopleSource(source).WithPlayerTeam("alpha");

    private static WorldInitResult Create(PeopleSource source) =>
        WorldInitializer.Create(Config(source), WorldInitFixtures.Data(), WorldInitFixtures.Provider(), Seed);

    [Theory]
    [InlineData(CareerPreset.Balanced)]
    [InlineData(CareerPreset.Chaos)]
    public void A1955WorldFillsEveryTeamAndKeepsEngineDesignersOffTheRoster(CareerPreset preset)
    {
        var data = LoadRealData();
        var config = CareerConfig.FromPreset(preset).WithStartYear(1955).WithPlayerTeam("ferrari");
        var first = WorldInitializer.Create(config, data, EmptyPeopleProvider.Instance, Seed);
        var second = WorldInitializer.Create(config, data, EmptyPeopleProvider.Instance, Seed);
        Assert.Equal(first.World.StateHash(), second.World.StateHash());

        var world = first.World;
        var on = GameDate.SeasonStart(1955);
        AssertRoster(world, on);

        var ferrari = OrganizationId.Real("ferrari");
        var roster = Paddock.Application.Staff.StaffQuery.Of(world, ferrari, on);
        Assert.DoesNotContain(roster, row => row.Role is nameof(StaffRole.EngineDesigner) or nameof(StaffRole.TeamPrincipal));
        Assert.All(roster.Where(row => row.OwnTeam), row => Assert.NotNull(row.Attributes));
        Assert.All(roster.Where(row => !row.OwnTeam), row => Assert.Null(row.Attributes));
        Assert.All(roster.Where(row => !row.OwnTeam), row => Assert.Null(row.Relationship));

        if (preset == CareerPreset.Balanced)
        {
            var lampredi = world.Contracts.Single(contract => contract.PersonId == PersonId.Real("aurelio_lampredi"));
            Assert.Equal(ferrari, lampredi.OrganizationId);
            Assert.Equal(StaffRole.EngineDesigner, lampredi.Role.StaffRole);
            var busso = world.Contracts.Single(contract => contract.PersonId == PersonId.Real("giuseppe_busso"));
            Assert.Equal(OrganizationId.Real("supplier:alfa_romeo"), busso.OrganizationId);
            Assert.Equal(StaffRole.EngineDesigner, busso.Role.StaffRole);
            Assert.Contains(
                world.Contracts,
                contract => contract.PersonId == PersonId.Real("valerio_colotti")
                    && contract.OrganizationId == OrganizationId.Real("maserati")
                    && contract.Role.StaffRole == StaffRole.ChiefDesigner);
            var maserati = OrganizationId.Real("maserati");
            var own = Paddock.Application.Staff.StaffQuery.Of(world, maserati, on);
            Assert.Contains(own, row => row.PersonId == "valerio_colotti" && row.OwnTeam && row.Attributes is not null);
            Assert.DoesNotContain(roster, row => row.PersonId is "aurelio_lampredi" or "giuseppe_busso" or "enzo_ferrari");
        }

        var engineers = Paddock.Domain.Development.EngineerRoster.Of(world, ferrari, on);
        Assert.Contains(engineers, engineer => engineer.Role == StaffRole.ChiefDesigner);
        Assert.DoesNotContain(engineers, engineer => engineer.Role is null);
    }

    private static string[] Subjects(WorldInitResult result, string code) =>
        result.Report.Gaps.SingleOrDefault(gap => gap.Code == code)?.Subjects.ToArray() ?? [];

    private static void AssertSameTruth(WorldState left, WorldState right, PersonId person)
    {
        Assert.Equal(left.TruthOf(person).Attributes, right.TruthOf(person).Attributes);
        Assert.Equal(left.TruthOf(person).Potential, right.TruthOf(person).Potential);
    }

    private static void AssertRoster(WorldState world, GameDate on)
    {
        var teams = world.Organizations.Where(organization => organization.Kind == OrganizationKind.Team && organization.Dissolved is null).ToArray();
        Assert.NotEmpty(teams);
        foreach (var team in teams)
        {
            foreach (var role in StaffCatalogue.TeamRoster)
            {
                var held = world.Contracts.Count(contract =>
                    contract.OrganizationId == team.Id
                    && contract.IsActiveOn(on)
                    && contract.Role.IsStaff
                    && contract.Role.StaffRole == role);
                var needed = role == StaffRole.RaceEngineer ? StaffEstimates.RaceEngineersPerTeam : 1;
                Assert.True(held >= needed, team.Id.Value + " " + role + " has " + held);
            }
        }
    }
}
