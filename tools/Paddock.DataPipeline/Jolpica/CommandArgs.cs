namespace Paddock.DataPipeline;

internal static class CommandArgs
{
    public static bool TryParse(
        string[] args,
        int start,
        string[] allowed,
        TextWriter stderr,
        out Dictionary<string, string> options,
        out bool force)
    {
        options = new Dictionary<string, string>(StringComparer.Ordinal);
        force = false;

        for (var i = start; i < args.Length; i++)
        {
            var flag = args[i];
            if (flag == "--force")
            {
                if (!allowed.Contains(flag))
                {
                    stderr.WriteLine($"Unknown argument: {flag}");
                    return false;
                }

                if (force)
                {
                    stderr.WriteLine("Duplicate option: --force");
                    return false;
                }

                force = true;
                continue;
            }

            if (!allowed.Contains(flag) || flag is not ("--from" or "--to" or "--cache"))
            {
                stderr.WriteLine($"Unknown argument: {flag}");
                return false;
            }

            if (options.ContainsKey(flag))
            {
                stderr.WriteLine($"Duplicate option: {flag}");
                return false;
            }

            if (i + 1 >= args.Length)
            {
                stderr.WriteLine($"Missing value for {flag}.");
                return false;
            }

            var value = args[++i];
            if (value.Length == 0 || value.StartsWith("--", StringComparison.Ordinal))
            {
                stderr.WriteLine($"Missing value for {flag}.");
                return false;
            }

            options[flag] = value;
        }

        return true;
    }
}
