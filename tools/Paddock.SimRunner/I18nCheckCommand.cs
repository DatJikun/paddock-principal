using Paddock.Application.Localization;

namespace Paddock.SimRunner;

/// <summary><c>i18n-check [--root &lt;repo&gt;]</c>: runs the localization checks, exits 1 on any problem.</summary>
public static class I18nCheckCommand
{
    public const string Name = "i18n-check";

    public static int Execute(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        string? root = null;
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] != "--root" || i + 1 >= args.Length || root is not null)
            {
                stderr.WriteLine("Usage: i18n-check [--root <repo directory>]");
                return 1;
            }

            root = args[++i];
        }

        root ??= FindRoot();
        if (root is null)
        {
            stderr.WriteLine("Could not locate PaddockPrincipal.sln; pass --root <repo directory>.");
            return 1;
        }

        var problems = I18nChecker.CheckRepository(root);
        foreach (var problem in problems)
        {
            stderr.WriteLine(problem);
        }

        if (problems.Count > 0)
        {
            stderr.WriteLine($"i18n-check failed: {problems.Count} problem(s).");
            return 1;
        }

        stdout.WriteLine("i18n-check passed.");
        return 0;
    }

    private static string? FindRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
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

        return null;
    }
}
