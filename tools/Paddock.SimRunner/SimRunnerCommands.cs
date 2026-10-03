namespace Paddock.SimRunner;

public static class SimRunnerCommands
{
    public static int Execute(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (args.Length == 0)
        {
            stderr.WriteLine("Unknown command. Expected: rng, config, or i18n-check.");
            return 1;
        }

        return args[0] switch
        {
            "rng" => RngCommand.Execute(args, stdout, stderr),
            "config" => ConfigCommand.Execute(args, stdout, stderr),
            I18nCheckCommand.Name => I18nCheckCommand.Execute(args, stdout, stderr),
            _ => Unknown(stderr),
        };
    }

    private static int Unknown(TextWriter stderr)
    {
        stderr.WriteLine("Unknown command. Expected: rng, config, or i18n-check.");
        return 1;
    }
}
