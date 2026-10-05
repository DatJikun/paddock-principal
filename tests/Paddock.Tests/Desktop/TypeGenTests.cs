using Paddock.TypeGen;

namespace Paddock.Tests.Desktop;

public class TypeGenTests
{
    [Fact]
    public void GeneratedFileMatchesTheBridgeRegistry()
    {
        var path = Path.Combine(RepoPaths.Root(), "ui", "app", "src", "lib", "api", "types.generated.ts");
        Assert.True(TypeGenerator.IsCurrent(path), "Bridge types are stale. Run: dotnet run --project tools/Paddock.TypeGen");
    }
}
