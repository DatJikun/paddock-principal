namespace Paddock.SimRunner;

internal static class Program
{
<<<<<<< HEAD
    private static int Main(string[] args) =>
        args.Length == 0 ? RngCommand.Execute(args, Console.Out, Console.Error) : args[0] switch
        {
            "toy" => ToyCommand.Execute(args, Console.Out, Console.Error),
            "vote-sim" => VoteSimCommand.Execute(args, Console.Out, Console.Error),
            RaceReplayCommand.Name => RaceReplayCommand.Execute(args, Console.Out, Console.Error),
            I18nCheckCommand.Name => I18nCheckCommand.Execute(args, Console.Out, Console.Error),
            _ => RngCommand.Execute(args, Console.Out, Console.Error),
        };
=======
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Expected a command: rng, gen-people, i18n-check.");
            return 1;
        }

        return args[0] switch
        {
            "rng" => RngCommand.Execute(args, Console.Out, Console.Error),
            "gen-people" => GenPeopleCommand.Execute(args, Console.Out, Console.Error),
            I18nCheckCommand.Name => I18nCheckCommand.Execute(args, Console.Out, Console.Error),
            _ => Unknown(args[0]),
        };
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine("Unknown command: " + command + ". Expected: rng, gen-people, i18n-check.");
        return 1;
    }
>>>>>>> ef9d424 (T13: Deterministic generator of fictional drivers and staff)
}
