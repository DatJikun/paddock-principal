using Paddock.TypeGen;

var root = TypeGenerator.RepositoryRoot();
var path = args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal))
    ?? Path.Combine(root, "ui", "app", "src", "lib", "api", "types.generated.ts");
var check = args.Contains("--check", StringComparer.Ordinal);
if (check)
{
    if (TypeGenerator.IsCurrent(path))
    {
        return 0;
    }

    Console.Error.WriteLine("Bridge types are stale. Run: dotnet run --project tools/Paddock.TypeGen");
    return 1;
}

TypeGenerator.Write(path);
Console.WriteLine(path);
return 0;
