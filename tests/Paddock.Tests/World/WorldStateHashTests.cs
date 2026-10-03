using System.Globalization;
using Paddock.Domain.People;
using Paddock.Domain.Time;
using Paddock.Domain.World;

namespace Paddock.Tests.World;

/// <summary>
/// Pins the <c>paddock-world/1</c> state-hash format (INV-002): same contents, same digest, in any
/// insertion order and under any culture, and a change anywhere in the state changes the digest.
/// </summary>
public class WorldStateHashTests
{
    private static readonly GameDate Opening = GameDate.SeasonStart(1950);

    [Fact]
    public void StateHashMatchesTheGoldenDigestOfTheDocumentedFormat()
    {
        // If this fails after an intended format change, bump the format name (paddock-world/N) and re-pin.
        Assert.Equal("c209cf530de2740b796ebb52fce583461c47a356953d5dd6f1c4116164c71968", Build(reversed: false).StateHash());
    }

    [Fact]
    public void StateHashDoesNotDependOnInsertionOrder()
    {
        Assert.Equal(Build(reversed: false).StateHash(), Build(reversed: true).StateHash());
    }

    [Fact]
    public void StateHashDoesNotDependOnTheCurrentCulture()
    {
        var expected = Build(reversed: false).StateHash();
        var original = CultureInfo.CurrentCulture;
        try
        {
            foreach (var name in new[] { "tr-TR", "pl-PL", "ar-SA" })
            {
                CultureInfo.CurrentCulture = new CultureInfo(name);
                Assert.Equal(expected, Build(reversed: false).StateHash());
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void StateHashChangesWhenContractsKnowledgeOrDateChange()
    {
        var world = Build(reversed: false);
        var baseline = world.StateHash();
        var person = world.GetPerson(PersonId.Real("fangio")).Id;
        var team = world.Organizations[0].Id;

        var removed = world.RemoveContract(world.Contracts[0].Id);
        Assert.NotEqual(baseline, removed.StateHash());

        var believed = world.SetKnowledge(new PersonKnowledge(team, person, [new KnownAttribute("cornering", new AttributeBand(9, 13))], null));
        Assert.NotEqual(baseline, believed.StateHash());
        var narrower = world.SetKnowledge(new PersonKnowledge(team, person, [new KnownAttribute("cornering", new AttributeBand(9, 14))], null));
        Assert.NotEqual(believed.StateHash(), narrower.StateHash());

        Assert.NotEqual(baseline, world.WithDate(Opening.AddDays(1)).StateHash());
    }

    private static WorldState Build(bool reversed)
    {
        var world = WorldState.At(Opening);
        var specs = new[] { ("fangio", "Juan", "Fangio", 10), ("farina", "Nino", "Farina", 8) };
        if (reversed)
        {
            Array.Reverse(specs);
        }

        foreach (var (id, given, family, cornering) in specs)
        {
            (world, _) = world.AddPerson(Driver(id, given, family, cornering));
        }

        var fangio = PersonId.Real("fangio");
        var (withTeam, team) = world.AddOrganization(new OrganizationSpec(
            OrganizationKind.Team,
            true,
            "alfa",
            Opening,
            null,
            5_000_000,
            [new OrganizationNameSpan("Alfa Romeo", Opening, null)]));
        var (withContract, _) = withTeam.AddContract(new ContractSpec(
            fangio,
            team,
            ContractRole.Driver(SeatStatus.NumberOne),
            Opening,
            new GameDate(1950, 12, 31),
            1000,
            true,
            new ContractOption(new GameDate(1950, 10, 1), 1),
            new ReleaseClause(250_000)));
        return withContract.SetKnowledge(new PersonKnowledge(team, fangio, [new KnownAttribute("cornering", new AttributeBand(8, 12))], new AttributeBand(10, 16)));
    }

    private static PersonSpec Driver(string id, string given, string family, int cornering)
    {
        var current = new DriverAttributes(cornering, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10);
        var potential = new DriverAttributes(Math.Max(12, cornering), 12, 12, 12, 12, 12, 12, 12, 12, 12, 12);
        return new PersonSpec(given, family, new GameDate(1911, 6, 24), "AR", true, id, [PersonRole.Driver], PersonTruth.FromDriver(current, potential));
    }
}
