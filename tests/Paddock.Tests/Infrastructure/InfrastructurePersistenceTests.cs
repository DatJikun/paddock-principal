using Paddock.Domain.Infrastructure;
using Paddock.Domain.Time;
using Paddock.Persistence;
using Paddock.Tests.Persistence;

namespace Paddock.Tests.Infrastructure;

public class InfrastructurePersistenceTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("paddock-infrastructure-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void AnInfrastructureSectionRoundTripsThroughTheSaveWithTheSameHash()
    {
        var world = WorldFixtures.Small();
        var organization = world.Organizations[0].Id;
        var opening = WorldFixtures.Opening;
        var section = InfrastructureSection.Empty
            .Upsert(new Facility(organization, FacilityKind.Factory, 50_000, null, null, null, 0))
            .AddTest(new TestBooking(organization, opening, 12_345));
        var building = section.Find(organization, FacilityKind.Factory)!
            .StartBuild(62_000, 99_000, opening, opening.AddDays(40));
        world = world.WithSection(section.Upsert(building));

        using var file = SaveFile.Create(Path.Combine(_directory, "infra.paddock"), WorldFixtures.Meta());
        var repository = new WorldRepository(file);
        repository.SaveWorld(world, WorldFixtures.Opening);
        var loaded = repository.LoadWorld();

        Assert.Equal(world.StateHash(), loaded.StateHash());
        var restored = loaded.Section<InfrastructureSection>(InfrastructureSection.SectionName)!;
        var factory = restored.Find(organization, FacilityKind.Factory)!;
        Assert.True(factory.IsBuilding);
        Assert.Equal(62_000, factory.TargetQualityMilli);
        Assert.Equal(12_345, Assert.Single(restored.Tests).CostCents);
    }
}
