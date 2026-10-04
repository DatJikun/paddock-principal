namespace Paddock.Tests;

internal static class RepoPaths
{
    public static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PaddockPrincipal.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate PaddockPrincipal.sln from the test output directory.");
    }

    private static readonly Lazy<string> AuthoredOnly = new(CopyAuthored, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// A data root holding a copy of <c>data/authored</c> and no <c>cache</c>, so a command that reads the local Jolpica cache
    /// by default gives the same output on a machine that has one (the owner's) as on CI, which never has one.
    /// </summary>
    public static string AuthoredOnlyDataRoot() => AuthoredOnly.Value;

    private static string CopyAuthored()
    {
        var source = Path.Combine(Root(), "data", "authored");
        var root = Path.Combine(Path.GetTempPath(), "paddock-authored-" + Guid.NewGuid().ToString("N"));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(root, "authored", Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        return root;
    }
}
