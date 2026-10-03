using Paddock.Domain.Time;
using Paddock.Domain.World;
using Paddock.Tests.Persistence;

namespace Paddock.Tests.World;

/// <summary>
/// <see cref="WorldState.Restore"/> rebuilds a state from stored parts and refuses parts that no edit could produce.
/// </summary>
public class WorldRestoreTests
{
    [Fact]
    public void RestoringAStatesPartsGivesTheSameHash()
    {
        var world = WorldFixtures.Small();

        var restored = WorldState.Restore(world.CurrentDate, world.Ids, world.Persons, world.Organizations, world.Contracts, world.Knowledge);

        Assert.Equal(world.StateHash(), restored.StateHash());
        Assert.Equal(world.Ids.Issued, restored.Ids.Issued);
    }

    [Fact]
    public void AnIdThatWasNeverIssuedIsRefused()
    {
        var world = WorldFixtures.Small();
        var nobody = new IdAllocator(1, 1, 1, []);

        Assert.Throws<InvalidOperationException>(() =>
            WorldState.Restore(world.CurrentDate, nobody, world.Persons, [], [], []));
    }

    [Fact]
    public void ADuplicateEntityIsRefused()
    {
        var world = WorldFixtures.Small();

        Assert.Throws<InvalidOperationException>(() =>
            WorldState.Restore(world.CurrentDate, world.Ids, world.Persons.Concat([world.Persons[0]]), world.Organizations, world.Contracts, world.Knowledge));
    }

    [Fact]
    public void AContractWithoutItsPersonOrOrganizationIsRefused()
    {
        var world = WorldFixtures.Small();
        var contract = world.Contracts[0];

        Assert.Throws<InvalidOperationException>(() => WorldState.Restore(
            world.CurrentDate,
            world.Ids,
            world.Persons.Where(person => person.Id != contract.PersonId),
            world.Organizations,
            world.Contracts,
            []));
        Assert.Throws<InvalidOperationException>(() => WorldState.Restore(
            world.CurrentDate,
            world.Ids,
            world.Persons,
            world.Organizations.Where(organization => organization.Id != contract.OrganizationId),
            world.Contracts,
            []));
    }

    [Fact]
    public void OverlappingExclusiveContractsAreRefusedButNonExclusiveOnesAreNot()
    {
        var world = WorldFixtures.Small();
        var first = world.Contracts.First(contract => contract.Exclusive);
        var clash = new Contract(
            ContractId.Generated(world.Ids.NextContract),
            first.PersonId,
            first.OrganizationId,
            first.Role,
            first.Start,
            first.End,
            1,
            exclusive: true,
            null,
            null);
        var shared = new Contract(
            ContractId.Generated(world.Ids.NextContract + 1),
            first.PersonId,
            first.OrganizationId,
            first.Role,
            first.Start,
            first.End,
            1,
            exclusive: false,
            null,
            null);
        var wider = new IdAllocator(
            world.Ids.NextPerson,
            world.Ids.NextOrganization,
            world.Ids.NextContract + 2,
            world.Ids.Issued.Concat([clash.Id.Value, shared.Id.Value]).ToArray());

        Assert.Throws<InvalidOperationException>(() =>
            WorldState.Restore(world.CurrentDate, wider, world.Persons, world.Organizations, world.Contracts.Append(clash), world.Knowledge));
        var ok = WorldState.Restore(world.CurrentDate, wider, world.Persons, world.Organizations, world.Contracts.Append(shared), world.Knowledge);
        Assert.Equal(world.Contracts.Count + 1, ok.Contracts.Count);
    }

    [Fact]
    public void BeliefsAboutUnknownPeopleOrOrganizationsAreRefused()
    {
        var world = WorldFixtures.Small();
        var belief = world.Knowledge[0];

        Assert.Throws<InvalidOperationException>(() => WorldState.Restore(
            world.CurrentDate, world.Ids, world.Persons.Where(person => person.Id != belief.SubjectId), world.Organizations, [], world.Knowledge));
        Assert.Throws<InvalidOperationException>(() => WorldState.Restore(
            world.CurrentDate, world.Ids, world.Persons, world.Organizations, world.Contracts, world.Knowledge.Concat([belief])));
    }

    [Fact]
    public void LineageMustBeMirroredAndMustNotCycle()
    {
        var world = WorldState.At(new GameDate(1955, 1, 1));
        OrganizationId a;
        OrganizationId b;
        (world, a) = world.AddOrganization(Team("alpha"));
        (world, b) = world.AddOrganization(Team("beta"));
        var linked = world.LinkLineage(a, b, new GameDate(1956, 1, 1), null);
        var alpha = linked.GetOrganization(a);
        var beta = linked.GetOrganization(b);

        // One side of the edge only.
        var lonely = alpha;
        Assert.Throws<InvalidOperationException>(() => WorldState.Restore(world.CurrentDate, world.Ids, [], [lonely, world.GetOrganization(b)], [], []));

        // Both sides, both ways: every organization has a predecessor, so the chain has no start.
        var from = new GameDate(1956, 1, 1);
        var cycleA = new Organization(a, OrganizationKind.Team, true, alpha.Founded, null, 0, alpha.Names,
            [new LineageLink(b, LineageDirection.Successor, from, null), new LineageLink(b, LineageDirection.Predecessor, from, null)]);
        var cycleB = new Organization(b, OrganizationKind.Team, true, beta.Founded, null, 0, beta.Names,
            [new LineageLink(a, LineageDirection.Successor, from, null), new LineageLink(a, LineageDirection.Predecessor, from, null)]);
        Assert.Throws<InvalidOperationException>(() => WorldState.Restore(world.CurrentDate, world.Ids, [], [cycleA, cycleB], [], []));

        var fine = WorldState.Restore(world.CurrentDate, world.Ids, [], linked.Organizations, [], []);
        Assert.Equal(linked.StateHash(), fine.StateHash());
    }

    private static OrganizationSpec Team(string id) => new(
        OrganizationKind.Team,
        true,
        id,
        new GameDate(1950, 1, 1),
        null,
        1,
        [new OrganizationNameSpan(id, new GameDate(1950, 1, 1), null)]);
}
