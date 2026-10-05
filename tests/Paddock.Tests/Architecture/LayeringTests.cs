using System.Xml.Linq;
using Paddock.Domain.Random;
using Paddock.Domain.Spy;
using Paddock.Simulation.Racing.Pits;

namespace Paddock.Tests.Architecture;

public class LayeringTests
{
    [Fact]
    public void DomainHasNoReferencesToOtherPaddockProjects()
    {
        Assert.Empty(ProjectReferences("src/Paddock.Domain/Paddock.Domain.csproj"));

        var paddockReferences = typeof(Xoshiro256StarStar).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .Where(name => name is not null && name.StartsWith("Paddock.", StringComparison.Ordinal))
            .ToList();
        Assert.Empty(paddockReferences);
        Assert.DoesNotContain(
            typeof(Xoshiro256StarStar).Assembly.GetReferencedAssemblies(),
            assembly => assembly.Name == "System.Text.Json");
    }

    [Fact]
    public void ProjectReferencesFollowTheLayering()
    {
        Assert.Equal(["Paddock.Domain"], ProjectReferences("src/Paddock.Simulation/Paddock.Simulation.csproj"));
        Assert.Equal(["Paddock.Simulation"], ProjectReferences("src/Paddock.Application/Paddock.Application.csproj"));
        Assert.Equal(["Paddock.Domain"], ProjectReferences("src/Paddock.Persistence/Paddock.Persistence.csproj"));
        Assert.Equal(["Paddock.Domain"], ProjectReferences("src/Paddock.Data/Paddock.Data.csproj"));
        Assert.Equal(
            ["Paddock.Application", "Paddock.Data"],
            ProjectReferences("src/Paddock.Desktop/Paddock.Desktop.csproj"));
        // SimRunner is the composition root that opens a .paddock file. It writes the snapshot; it does not apply game rules.
        // It also references DataPipeline for `calibrate-race` (#122), which reads the local Jolpica cache with the pipeline's
        // own status mapping instead of copying it.
        Assert.Equal(
            ["Paddock.Application", "Paddock.Data", "Paddock.DataPipeline", "Paddock.Persistence"],
            ProjectReferences("tools/Paddock.SimRunner/Paddock.SimRunner.csproj"));
        Assert.Equal(["Paddock.Application", "Paddock.Data"], ProjectReferences("tools/Paddock.DataPipeline/Paddock.DataPipeline.csproj"));
    }

    [Fact]
    public void TraceTypesLiveInDomainSoSimulationAiCanRecordThem()
    {
        var domain = typeof(Xoshiro256StarStar).Assembly;
        foreach (var type in new[] { typeof(DecisionTrace), typeof(TraceOption), typeof(TraceFactor), typeof(WeekendKey), typeof(ITraceSink), typeof(NullSink), typeof(MemorySink) })
        {
            Assert.Same(domain, type.Assembly);
            Assert.Equal("Paddock.Domain.Spy", type.Namespace);
        }

        var simulation = typeof(RuleBasedStrategist).Assembly;
        Assert.DoesNotContain(simulation.GetReferencedAssemblies(), assembly => assembly.Name == "Paddock.Application");
    }

    private static List<string> ProjectReferences(string relativeCsproj)
    {
        var path = Path.Combine(RepoPaths.Root(), relativeCsproj.Replace('/', Path.DirectorySeparatorChar));
        var document = XDocument.Load(path);
        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element =>
            {
                var include = element.Attribute("Include")?.Value
                    ?? throw new InvalidOperationException($"ProjectReference in {relativeCsproj} has no Include.");
                return Path.GetFileNameWithoutExtension(include.Replace('\\', '/'));
            })
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }
}
