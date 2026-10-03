namespace Paddock.SimRunner;

internal static class Program
{
    private static int Main(string[] args) =>
        args.Length == 0 ? RngCommand.Execute(args, Console.Out, Console.Error) : args[0] switch
        {
            "rng" => RngCommand.Execute(args, Console.Out, Console.Error),
            "gen-people" => GenPeopleCommand.Execute(args, Console.Out, Console.Error),
            "toy" => ToyCommand.Execute(args, Console.Out, Console.Error),
            "vote-sim" => VoteSimCommand.Execute(args, Console.Out, Console.Error),
            "config" => ConfigCommand.Execute(args, Console.Out, Console.Error),
            RaceReplayCommand.Name => RaceReplayCommand.Execute(args, Console.Out, Console.Error),
            I18nCheckCommand.Name => I18nCheckCommand.Execute(args, Console.Out, Console.Error),
            InitWorldCommand.Name => InitWorldCommand.Execute(args, Console.Out, Console.Error),
            _ => Unknown(args[0]),
        };

    private static int Unknown(string command)
    {
        Console.Error.WriteLine(
            "Unknown command: " + command + ". Expected: rng, gen-people, toy, vote-sim, config, race-replay, i18n-check, init-world.");
        return 1;
    }
}
