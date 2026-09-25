namespace Paddock.DataPipeline;

public static class JolpicaCache
{
    public const string RawDirectoryName = "raw";
    public const string NormalizedDirectoryName = "normalized";
    public const int PageLimit = 100;

    public static readonly string[] NormalizedFileNames =
    [
        "drivers.json",
        "constructors.json",
        "circuits.json",
        "races.json",
        "results.json",
        "qualifying.json",
    ];

    public static string DefaultRoot()
    {
        return Path.Combine(FindRepoRoot(), "data", "cache", "jolpica");
    }

    public static string Raw(string cacheRoot)
    {
        return Path.Combine(cacheRoot, RawDirectoryName);
    }

    public static string Normalized(string cacheRoot)
    {
        return Path.Combine(cacheRoot, NormalizedDirectoryName);
    }

    public static void WriteAtomic(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"Path has no directory: {path}");
        Directory.CreateDirectory(directory);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, contents);
        File.Move(temporary, path, overwrite: true);
    }

    public static string FindRepoRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "PaddockPrincipal.sln")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate PaddockPrincipal.sln. Pass --cache.");
    }
}
