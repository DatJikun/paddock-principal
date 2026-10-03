using System.Reflection;
using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Tests.World;

public class WorldStateTests
{
    private static readonly GameDate Opening = GameDate.SeasonStart(1950);

    [Fact]
    public void GeneratedPersonIdIsNeverReusedAfterRemoval()
    {
        var world = WorldState.At(Opening);
        var (withFirst, first) = world.AddPerson(Driver(null, 10));
        var removed = withFirst.RemovePerson(first);
        var (withSecond, second) = removed.AddPerson(Driver(null, 10));

        Assert.Equal("gen:1", first.Value);
        Assert.Equal(new StablePersonId(1).Value, first.Value);
        Assert.False(first.IsReal);
        Assert.Equal("gen:2", second.Value);
        Assert.NotEqual(first, second);
        Assert.DoesNotContain(withSecond.Persons, person => person.Id == first);
        Assert.Contains(first.Value, removed.Ids.Issued);
        Assert.Equal(3, withSecond.Ids.NextPerson);
    }

    [Fact]
    public void RealPersonIdIsNeverReusedAfterRemoval()
    {
        var world = WorldState.At(Opening);
        var (withFangio, fangio) = world.AddPerson(Driver("fangio", 10));
        var removed = withFangio.RemovePerson(fangio);

        Assert.True(fangio.IsReal);
        Assert.Equal("fangio", fangio.Value);
        var ex = Assert.Throws<InvalidOperationException>(() => removed.AddPerson(Driver("fangio", 10)));
        Assert.Contains("fangio", ex.Message, StringComparison.Ordinal);
        Assert.Contains(fangio.Value, removed.Ids.Issued);
    }

