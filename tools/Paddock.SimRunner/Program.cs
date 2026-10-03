namespace Paddock.SimRunner;

internal static class Program
{
    private static int Main(string[] args) =>
        args.Length == 0 ? RngCommand.Execute(args, Console.Out, Console.Error) : args[0] switch
        {
            "toy" => ToyCommand.Execute(args, Console.Out, Console.Error),
            "vote-sim" => VoteSimCommand.Execute(args, Console.Out, Console.Error),
            RaceReplayCommand.Name => RaceReplayCommand.Execute(args, Console.Out, Console.Error),
            I18nCheckCommand.Name => I18nCheckCommand.Execute(args, Console.Out, Console.Error),
            _ => RngCommand.Execute(args, Console.Out, Console.Error),
        };
}
