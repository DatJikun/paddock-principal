using System.Xml.Linq;
using Paddock.Domain.Random;

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
    }

    [Fact]
    public void ProjectReferencesFollowTheLayering()
    {
        Assert.Equal(["Paddock.Domain"], ProjectReferences("src/Paddock.Simulation/Paddock.Simulation.csproj"));
        Assert.Equal(["Paddock.Simulation"], ProjectReferences("src/Paddock.Application/Paddock.Application.csproj"));
        Assert.Equal(["Paddock.Domain"], ProjectReferences("src/Paddock.Persistence/Paddock.Persistence.csproj"));
        Assert.Equal(["Paddock.Domain"], ProjectReferences("src/Paddock.Data/Paddock.Data.csproj"));
        Assert.Equal(["Paddock.Application"], ProjectReferences("src/Paddock.Desktop/Paddock.Desktop.csproj"));
        Assert.Equal(["Paddock.Application"], ProjectReferences("tools/Paddock.SimRunner/Paddock.SimRunner.csproj"));
        Assert.Equal(["Paddock.Application"], ProjectReferences("tools/Paddock.DataPipeline/Paddock.DataPipeline.csproj"));
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