    [Fact]
    public void OneCanonicalStringCannotBeBothAPersonAndAnOrganization()
    {
        var (world, _) = WorldState.At(Opening).AddPerson(Driver("mercedes", 10));
        var ex = Assert.Throws<InvalidOperationException>(() => world.AddOrganization(Team("mercedes", "Mercedes")));
        Assert.Contains("mercedes", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OrganizationAndContractIdsAreNeverReusedAfterRemoval()
    {
        var world = WorldState.At(Opening);
        var (withOrg, org) = world.AddOrganization(Team(null, "Privateer"));
        var removedOrg = withOrg.RemoveOrganization(org);
        var (_, nextOrg) = removedOrg.AddOrganization(Team(null, "Privateer"));
        Assert.Equal("org:1", org.Value);
        Assert.Equal("org:2", nextOrg.Value);

        var (withPerson, person) = withOrg.AddPerson(Driver(null, 10));
        var (withContract, contract) = withPerson.AddContract(Seat(person, org, Opening, new GameDate(1950, 12, 31), exclusive: true));
        var removedContract = withContract.RemoveContract(contract);
        var (_, nextContract) = removedContract.AddContract(Seat(person, org, Opening, new GameDate(1950, 12, 31), exclusive: true));
        Assert.Equal("con:1", contract.Value);
        Assert.Equal("con:2", nextContract.Value);
    }

    [Fact]
    public void PersonsEnumerateInOrdinalIdOrder()
    {
        var zetaFirst = WorldState.At(Opening);
        var (withZeta, _) = zetaFirst.AddPerson(Driver("zeta", 10));
        var (zetaThenAlpha, _) = withZeta.AddPerson(Driver("alpha", 10));

        var alphaFirst = WorldState.At(Opening);
        var (withAlpha, _) = alphaFirst.AddPerson(Driver("alpha", 10));
        var (alphaThenZeta, _) = withAlpha.AddPerson(Driver("zeta", 10));

        Assert.Equal(["alpha", "zeta"], zetaThenAlpha.Persons.Select(person => person.Id.Value));
        Assert.Equal(zetaThenAlpha.StateHash(), alphaThenZeta.StateHash());
    }

    [Fact]
    public void APersonCanHoldDriverAndTeamPrincipalRoles()
    {
        var spec = Driver(null, 10, [PersonRole.Driver, PersonRole.TeamPrincipal]);
        var (world, id) = WorldState.At(Opening).AddPerson(spec);
        var person = world.GetPerson(id);

        Assert.Contains(person.Roles, role => role.IsDriver);
        Assert.Contains(person.Roles, role => role.IsStaff && role.StaffRole == StaffRole.TeamPrincipal);
    }

    [Fact]
    public void OverlappingExclusiveContractsAreRejected()
    {
        var (world, person) = WorldState.At(Opening).AddPerson(Driver("fangio", 10));
        var (withTeam, team) = world.AddOrganization(Team("alfa", "Alfa Romeo"));
        var end = new GameDate(1950, 12, 31);
        var (withContract, _) = withTeam.AddContract(Seat(person, team, Opening, end, exclusive: true));

        var overlap = Assert.Throws<InvalidOperationException>(() =>
            withContract.AddContract(Seat(person, team, end, new GameDate(1951, 12, 31), exclusive: true)));
        Assert.Contains("fangio", overlap.Message, StringComparison.Ordinal);
        Assert.Contains(withContract.Contracts[0].Id.Value, overlap.Message, StringComparison.Ordinal);

        var (adjacent, _) = withContract.AddContract(Seat(person, team, end.AddDays(1), new GameDate(1951, 12, 31), exclusive: true));
        Assert.Equal(2, adjacent.Contracts.Count);

        var (shared, _) = withTeam.AddContract(Seat(person, team, Opening, end, exclusive: false));
        var (alsoShared, _) = shared.AddContract(Seat(person, team, Opening, end, exclusive: false));
        Assert.Equal(2, alsoShared.Contracts.Count);

        var (mixed, _) = withContract.AddContract(Seat(person, team, Opening, end, exclusive: false));
        Assert.Equal(2, mixed.Contracts.Count);
    }

    [Fact]
    public void IsActiveOnIncludesTheStartAndEndDays()
    {
        var (world, person) = WorldState.At(Opening).AddPerson(Driver("fangio", 10));
        var (withTeam, team) = world.AddOrganization(Team("alfa", "Alfa Romeo"));
        var end = new GameDate(1950, 12, 31);
        var (withContract, id) = withTeam.AddContract(Seat(
            person,
            team,
            Opening,
            end,
            exclusive: true,
            option: new ContractOption(new GameDate(1950, 11, 1), 1),
            release: new ReleaseClause(5000)));
        var contract = withContract.GetContract(id);

        Assert.False(contract.IsActiveOn(Opening.AddDays(-1)));
        Assert.True(contract.IsActiveOn(Opening));
        Assert.True(contract.IsActiveOn(end));
        Assert.False(contract.IsActiveOn(end.AddDays(1)));
        Assert.Equal(5000, contract.ReleaseClause!.Value.Amount);
        Assert.Equal(1, contract.Option!.Value.ExtraYears);
        Assert.True(contract.ExclusivelyOverlaps(new Contract(
            ContractId.Generated(99),
            person,
            team,
            ContractRole.Driver(SeatStatus.Equal),
            end,
            end,
            1,
            true,
            null,
            null)));
    }

    [Fact]
    public void KnowledgeViewCannotReadTruth()
    {
        var (world, person) = WorldState.At(Opening).AddPerson(Driver("fangio", 18));
        var (withTeam, team) = world.AddOrganization(Team("alfa", "Alfa Romeo"));

        Assert.Null(withTeam.KnowledgeOf(team, person));
        Assert.Equal(18, withTeam.TruthOf(person).Value("cornering"));

        var belief = new PersonKnowledge(
            team,
            person,
            [new KnownAttribute("cornering", new AttributeBand(10, 14))],
            new AttributeBand(14, 18));
        var informed = withTeam.SetKnowledge(belief);
        var view = informed.KnowledgeOf(team, person);

        Assert.NotNull(view);
        var known = Assert.Single(view.Value.Attributes);
        Assert.Equal("cornering", known.Key);
        Assert.Equal(10, known.Band.Low);
        Assert.Equal(14, known.Band.High);
        Assert.Equal(18, informed.TruthOf(person).Value("cornering"));
        AssertNoTruthMember(typeof(PersonKnowledge));
        AssertNoTruthMember(typeof(PersonKnowledgeView));
        AssertNoTruthMember(typeof(KnownAttribute));
        AssertNoTruthMember(typeof(AttributeBand));
    }

    [Fact]
    public void StateHashIsStableAndChangesWhenOneAttributeChanges()
    {
        var first = Signed(10);
        var second = Signed(10);
        var raised = Signed(11);

        Assert.Equal(first.StateHash(), second.StateHash());
        Assert.NotEqual(first.StateHash(), raised.StateHash());
        Assert.Matches("^[0-9a-f]{64}$", first.StateHash());
        Assert.Equal(10, first.TruthOf(first.Persons[0].Id).Value("cornering"));
        Assert.Equal(11, raised.TruthOf(raised.Persons[0].Id).Value("cornering"));
    }

    [Fact]
    public void AttributeScaleArgumentsComeFromTheSingleEstimateTable()
    {
        Assert.Equal(
            [("min", GenerationEstimates.AttributeMin), ("max", GenerationEstimates.AttributeMax)],
            WorldText.AttributeScaleArguments());
        Assert.Equal(WorldText.AttributeScaleExplanation, "world.attribute.scale_explanation");
        Assert.Equal(WorldText.SalaryExplanation, "world.salary.explanation");
        Assert.Equal(WorldText.BudgetExplanation, "world.budget.explanation");
    }

    private static WorldState Signed(int cornering)
    {
        var (world, _) = WorldState.At(Opening).AddPerson(Driver("fangio", cornering));
        return world;
    }

    private static PersonSpec Driver(string? realId, int cornering, IReadOnlyList<PersonRole>? roles = null)
    {
        var ceiling = Math.Max(12, cornering);
        var current = new DriverAttributes(cornering, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10);
        var potential = new DriverAttributes(ceiling, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12);
        return new PersonSpec(
            "Juan",
            "Fangio",
            new GameDate(1911, 6, 24),
            "AR",
            realId is not null,
            realId,
            roles ?? [PersonRole.Driver],
            PersonTruth.FromDriver(current, potential));
    }

    private static OrganizationSpec Team(string? realId, string name) => new(
        OrganizationKind.Team,
        realId is not null,
        realId,
        Opening,
        null,
        0,
        [new OrganizationNameSpan(name, Opening, null)]);

    private static ContractSpec Seat(
        PersonId person,
        OrganizationId team,
        GameDate start,
        GameDate end,
        bool exclusive,
        ContractOption? option = null,
        ReleaseClause? release = null) => new(
        person,
        team,
        ContractRole.Driver(SeatStatus.NumberOne),
        start,
        end,
        1000,
        exclusive,
        option,
        release);

    private static void AssertNoTruthMember(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var property in type.GetProperties(flags))
        {
            AssertNotTruth(property.PropertyType, type, property.Name);
        }

        foreach (var field in type.GetFields(flags))
        {
            AssertNotTruth(field.FieldType, type, field.Name);
        }

        foreach (var method in type.GetMethods(flags))
        {
            AssertNotTruth(method.ReturnType, type, method.Name);
            foreach (var parameter in method.GetParameters())
            {
                AssertNotTruth(parameter.ParameterType, type, method.Name);
            }
        }

        foreach (var ctor in type.GetConstructors(flags))
        {
            foreach (var parameter in ctor.GetParameters())
            {
                AssertNotTruth(parameter.ParameterType, type, ctor.Name);
            }
        }
    }

    private static void AssertNotTruth(Type candidate, Type owner, string member)
    {
        var type = Nullable.GetUnderlyingType(candidate) ?? candidate;
        if (type.IsByRef || type.IsArray)
        {
            type = type.GetElementType() ?? type;
        }

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                AssertNotTruth(argument, owner, member);
            }
        }

        Assert.False(
            type == typeof(Person) || type == typeof(PersonTruth) || type == typeof(NamedAttribute),
            owner.Name + "." + member + " exposes " + type.Name);
    }
}
